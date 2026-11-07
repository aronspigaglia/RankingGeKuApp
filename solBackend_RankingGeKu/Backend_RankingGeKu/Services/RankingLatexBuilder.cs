using System.Globalization;
using System.Text;
using Backend_RankingGeKu.Models;

namespace Backend_RankingGeKu.Services;

/// <summary>Erzeugt das LaTeX-Dokument für die Rangliste einer Kategorie.</summary>
public class RankingLatexBuilder
{
    /// <param name="title">Dokument-Titel, z. B. "Rangliste Kutu P1".</param>
    /// <param name="rows">Bereits gefilterte und sortierte Zeilen der Kategorie.</param>
    /// <param name="apparatus">Alle Geräte-Namen (Index passend zu den Noten-Arrays).</param>
    /// <param name="visibleDeviceIndices">Indizes der Geräte, die als Spalten erscheinen.</param>
    public string Build(
        string title,
        IReadOnlyList<RankingRow> rows,
        IReadOnlyList<string> apparatus,
        IReadOnlyList<int> visibleDeviceIndices)
    {
        var body = new StringBuilder();
        body.AppendLine($@"\section*{{{LatexEscaper.Escape(title)}}}");
        AppendTable(body, rows, apparatus, visibleDeviceIndices);
        return WrapDocument(body.ToString());
    }

    /// <summary>Tabelle: Ausz., Rang, Name, Verein, JG, pro Gerät (E / D / Rang), Total.</summary>
    private static void AppendTable(
        StringBuilder body,
        IReadOnlyList<RankingRow> rows,
        IReadOnlyList<string> apparatus,
        IReadOnlyList<int> visibleDeviceIndices)
    {
        body.AppendLine(@"{\fontsize{8pt}{8.5pt}\selectfont"); // kleiner als \small
        body.AppendLine(@"\rowcolors{3}{rowgray}{white}"); // ab der 1. Datenzeile (nach 2 Headerzeilen) einfärben
        body.Append(@"\begin{tabular}{c l l l l l");
        body.Append(new string('r', visibleDeviceIndices.Count * 3));
        body.AppendLine(" >{\\bfseries}r}");

        // 1. Headerzeile: Geräte-Namen über je 3 Spalten
        body.Append(@" & \textbf{Rang} & \textbf{Nachname} & \textbf{Vorname} & \textbf{Verein} & \textbf{JG}");
        foreach (var deviceIdx in visibleDeviceIndices)
        {
            body.Append(" & \\multicolumn{3}{l}{\\textbf{" + LatexEscaper.Escape(apparatus[deviceIdx]) + "}}");
        }
        body.AppendLine(" & \\textbf{Total} \\\\");

        // 2. Headerzeile: E / D / (Rang) unter jedem Gerät
        body.Append(" &  &  &  &  & "); // 6 Basis-Spalten leer
        foreach (var _ in visibleDeviceIndices)
        {
            body.Append(" & E & {\\fontsize{6pt}{7pt}\\selectfont D} & {\\fontsize{6pt}{7pt}\\selectfont (R)}");
        }
        body.AppendLine(" & \\\\");
        body.AppendLine(@"\hline");

        foreach (var row in rows)
        {
            AppendRow(body, row, visibleDeviceIndices);
        }

        body.AppendLine(@"\end{tabular}");
        body.AppendLine(@"}");
    }

    private static void AppendRow(StringBuilder body, RankingRow r, IReadOnlyList<int> visibleDeviceIndices)
    {
        var smiley = r.Awarded ? "$\\smiley$" : string.Empty;

        body.Append(
            $"{smiley} & {r.Rank} & {LatexEscaper.Escape(r.Nachname)} & {LatexEscaper.Escape(r.Vorname)} & {LatexEscaper.Escape(r.Verein)} & {LatexEscaper.Escape(r.Jg)}");

        foreach (var deviceIdx in visibleDeviceIndices)
        {
            var e = deviceIdx < r.DeviceEndScores.Length ? r.DeviceEndScores[deviceIdx] : 0m;
            var d = deviceIdx < r.DeviceDNotes.Length ? r.DeviceDNotes[deviceIdx] : 0m;
            var devRank = deviceIdx < r.DeviceRanks.Length ? r.DeviceRanks[deviceIdx] : 0;

            var eStr = e == 0m ? string.Empty : FormatScore(e);
            var dStr = d == 0m ? string.Empty : $@"\smallD{{{FormatScore(d)}}}";
            var devRankStr = devRank > 0 ? $@"\smallR{{{devRank}}}" : string.Empty;

            body.Append($" & {eStr} & {dStr} & {devRankStr}");
        }

        body.AppendLine(" & \\textbf{" + FormatScore(r.Total) + "} \\\\");
    }

    private static string FormatScore(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string WrapDocument(string body) => $@"
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

% helles Grau für alternierende Zeilen
\definecolor{{rowgray}}{{RGB}}{{215,215,215}}

% Header / Footer
\pagestyle{{fancy}}
\fancyhf{{}}
\renewcommand{{\headrulewidth}}{{0pt}}
\renewcommand{{\footrulewidth}}{{0pt}}
\lhead{{\small {DateTime.UtcNow:dd.MM.yyyy}}}
\rhead{{\includegraphics[height=14mm]{{{{geku-logo.png}}}}}}
\lfoot{{\small Geku Rickenbach 21/22 März 2026}}
\cfoot{{\small www.geku.ch}}
\rfoot{{\includegraphics[height=10mm]{{{{alltex-logo.png}}}}\hspace{{5mm}}\includegraphics[height=10mm]{{{{Schaerli_und_Partner-logo.png}}}}}}

\begin{{document}}
{body}
\end{{document}}
";
}
