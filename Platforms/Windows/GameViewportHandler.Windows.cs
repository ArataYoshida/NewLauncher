#if WINDOWS
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml.Controls;
using NewLauncher.Runtime;

namespace NewLauncher.Platforms.Windows;

public sealed class GameViewportHandler : ViewHandler<GameViewport, Microsoft.UI.Xaml.Controls.Grid>
{
    public static IPropertyMapper<GameViewport, GameViewportHandler> Mapper =
        new PropertyMapper<GameViewport, GameViewportHandler>(ViewHandler.ViewMapper);

    private static int _activeViewportCount;
    private static EventHandler<object>? _renderingHandler;

    private SwapChainPanel _swapChainPanel = null!;
    private bool _isDisconnected;
    private bool _isViewportRegistered;
    private bool _isResizePending;
    private int _pendingWidth;
    private int _pendingHeight;

    public GameViewportHandler()
        : base(Mapper)
    {
    }

    protected override Microsoft.UI.Xaml.Controls.Grid CreatePlatformView()
    {
        var grid = new Microsoft.UI.Xaml.Controls.Grid
        {
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch
        };

        _swapChainPanel = new SwapChainPanel
        {
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch
        };
        grid.Children.Add(_swapChainPanel);

        _swapChainPanel.Loaded += OnSwapChainPanelLoaded;
        _swapChainPanel.SizeChanged += OnSwapChainPanelSizeChanged;
        return grid;
    }

    private void OnSwapChainPanelLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_isDisconnected)
        {
            return;
        }

        IntPtr nativePointer = IntPtr.Zero;
        try
        {
            nativePointer = System.Runtime.InteropServices.Marshal.GetIUnknownForObject(_swapChainPanel);
            if (nativePointer == IntPtr.Zero)
            {
                return;
            }

            int width = Math.Max(1, (int)_swapChainPanel.ActualWidth);
            int height = Math.Max(1, (int)_swapChainPanel.ActualHeight);
            bool isGameView = VirtualView?.IsGameView ?? true;
            if (!EngineInterop.Engine_InitGlobal())
            {
                return;
            }

            if (!EngineInterop.Engine_RegisterViewport(nativePointer, width, height, isGameView))
            {
                return;
            }

            _isViewportRegistered = true;
            _activeViewportCount++;
            EnsureRenderingLoop();
        }
        finally
        {
            if (nativePointer != IntPtr.Zero)
            {
                System.Runtime.InteropServices.Marshal.Release(nativePointer);
            }
        }
    }

    private void OnSwapChainPanelSizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        if (_isDisconnected || !_isViewportRegistered)
        {
            return;
        }

        _pendingWidth = (int)e.NewSize.Width;
        _pendingHeight = (int)e.NewSize.Height;
        if (_isResizePending)
        {
            return;
        }

        _isResizePending = true;
        VirtualView?.Dispatcher.Dispatch(() =>
        {
            _isResizePending = false;
            if (_isDisconnected || !_isViewportRegistered || _pendingWidth <= 0 || _pendingHeight <= 0)
            {
                return;
            }

            EngineInterop.Engine_QueueResize(VirtualView?.IsGameView ?? true, _pendingWidth, _pendingHeight);
        });
    }

    private static void EnsureRenderingLoop()
    {
        if (_renderingHandler != null)
        {
            return;
        }

        _renderingHandler = (_, _) =>
        {
            if (_activeViewportCount <= 0)
            {
                return;
            }

            try
            {
                EngineInterop.Engine_Tick();
            }
            catch
            {
            }
        };
        Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += _renderingHandler;
    }

    protected override void DisconnectHandler(Microsoft.UI.Xaml.Controls.Grid platformView)
    {
        _isDisconnected = true;
        _swapChainPanel.Loaded -= OnSwapChainPanelLoaded;
        _swapChainPanel.SizeChanged -= OnSwapChainPanelSizeChanged;

        if (_isViewportRegistered)
        {
            try
            {
                EngineInterop.Engine_RegisterViewport(IntPtr.Zero, 0, 0, VirtualView?.IsGameView ?? true);
            }
            catch
            {
            }

            _isViewportRegistered = false;
            _activeViewportCount--;
        }

        if (_activeViewportCount <= 0 && _renderingHandler != null)
        {
            Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= _renderingHandler;
            _renderingHandler = null;
        }

        base.DisconnectHandler(platformView);
    }
}
#endif
