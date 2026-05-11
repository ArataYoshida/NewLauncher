using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using System.Runtime.InteropServices;

namespace NewLauncher;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureMauiHandlers(handlers =>
            {
#if WINDOWS
                handlers.AddHandler(typeof(Runtime.GameViewport), typeof(Platforms.Windows.GameViewportHandler));
#endif
            })
            .ConfigureLifecycleEvents(events =>
            {
#if WINDOWS
                events.AddWindows(windows =>
                {
                    windows.OnWindowCreated(window =>
                    {
                        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                        string iconPath = Path.Combine(AppContext.BaseDirectory, "newengine_launcher.ico");
                        appWindow.SetIcon(iconPath);
                        SetWindowIcon(hwnd, iconPath);
                    });
                });
#endif
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

#if WINDOWS
    private const int ImageIcon = 1;
    private const int LoadFromFile = 0x00000010;
    private const int WmSetIcon = 0x0080;
    private const int IconSmall = 0;
    private const int IconBig = 1;
    private const int IconSmall2 = 2;

    private static void SetWindowIcon(IntPtr windowHandle, string iconPath)
    {
        IntPtr smallIcon = LoadImage(IntPtr.Zero, iconPath, ImageIcon, 16, 16, LoadFromFile);
        IntPtr largeIcon = LoadImage(IntPtr.Zero, iconPath, ImageIcon, 32, 32, LoadFromFile);
        if (smallIcon != IntPtr.Zero)
        {
            SendMessage(windowHandle, WmSetIcon, IconSmall, smallIcon);
            SendMessage(windowHandle, WmSetIcon, IconSmall2, smallIcon);
        }

        if (largeIcon != IntPtr.Zero)
        {
            SendMessage(windowHandle, WmSetIcon, IconBig, largeIcon);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr instance, string imageName, int imageType, int width, int height, int load);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr windowHandle, int message, int wParam, IntPtr lParam);
#endif
}
