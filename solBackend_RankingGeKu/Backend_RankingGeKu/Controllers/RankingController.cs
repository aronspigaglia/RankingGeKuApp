using Backend_RankingGeKu.Domain;
using Backend_RankingGeKu.Models;
using Backend_RankingGeKu.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend_RankingGeKu.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RankingController : ControllerBase
{
    private readonly RankingCalculator _calculator;
    private readonly RankingLatexBuilder _latexBuilder;
    private readonly PdfCompiler _pdfCompiler;

    public RankingController(RankingCalculator calculator, RankingLatexBuilder latexBuilder, PdfCompiler pdfCompiler)
    {
        _calculator = calculator;
        _latexBuilder = latexBuilder;
        _pdfCompiler = pdfCompiler;
    }

    /// <summary>Erzeugt die Ranglisten-PDF für die Kategorie im Request (das Frontend schickt eine Kategorie pro Request).</summary>
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] RankingRequestDto request, CancellationToken ct)
    {
        if (request.Athletes is null || request.Athletes.Count == 0)
        {
            return BadRequest(new
            {
                title = "Keine Athleten im Request.",
                detail = "Es wurden keine Athletendaten für die Rangliste übergeben."
            });
        }

        var rows = _calculator.BuildRows(request);
        _calculator.AssignRanksPerCategory(rows);

        var distinctKats = rows
            .Select(r => r.Kat)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct()
            .OrderBy(k => k)
            .ToList();

        if (distinctKats.Count == 0)
        {
            return BadRequest(new { title = "Keine Kategorien gefunden." });
        }

        // Geräte-Namen: entweder vom Frontend oder Default
        var apparatus = request.Apparatus is { Count: Gymnastics.ApparatusCount }
            ? request.Apparatus.ToArray()
            : Gymnastics.DefaultApparatus;

        _calculator.AssignDeviceRanksPerCategory(rows, apparatus.Length);

        var kat = distinctKats[0];
        var visibleDeviceIndices = _calculator.GetVisibleDeviceIndices(kat, apparatus);
        var title = "Rangliste Kutu " + string.Join(", ", distinctKats);

        var katRows = rows
            .Where(r => r.Kat == kat)
            .OrderBy(r => r.Rank)
            .ThenBy(r => r.Nachname)
            .ThenBy(r => r.Vorname)
            .ToList();

        var latex = _latexBuilder.Build(title, katRows, apparatus, visibleDeviceIndices);
        var pdfBytes = await _pdfCompiler.CompileAsync(latex, ct);

        var fileName = distinctKats.Count == 1 && !string.IsNullOrWhiteSpace(distinctKats[0])
            ? $"Rangliste_{distinctKats[0]}.pdf"
            : "Rangliste.pdf";

        return File(pdfBytes, "application/pdf", fileName);
    }
}
