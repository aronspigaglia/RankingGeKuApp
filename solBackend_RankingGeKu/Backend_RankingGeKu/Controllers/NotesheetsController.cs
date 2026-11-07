using Backend_RankingGeKu.Domain;
using Backend_RankingGeKu.Models;
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
    /// für jede Gruppe 6 Sektionen (Durchgang 1..6) mit rotiertem Gerät (Boden..Reck).
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

        var groups = await _csvParser.ParseGroupsAsync(ms, delimiter);
        var sections = BuildSections(groups);

        if (sections.Count == 0)
            return BadRequest("Keine Notenblätter erzeugt (alle Gruppen EPA an Pferd/Ring).");

        var tex = _latexBuilder.BuildMany(sections);
        var pdfBytes = await _pdfCompiler.CompileAsync(tex, ct);

        return File(pdfBytes, "application/pdf", "Notenblaetter");
    }

    /// <summary>
    /// Je Gruppe 6 Durchgänge; das Gerät rotiert pro Gruppe (Gruppe 2 startet am Pferd usw.).
    /// EPA-Athleten turnen nicht an Pferd/Ring: sie werden dort weggelassen,
    /// besteht die ganze Gruppe aus EPA, entfällt die Sektion.
    /// </summary>
    private static List<(string Title, List<AthleteDto> Data)> BuildSections(List<List<AthleteDto>> groups)
    {
        var sections = new List<(string Title, List<AthleteDto> Data)>();

        for (int g = 0; g < groups.Count; g++)
        {
            var groupData = groups[g];

            for (int d = 1; d <= Gymnastics.ApparatusCount; d++)
            {
                int appIndex = (d - 1 + g) % Gymnastics.ApparatusCount; // Rotation je Gruppe
                var appName = Gymnastics.DefaultApparatus[appIndex];
                var isExcludedForEpa = Gymnastics.IsEpaExcludedApparatus(appName);

                var sectionData = isExcludedForEpa
                    ? groupData.Where(a => !Gymnastics.IsEpa(a.Kat)).ToList()
                    : groupData;

                if (isExcludedForEpa && sectionData.Count == 0)
                    continue; // ganze Gruppe EPA -> keine Sektion erzeugen

                // Titel zweizeilig: Gerät oben, darunter Durchgang/Gruppe (per \n, wird in LaTeX zu \\)
                var title = $"{appName}\nDurchgang {d}, Gruppe {g + 1}";
                sections.Add((title, sectionData));
            }
        }

        return sections;
    }
}
