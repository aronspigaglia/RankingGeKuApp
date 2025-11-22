using System.Diagnostics;

namespace Backend_RankingGeKu.Services;

public class PdfCompiler
{
    private readonly string _enginePath;

    // enginePath: "tectonic" (im PATH) oder absoluter Pfad zur exe
    public PdfCompiler(string enginePath = "tectonic")
    {
        _enginePath = enginePath;
    }

    public async Task<byte[]> CompileAsync(string latexSource, CancellationToken ct = default)
    {
        var workdir = Directory.CreateTempSubdirectory("notesheets_");
        var texPath = Path.Combine(workdir.FullName, "notesheets.tex");
        await File.WriteAllTextAsync(texPath, latexSource);

        var psi = new ProcessStartInfo
        {
            FileName = _enginePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workdir.FullName
        };

        // tectonic Argumente
        psi.ArgumentList.Add(Path.GetFileName(texPath));
        psi.ArgumentList.Add("--keep-logs");
        psi.ArgumentList.Add("--keep-intermediates");

        using var p = Process.Start(psi)!;
        var stdout = await p.StandardOutput.ReadToEndAsync();
        var stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync(ct);

        if (!string.IsNullOrWhiteSpace(stdout)) Console.WriteLine(stdout);
        if (!string.IsNullOrWhiteSpace(stderr)) Console.Error.WriteLine(stderr);
        if (p.ExitCode != 0) throw new Exception($"LaTeX-Compiler ExitCode {p.ExitCode}");

        var pdfPath = Path.Combine(workdir.FullName, "notesheets.pdf");
        if (!File.Exists(pdfPath)) throw new FileNotFoundException("PDF nicht erzeugt.", pdfPath);

        var bytes = await File.ReadAllBytesAsync(pdfPath, ct);

        try { workdir.Delete(true); } catch { /* ignore */ }
        return bytes;
    }
}