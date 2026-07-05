using System.Diagnostics;

namespace Backend_RankingGeKu.Services;

/// <summary>Kompiliert LaTeX-Quelltext mit Tectonic zu einem PDF.</summary>
public class PdfCompiler
{
    private static readonly TimeSpan CompileTimeout = TimeSpan.FromSeconds(60);

    private static readonly string[] LogoAssets =
    {
        "geku-logo.png",
        "alltex-logo.png",
        "Schaerli_und_Partner-logo.png"
    };

    private readonly string _enginePath;

    // enginePath: "tectonic" (im PATH) oder absoluter Pfad zur exe
    public PdfCompiler(string enginePath = "tectonic")
    {
        _enginePath = enginePath;
    }

    /// <summary>
    /// Prefer a bundled Tectonic (app/backend/tectonic/&lt;platform&gt;/tectonic[.exe]),
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
        var platform = PlatformFolderName();

        // Probe a few likely roots (AppContext + process location) to be robust in packaged apps
        var candidates = ProbeBaseDirectories();

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
        try
        {
            var texPath = Path.Combine(workdir.FullName, "notesheets.tex");
            await File.WriteAllTextAsync(texPath, latexSource, ct);

            CopyLogoAssets(workdir.FullName);
            await RunTectonicAsync(texPath, workdir.FullName, ct);

            var pdfPath = Path.Combine(workdir.FullName, "notesheets.pdf");
            if (!File.Exists(pdfPath)) throw new FileNotFoundException("PDF nicht erzeugt.", pdfPath);

            return await File.ReadAllBytesAsync(pdfPath, ct);
        }
        finally
        {
            try { workdir.Delete(true); } catch { /* ignore */ }
        }
    }

    /// <summary>Logos ins Temp-Verzeichnis kopieren, damit Tectonic sie findet. Fehlende Logos sind kein Fehler.</summary>
    private static void CopyLogoAssets(string workdir)
    {
        try
        {
            var candidates = new[]
            {
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory(),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory ?? ".", "..", "..")),          // bin/... -> project root
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory ?? ".", "..", "..", "..")),    // bei anderen Buildpfaden
            };

            var assetsDir = candidates
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => Path.Combine(p!, "assets"))
                .FirstOrDefault(Directory.Exists);

            if (assetsDir == null) return;

            foreach (var name in LogoAssets)
            {
                var src = Path.Combine(assetsDir, name);
                if (File.Exists(src))
                {
                    File.Copy(src, Path.Combine(workdir, name), overwrite: true);
                }
            }
        }
        catch
        {
            // Falls Logos fehlen oder Copy fehlschlägt: PDF läuft weiter ohne Header/Footer-Bilder
        }
    }

    /// <summary>
    /// Tectonic-Cache neben dem Backend, um den Kaltstart (Paket-Downloads) zu vermeiden.
    /// Pro Plattform ein eigener Unterordner, damit macOS/Windows/Linux sich nicht in die Quere kommen.
    /// </summary>
    private static string EnsureCacheDir()
    {
        // Basis: aktuelles WorkingDirectory (wird von Electron auf resources/backend gesetzt)
        var cacheBase = Directory.GetCurrentDirectory();
        if (string.IsNullOrWhiteSpace(cacheBase))
            cacheBase = AppContext.BaseDirectory;

        var cacheDir = Path.Combine(cacheBase, "tectonic-cache", PlatformFolderName() ?? "generic");
        Directory.CreateDirectory(cacheDir);
        return cacheDir;
    }

    private async Task RunTectonicAsync(string texPath, string workdir, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _enginePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workdir
        };
        psi.Environment["TECTONIC_CACHE_DIR"] = EnsureCacheDir();

        psi.ArgumentList.Add(Path.GetFileName(texPath));
        psi.ArgumentList.Add("--keep-logs");
        psi.ArgumentList.Add("--keep-intermediates");

        Console.WriteLine("[PDF] start tectonic");
        using var p = Process.Start(psi)!;
        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();

        // auf Exit oder Timeout warten, um Hänger zu vermeiden
        await Task.WhenAny(p.WaitForExitAsync(ct), Task.Delay(CompileTimeout, ct));
        if (!p.HasExited)
        {
            try { p.Kill(true); } catch { /* ignore */ }
            throw new Exception($"Tectonic hang: aborted after {CompileTimeout.TotalSeconds:0}s");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        Console.WriteLine($"[PDF] tectonic exit {p.ExitCode}");

        if (!string.IsNullOrWhiteSpace(stdout)) Console.WriteLine(stdout);
        if (!string.IsNullOrWhiteSpace(stderr)) Console.Error.WriteLine(stderr);
        if (p.ExitCode != 0) throw new Exception($"LaTeX-Compiler ExitCode {p.ExitCode}");
    }

    /// <summary>Wahrscheinliche Wurzelverzeichnisse (AppContext, Prozess, CWD) für die Suche nach gebündelten Dateien.</summary>
    private static List<string> ProbeBaseDirectories()
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory))
            candidates.Add(AppContext.BaseDirectory);

        var processDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (!string.IsNullOrWhiteSpace(processDir))
            candidates.Add(processDir);

        var cwd = Directory.GetCurrentDirectory();
        if (!string.IsNullOrWhiteSpace(cwd))
            candidates.Add(cwd);

        return candidates;
    }

    private static string? PlatformFolderName()
    {
        if (OperatingSystem.IsWindows()) return "windows";
        if (OperatingSystem.IsMacOS()) return "macos";
        if (OperatingSystem.IsLinux()) return "linux";
        return null;
    }
}
