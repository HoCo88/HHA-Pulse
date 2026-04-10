using Microsoft.Gaming.XboxGameBar;
using HHAPulse.Shared.Models;
using HHAPulse.Shared.Protocol;
using HHAPulse.Widget.Services;
using Windows.System;
using Windows.System.Power;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace HHAPulse.Widget;

public sealed partial class CompactWidget : Page
{
    private readonly WidgetMetricService metricService = new();
    private readonly DispatcherTimer refreshTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly CancellationTokenSource shutdown = new();

    public XboxGameBarWidget? Widget { get; private set; }

    public CompactWidget()
    {
        InitializeComponent();
        refreshTimer.Tick += OnRefreshTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is XboxGameBarWidget w)
            Widget = w;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        refreshTimer.Start();
        _ = RefreshAsync();
    }

    private async void OnRefreshTick(object sender, object e)
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        TelemetrySnapshot? snapshot = null;

        try
        {
            snapshot = await metricService.TryReadEnhancedSnapshotAsync(shutdown.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (snapshot is not null)
        {
            ApplyEnhancedSnapshot(snapshot);
            return;
        }

        ApplyStandaloneSnapshot();
    }

    private void ApplyEnhancedSnapshot(TelemetrySnapshot snapshot)
    {
        var accent = new SolidColorBrush(Color.FromArgb(255, 56, 189, 120));
        ModeText.Text = "Enhanced companion";
        ConnectionBadgeText.Text = "Connected";
        ConnectionBadgeText.Foreground = new SolidColorBrush(Colors.White);
        ModeValueText.Text = "Enhanced";
        SummaryText.Text = "Live overlay telemetry is flowing through the read-only companion pipe.";
        SummaryText.Foreground = new SolidColorBrush(Color.FromArgb(255, 181, 189, 208));

        FpsValueText.Text = snapshot.AvailableMetrics.HasFlag(MetricFlags.Fps) ? $"{snapshot.Performance.FramesPerSecond:0}" : "--";
        LowValueText.Text = snapshot.AvailableMetrics.HasFlag(MetricFlags.Fps) ? $"{snapshot.Performance.OnePercentLowFramesPerSecond:0}" : "--";
        FrameTimeValueText.Text = snapshot.AvailableMetrics.HasFlag(MetricFlags.FrameTime) ? $"{snapshot.Performance.FrameTimeMilliseconds:0.0}ms" : "--";
        CpuValueText.Text = snapshot.AvailableMetrics.HasFlag(MetricFlags.CpuUsage) ? $"{snapshot.Cpu.UsagePercent:0}%" : "--";
        GpuValueText.Text = snapshot.AvailableMetrics.HasFlag(MetricFlags.GpuUsage) ? $"{snapshot.Gpu.UsagePercent:0}%" : "--";
        RamValueText.Text = snapshot.AvailableMetrics.HasFlag(MetricFlags.Memory)
            ? $"{snapshot.Memory.RamUsedMegabytes / 1024:0.0}G"
            : "--";
        BatteryValueText.Text = snapshot.AvailableMetrics.HasFlag(MetricFlags.Battery)
            ? snapshot.Battery.IsCharging
                ? $"{snapshot.Battery.ChargePercent:0}% AC"
                : $"{snapshot.Battery.ChargePercent:0}%"
            : "--";
        RefreshValueText.Text = snapshot.AvailableMetrics.HasFlag(MetricFlags.Display) && snapshot.Display.RefreshRateHertz > 1
            ? $"{snapshot.Display.RefreshRateHertz:0}"
            : "--";

        BadgeBorder.Background = accent;
    }

    private void ApplyStandaloneSnapshot()
    {
        var accent = new SolidColorBrush(Color.FromArgb(255, 249, 115, 22));
        ModeText.Text = "Standalone companion";
        ConnectionBadgeText.Text = "Standalone";
        ConnectionBadgeText.Foreground = new SolidColorBrush(Colors.White);
        ModeValueText.Text = "Standalone";
        SummaryText.Text = "Only sandbox-safe companion state is available until the overlay pipe connects.";
        SummaryText.Foreground = new SolidColorBrush(Color.FromArgb(255, 181, 189, 208));

        var batteryPercent = PowerManager.RemainingChargePercent;
        var supply = PowerManager.PowerSupplyStatus;

        FpsValueText.Text = "--";
        LowValueText.Text = "--";
        FrameTimeValueText.Text = "--";
        CpuValueText.Text = "--";
        GpuValueText.Text = "--";
        RamValueText.Text = "--";
        BatteryValueText.Text = batteryPercent >= 0
            ? supply == PowerSupplyStatus.Adequate
                ? $"{batteryPercent}% AC"
                : $"{batteryPercent}%"
            : "--";
        RefreshValueText.Text = "--";

        BadgeBorder.Background = accent;
    }

    private async void OnStoreClicked(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?ProductId=9PLACEHOLDER"));
    }

    private async void OnSupportClicked(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("https://handheldally.com/support"));
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        refreshTimer.Stop();
        refreshTimer.Tick -= OnRefreshTick;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        shutdown.Cancel();
        await metricService.DisposeAsync();
        shutdown.Dispose();
    }
}
