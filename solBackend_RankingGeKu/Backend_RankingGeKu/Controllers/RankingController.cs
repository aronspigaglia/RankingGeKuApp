using Backend_RankingGeKu.Models;
using Backend_RankingGeKu.Services;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;


namespace Backend_RankingGeKu.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RankingController : ControllerBase
{
    private readonly PdfCompiler _pdfCompiler;

    public RankingController(PdfCompiler pdfCompiler)
    {
        _pdfCompiler = pdfCompiler;
    }
    
    private sealed class RankingRow
    {
        public string Kat { get; set; } = string.Empty;
        public string Nachname { get; set; } = string.Empty;
        public string Vorname { get; set; } = string.Empty;
        public string Jg { get; set; } = string.Empty;
        public string Verein { get; set; } = string.Empty;

        public decimal Total { get; set; }
        public int Rank { get; set; }

        // verwendete Geräte-Note (für Total / Geräte-Rang)
        public decimal[] DeviceUsedScores { get; set; } = Array.Empty<decimal>();

        // separat Endnote / D-Note je Gerät
        public decimal[] DeviceEndScores { get; set; } = Array.Empty<decimal>();
        public decimal[] DeviceDNotes { get; set; } = Array.Empty<decimal>();

        // Geräte-Ränge je Gerät (1..n), 0 = kein Rang
        public int[] DeviceRanks { get; set; } = Array.Empty<int>();

        // true = Auszeichnung (Top 40%)
        public bool Awarded { get; set; }
    }




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

    var rows = BuildRankingRows(request);
    AssignRanksPerCategory(rows);

    var totalAthletes = rows.Count;
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

    // Apparate-Namen: entweder vom Frontend oder Default
    string[] apparatus = request.Apparatus != null && request.Apparatus.Count == 6
        ? request.Apparatus.ToArray()
        : new[] { "Boden", "Pferd", "Ring", "Sprung", "Barren", "Reck" };

    // Geräte-Ränge berechnen (pro Kat, pro Gerät)
    AssignDeviceRanksPerCategory(rows, apparatus.Length);

    var kat = distinctKats[0]; // Frontend schickt eine Kat pro Request
    var katList = string.Join(", ", distinctKats);
    var title = "Rangliste Kutu " + katList;

    var katRows = rows
        .Where(r => r.Kat == kat)
        .OrderBy(r => r.Rank)
        .ThenBy(r => r.Nachname)
        .ThenBy(r => r.Vorname)
        .ToList();


    var bodyBuilder = new StringBuilder();

    bodyBuilder.AppendLine($@"\section*{{{EscapeLatex(title)}}}");

    // Tabellenkopf mit 3 Spalten pro Gerät
    // Spalten: Rang, Ausz, Nachname, Vorname, Verein, JG, (E,D,(Rang))*6, Total


    bodyBuilder.AppendLine(@"{\fontsize{8pt}{8.5pt}\selectfont"); // kleiner als \small
    bodyBuilder.AppendLine(@"\rowcolors{3}{rowgray}{white}"); // ab der 1. Datenzeile (nach 2 Headerzeilen) einfärben
    bodyBuilder.Append(@"\begin{tabular}{c l l l l l");
    bodyBuilder.Append(new string('r', apparatus.Length * 3));
    bodyBuilder.AppendLine(" >{\\bfseries}r}");

    // 1. Headerzeile
    bodyBuilder.Append(@" & \textbf{Rang} & \textbf{Nachname} & \textbf{Vorname} & \textbf{Verein} & \textbf{JG}");
    foreach (var app in apparatus)
    {
        bodyBuilder.Append(" & \\multicolumn{3}{l}{\\textbf{" + EscapeLatex(app) + "}}");
    }
    bodyBuilder.AppendLine(" & \\textbf{Total} \\\\");

    // 2. Headerzeile: E / D / (Rang) unter jedem Gerät
    bodyBuilder.Append(" &  &  &  &  & "); // 6 Basis-Spalten leer
    foreach (var _ in apparatus)
    {
        bodyBuilder.Append(" & E & {\\fontsize{6pt}{7pt}\\selectfont D} & {\\fontsize{6pt}{7pt}\\selectfont (R)}");
    }
    bodyBuilder.AppendLine(" & \\\\");
    bodyBuilder.AppendLine(@"\hline");
        

