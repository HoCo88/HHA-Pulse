using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace HHAPulse.Overlay.Controls;

public sealed partial class FeelAndFitFlyout : UserControl
{
    private bool suppressEvents;

    public FeelAndFitFlyout()
    {
        InitializeComponent();
    }

    public event Action<double, double>? OpacityChanged;
    public event Action<double>? TextSizeChanged;

    public void ApplySettings(double backgroundOpacity, double textOpacity, double textSize)
    {
        suppressEvents = true;
        try
        {
            BgOpacitySlider.Value = backgroundOpacity * 100;
            TextOpacitySlider.Value = textOpacity * 100;
            TextSizeSlider.Value = textSize;
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
}
