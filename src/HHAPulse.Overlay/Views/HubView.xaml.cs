using HHAPulse.Overlay.Settings;
using HHAPulse.Shared.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HHAPulse.Overlay.Views;

public sealed partial class HubView : UserControl
{
    private const double CompactBreakpoint = 900;
    private const double WideBreakpoint = 1280;

    private Style PrimaryButtonStyle => (Style)Application.Current.Resources["HhapPrimaryButtonStyle"];
    private Style GhostButtonStyle => (Style)Application.Current.Resources["HhapGhostButtonStyle"];
    private Style ChipButtonStyle => (Style)Application.Current.Resources["HhapChipButtonStyle"];

    public HubView()
    {
        InitializeComponent();
    }

    public event Action? ToggleOverlayRequested;
    public event Action? CustomizeRequested;
    public event Action<OverlayPreset>? PresetRequested;
    public event Action<OverlayShowMode>? ShowModeRequested;
    public event Action<OverlayEdge>? PositionRequested;

    public void ApplyState(AppSettings settings, TelemetrySnapshot snapshot)
    {
        HeroHeadline.Text = ControlShellTextBuilder.FriendlyPresetName(settings.ActivePreset);
        HeroSubtitle.Text = settings.ActivePreset switch
        {
            OverlayPreset.Minimal => "Minimal keeps the HUD light and easy to glance at.",
            OverlayPreset.Standard => "Standard is the sane default for most players.",
            OverlayPreset.Tuner => "Performance adds more live context for tuning.",
            OverlayPreset.Custom => "Custom shapes the HUD to your taste.",
            OverlayPreset.Off => "Off hides the HUD while keeping Pulse ready.",
            _ => "Standard is the sane default for most players.",
        };

        var deps = snapshot.Dependencies;
        CaptureChipText.Text = deps.CaptureServiceConnected ? "Capture ready" : "Capture limited";
        BatteryChipText.Text = snapshot.AvailableMetrics.HasFlag(MetricFlags.Battery)
            ? ControlShellTextBuilder.FormatBatterySummary(snapshot)
            : "Battery --";
        VisibilityChipText.Text = settings.ActivePreset == OverlayPreset.Off
            ? "Off"
            : ControlShellTextBuilder.ShowModeText(settings.ShowMode);
        WidgetChipText.Text = deps.WidgetClientCount > 0 ? "Widget linked" : "Widget optional";

        VisibilityText.Text = settings.ActivePreset == OverlayPreset.Off
            ? "Overlay off"
            : ControlShellTextBuilder.ShowModeText(settings.ShowMode);
        PositionText.Text = settings.TopBarPosition == OverlayEdge.Bottom ? "Bottom of screen" : "Top of screen";
        PreviewLayoutText.Text = settings.TopBarPosition == OverlayEdge.Bottom ? "Bottom bar layout" : "Top bar layout";

        HighlightActivePreset(settings.ActivePreset);
        HighlightActiveVisibility(settings);
        HighlightActivePosition(settings.TopBarPosition);
        UpdateLiveStrip(snapshot);
    }

    private void HighlightActivePreset(OverlayPreset preset)
    {
        PresetMinimalButton.Style = preset == OverlayPreset.Minimal ? PrimaryButtonStyle : GhostButtonStyle;
        PresetStandardButton.Style = preset == OverlayPreset.Standard ? PrimaryButtonStyle : GhostButtonStyle;
        PresetTunerButton.Style = preset == OverlayPreset.Tuner ? PrimaryButtonStyle : GhostButtonStyle;
        PresetCustomButton.Style = preset == OverlayPreset.Custom ? PrimaryButtonStyle : GhostButtonStyle;
    }

    private void HighlightActiveVisibility(AppSettings settings)
    {
        var isOff = settings.ActivePreset == OverlayPreset.Off;
        var isAlways = !isOff && settings.ShowMode == OverlayShowMode.Always;
        var isInGame = !isOff && settings.ShowMode == OverlayShowMode.InGameOnly;

        ShowAlwaysButton.Style = isAlways ? PrimaryButtonStyle : ChipButtonStyle;
        ShowInGameButton.Style = isInGame ? PrimaryButtonStyle : ChipButtonStyle;
        PresetOffButton.Style = isOff ? PrimaryButtonStyle : ChipButtonStyle;
    }

    private void HighlightActivePosition(OverlayEdge edge)
    {
        LayoutTopButton.Style = edge == OverlayEdge.Top ? PrimaryButtonStyle : ChipButtonStyle;
        LayoutBottomButton.Style = edge == OverlayEdge.Bottom ? PrimaryButtonStyle : ChipButtonStyle;
    }

