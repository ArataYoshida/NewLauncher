namespace NewLauncher;

public sealed class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        string[] args = Environment.GetCommandLineArgs();
        string? romManifestPath = Runtime.RomStartup.ResolveManifestPath(args);
        Page rootPage = romManifestPath != null && File.Exists(romManifestPath)
            ? new Runtime.RomRuntimePage(Runtime.RomManifest.Load(romManifestPath))
            : new MainPage();

        var window = new Window(rootPage)
        {
            Title = rootPage.Title ?? "New Launcher",
            Width = 1180,
            Height = 760
        };

        return window;
    }
}
