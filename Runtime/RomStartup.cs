namespace NewLauncher.Runtime;

internal static class RomStartup
{
    public static string? ResolveManifestPath(string[] args)
    {
        for (int index = 0; index < args.Length; index++)
        {
            string arg = args[index];
            if (arg.Equals("--rom", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                return NormalizeCandidate(args[index + 1]);
            }

            const string prefix = "--rom=";
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeCandidate(arg[prefix.Length..].Trim('"'));
            }
        }

        string localManifest = Path.Combine(AppContext.BaseDirectory, "rom.json");
        return File.Exists(localManifest) ? localManifest : null;
    }

    private static string NormalizeCandidate(string candidate)
    {
        string fullPath = Path.GetFullPath(candidate);
        if (Directory.Exists(fullPath))
        {
            return Path.Combine(fullPath, "rom.json");
        }

        return fullPath;
    }
}
