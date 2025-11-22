namespace Backend_RankingGeKu.Models;

public class RankingRequestDto
{
    public string? CompetitionName { get; set; }

    // ["Boden", "Pferd", ...]
    public List<string> Apparatus { get; set; } = new();

    public List<RankingAthleteDto> Athletes { get; set; } = new();
}