    // Datenzeilen
    foreach (var r in katRows)
    {
        var totalStr = r.Total.ToString("0.00", CultureInfo.InvariantCulture);
        var smiley = r.Awarded ? "$\\smiley$" : string.Empty;

        bodyBuilder.Append(
            $"{smiley} & {r.Rank} & {EscapeLatex(r.Nachname)} & {EscapeLatex(r.Vorname)} & {EscapeLatex(r.Verein)} & {EscapeLatex(r.Jg)}");

        for (int i = 0; i < apparatus.Length; i++)
        {
            decimal e = (r.DeviceEndScores != null && i < r.DeviceEndScores.Length) ? r.DeviceEndScores[i] : 0m;
            decimal d = (r.DeviceDNotes != null && i < r.DeviceDNotes.Length) ? r.DeviceDNotes[i] : 0m;
            int devRank = (r.DeviceRanks != null && i < r.DeviceRanks.Length) ? r.DeviceRanks[i] : 0;

            string eStr = e == 0m 
                ? string.Empty 
                : e.ToString("0.00", CultureInfo.InvariantCulture);

            string dStr = d == 0m 
                ? string.Empty 
                : $@"\smallD{{{d.ToString("0.00", CultureInfo.InvariantCulture)}}}";

            string devRankStr = devRank > 0
                ? $@"\smallR{{{devRank}}}"
                : string.Empty;

            bodyBuilder.Append($" & {eStr} & {dStr} & {devRankStr}");
        }

        bodyBuilder.AppendLine(" & \\textbf{" + totalStr + "} \\\\");
    }

    bodyBuilder.AppendLine(@"\end{tabular}");
    bodyBuilder.AppendLine(@"}");  

