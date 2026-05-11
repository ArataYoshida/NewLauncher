using Microsoft.Maui.Controls.Shapes;

namespace NewLauncher.Runtime;

internal sealed class RomRuntimePage : ContentPage
{
    private readonly RomManifest _manifest;
    private IRomScriptRuntime? _scriptRuntime;
    private readonly Border _errorOverlay;
    private string _assetsRoot = string.Empty;
    private readonly Label _statusLabel = new()
    {
        TextColor = Colors.White,
        FontSize = 13,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center,
        IsVisible = false
    };

    public RomRuntimePage(RomManifest manifest)
    {
        _manifest = manifest;
        Title = manifest.GameName;
        BackgroundColor = Colors.Black;
        _errorOverlay = CreateErrorOverlay();
        Content = CreateLayout();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private View CreateLayout()
    {
        var viewport = new GameViewport
        {
            IsGameView = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        var grid = new Grid();
        grid.Add(viewport);
        grid.Add(_errorOverlay);
        return grid;
    }

    private Border CreateErrorOverlay()
    {
        return new Border
        {
            Padding = new Thickness(16),
            BackgroundColor = Color.FromArgb("#CC111820"),
            Stroke = Color.FromArgb("#6649A078"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Content = _statusLabel,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            MaximumWidthRequest = 640,
            IsVisible = false
        };
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        try
        {
            string assetsRoot = _manifest.ResolveAssetsRoot();
            string startupScene = _manifest.ResolveStartupScene();

            _assetsRoot = assetsRoot;
            EngineInterop.Engine_SetAssetsRoot(assetsRoot);
            IRomScriptRuntime scriptRuntime = CreateScriptRuntime();
            _scriptRuntime = scriptRuntime;
            scriptRuntime.Load(_manifest);
            scriptRuntime.ConfigureLocalization(assetsRoot);
            scriptRuntime.ConfigureSceneManager(LoadSceneFromScript, UnloadSceneFromScript);
            scriptRuntime.AttachToEngine();

            if (!EngineInterop.Engine_InitGlobal())
            {
                throw new InvalidOperationException("Engine initialization failed.");
            }

            RomSceneLoader.LoadScene(startupScene, assetsRoot, scriptRuntime);
            EngineInterop.Engine_SetPauseMode(false);
            EngineInterop.Engine_SetPlayMode(true);
            await Task.Yield();
        }
        catch (Exception ex)
        {
            try
            {
                EngineInterop.Engine_SetPlayMode(false);
                EngineInterop.Engine_Shutdown();
            }
            catch
            {
            }

            _scriptRuntime?.Dispose();
            _scriptRuntime = null;
            _statusLabel.Text = ex.Message;
            _statusLabel.IsVisible = true;
            _errorOverlay.IsVisible = true;
        }
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        try
        {
            EngineInterop.Engine_SetPlayMode(false);
            EngineInterop.Engine_Shutdown();
            _scriptRuntime?.Dispose();
            _scriptRuntime = null;
        }
        catch
        {
        }
    }

    private void LoadSceneFromScript(string scenePath)
    {
        IRomScriptRuntime scriptRuntime = _scriptRuntime ?? throw new InvalidOperationException("Script runtime is not active.");
        string resolvedScenePath = ResolveRuntimeScenePath(scenePath);
        RomSceneLoader.LoadScene(resolvedScenePath, _assetsRoot, scriptRuntime);
        EngineInterop.Engine_SetPauseMode(false);
        EngineInterop.Engine_SetPlayMode(true);
    }

    private void UnloadSceneFromScript()
    {
        EngineInterop.Engine_ClearScene();
        _scriptRuntime?.ClearLoadedBehaviors();
    }

    private IRomScriptRuntime CreateScriptRuntime()
    {
        if (!string.IsNullOrWhiteSpace(_manifest.NativeScriptLibrary) &&
            File.Exists(_manifest.ResolveNativeScriptLibrary()))
        {
            return new NativeRomScriptRuntime();
        }

        return new RomScriptRuntime();
    }

    private string ResolveRuntimeScenePath(string scenePath)
    {
        if (System.IO.Path.IsPathRooted(scenePath))
        {
            return System.IO.Path.GetFullPath(scenePath);
        }

        string normalizedPath = scenePath.Replace('/', System.IO.Path.DirectorySeparatorChar);
        string rootCandidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(_manifest.RootDirectory, normalizedPath));
        if (File.Exists(rootCandidate))
        {
            return rootCandidate;
        }

        string assetsCandidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(_assetsRoot, normalizedPath));
        return assetsCandidate;
    }
}
