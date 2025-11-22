using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Backend_RankingGeKu.Models;

namespace Backend_RankingGeKu.Services;

public class CsvParser
{
    public async Task<List<List<AthleteDto>>> ParseGroupsAsync(Stream csvStream, string delimiter = ";")
    {
        if (csvStream.CanSeek) csvStream.Position = 0;

        using var reader = new StreamReader(csvStream, detectEncodingFromByteOrderMarks: true);
        var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = false,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = null
        };

        using var csv = new CsvReader(reader, cfg);

        var groups = new List<List<AthleteDto>>();
        var current = new List<AthleteDto>();
        groups.Add(current);

        while (await csv.ReadAsync())
        {
            // Hol die "rohe" erste Spalte (falls nur "-" als Trenner benutzt wird)
            var first = csv.TryGetField(0, out string? f0) ? f0?.Trim() ?? "" : "";

            // Zeile ist nur ein Trenner?
            var fieldCount = csv.Parser.Count;
            var isDashOnly =
                (string.Equals(first, "-", StringComparison.Ordinal) || string.Equals(first, "—", StringComparison.Ordinal)) &&
                (fieldCount == 1 || // nur ein Feld
                 // oder mehrere Spalten aber nur die erste hat "-"
                 (fieldCount > 1 && Enumerable.Range(1, fieldCount - 1).All(i => !csv.TryGetField(i, out string? _v) || string.IsNullOrWhiteSpace(_v))));

            if (isDashOnly)
            {
                // neue Gruppe beginnen (auch wenn die vorige leer war)
                current = new List<AthleteDto>();
                groups.Add(current);
                continue;
            }

            // Normale Datensatz-Zeile
            string F(int i) => csv.TryGetField(i, out string? v) ? (v?.Trim() ?? "") : "";

            var a = new AthleteDto(
                Nachname: F(0),
                Vorname:  F(1),
                JG:       F(2),
                Verein:   F(3),
                Kat:      F(4)
            );

            // komplett leere Zeilen überspringen
            if (string.IsNullOrWhiteSpace(a.Nachname) &&
                string.IsNullOrWhiteSpace(a.Vorname)  &&
                string.IsNullOrWhiteSpace(a.JG)       &&
                string.IsNullOrWhiteSpace(a.Verein)   &&
                string.IsNullOrWhiteSpace(a.Kat))
            {
                continue;
            }

            current.Add(a);
        }

        // trailing leere Gruppe am Ende entfernen
        if (groups.Count > 0 && groups[^1].Count == 0)
            groups.RemoveAt(groups.Count - 1);

        return groups;
    }
}
