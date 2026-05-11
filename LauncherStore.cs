using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace NewLauncher;

public sealed class LauncherStore
{
    private const string DefaultReleaseOwner = "ArataYoshida";
    private const string DefaultReleaseRepository = "NewEngine";
    private const string DefaultLatestReleaseApiUrl = "https://api.github.com/repos/ArataYoshida/NewEngine/releases/latest";
    private const string DefaultReleasesApiUrl = "https://api.github.com/repos/ArataYoshida/NewEngine/releases";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string DataRoot { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NewGameEngine");
    public string EnginesRoot => Path.Combine(DataRoot, "Engines");
    public string LauncherRoot => Path.Combine(DataRoot, "Launcher");
    public string DefaultProjectsRoot { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "New Projects");
    public string ReleaseSourceLabel => GetReleaseSourceLabel();
    public LauncherSettings Settings { get; private set; } = new();
    public EngineReleaseManifest? LatestRelease { get; private set; }
    public List<EngineReleaseManifest> AvailableReleases { get; private set; } = new();

    private string SettingsPath => Path.Combine(LauncherRoot, "launcher-settings.json");

    public void Load()
    {
        Directory.CreateDirectory(EnginesRoot);
        Directory.CreateDirectory(LauncherRoot);
        Directory.CreateDirectory(DefaultProjectsRoot);

        Settings = File.Exists(SettingsPath)
            ? JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(SettingsPath), JsonOptions) ?? new LauncherSettings()
            : new LauncherSettings();

        RegisterLocalDevelopmentEngine();
        MigrateLegacyAssetsOnce();
        PruneMissingEntries();
        Save();
    }

