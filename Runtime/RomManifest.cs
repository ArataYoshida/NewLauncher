using System.Text.Json;
using System.Text.Json.Serialization;

namespace NewLauncher.Runtime;

internal sealed class RomManifest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    [JsonPropertyName("gameName")]
    public string GameName { get; set; } = "New Game";

    [JsonPropertyName("startupScene")]
    public string StartupScene { get; set; } = string.Empty;

    [JsonPropertyName("assetsRoot")]
    public string AssetsRoot { get; set; } = "Assets";

    [JsonPropertyName("applicationAssembly")]
    public string ApplicationAssembly { get; set; } = "Application.dll";

    [JsonPropertyName("nativeScriptLibrary")]
    public string NativeScriptLibrary { get; set; } = string.Empty;

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public string ManifestPath { get; private set; } = string.Empty;

    [JsonIgnore]
    public string RootDirectory => Path.GetDirectoryName(ManifestPath) ?? AppContext.BaseDirectory;

    public static RomManifest Load(string path)
    {
        var manifest = JsonSerializer.Deserialize<RomManifest>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidOperationException("rom.json could not be read.");
        manifest.ManifestPath = Path.GetFullPath(path);
        return manifest;
    }

    public string ResolveAssetsRoot() => ResolvePath(AssetsRoot);

    public string ResolveStartupScene() => ResolvePath(StartupScene);

    public string ResolveApplicationAssembly() => ResolvePath(ApplicationAssembly);

    public string ResolveNativeScriptLibrary() => ResolvePath(NativeScriptLibrary);

    private string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return RootDirectory;
        }

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(RootDirectory, path));
    }
}