    private void UpdateLiveStrip(TelemetrySnapshot snapshot)
    {
        if (snapshot.AvailableMetrics == 0)
        {
            LiveStripCard.Visibility = Visibility.Collapsed;
            return;
        }

        var anyTile = false;

        if (snapshot.AvailableMetrics.HasFlag(MetricFlags.Fps) && snapshot.Performance.FramesPerSecond > 0)
        {
            LiveFpsValue.Text = $"{snapshot.Performance.FramesPerSecond:0}";
            LiveTileFps.Visibility = Visibility.Visible;
            anyTile = true;
        }
        else
        {
            LiveTileFps.Visibility = Visibility.Collapsed;
        }

        if (snapshot.AvailableMetrics.HasFlag(MetricFlags.CpuUsage))
        {
            LiveCpuValue.Text = $"{snapshot.Cpu.UsagePercent:0}%";
            LiveTileCpu.Visibility = Visibility.Visible;
            anyTile = true;
        }
        else
        {
            LiveTileCpu.Visibility = Visibility.Collapsed;
        }

        if (snapshot.AvailableMetrics.HasFlag(MetricFlags.GpuUsage))
        {
            LiveGpuValue.Text = $"{snapshot.Gpu.UsagePercent:0}%";
            LiveTileGpu.Visibility = Visibility.Visible;
            anyTile = true;
        }
        else
        {
            LiveTileGpu.Visibility = Visibility.Collapsed;
        }

        if (snapshot.AvailableMetrics.HasFlag(MetricFlags.Battery))
        {
            LiveBatteryValue.Text = $"{snapshot.Battery.ChargePercent:0}%";
            LiveTileBattery.Visibility = Visibility.Visible;
            anyTile = true;
        }
        else
        {
            LiveTileBattery.Visibility = Visibility.Collapsed;
        }

        LiveStripCard.Visibility = anyTile ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetHudPreview(UIElement? preview)
    {
        HudPreviewHost.Content = preview;
    }

    private void OnHubSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = e.NewSize.Width;
        var stackHeroAndPreview = width < CompactBreakpoint;

        if (stackHeroAndPreview)
        {
            HeroColumn.Width = new GridLength(1, GridUnitType.Star);
            PreviewColumn.Width = new GridLength(0);
            HeroStackRow.Height = new GridLength(1, GridUnitType.Auto);
            Grid.SetColumn(PreviewCard, 0);
            Grid.SetRow(PreviewCard, 1);
            PresetScrollHost.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            PresetScrollHost.HorizontalScrollMode = ScrollMode.Enabled;
        }
        else
        {
            HeroColumn.Width = new GridLength(1.35, GridUnitType.Star);
            PreviewColumn.Width = new GridLength(0.95, GridUnitType.Star);
            HeroStackRow.Height = new GridLength(0);
            Grid.SetColumn(PreviewCard, 1);
            Grid.SetRow(PreviewCard, 0);
            PresetScrollHost.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            PresetScrollHost.HorizontalScrollMode = ScrollMode.Disabled;
        }

        // Tighter side padding between compact and wide breakpoints.
        var heroPadding = width >= WideBreakpoint ? new Thickness(28) : new Thickness(22);
        HubRoot.Padding = heroPadding;
    }

    private void OnToggleClicked(object sender, RoutedEventArgs e) => ToggleOverlayRequested?.Invoke();
    private void OnCustomizeClicked(object sender, RoutedEventArgs e) => CustomizeRequested?.Invoke();
    private void OnPresetMinimalClicked(object sender, RoutedEventArgs e) => PresetRequested?.Invoke(OverlayPreset.Minimal);
    private void OnPresetStandardClicked(object sender, RoutedEventArgs e) => PresetRequested?.Invoke(OverlayPreset.Standard);
    private void OnPresetTunerClicked(object sender, RoutedEventArgs e) => PresetRequested?.Invoke(OverlayPreset.Tuner);
    private void OnPresetCustomClicked(object sender, RoutedEventArgs e)
    {
        PresetRequested?.Invoke(OverlayPreset.Custom);
        CustomizeRequested?.Invoke();
    }
    private void OnPresetOffClicked(object sender, RoutedEventArgs e) => PresetRequested?.Invoke(OverlayPreset.Off);
    private void OnShowAlwaysClicked(object sender, RoutedEventArgs e) => ShowModeRequested?.Invoke(OverlayShowMode.Always);
    private void OnShowInGameClicked(object sender, RoutedEventArgs e) => ShowModeRequested?.Invoke(OverlayShowMode.InGameOnly);
    private void OnLayoutTopClicked(object sender, RoutedEventArgs e) => PositionRequested?.Invoke(OverlayEdge.Top);
    private void OnLayoutBottomClicked(object sender, RoutedEventArgs e) => PositionRequested?.Invoke(OverlayEdge.Bottom);
}
