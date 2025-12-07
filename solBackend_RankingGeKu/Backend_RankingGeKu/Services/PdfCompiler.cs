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

    /// <summary>
    /// Prefer a bundled Tectonic (app/backend/tectonic/<platform>/tectonic[.exe]),
    /// fall back to an explicitly configured path, then PATH lookup ("tectonic").
    /// </summary>
    public static string ResolveEnginePath(string? configuredPath = null)
    {
        // Explicit config overrides everything
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        var exeName = OperatingSystem.IsWindows() ? "tectonic.exe" : "tectonic";
        string? platform = null;

        if (OperatingSystem.IsWindows()) platform = "windows";
        else if (OperatingSystem.IsMacOS()) platform = "macos";
        else if (OperatingSystem.IsLinux()) platform = "linux";

        // Probe a few likely roots (AppContext + process location) to be robust in packaged apps
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory))
            candidates.Add(AppContext.BaseDirectory);

        var processDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (!string.IsNullOrWhiteSpace(processDir))
            candidates.Add(processDir);

        foreach (var baseDir in candidates)
        {
            if (platform is not null)
            {
                var bundled = Path.Combine(baseDir, "tectonic", platform, exeName);
                if (File.Exists(bundled))
                {
                    Console.WriteLine($"[PDF] Using bundled Tectonic at {bundled}");
                    return bundled;
                }
            }
        }

        // Fallback: try to locate any tectonic* nearby (helps if platform folder is missing)
        foreach (var baseDir in candidates)
        {
            try
            {
                var probe = Directory
                    .EnumerateFiles(baseDir, "tectonic*", SearchOption.AllDirectories)
                    .FirstOrDefault(File.Exists);
                if (probe is not null)
                {
                    Console.WriteLine($"[PDF] Using discovered Tectonic at {probe}");
                    return probe;
                }
            }
            catch
            {
                // ignore probing errors
            }
        }

        // Fallback: rely on PATH
        Console.WriteLine("[PDF] Using Tectonic from PATH");
        return "tectonic";
    }

    public async Task<byte[]> CompileAsync(string latexSource, CancellationToken ct = default)
    {
        var workdir = Directory.CreateTempSubdirectory("notesheets_");
        var texPath = Path.Combine(workdir.FullName, "notesheets.tex");
        await File.WriteAllTextAsync(texPath, latexSource);

        // Tectonic-Cache neben dem Backend bundlen, um den Kaltstart zu vermeiden.
        // Pro Plattform ein eigener Unterordner, damit macOS/Windows/Linux sich nicht in die Quere kommen.
        var cacheRoot = Path.Combine(AppContext.BaseDirectory, "tectonic-cache");
        var cachePlatform = OperatingSystem.IsWindows() ? "windows"
            : OperatingSystem.IsMacOS() ? "macos"
            : OperatingSystem.IsLinux() ? "linux"
            : "generic";
        var cacheDir = Path.Combine(cacheRoot, cachePlatform);
        Directory.CreateDirectory(cacheDir);

        var psi = new ProcessStartInfo
        {
            FileName = _enginePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workdir.FullName
        };
        psi.Environment["TECTONIC_CACHE_DIR"] = cacheDir;

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