    public void Save()
    {
        Directory.CreateDirectory(LauncherRoot);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Settings, JsonOptions));
    }

    public ProjectInfo CreateProject(string requestedName, EngineInstallInfo? engine)
    {
        string projectName = SanitizeProjectName(requestedName);
        string projectRoot = GetAvailableDirectory(Path.Combine(DefaultProjectsRoot, projectName));
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(Path.Combine(projectRoot, "Assets"));

        string engineVersion = engine?.Version ?? "local-dev";
        CopyTemplateAssets(engine, Path.Combine(projectRoot, "Assets"));
        WriteProjectManifest(projectRoot, new ProjectManifest
        {
            ProjectName = projectName,
            EngineVersion = engineVersion,
            ScriptApiVersion = engine?.ScriptApiVersion ?? "1",
            ProjectSchemaVersion = engine?.ProjectSchemaVersion ?? 1,
            CreatedAtUtc = DateTime.UtcNow
        });

        var project = new ProjectInfo
        {
            ProjectName = projectName,
            Path = projectRoot,
            EngineVersion = engineVersion,
            LastOpenedUtc = DateTime.UtcNow
        };
        UpsertProject(project);
        Save();
        return project;
    }

    public ProjectInfo AddExistingProject(string projectRoot, EngineInstallInfo? engine)
    {
        string normalizedRoot = Path.GetFullPath(projectRoot.Trim('"'));
        Directory.CreateDirectory(normalizedRoot);
        Directory.CreateDirectory(Path.Combine(normalizedRoot, "Assets"));

        ProjectManifest manifest = ReadProjectManifest(normalizedRoot) ?? new ProjectManifest
        {
            ProjectName = Path.GetFileName(normalizedRoot),
            EngineVersion = engine?.Version ?? "local-dev",
            ScriptApiVersion = engine?.ScriptApiVersion ?? "1",
            ProjectSchemaVersion = engine?.ProjectSchemaVersion ?? 1,
            CreatedAtUtc = DateTime.UtcNow
        };
        WriteProjectManifest(normalizedRoot, manifest);

        var project = new ProjectInfo
        {
            ProjectName = manifest.ProjectName,
            Path = normalizedRoot,
            EngineVersion = manifest.EngineVersion,
            LastOpenedUtc = DateTime.UtcNow
        };
        UpsertProject(project);
        Save();
        return project;
    }

    public void LaunchEditor(ProjectInfo project, EngineInstallInfo engine)
    {
        if (!File.Exists(engine.EditorPath))
        {
            throw new FileNotFoundException("NewEditor.exe was not found.", engine.EditorPath);
        }

        if (!Directory.Exists(project.Path))
        {
            throw new DirectoryNotFoundException(project.Path);
        }

        project.EngineVersion = engine.Version;
        project.LastOpenedUtc = DateTime.UtcNow;
        UpsertProject(project);
        Save();

        var startInfo = new ProcessStartInfo
        {
            FileName = engine.EditorPath,
            Arguments = $"--project \"{project.Path}\"",
            WorkingDirectory = engine.Path,
            UseShellExecute = false
        };
        Process.Start(startInfo);
    }

    public async Task<EngineReleaseManifest> CheckLatestReleaseAsync()
    {
        string releaseApiUrl = GetLatestReleaseApiUrl();
        ResetReleaseCacheWhenSourceChanges(releaseApiUrl);
        using var httpClient = CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, releaseApiUrl);
        if (!string.IsNullOrWhiteSpace(Settings.LatestReleaseEtag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", Settings.LatestReleaseEtag);
        }

        using HttpResponseMessage response = await httpClient.SendAsync(request);
        string releaseJson;
        if (response.StatusCode == System.Net.HttpStatusCode.NotModified && !string.IsNullOrWhiteSpace(Settings.CachedLatestReleaseJson))
        {
            releaseJson = Settings.CachedLatestReleaseJson;
        }
        else
        {
            response.EnsureSuccessStatusCode();
            releaseJson = await response.Content.ReadAsStringAsync();
            Settings.CachedLatestReleaseJson = releaseJson;
            Settings.LatestReleaseEtag = response.Headers.ETag?.Tag ?? string.Empty;
        }

        LatestRelease = await ReadManifestFromReleaseAsync(httpClient, releaseJson);
        AvailableReleases = MergeReleaseList(LatestRelease, AvailableReleases);
        Save();
        return LatestRelease;
    }

    public async Task<IReadOnlyList<EngineReleaseManifest>> CheckAvailableReleasesAsync()
    {
        using var httpClient = CreateHttpClient();
        using HttpResponseMessage response = await httpClient.GetAsync(GetReleasesApiUrl());
        response.EnsureSuccessStatusCode();
        string releasesJson = await response.Content.ReadAsStringAsync();
        using JsonDocument releasesDocument = JsonDocument.Parse(releasesJson);

        var releases = new List<EngineReleaseManifest>();
        foreach (JsonElement releaseElement in releasesDocument.RootElement.EnumerateArray())
        {
            if (releaseElement.TryGetProperty("draft", out JsonElement draft) && draft.GetBoolean())
            {
                continue;
            }

            try
            {
                string releaseJson = releaseElement.GetRawText();
                EngineReleaseManifest manifest = await ReadManifestFromReleaseAsync(httpClient, releaseJson);
                releases.Add(manifest);
            }
            catch
            {
                // Release-only repositories may contain launcher releases or notes; skip assets that are not engine packages.
            }
        }

        AvailableReleases = releases
            .OrderByDescending(release => release.PublishedAtUtc)
            .ThenByDescending(release => release.Version, StringComparer.OrdinalIgnoreCase)
            .ToList();
        LatestRelease = AvailableReleases.FirstOrDefault() ?? LatestRelease;
        Save();
        return AvailableReleases;
    }

    public async Task<EngineInstallInfo> InstallLatestReleaseAsync(IProgress<EngineInstallProgress>? progress = null)
    {
        EngineReleaseManifest manifest = LatestRelease ?? await CheckLatestReleaseAsync();
        return await InstallReleaseAsync(manifest, progress);
    }

    public async Task<EngineInstallInfo> InstallReleaseAsync(EngineReleaseManifest manifest, IProgress<EngineInstallProgress>? progress = null)
    {
        if (!IsLauncherVersionSupported(manifest.MinLauncherVersion))
        {
            throw new InvalidOperationException($"This release requires NewLauncher {manifest.MinLauncherVersion} or newer.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new InvalidOperationException("engine-manifest.json does not define version.");
        }

        string editorExecutable = string.IsNullOrWhiteSpace(manifest.EditorExecutable) ? "NewEditor.exe" : manifest.EditorExecutable;
        if (string.IsNullOrWhiteSpace(manifest.PackageDownloadUrl))
        {
            throw new InvalidOperationException("Release package download URL was not found.");
        }

        string destinationRoot = Path.Combine(EnginesRoot, manifest.Version);
        if (Directory.Exists(destinationRoot))
        {
            if (!File.Exists(Path.Combine(destinationRoot, editorExecutable)))
            {
                throw new InvalidOperationException($"Engine folder already exists but is incomplete: {destinationRoot}");
            }

            var existing = Settings.Engines.FirstOrDefault(engine => string.Equals(engine.Version, manifest.Version, StringComparison.OrdinalIgnoreCase))
                ?? new EngineInstallInfo
                {
                    Version = manifest.Version,
                    Channel = manifest.Channel,
                    Path = destinationRoot,
                    ScriptApiVersion = manifest.ScriptApiVersion,
                    ProjectSchemaVersion = manifest.ProjectSchemaVersion
            };
            UpsertEngine(existing);
            Save();
            progress?.Report(new EngineInstallProgress("Already installed.", 100));
            return existing;
        }

        string downloadRoot = Path.Combine(DataRoot, "Downloads");
        Directory.CreateDirectory(downloadRoot);
        string packagePath = Path.Combine(downloadRoot, manifest.PackageFile);
        progress?.Report(new EngineInstallProgress("Downloading package...", 0));

        using (var httpClient = CreateHttpClient())
        using (HttpResponseMessage response = await httpClient.GetAsync(manifest.PackageDownloadUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            long totalBytes = response.Content.Headers.ContentLength ?? manifest.SizeBytes;
            await using Stream remoteStream = await response.Content.ReadAsStreamAsync();
            await using var localStream = new FileStream(packagePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await CopyToFileWithProgressAsync(remoteStream, localStream, totalBytes, progress);
        }

        progress?.Report(new EngineInstallProgress("Verifying package...", 100));
        string actualHash = ComputeSha256(packagePath);
        if (!string.IsNullOrWhiteSpace(manifest.Sha256) &&
            !actualHash.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(packagePath);
            throw new InvalidOperationException("Downloaded package SHA256 did not match engine-manifest.json.");
        }

        string extractParent = Path.Combine(DataRoot, "InstallTemp", Guid.NewGuid().ToString("N"));
        string extractRoot = Path.Combine(extractParent, "extract");
        Directory.CreateDirectory(extractRoot);
        progress?.Report(new EngineInstallProgress("Extracting package...", 100));
        ExtractZipSafely(packagePath, extractRoot);

        string engineRoot = FindEngineRoot(extractRoot, editorExecutable);
        if (string.IsNullOrWhiteSpace(engineRoot))
        {
            Directory.Delete(extractParent, recursive: true);
            throw new InvalidOperationException($"Extracted package does not contain {editorExecutable}.");
        }

        Directory.CreateDirectory(EnginesRoot);
        Directory.Move(engineRoot, destinationRoot);
        TryDeleteDirectory(extractParent);
        TryDeleteFile(packagePath);

        var engineInstall = new EngineInstallInfo
        {
            Version = manifest.Version,
            Channel = manifest.Channel,
            Path = destinationRoot,
            ScriptApiVersion = manifest.ScriptApiVersion,
            ProjectSchemaVersion = manifest.ProjectSchemaVersion
        };
        UpsertEngine(engineInstall);
        Save();
        progress?.Report(new EngineInstallProgress("Install complete.", 100));
        return engineInstall;
    }

    public bool IsEngineInstalled(EngineReleaseManifest? manifest)
    {
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
        {
            return false;
        }

        return Settings.Engines.Any(engine =>
            string.Equals(engine.Version, manifest.Version, StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(engine.Path) &&
            File.Exists(engine.EditorPath));
    }

    private void RegisterLocalDevelopmentEngine()
    {
        foreach (string candidate in GetLocalEngineCandidates())
        {
            string editorPath = Path.Combine(candidate, "NewEditor.exe");
            if (!File.Exists(editorPath))
            {
                continue;
            }

            UpsertEngine(new EngineInstallInfo
            {
                Version = "local-dev",
                Channel = "local",
                Path = Path.GetFullPath(candidate),
                ScriptApiVersion = "1",
                ProjectSchemaVersion = 1
            });
            return;
        }
    }

    private IEnumerable<string> GetLocalEngineCandidates()
    {
        string baseDirectory = AppContext.BaseDirectory;
        yield return baseDirectory;
        yield return Path.GetFullPath(Path.Combine(baseDirectory, "..", "bin"));
        yield return Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "build", "bin"));
    }

    private void MigrateLegacyAssetsOnce()
    {
        if (Settings.LegacyAssetsMigrated)
        {
            return;
        }

        EngineInstallInfo? localEngine = Settings.Engines.FirstOrDefault(engine => engine.Version == "local-dev");
        if (localEngine == null)
        {
            Settings.LegacyAssetsMigrated = true;
            return;
        }

        string legacyAssetsPath = Path.Combine(localEngine.Path, "Assets");
        if (string.IsNullOrWhiteSpace(legacyAssetsPath) ||
            !Directory.Exists(legacyAssetsPath) ||
            !Directory.EnumerateFileSystemEntries(legacyAssetsPath).Any())
        {
            Settings.LegacyAssetsMigrated = true;
            return;
        }

        string migratedRoot = GetAvailableDirectory(Path.Combine(DefaultProjectsRoot, "MigratedProject"));
        string migratedAssetsPath = Path.Combine(migratedRoot, "Assets");
        string migratedProjectName = Path.GetFileName(migratedRoot) ?? "MigratedProject";
        CopyDirectory(legacyAssetsPath, migratedAssetsPath, overwrite: false);

        WriteProjectManifest(migratedRoot, new ProjectManifest
        {
            ProjectName = migratedProjectName,
            EngineVersion = localEngine.Version,
            ScriptApiVersion = localEngine.ScriptApiVersion,
            ProjectSchemaVersion = localEngine.ProjectSchemaVersion,
            CreatedAtUtc = DateTime.UtcNow
        });

        UpsertProject(new ProjectInfo
        {
            ProjectName = migratedProjectName,
            Path = migratedRoot,
            EngineVersion = localEngine.Version,
            LastOpenedUtc = DateTime.UtcNow
        });
        Settings.LegacyAssetsMigrated = true;
    }

    private void CopyTemplateAssets(EngineInstallInfo? engine, string destinationAssetsPath)
    {
        if (engine == null)
        {
            return;
        }

        string templateAssetsPath = Path.Combine(engine.Path, "ProjectTemplates", "Default", "Assets");
        if (Directory.Exists(templateAssetsPath))
        {
            CopyDirectory(templateAssetsPath, destinationAssetsPath, overwrite: false);
            return;
        }

        string legacyAssetsPath = Path.Combine(engine.Path, "Assets");
        if (Directory.Exists(legacyAssetsPath))
        {
            CopyDirectory(legacyAssetsPath, destinationAssetsPath, overwrite: false);
        }
    }

    private static async Task<EngineReleaseManifest> ReadManifestFromReleaseAsync(HttpClient httpClient, string releaseJson)
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
            if (name.Equals("engine-manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                manifestUrl = downloadUrl;
            }
        }

        if (string.IsNullOrWhiteSpace(manifestUrl))
        {
            throw new InvalidOperationException("The release does not contain engine-manifest.json.");
        }

        string manifestJson = await httpClient.GetStringAsync(manifestUrl);
        EngineReleaseManifest manifest = JsonSerializer.Deserialize<EngineReleaseManifest>(manifestJson, JsonOptions)
            ?? throw new InvalidOperationException("engine-manifest.json is invalid.");

        if (string.IsNullOrWhiteSpace(manifest.PackageFile))
        {
            throw new InvalidOperationException("engine-manifest.json does not define packageFile.");
        }

        if (!assetUrls.TryGetValue(manifest.PackageFile, out string? packageUrl))
        {
            throw new InvalidOperationException($"The release does not contain {manifest.PackageFile}.");
        }

        manifest.PackageDownloadUrl = packageUrl;
        manifest.ReleaseName = releaseDocument.RootElement.TryGetProperty("name", out JsonElement releaseName)
            ? releaseName.GetString() ?? manifest.Version
            : manifest.Version;
        manifest.PublishedAtUtc = releaseDocument.RootElement.TryGetProperty("published_at", out JsonElement publishedAt) &&
            publishedAt.TryGetDateTime(out DateTime publishedAtUtc)
                ? publishedAtUtc
                : DateTime.MinValue;
        return manifest;
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NewLauncher/1.0");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return httpClient;
    }

    private static string GetLatestReleaseApiUrl()
    {
        string? overrideUrl = Environment.GetEnvironmentVariable("NEWLAUNCHER_RELEASE_API_URL");
        return string.IsNullOrWhiteSpace(overrideUrl) ? DefaultLatestReleaseApiUrl : overrideUrl.Trim();
    }

    private static string GetReleasesApiUrl()
    {
        string latestUrl = GetLatestReleaseApiUrl();
        const string latestSuffix = "/latest";
        return latestUrl.EndsWith(latestSuffix, StringComparison.OrdinalIgnoreCase)
            ? latestUrl[..^latestSuffix.Length]
            : DefaultReleasesApiUrl;
    }

    private static string GetReleaseSourceLabel()
    {
        string releaseApiUrl = GetLatestReleaseApiUrl();
        if (releaseApiUrl.Equals(DefaultLatestReleaseApiUrl, StringComparison.OrdinalIgnoreCase))
        {
            return $"{DefaultReleaseOwner}/{DefaultReleaseRepository}";
        }

        return releaseApiUrl;
    }

    private void ResetReleaseCacheWhenSourceChanges(string releaseApiUrl)
    {
        if (Settings.ReleaseCacheKey.Equals(releaseApiUrl, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Settings.ReleaseCacheKey = releaseApiUrl;
        Settings.LatestReleaseEtag = string.Empty;
        Settings.CachedLatestReleaseJson = string.Empty;
    }

    private static List<EngineReleaseManifest> MergeReleaseList(
        EngineReleaseManifest release,
        IEnumerable<EngineReleaseManifest> existingReleases)
    {
        var releases = existingReleases
            .Where(existing => !string.Equals(existing.Version, release.Version, StringComparison.OrdinalIgnoreCase))
            .ToList();
        releases.Insert(0, release);
        return releases;
    }

    private void UpsertEngine(EngineInstallInfo engine)
    {
        Settings.Engines.RemoveAll(existing =>
            string.Equals(existing.Version, engine.Version, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFullPath(existing.Path), Path.GetFullPath(engine.Path), StringComparison.OrdinalIgnoreCase));
        Settings.Engines.Insert(0, engine);
    }

    private void UpsertProject(ProjectInfo project)
    {
        Settings.Projects.RemoveAll(existing => string.Equals(Path.GetFullPath(existing.Path), Path.GetFullPath(project.Path), StringComparison.OrdinalIgnoreCase));
        Settings.Projects.Insert(0, project);
    }

    private void PruneMissingEntries()
    {
        Settings.Engines = Settings.Engines
            .Where(engine => Directory.Exists(engine.Path) && File.Exists(engine.EditorPath))
            .GroupBy(engine => Path.GetFullPath(engine.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        Settings.Projects = Settings.Projects
            .Where(project => Directory.Exists(project.Path))
            .GroupBy(project => Path.GetFullPath(project.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static ProjectManifest? ReadProjectManifest(string projectRoot)
    {
        string manifestPath = Path.Combine(projectRoot, "NewProject.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ProjectManifest>(File.ReadAllText(manifestPath), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void WriteProjectManifest(string projectRoot, ProjectManifest manifest)
    {
        Directory.CreateDirectory(projectRoot);
        File.WriteAllText(Path.Combine(projectRoot, "NewProject.json"), JsonSerializer.Serialize(manifest, JsonOptions));
    }

    private static string SanitizeProjectName(string requestedName)
    {
        string sanitized = new(requestedName
            .Where(character => char.IsLetterOrDigit(character) || character is ' ' or '_' or '-')
            .ToArray());
        sanitized = sanitized.Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "NewProject" : sanitized;
    }

    private static string GetAvailableDirectory(string requestedPath)
    {
        if (!Directory.Exists(requestedPath) && !File.Exists(requestedPath))
        {
            return requestedPath;
        }

        for (int index = 1; index < 1000; index++)
        {
            string candidate = $"{requestedPath}_{index}";
            if (!Directory.Exists(candidate) && !File.Exists(candidate))
            {
                return candidate;
            }
        }

        return $"{requestedPath}_{Guid.NewGuid():N}";
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory, bool overwrite)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (string sourcePath in Directory.EnumerateFileSystemEntries(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, sourcePath);
            string destinationPath = Path.Combine(destinationDirectory, relativePath);
            if (Directory.Exists(sourcePath))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationDirectory);
            if (!overwrite && File.Exists(destinationPath))
            {
                continue;
            }

            File.Copy(sourcePath, destinationPath, overwrite);
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static async Task CopyToFileWithProgressAsync(
        Stream source,
        Stream destination,
        long totalBytes,
        IProgress<EngineInstallProgress>? progress)
    {
        byte[] buffer = new byte[1024 * 128];
        long copiedBytes = 0;
        int lastReportedPercent = -1;

        while (true)
        {
            int readBytes = await source.ReadAsync(buffer);
            if (readBytes == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, readBytes));
            copiedBytes += readBytes;
            if (totalBytes <= 0)
            {
                progress?.Report(new EngineInstallProgress($"Downloading package... {FormatBytes(copiedBytes)}"));
                continue;
            }

            int percent = (int)Math.Clamp(Math.Floor(copiedBytes * 100d / totalBytes), 0, 100);
            if (percent == lastReportedPercent)
            {
                continue;
            }

            lastReportedPercent = percent;
            progress?.Report(new EngineInstallProgress("Downloading package...", percent));
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double value = bytes;
        int unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }

    private static bool IsLauncherVersionSupported(string minimumVersion)
    {
        if (string.IsNullOrWhiteSpace(minimumVersion))
        {
            return true;
        }

        Version currentVersion = typeof(LauncherStore).Assembly.GetName().Version ?? new Version(1, 0);
        return Version.TryParse(minimumVersion, out Version? requiredVersion) && currentVersion >= requiredVersion;
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
                throw new InvalidOperationException("Release package contains an unsafe path.");
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

    private static string FindEngineRoot(string extractRoot, string editorExecutable)
    {
        if (File.Exists(Path.Combine(extractRoot, editorExecutable)))
        {
            return extractRoot;
        }

        return Directory.EnumerateFiles(extractRoot, editorExecutable, SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .OrderBy(directory => directory!.Length)
            .FirstOrDefault() ?? string.Empty;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
