using System.Text;
using Backend_RankingGeKu.Models;

namespace Backend_RankingGeKu.Services;

public class LatexBuilder
{
    public string BuildMany(List<(string Title, List<AthleteDto> Data)> sections)
    {
        var body = new StringBuilder();
        for (int i = 0; i < sections.Count; i++)
        {
            var (title, data) = sections[i];

            body.AppendLine($@"
{FormatTitle(title)}\par\vspace{{6mm}}

\begin{{longtable}}{{|p{{3.5cm}}|p{{3.5cm}}|p{{1.2cm}}|p{{6.0cm}}|p{{1.4cm}}|p{{1.7cm}}|p{{2.2cm}}|}}
\hline
\textbf{{Nachname}} & \textbf{{Vorname}} & \textbf{{JG}} & \textbf{{Verein}} & \textbf{{Kat.}} & \textbf{{D-Note}} & \textbf{{END-Note}} \\
\hline
\endhead
{MakeRows(data)}
\hline
\end{{longtable}}
");

            if (i < sections.Count - 1)
                body.AppendLine(@"\newpage");
        }

        return WrapDocument(body.ToString());
    }
    
    private static string WrapDocument(string body) => 
$@"\documentclass[a4paper,10pt,landscape]{{article}}
\usepackage[margin=12mm]{{geometry}}
\usepackage{{booktabs,longtable}}
\usepackage[T1]{{fontenc}}
\usepackage[utf8]{{inputenc}}
\usepackage[ngerman]{{babel}}
\usepackage{{helvet}}
\renewcommand\familydefault{{\sfdefault}}
\setlength{{\parindent}}{{0pt}}
\renewcommand\arraystretch{{1.8}} % Zeilenhöhe
\setlength\tabcolsep{{7pt}}      % Zellabstand (leicht größer)

\begin{{document}}
{body}
\end{{document}}";

    private static string MakeRows(List<AthleteDto> data)
    {
        if (data.Count == 0)
            return @"\multicolumn{7}{|c|}{\emph{(keine Einträge)}} \\ \hline";

        var sb = new StringBuilder();
        foreach (var a in data)
        {
            sb.AppendLine($"{E(a.Nachname)} & {E(a.Vorname)} & {E(a.JG)} & {E(a.Verein)} & {E(a.Kat)} & & \\\\ \\hline");
        }
        return sb.ToString();
    }

    private static string FormatTitle(string title)
    {
        var parts = title
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        if (parts.Length == 0)
            return string.Empty;

        var first = E(parts[0]); // Apparatur: groß & fett

        if (parts.Length == 1)
            return $@"{{\LARGE \textbf{{{first}}}}}";

        // Zweite Zeile leicht kleiner + etwas Abstand
        var rest = string.Join(@"\\", parts.Skip(1).Select(E));
        return $@"{{\LARGE \textbf{{{first}}}}}\\[3mm]{{\Large {rest}}}";
    }

    private static string E(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
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
}
