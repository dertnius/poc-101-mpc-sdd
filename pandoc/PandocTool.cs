using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Configuration for the Pandoc tool. Loaded from pandoc-settings.json next to the executable.
/// </summary>
public sealed class PandocSettings
{
    /// <summary>
    /// Where the portable Pandoc binary is stored. Defaults to %LOCALAPPDATA%\pandoc-portable.
    /// </summary>
    [JsonPropertyName("portableDirectory")]
    public string? PortableDirectory { get; set; }

    /// <summary>
    /// Direct download URL for the Pandoc archive (zip or tar.gz).
    /// Use this for enterprise Nexus / Artifactory repos.
    /// When set, the GitHub API lookup is skipped entirely.
    /// Example: "https://nexus.corp.com/repository/tools/pandoc/pandoc-3.1.11-windows-x86_64.zip"
    /// </summary>
    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// Extra curl arguments (e.g. ["--insecure", "--proxy", "http://proxy:8080", "-u", "user:pass"]).
    /// Only used when downloadUrl is set (curl is used instead of HttpClient).
    /// </summary>
    [JsonPropertyName("curlArgs")]
    public string[]? CurlArgs { get; set; }

    /// <summary>
    /// If true, always use curl even for GitHub downloads. Default is false (curl only for custom URLs).
    /// </summary>
    [JsonPropertyName("alwaysUseCurl")]
    public bool AlwaysUseCurl { get; set; }

    private static PandocSettings? _cached;

    public static PandocSettings Load()
    {
        if (_cached is not null)
            return _cached;

        var settingsPath = Path.Combine(AppContext.BaseDirectory, "pandoc-settings.json");
        if (File.Exists(settingsPath))
        {
            var json = File.ReadAllText(settingsPath);
            _cached = JsonSerializer.Deserialize<PandocSettings>(json) ?? new PandocSettings();
        }
        else
        {
            _cached = new PandocSettings();
        }

        return _cached;
    }
}