    var latex = $@"
\documentclass[10pt]{{article}}
\usepackage[a4paper,landscape,top=12mm,bottom=18mm,left=10mm,right=10mm,includefoot]{{geometry}}
\usepackage[T1]{{fontenc}}
\usepackage[utf8]{{inputenc}}
\usepackage[ngerman]{{babel}}
\usepackage{{helvet}}
\usepackage{{wasysym}}
\usepackage{{booktabs}}
\usepackage[table]{{xcolor}}
\usepackage{{graphicx}}
\usepackage{{fancyhdr}}
\setlength{{\headheight}}{{18mm}} % mehr Platz für größeres Header-Bild
\setlength{{\headsep}}{{5mm}}     % Abstand zwischen Header und Inhalt
\setlength{{\footskip}}{{10mm}}   % mehr Platz im Footer-Bereich
\renewcommand{{\arraystretch}}{{1.3}} % mehr Zeilenabstand

\renewcommand\familydefault{{\sfdefault}}

% kleinere Noten (explizit kleiner als Tabellenfont)
\newcommand{{\smallD}}[1]{{{{\fontsize{{6pt}}{{7pt}}\selectfont #1}}}}
\newcommand{{\smallR}}[1]{{{{\fontsize{{6pt}}{{7pt}}\selectfont (#1)}}}}

% sehr helles blau für alternate rows
\definecolor{{rowgray}}{{RGB}}{{215,215,215}} % deutlich dunkler für bessere Lesbarkeit


% Header / Footer
\pagestyle{{fancy}}
\fancyhf{{}}
\renewcommand{{\headrulewidth}}{{0pt}}
\renewcommand{{\footrulewidth}}{{0pt}}
\rhead{{\includegraphics[height=14mm]{{{{geku-logo.png}}}}}}
\lfoot{{\small Rangliste}}
\cfoot{{\small Kutu}}
\rfoot{{\includegraphics[height=10mm]{{{{alltex-logo.png}}}}}}

\begin{{document}}
{bodyBuilder}
\end{{document}}
";


    var pdfBytes = await _pdfCompiler.CompileAsync(latex, ct);

    string fileName;
    if (distinctKats.Count == 1 && !string.IsNullOrWhiteSpace(distinctKats[0]))
    {
        fileName = $"Rangliste_{distinctKats[0]}.pdf";
    }
    else
    {
        fileName = "Rangliste.pdf";
    }

    return File(pdfBytes, "application/pdf", fileName);
}



    private static string EscapeLatex(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;

        return s
            .Replace(@"\", @"\textbackslash{}")
            .Replace("&", @"\&")
            .Replace("%", @"\%")
            .Replace("$", @"\$")
            .Replace("#", @"\#")
            .Replace("_", @"\_")
            .Replace("{", @"\{")
            .Replace("}", @"\}")
            .Replace("~", @"\textasciitilde{}")
            .Replace("^", @"\textasciicircum{}");
    }
    private static decimal ParseScore(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;

        // Komma und Punkt zulassen
        s = s.Trim().Replace(',', '.');

        return decimal.TryParse(s, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var d)
            ? d
            : 0m;
    }
    private static List<RankingRow> BuildRankingRows(RankingRequestDto request)
    {
        var rows = new List<RankingRow>();

        foreach (var a in request.Athletes)
        {
            var deviceUsed = new decimal[6];
            var deviceEnd  = new decimal[6];
            var deviceD    = new decimal[6];
            decimal total  = 0m;

            if (a.Notes != null)
            {
                for (int i = 0; i < 6 && i < a.Notes.Count; i++)
                {
                    var n = a.Notes[i];
                    var endScore = ParseScore(n?.EndNote);
                    var dScore   = ParseScore(n?.DNote);

                    deviceEnd[i] = endScore;
                    deviceD[i]   = dScore;

                    var used = endScore != 0m ? endScore : dScore;
                    deviceUsed[i] = used;
                    total += used;
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
                DeviceDNotes = deviceD,
                DeviceRanks = new int[6]
            });
        }

        return rows;
    }


    private static void AssignRanksPerCategory(List<RankingRow> rows)
    {
        var groupsByKat = rows
            .GroupBy(r => r.Kat)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var katGroup in groupsByKat)
        {
            var ordered = katGroup
                .OrderByDescending(r => r.Total)
                .ThenBy(r => r.Nachname)
                .ThenBy(r => r.Vorname)
                .ToList();

            int index = 0;
            int currentRank = 0;
            decimal? lastScore = null;

            foreach (var r in ordered)
            {
                index++;

                if (lastScore == null || r.Total != lastScore.Value)
                {
                    currentRank = index;
                    lastScore = r.Total;
                }

                r.Rank = currentRank;
            }

            // Top 40 % bekommen Auszeichnung (Smiley)
            var count = ordered.Count;
            if (count == 0) continue;

            var awardCount = (int)Math.Ceiling(count * 0.4m);
            var cutoffRank = ordered
                .Take(awardCount)
                .Max(r => r.Rank);

            foreach (var r in ordered)
            {
                r.Awarded = r.Rank <= cutoffRank;
            }
        }
    }
    private static void AssignDeviceRanksPerCategory(List<RankingRow> rows, int deviceCount)
    {
        var groupsByKat = rows.GroupBy(r => r.Kat);

        foreach (var katGroup in groupsByKat)
        {
            var list = katGroup.ToList();

            // sicherstellen, dass DeviceRanks lang genug ist
            foreach (var r in list)
            {
                if (r.DeviceRanks == null || r.DeviceRanks.Length < deviceCount)
                    r.DeviceRanks = new int[deviceCount];
            }

            for (int d = 0; d < deviceCount; d++)
            {
                var withScore = list
                    .Where(r => r.DeviceUsedScores != null
                                && r.DeviceUsedScores.Length > d
                                && r.DeviceUsedScores[d] > 0m)
                    .OrderByDescending(r => r.DeviceUsedScores[d])
                    .ThenBy(r => r.Nachname)
                    .ThenBy(r => r.Vorname)
                    .ToList();

                int index = 0;
                int currentRank = 0;
                decimal? lastScore = null;

                foreach (var r in withScore)
                {
                    index++;

                    if (lastScore == null || r.DeviceUsedScores[d] != lastScore.Value)
                    {
                        currentRank = index;
                        lastScore = r.DeviceUsedScores[d];
                    }

                    r.DeviceRanks[d] = currentRank;
                }
            }
        }
    }


}
