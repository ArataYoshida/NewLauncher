using Microsoft.Maui.Controls;

namespace NewLauncher.Runtime;

public sealed class GameViewport : View
{
    public static readonly BindableProperty IsGameViewProperty =
        BindableProperty.Create(nameof(IsGameView), typeof(bool), typeof(GameViewport), true);

    public bool IsGameView
    {
        get => (bool)GetValue(IsGameViewProperty);
        set => SetValue(IsGameViewProperty, value);
    }
}
