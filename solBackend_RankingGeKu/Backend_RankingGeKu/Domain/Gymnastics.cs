namespace Backend_RankingGeKu.Domain;

/// <summary>
/// Fachliche Konstanten und Regeln des GeKu-Wettkampfs,
/// die von mehreren Controllern/Services gebraucht werden.
/// </summary>
public static class Gymnastics
{
    /// <summary>Anzahl Geräte bzw. Durchgänge (D1..D6).</summary>
    public const int ApparatusCount = 6;

    /// <summary>Geräte in fixer Reihenfolge (entspricht D1..D6 für Gruppe 1).</summary>
    public static readonly string[] DefaultApparatus =
        { "Boden", "Pferd", "Ring", "Sprung", "Barren", "Reck" };

    /// <summary>Kategorie EPA (turnt nicht an Pferd und Ring).</summary>
    public static bool IsEpa(string? kat) =>
        !string.IsNullOrWhiteSpace(kat) &&
        string.Equals(kat.Trim(), "EPA", StringComparison.OrdinalIgnoreCase);

    /// <summary>Geräte, an denen EPA-Athleten nicht turnen.</summary>
    public static bool IsEpaExcludedApparatus(string? apparatusName) =>
        string.Equals(apparatusName, "Pferd", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(apparatusName, "Ring", StringComparison.OrdinalIgnoreCase);
}
