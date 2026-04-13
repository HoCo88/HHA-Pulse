using HHAPulse.Overlay.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace HHAPulse.Overlay.Views;

public sealed partial class SettingsSheet : UserControl
{
    private bool suppressEvents;

    public SettingsSheet()
    {
        InitializeComponent();
    }

    public event Action? CloseRequested;
    public event Action<double, double>? OpacityChanged;
    public event Action<double>? TextSizeChanged;
    public event Action<OverlayEdge>? PositionChanged;

    public void ApplySettings(AppSettings settings)
    {
        suppressEvents = true;
        try
        {
            BgOpacitySlider.Value = settings.BackgroundOpacity * 100;
            TextOpacitySlider.Value = settings.TextOpacity * 100;
            TextSizeSlider.Value = settings.TextSizePixels > 0 ? settings.TextSizePixels : 15;
            BgOpacityValue.Text = $"Background opacity: {(int)BgOpacitySlider.Value}%";
            TextOpacityValue.Text = $"Text opacity: {(int)TextOpacitySlider.Value}%";
            TextSizeValue.Text = $"HUD text size: {(int)TextSizeSlider.Value}px";
        }
        finally
        {
            suppressEvents = false;
        }
    }

    private void OnBgOpacityChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (suppressEvents) return;
        BgOpacityValue.Text = $"Background opacity: {(int)e.NewValue}%";
        OpacityChanged?.Invoke(e.NewValue / 100.0, TextOpacitySlider.Value / 100.0);
    }

    private void OnTextOpacityChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (suppressEvents) return;
        TextOpacityValue.Text = $"Text opacity: {(int)e.NewValue}%";
        OpacityChanged?.Invoke(BgOpacitySlider.Value / 100.0, e.NewValue / 100.0);
    }

    private void OnTextSizeChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (suppressEvents) return;
        TextSizeValue.Text = $"HUD text size: {(int)e.NewValue}px";
        TextSizeChanged?.Invoke(e.NewValue);
    }

    private void OnTopClicked(object sender, RoutedEventArgs e) => PositionChanged?.Invoke(OverlayEdge.Top);
    private void OnBottomClicked(object sender, RoutedEventArgs e) => PositionChanged?.Invoke(OverlayEdge.Bottom);
    private void OnCloseClicked(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();
}
