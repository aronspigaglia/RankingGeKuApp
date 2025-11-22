namespace Backend_RankingGeKu.Models;

public class RankingAthleteDto
{
    public string Nachname { get; set; } = string.Empty;
    public string Vorname { get; set; } = string.Empty;
    public string Jg { get; set; } = string.Empty;
    public string Verein { get; set; } = string.Empty;
    public string Kat { get; set; } = string.Empty;

    public int GroupIndex { get; set; }

    // D1..D6
    public List<PerDurchgangNotesDto> Notes { get; set; } = new();
}