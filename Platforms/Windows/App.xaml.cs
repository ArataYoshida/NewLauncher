using Microsoft.Maui;
using NewLauncher;
using System.Runtime.InteropServices;

namespace NewLauncher.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        SetCurrentProcessExplicitAppUserModelID("NewEngine.NewLauncher");
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp()
    {
        return MauiProgram.CreateMauiApp();
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SetCurrentProcessExplicitAppUserModelID(string appId);
}
