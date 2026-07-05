using System.Globalization;
using Backend_RankingGeKu.Domain;
using Backend_RankingGeKu.Models;

namespace Backend_RankingGeKu.Services;

/// <summary>
/// Berechnet aus den übermittelten Noten die Ranglisten-Zeilen:
/// Totale, Ränge pro Kategorie, Geräte-Ränge und Auszeichnungen.
/// </summary>
public class RankingCalculator
{
    /// <summary>Anteil der Athleten pro Kategorie, der eine Auszeichnung (Smiley) erhält.</summary>
    private const decimal AwardQuota = 0.4m;

    public List<RankingRow> BuildRows(RankingRequestDto request)
    {
        var rows = new List<RankingRow>();

        foreach (var a in request.Athletes)
        {
            var deviceUsed = new decimal[Gymnastics.ApparatusCount];
            var deviceEnd  = new decimal[Gymnastics.ApparatusCount];
            var deviceD    = new decimal[Gymnastics.ApparatusCount];
            decimal total  = 0m;

            if (a.Notes != null)
            {
                for (int i = 0; i < Gymnastics.ApparatusCount && i < a.Notes.Count; i++)
                {
                    var note = a.Notes[i];
                    deviceEnd[i] = ParseScore(note?.EndNote);
                    deviceD[i]   = ParseScore(note?.DNote);

                    // Ins Total fließt die End-Note; fehlt sie, die D-Note.
                    deviceUsed[i] = deviceEnd[i] != 0m ? deviceEnd[i] : deviceD[i];
                    total += deviceUsed[i];
                }
            }

            rows.Add(new RankingRow
            {
                Kat = a.Kat ?? string.Empty,
                Nachname = a.Nachname ?? string.Empty,
                Vorname = a.Vorname ?? string.Empty,
                Jg = a.Jg ?? string.Empty,
                Verein = a.Verein ?? string.Empty,
                Total = total,
                DeviceUsedScores = deviceUsed,
                DeviceEndScores = deviceEnd,
                DeviceDNotes = deviceD
            });
        }

        return rows;
    }

    /// <summary>Vergibt pro Kategorie Ränge nach Total (gleiches Total = gleicher Rang) und markiert die Top 40 % als ausgezeichnet.</summary>
    public void AssignRanksPerCategory(List<RankingRow> rows)
    {
        foreach (var katGroup in rows.GroupBy(r => r.Kat))
        {
            var ordered = katGroup
                .OrderByDescending(r => r.Total)
                .ThenBy(r => r.Nachname)
                .ThenBy(r => r.Vorname)
                .ToList();

            AssignCompetitionRanks(ordered, r => r.Total, (r, rank) => r.Rank = rank);

            if (ordered.Count == 0) continue;

            var awardCount = (int)Math.Ceiling(ordered.Count * AwardQuota);
            var cutoffRank = ordered.Take(awardCount).Max(r => r.Rank);

            foreach (var r in ordered)
            {
                r.Awarded = r.Rank <= cutoffRank;
            }
        }
    }

    /// <summary>Vergibt pro Kategorie und Gerät Ränge (nur Athleten mit Note > 0).</summary>
    public void AssignDeviceRanksPerCategory(List<RankingRow> rows, int deviceCount)
    {
        foreach (var katGroup in rows.GroupBy(r => r.Kat))
        {
            var list = katGroup.ToList();

            for (int d = 0; d < deviceCount; d++)
            {
                int device = d;
                var withScore = list
                    .Where(r => r.DeviceUsedScores.Length > device && r.DeviceUsedScores[device] > 0m)
                    .OrderByDescending(r => r.DeviceUsedScores[device])
                    .ThenBy(r => r.Nachname)
                    .ThenBy(r => r.Vorname)
                    .ToList();

                AssignCompetitionRanks(withScore, r => r.DeviceUsedScores[device], (r, rank) => r.DeviceRanks[device] = rank);
            }
        }
    }

    /// <summary>
    /// Sichtbare Geräte-Spalten für eine Kategorie:
    /// EPA turnt nicht an Pferd/Ring, diese Spalten werden ausgeblendet.
    /// </summary>
    public List<int> GetVisibleDeviceIndices(string category, IReadOnlyList<string> apparatus)
    {
        var indices = Enumerable.Range(0, apparatus.Count).ToList();

        if (Gymnastics.IsEpa(category))
        {
            var toHide = apparatus
                .Select((name, idx) => (name, idx))
                .Where(x => Gymnastics.IsEpaExcludedApparatus(x.name))
                .Select(x => x.idx)
                .ToHashSet();

            // Fallback auf die Default-Positionen, falls die Namen nicht gefunden werden
            if (toHide.Count == 0)
            {
                toHide.UnionWith(new[] { 1, 2 }.Where(i => i < apparatus.Count));
            }

            indices = indices.Where(i => !toHide.Contains(i)).ToList();
        }

        // Sicherheitsnetz: falls alles weggefiltert wird, lieber alle anzeigen
        if (indices.Count == 0)
        {
            indices.AddRange(Enumerable.Range(0, apparatus.Count));
        }

        return indices;
    }

    /// <summary>"Competition ranking": gleiche Note = gleicher Rang, danach wird entsprechend übersprungen (1,1,3,...).</summary>
    private static void AssignCompetitionRanks(
        IReadOnlyList<RankingRow> orderedDescending,
        Func<RankingRow, decimal> score,
        Action<RankingRow, int> setRank)
    {
        int index = 0;
        int currentRank = 0;
        decimal? lastScore = null;

        foreach (var r in orderedDescending)
        {
            index++;

            if (lastScore == null || score(r) != lastScore.Value)
            {
                currentRank = index;
                lastScore = score(r);
            }

            setRank(r, currentRank);
        }
    }

    /// <summary>Parst eine Note; Komma und Punkt sind als Dezimaltrenner erlaubt. Ungültig/leer = 0.</summary>
    private static decimal ParseScore(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;

        s = s.Trim().Replace(',', '.');

        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
            ? d
            : 0m;
    }
}
