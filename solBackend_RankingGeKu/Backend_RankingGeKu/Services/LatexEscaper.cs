namespace Backend_RankingGeKu.Services;

/// <summary>Escaped Benutzereingaben für die Verwendung in LaTeX-Quelltext.</summary>
public static class LatexEscaper
{
    public static string Escape(string? s)
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
}
