using Backend_RankingGeKu.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend_RankingGeKu.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotesheetsController : ControllerBase
{
    private readonly CsvParser _csvParser;
    private readonly LatexBuilder _latexBuilder;
    private readonly PdfCompiler _pdfCompiler;

    public NotesheetsController(CsvParser csvParser, LatexBuilder latexBuilder, PdfCompiler pdfCompiler)
    {
        _csvParser = csvParser;
        _latexBuilder = latexBuilder;
        _pdfCompiler = pdfCompiler;
    }

    /// <summary>
    /// Nimmt eine CSV (ohne Header; Gruppen mit "-" getrennt), erzeugt EIN PDF:
    /// für jede Gruppe 6 Sektionen (Durchgang 1..6) mit rotiertem Apparat (Boden..Reck).
    /// </summary>
    [HttpPost("merged")]
    [Consumes("multipart/form-data")]
    [Produces("application/pdf")]
    public async Task<IActionResult> PostMerged([FromForm] IFormFile file, [FromQuery] string delimiter = ";", CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest("CSV-Datei fehlt.");

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        ms.Position = 0;

        // Gruppen aus CSV lesen (ohne Kopf; "-" trennt Gruppen)
        var groups = await _csvParser.ParseGroupsAsync(ms, delimiter);

        // Apparate (Reihenfolge fix)
        var apparatus = new[] { "Boden", "Pferd", "Ring", "Sprung", "Barren", "Reck" };

        // Sektionen: je Gruppe 6 Durchgänge, Apparat rotiert pro Gruppe
        var sections = new List<(string Title, List<Backend_RankingGeKu.Models.AthleteDto> Data)>();
        for (int g = 0; g < groups.Count; g++)
        {
            var groupData = groups[g];

            for (int d = 1; d <= 6; d++)
            {
                int appIndex = (d - 1 + g) % 6;  // Rotation je Gruppe
                string title = $"{apparatus[appIndex]} — Durchgang {d} — Gruppe {g + 1}";
                sections.Add((title, groupData));
            }
        }

        var tex = _latexBuilder.BuildMany(sections);
        var pdfBytes = await _pdfCompiler.CompileAsync(tex, ct);

        return File(pdfBytes, "application/pdf", $"Notenblaetter_{groups.Count}Gruppen_x6_merged.pdf");
    }
}
