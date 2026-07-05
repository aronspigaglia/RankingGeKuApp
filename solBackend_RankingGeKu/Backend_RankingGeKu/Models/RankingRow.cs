namespace Backend_RankingGeKu.Models;

/// <summary>Berechnete Ranglisten-Zeile eines Athleten (Ergebnis von <see cref="Services.RankingCalculator"/>).</summary>
public sealed class RankingRow
{
    public string Kat { get; init; } = string.Empty;
    public string Nachname { get; init; } = string.Empty;
    public string Vorname { get; init; } = string.Empty;
    public string Jg { get; init; } = string.Empty;
    public string Verein { get; init; } = string.Empty;

    public decimal Total { get; init; }
    public int Rank { get; set; }

    /// <summary>Verwendete Note je Gerät (End-Note, sonst D-Note) – Basis für Total und Geräte-Rang.</summary>
    public decimal[] DeviceUsedScores { get; init; } = new decimal[Domain.Gymnastics.ApparatusCount];

    /// <summary>End-Note je Gerät.</summary>
    public decimal[] DeviceEndScores { get; init; } = new decimal[Domain.Gymnastics.ApparatusCount];

    /// <summary>D-Note je Gerät.</summary>
    public decimal[] DeviceDNotes { get; init; } = new decimal[Domain.Gymnastics.ApparatusCount];

    /// <summary>Rang je Gerät (1..n), 0 = kein Rang (keine Note).</summary>
    public int[] DeviceRanks { get; init; } = new int[Domain.Gymnastics.ApparatusCount];

    /// <summary>true = Auszeichnung (Top 40 % der Kategorie).</summary>
    public bool Awarded { get; set; }
}
