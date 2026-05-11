using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace NewLauncher;

public static class LauncherUpdateService
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/ArataYoshida/NewLauncher/releases/latest";

    public static async Task<bool> CheckAndStartUpdateAsync(IProgress<string>? progress = null)
    {
        using HttpClient httpClient = CreateHttpClient();
        using HttpResponseMessage releaseResponse = await httpClient.GetAsync(LatestReleaseApiUrl);
        if (releaseResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        releaseResponse.EnsureSuccessStatusCode();
        string releaseJson = await releaseResponse.Content.ReadAsStringAsync();
        LauncherReleaseManifest manifest = await ReadManifestFromReleaseAsync(httpClient, releaseJson);
        if (!IsNewerThanCurrent(manifest.Version))
        {
            return false;
        }

        progress?.Report($"Updating launcher to {manifest.Version}...");
        string updateRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NewGameEngine",
            "LauncherUpdates",
            manifest.Version);
        string packagePath = Path.Combine(updateRoot, manifest.PackageFile);
        string extractRoot = Path.Combine(updateRoot, "extract");
        Directory.CreateDirectory(updateRoot);
        if (Directory.Exists(extractRoot))
        {
            Directory.Delete(extractRoot, recursive: true);
        }

        await using (Stream packageStream = await httpClient.GetStreamAsync(manifest.PackageDownloadUrl))
        await using (var localStream = new FileStream(packagePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await packageStream.CopyToAsync(localStream);
        }

        string actualHash = ComputeSha256(packagePath);
        if (!string.IsNullOrWhiteSpace(manifest.Sha256) &&
            !actualHash.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(packagePath);
            throw new InvalidOperationException("Downloaded launcher package SHA256 did not match launcher-manifest.json.");
        }

        Directory.CreateDirectory(extractRoot);
        ExtractZipSafely(packagePath, extractRoot);
        string launcherRoot = FindLauncherRoot(extractRoot);
        if (string.IsNullOrWhiteSpace(launcherRoot))
        {
            throw new InvalidOperationException("Launcher update package does not contain NewLauncher.exe.");
        }

        StartUpdaterScript(launcherRoot, AppContext.BaseDirectory, Environment.ProcessId);
        return true;
    }

    private static async Task<LauncherReleaseManifest> ReadManifestFromReleaseAsync(HttpClient httpClient, string releaseJson)
    {
        using JsonDocument releaseDocument = JsonDocument.Parse(releaseJson);
        JsonElement assets = releaseDocument.RootElement.GetProperty("assets");
        string manifestUrl = string.Empty;
        var assetUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            string name = asset.GetProperty("name").GetString() ?? string.Empty;
            string downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(downloadUrl))
            {
                continue;
            }

            assetUrls[name] = downloadUrl;
            if (name.Equals("launcher-manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                manifestUrl = downloadUrl;
            }
        }

        if (string.IsNullOrWhiteSpace(manifestUrl))
        {
            throw new InvalidOperationException("The release does not contain launcher-manifest.json.");
        }

        string manifestJson = await httpClient.GetStringAsync(manifestUrl);
        LauncherReleaseManifest manifest = JsonSerializer.Deserialize<LauncherReleaseManifest>(manifestJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("launcher-manifest.json is invalid.");

        if (!assetUrls.TryGetValue(manifest.PackageFile, out string? packageUrl))
        {
            throw new InvalidOperationException($"The release does not contain {manifest.PackageFile}.");
        }

        manifest.PackageDownloadUrl = packageUrl;
        return manifest;
    }

    private static bool IsNewerThanCurrent(string version)
    {
        Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0);
        return Version.TryParse(version, out Version? releaseVersion) && releaseVersion > currentVersion;
    }

    private static void StartUpdaterScript(string sourceDirectory, string targetDirectory, int processId)
    {
        string scriptPath = Path.Combine(Path.GetTempPath(), $"NewLauncherUpdate-{Guid.NewGuid():N}.ps1");
        string launcherPath = Path.Combine(targetDirectory, "NewLauncher.exe");
        string script = $$"""
$ErrorActionPreference = "Stop"
Wait-Process -Id {{processId}} -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
Get-ChildItem -LiteralPath "{{sourceDirectory}}" -Force | Copy-Item -Destination "{{targetDirectory}}" -Recurse -Force
Start-Process -FilePath "{{launcherPath}}"
Remove-Item -LiteralPath $PSCommandPath -Force
""";
        File.WriteAllText(scriptPath, script);
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private static string FindLauncherRoot(string extractRoot)
    {
        if (File.Exists(Path.Combine(extractRoot, "NewLauncher.exe")))
        {
            return extractRoot;
        }

        return Directory.EnumerateFiles(extractRoot, "NewLauncher.exe", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .OrderBy(directory => directory!.Length)
            .FirstOrDefault() ?? string.Empty;
    }

    private static void ExtractZipSafely(string packagePath, string destinationDirectory)
    {
        string destinationRoot = Path.GetFullPath(destinationDirectory);
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
            if (!destinationPath.StartsWith(destinationRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !destinationPath.Equals(destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Launcher update package contains an unsafe path.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationRoot);
            entry.ExtractToFile(destinationPath, overwrite: false);
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NewLauncher/1.0");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return httpClient;
    }
}