[McpServerToolType]
public sealed class PandocTool
{
    private static string PortableDir
    {
        get
        {
            var settings = PandocSettings.Load();
            if (!string.IsNullOrWhiteSpace(settings.PortableDirectory))
                return settings.PortableDirectory;
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "pandoc-portable");
        }
    }

    private static string? _resolvedPandocPath;

    private readonly ILogger<PandocTool> _logger;

    public PandocTool(ILogger<PandocTool> logger)
    {
        _logger = logger;
    }

    [McpServerTool, Description("List .docx files in a directory without converting them.")]
    public string ListDocxFiles(
        [Description("Absolute path to the directory to scan for .docx files")] string inputDirectory,
        [Description("If true, also scan subdirectories for .docx files. Defaults to false.")] bool recursive = false)
    {
        if (!Directory.Exists(inputDirectory))
            return $"Error: Directory '{inputDirectory}' does not exist.";

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var docxFiles = Directory.GetFiles(inputDirectory, "*.docx", searchOption);

        if (docxFiles.Length == 0)
            return $"No .docx files found in '{inputDirectory}'.";

        var lines = docxFiles.Select(f => $"  - {Path.GetRelativePath(inputDirectory, f)}");
        return $"Found {docxFiles.Length} .docx file(s) in '{inputDirectory}':\n" + string.Join("\n", lines);
    }

    [McpServerTool, Description("Convert all .docx files in a directory to Markdown using Pandoc. Pandoc is auto-downloaded if not installed.")]
    public async Task<string> ConvertDocxToMarkdown(
        [Description("Absolute path to the directory containing .docx files")] string inputDirectory,
        [Description("Optional: absolute path to the output directory for .md files. Defaults to the input directory.")] string? outputDirectory = null,
        [Description("If true, also scan subdirectories for .docx files. Defaults to false.")] bool recursive = false,
        [Description("Markdown flavor: markdown (default), gfm, commonmark, markdown_strict")] string format = "markdown",
        [Description("If true, re-convert all files even if the .md is newer than the .docx. Defaults to false.")] bool force = false,
        [Description("If true, extract embedded images to a 'media' subfolder in the output directory. Defaults to true.")] bool extractMedia = true)
    {
        _logger.LogInformation("ConvertDocxToMarkdown called. inputDirectory={InputDir} outputDirectory={OutputDir} recursive={Recursive} format={Format} force={Force} extractMedia={ExtractMedia}",
            inputDirectory, outputDirectory ?? "(same as input)", recursive, format, force, extractMedia);

        if (!Directory.Exists(inputDirectory))
        {
            _logger.LogError("Input directory does not exist: {InputDir}", inputDirectory);
            return $"Error: Directory '{inputDirectory}' does not exist.";
        }

        string pandocPath;
        try
        {
            pandocPath = await ResolvePandocAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve Pandoc executable");
            return $"Error: Could not find or download Pandoc: {ex.Message}";
        }

        // Validate format
        var allowedFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "markdown", "gfm", "commonmark", "markdown_strict" };
        if (!allowedFormats.Contains(format))
            return $"Error: Unsupported format '{format}'. Allowed: {string.Join(", ", allowedFormats)}";

        var outDir = outputDirectory ?? inputDirectory;
        Directory.CreateDirectory(outDir);

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var docxFiles = Directory.GetFiles(inputDirectory, "*.docx", searchOption);
        if (docxFiles.Length == 0)
        {
            _logger.LogWarning("No .docx files found in '{InputDir}'", inputDirectory);
            return $"No .docx files found in '{inputDirectory}'.";
        }

        _logger.LogInformation("Found {Count} .docx file(s) to convert", docxFiles.Length);

        var results = new List<string>();

        foreach (var docxFile in docxFiles)
        {
            var relativePath = Path.GetRelativePath(inputDirectory, docxFile);
            var fileName = Path.GetFileNameWithoutExtension(docxFile);
            var fileOutDir = Path.Combine(outDir, Path.GetDirectoryName(relativePath) ?? "");
            Directory.CreateDirectory(fileOutDir);
            var mdFile = Path.Combine(fileOutDir, fileName + ".md");

            // Incremental: skip if .md is newer than .docx
            if (!force && File.Exists(mdFile) && File.GetLastWriteTimeUtc(mdFile) >= File.GetLastWriteTimeUtc(docxFile))
            {
                _logger.LogInformation("SKIP: {File} (up to date)", Path.GetFileName(docxFile));
                results.Add($"SKIP: {Path.GetFileName(docxFile)} (up to date)");
                continue;
            }

            _logger.LogInformation("Converting {File} -> {Output}", Path.GetFileName(docxFile), mdFile);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = pandocPath,
                    WorkingDirectory = fileOutDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add(docxFile);
                psi.ArgumentList.Add("-f");
                psi.ArgumentList.Add("docx");
                psi.ArgumentList.Add("-t");
                // Disable link_attributes and raw_html to get clean ![](image.png) without dimension noise
                psi.ArgumentList.Add(format + "-link_attributes-raw_html");
                psi.ArgumentList.Add("-o");
                psi.ArgumentList.Add(mdFile);
                psi.ArgumentList.Add("--wrap=none");
                if (extractMedia)
                {
                    // Use "." so pandoc creates media/ in the output dir with relative paths in the markdown
                    psi.ArgumentList.Add("--extract-media=.");
                }

                using var process = Process.Start(psi);
                if (process is null)
                {
                    _logger.LogError("Could not start pandoc process for {File}", Path.GetFileName(docxFile));
                    results.Add($"FAIL: {Path.GetFileName(docxFile)} — could not start pandoc");
                    continue;
                }

                var stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    _logger.LogInformation("OK: {File}", Path.GetFileName(docxFile));
                    results.Add($"OK: {Path.GetFileName(docxFile)} -> {mdFile}");
                }
                else
                {
                    _logger.LogError("Pandoc exited with code {ExitCode} for {File}: {Stderr}",
                        process.ExitCode, Path.GetFileName(docxFile), stderr.Trim());
                    results.Add($"FAIL: {Path.GetFileName(docxFile)} — pandoc exit code {process.ExitCode}: {stderr.Trim()}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception converting {File}", Path.GetFileName(docxFile));
                results.Add($"FAIL: {Path.GetFileName(docxFile)} — {ex.Message}");
            }
        }

        var converted = results.Count(r => r.StartsWith("OK"));
        var skipped = results.Count(r => r.StartsWith("SKIP"));
        var failed = results.Count(r => r.StartsWith("FAIL"));
        var summary = $"Converted {converted} of {docxFiles.Length} files (skipped {skipped}, failed {failed}):\n" +
                      string.Join("\n", results);
        _logger.LogInformation("Done. {Summary}", summary);
        return summary;
    }

    /// <summary>
    /// Resolves a working pandoc executable path:
    ///   1. Check if pandoc is on PATH
    ///   2. Check if a portable copy already exists
    ///   3. Try winget install (60 s timeout)
    ///   4. Fall back to downloading the portable zip from GitHub
    /// </summary>
    private async Task<string> ResolvePandocAsync()
    {
        if (_resolvedPandocPath is not null && File.Exists(_resolvedPandocPath))
        {
            _logger.LogInformation("Using cached pandoc path: {Path}", _resolvedPandocPath);
            return _resolvedPandocPath;
        }

        // 1. Check PATH
        _logger.LogInformation("Step 1: Checking if pandoc is on PATH...");
        var pathPandoc = await TryRunPandocAsync("pandoc");
        if (pathPandoc is not null)
        {
            _logger.LogInformation("Found pandoc on PATH: {Path}", pathPandoc);
            _resolvedPandocPath = pathPandoc;
            return _resolvedPandocPath;
        }
        _logger.LogInformation("pandoc not found on PATH");

        // 2. Check existing portable installation
        _logger.LogInformation("Step 2: Checking for portable pandoc in {PortableDir}...", PortableDir);
        var portableExe = FindPortableExe();
        if (portableExe is not null)
        {
            _logger.LogInformation("Found portable pandoc: {Path}", portableExe);
            _resolvedPandocPath = portableExe;
            return _resolvedPandocPath;
        }
        _logger.LogInformation("No portable pandoc found");

        // 3. Try winget install (Windows only, 60 s timeout)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogInformation("Step 3: Attempting winget install (timeout 60 s)...");
            var installed = await TryWingetInstallAsync();
            if (installed)
            {
                _logger.LogInformation("winget install succeeded. Verifying pandoc on PATH...");
                pathPandoc = await TryRunPandocAsync("pandoc");
                if (pathPandoc is not null)
                {
                    _logger.LogInformation("pandoc available after winget install: {Path}", pathPandoc);
                    _resolvedPandocPath = pathPandoc;
                    return _resolvedPandocPath;
                }
                _logger.LogWarning("winget reported success but pandoc still not found on PATH");
            }
            else
            {
                _logger.LogInformation("winget install failed or timed out; falling back to portable download");
            }
        }

        // 4. Download portable zip from GitHub releases
        _logger.LogInformation("Step 4: Downloading portable pandoc from GitHub releases to {PortableDir}...", PortableDir);
        await DownloadPortablePandocAsync();
        portableExe = FindPortableExe();
        if (portableExe is not null)
        {
            _logger.LogInformation("Portable pandoc downloaded and ready: {Path}", portableExe);
            _resolvedPandocPath = portableExe;
            return _resolvedPandocPath;
        }

        throw new InvalidOperationException(
            "Pandoc could not be found, installed, or downloaded. " +
            "Please install it manually from https://pandoc.org/installing.html");
    }

    private async Task<string?> TryRunPandocAsync(string pandocPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pandocPath,
                ArgumentList = { "--version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return null;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0 ? pandocPath : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindPortableExe()
    {
        if (!Directory.Exists(PortableDir))
            return null;

        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pandoc.exe" : "pandoc";

        // Search recursively — the zip may contain a versioned subfolder
        var candidates = Directory.GetFiles(PortableDir, exeName, SearchOption.AllDirectories);
        return candidates.Length > 0 ? candidates[0] : null;
    }

    private async Task<bool> TryWingetInstallAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "winget",
                ArgumentList = { "install", "--id", "JohnMacFarlane.Pandoc", "-e", "--accept-source-agreements", "--accept-package-agreements" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null)
            {
                _logger.LogWarning("Could not start winget process");
                return false;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("winget install timed out after 60 s; killing process");
                try { proc.Kill(entireProcessTree: true); } catch { /* best effort */ }
                return false;
            }

            if (proc.ExitCode != 0)
            {
                var stderr = await proc.StandardError.ReadToEndAsync();
                _logger.LogWarning("winget exited with code {ExitCode}: {Stderr}", proc.ExitCode, stderr.Trim());
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "winget install threw an exception");
            return false;
        }
    }

    private async Task DownloadPortablePandocAsync()
    {
        Directory.CreateDirectory(PortableDir);

        var settings = PandocSettings.Load();

        // If a custom download URL is configured, use curl (enterprise Nexus / Artifactory)
        if (!string.IsNullOrWhiteSpace(settings.DownloadUrl))
        {
            _logger.LogInformation("Using configured downloadUrl: {Url}", settings.DownloadUrl);
            await DownloadWithCurlAsync(settings.DownloadUrl, settings.CurlArgs);
            return;
        }

        _logger.LogInformation("Resolving latest Pandoc release from GitHub API...");
        string downloadUrl;
        string archiveExtension;
        (downloadUrl, archiveExtension) = await ResolveGitHubDownloadUrlAsync();
        _logger.LogInformation("Resolved download URL: {Url}", downloadUrl);

        if (settings.AlwaysUseCurl)
        {
            _logger.LogInformation("Downloading via curl...");
            await DownloadWithCurlAsync(downloadUrl, settings.CurlArgs);
        }
        else
        {
            _logger.LogInformation("Downloading via HttpClient...");
            await DownloadWithHttpClientAsync(downloadUrl, archiveExtension);
        }
    }

    private async Task<(string url, string extension)> ResolveGitHubDownloadUrlAsync()
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("PandocMcpTool/1.0");

        var releaseUrl = "https://api.github.com/repos/jgm/pandoc/releases/latest";
        var releaseJson = await http.GetStringAsync(releaseUrl);
        using var doc = JsonDocument.Parse(releaseJson);

        string? downloadUrl = null;
        string archiveExtension;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            archiveExtension = ".zip";
            foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.Contains("windows") && name.Contains("x86_64") && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }
            if (downloadUrl is null)
            {
                foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith("-windows-x86_64.zip", StringComparison.OrdinalIgnoreCase)
                        || (name.Contains("win") && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            archiveExtension = ".tar.gz";
            foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.Contains("linux") && name.Contains("x86_64") && name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }
        }
        else
        {
            archiveExtension = ".zip";
            foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.Contains("macOS") && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }
        }

        if (downloadUrl is null)
            throw new InvalidOperationException("Could not find a suitable Pandoc release for this platform.");

        return (downloadUrl, archiveExtension);
    }

    /// <summary>
    /// Downloads using curl — supports enterprise proxies, custom certs, and Nexus/Artifactory auth.
    /// </summary>
    private async Task DownloadWithCurlAsync(string url, string[]? extraArgs)
    {
        var archiveExtension = url.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ? ".tar.gz" : ".zip";
        var archivePath = Path.Combine(PortableDir, "pandoc-download" + archiveExtension);

        var psi = new ProcessStartInfo
        {
            FileName = "curl",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("-fSL");
        if (extraArgs is not null)
        {
            foreach (var arg in extraArgs)
                psi.ArgumentList.Add(arg);
        }
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(archivePath);
        psi.ArgumentList.Add(url);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Could not start curl. Ensure curl is available on PATH.");

        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"curl download failed (exit {proc.ExitCode}): {stderr.Trim()}");

        _logger.LogInformation("curl download complete. Extracting {Archive}...", archivePath);
        ExtractArchive(archivePath, archiveExtension);
    }

    private async Task DownloadWithHttpClientAsync(string downloadUrl, string archiveExtension)
    {
        var archivePath = Path.Combine(PortableDir, "pandoc-download" + archiveExtension);

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("PandocMcpTool/1.0");

        using (var stream = await http.GetStreamAsync(downloadUrl))
        using (var fs = File.Create(archivePath))
        {
            await stream.CopyToAsync(fs);
        }

        _logger.LogInformation("Download complete. Extracting {Archive}...", archivePath);
        ExtractArchive(archivePath, archiveExtension);
    }

    private void ExtractArchive(string archivePath, string archiveExtension)
    {
        if (archiveExtension == ".zip")
        {
            ZipFile.ExtractToDirectory(archivePath, PortableDir, overwriteFiles: true);
        }
        else
        {
            var psi = new ProcessStartInfo
            {
                FileName = "tar",
                ArgumentList = { "xzf", archivePath, "-C", PortableDir },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Could not start tar to extract Pandoc.");
            proc.WaitForExit();
            if (proc.ExitCode != 0)
                throw new InvalidOperationException("tar extraction failed.");
        }

        _logger.LogInformation("Extraction complete. Cleaning up archive...");
        try { File.Delete(archivePath); } catch { /* best effort */ }
    }
}
