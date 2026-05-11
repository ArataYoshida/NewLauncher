using System.Text.Json.Serialization;

namespace NewLauncher;

public sealed class LauncherSettings
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("colorTheme")]
    public string ColorTheme { get; set; } = "system";

    [JsonPropertyName("engines")]
    public List<EngineInstallInfo> Engines { get; set; } = new();

    [JsonPropertyName("projects")]
    public List<ProjectInfo> Projects { get; set; } = new();

    [JsonPropertyName("releaseApiUrl")]
    public string ReleaseApiUrl { get; set; } = string.Empty;

    [JsonPropertyName("releaseCacheKey")]
    public string ReleaseCacheKey { get; set; } = string.Empty;

    [JsonPropertyName("latestReleaseEtag")]
    public string LatestReleaseEtag { get; set; } = string.Empty;

    [JsonPropertyName("cachedLatestReleaseJson")]
    public string CachedLatestReleaseJson { get; set; } = string.Empty;

    [JsonPropertyName("legacyAssetsMigrated")]
    public bool LegacyAssetsMigrated { get; set; }
}

public sealed class EngineInstallInfo
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "local-dev";

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "local";

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("scriptApiVersion")]
    public string ScriptApiVersion { get; set; } = "1";

    [JsonPropertyName("projectSchemaVersion")]
    public int ProjectSchemaVersion { get; set; } = 1;

    [JsonIgnore]
    public string EditorPath => System.IO.Path.Combine(Path, "NewEditor.exe");
}

public sealed class ProjectInfo
{
    [JsonPropertyName("projectName")]
    public string ProjectName { get; set; } = "New Project";

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("engineVersion")]
    public string EngineVersion { get; set; } = "local-dev";

    [JsonPropertyName("lastOpenedUtc")]
    public DateTime LastOpenedUtc { get; set; }
}

public sealed class ProjectManifest
{
    [JsonPropertyName("projectName")]
    public string ProjectName { get; set; } = "New Project";

    [JsonPropertyName("projectSchemaVersion")]
    public int ProjectSchemaVersion { get; set; } = 1;

    [JsonPropertyName("engineVersion")]
    public string EngineVersion { get; set; } = "local-dev";

    [JsonPropertyName("scriptApiVersion")]
    public string ScriptApiVersion { get; set; } = "1";

    [JsonPropertyName("lastOpenedScene")]
    public string LastOpenedScene { get; set; } = string.Empty;

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class EngineReleaseManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "stable";

    [JsonPropertyName("packageFile")]
    public string PackageFile { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("minLauncherVersion")]
    public string MinLauncherVersion { get; set; } = "1.0";

    [JsonPropertyName("editorExecutable")]
    public string EditorExecutable { get; set; } = "NewEditor.exe";

    [JsonPropertyName("scriptApiVersion")]
    public string ScriptApiVersion { get; set; } = "1";

    [JsonPropertyName("projectSchemaVersion")]
    public int ProjectSchemaVersion { get; set; } = 1;

    [JsonPropertyName("releaseNotesUrl")]
    public string ReleaseNotesUrl { get; set; } = string.Empty;

    [JsonIgnore]
    public string PackageDownloadUrl { get; set; } = string.Empty;

    [JsonIgnore]
    public string ReleaseName { get; set; } = string.Empty;

    [JsonIgnore]
    public DateTime PublishedAtUtc { get; set; }
}

public sealed class EngineInstallProgress
{
    public EngineInstallProgress(string message, double? percent = null)
    {
        Message = message;
        Percent = percent;
    }

    public string Message { get; }

    public double? Percent { get; }
}

public sealed class LauncherReleaseManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("packageFile")]
    public string PackageFile { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonIgnore]
    public string PackageDownloadUrl { get; set; } = string.Empty;
}
