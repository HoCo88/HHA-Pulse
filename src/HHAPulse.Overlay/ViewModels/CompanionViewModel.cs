using System.Linq;
using HHAPulse.Overlay.Settings;
using HHAPulse.Overlay.Views;
using HHAPulse.Overlay.Helpers;
using HHAPulse.Shared.Models;
using Microsoft.UI.Xaml;

namespace HHAPulse.Overlay.ViewModels;

public sealed class CompanionViewModel : ViewModelBase
{
    private OverlayViewModel? overlayViewModel;
    private AppSettings settings = AppSettings.CreateDefault();
    private TelemetrySnapshot snapshot = new();
    private IReadOnlyList<CompanionPresetCardModel> presetCards = Array.Empty<CompanionPresetCardModel>();
    private IReadOnlyList<string> activeMetricIds = Array.Empty<string>();
    private string overlayChipText = "OVERLAY ON";
    private string captureChipText = "CAPTURE OFFLINE";
    private string gameTitleText = "NO GAME DETECTED";
    private string eventLineText = "--";
    private string eventLineShort = "--";
    private string positionText = "Top thin";
    private string feelAndFitText = "Default";
    private string manualSummaryText = "--";
    private string npuHeadline = "NPU not detected";
    private string npuDetail = "Utilization unavailable on current Windows.";
    private CompanionHealth captureHealth;
    private CompanionHealth gameHealth;
    private CompanionHealth widgetHealth = CompanionHealth.Limited;
    private string captureFooterText = "Capture offline";
    private string gameFooterText = "Game detect waiting";
    private string widgetFooterText = "Companion standalone";
    private bool isPositionFlyoutOpen;
    private bool isFeelAndFitOpen;
    private bool isManualSheetOpen;

    public OverlayViewModel? OverlayViewModel
    {
        get => overlayViewModel;
        set => SetProperty(ref overlayViewModel, value);
    }

    public AppSettings Settings => settings;
    public TelemetrySnapshot Snapshot => snapshot;
    public IReadOnlyList<CompanionPresetCardModel> PresetCards => presetCards;
    public IReadOnlyList<string> ActiveMetricIds => activeMetricIds;
    public string OverlayChipText { get => overlayChipText; private set => SetProperty(ref overlayChipText, value); }
    public string CaptureChipText { get => captureChipText; private set => SetProperty(ref captureChipText, value); }
    public string GameTitleText { get => gameTitleText; private set => SetProperty(ref gameTitleText, value); }
    public string EventLineText { get => eventLineText; private set => SetProperty(ref eventLineText, value); }
    public string EventLineShort { get => eventLineShort; private set => SetProperty(ref eventLineShort, value); }
    public string PositionText { get => positionText; private set => SetProperty(ref positionText, value); }
    public string FeelAndFitText { get => feelAndFitText; private set => SetProperty(ref feelAndFitText, value); }
    public string ManualSummaryText { get => manualSummaryText; private set => SetProperty(ref manualSummaryText, value); }
    public string NpuHeadline { get => npuHeadline; private set => SetProperty(ref npuHeadline, value); }
    public string NpuDetail { get => npuDetail; private set => SetProperty(ref npuDetail, value); }
    public CompanionHealth CaptureHealth { get => captureHealth; private set => SetProperty(ref captureHealth, value); }
    public CompanionHealth GameHealth { get => gameHealth; private set => SetProperty(ref gameHealth, value); }
    public CompanionHealth WidgetHealth { get => widgetHealth; private set => SetProperty(ref widgetHealth, value); }
    public string CaptureFooterText { get => captureFooterText; private set => SetProperty(ref captureFooterText, value); }
    public string GameFooterText { get => gameFooterText; private set => SetProperty(ref gameFooterText, value); }
    public string WidgetFooterText { get => widgetFooterText; private set => SetProperty(ref widgetFooterText, value); }
    public bool IsPositionFlyoutOpen { get => isPositionFlyoutOpen; private set => SetProperty(ref isPositionFlyoutOpen, value); }
    public bool IsFeelAndFitOpen { get => isFeelAndFitOpen; private set => SetProperty(ref isFeelAndFitOpen, value); }
    public bool IsManualSheetOpen { get => isManualSheetOpen; private set => SetProperty(ref isManualSheetOpen, value); }

    public void Apply(AppSettings nextSettings, TelemetrySnapshot nextSnapshot)
    {
        settings = nextSettings;
        snapshot = nextSnapshot;

        if (nextSettings.ActivePreset == OverlayPreset.Custom)
        {
            IsManualSheetOpen = true;
        }
        else
        {
            IsManualSheetOpen = false;
        }

        activeMetricIds = ResolveActiveMetricIds(nextSettings).ToArray();
        OnPropertyChanged(nameof(ActiveMetricIds));

        OverlayChipText = nextSettings.ActivePreset == OverlayPreset.Off ? "OVERLAY OFF" : "OVERLAY ON";
        CaptureChipText = nextSnapshot.Dependencies.CaptureServiceConnected
            ? nextSnapshot.Dependencies.CapturePayloadAgeMilliseconds > 2000 ? "CAPTURE LIMITED" : "CAPTURE READY"
            : "CAPTURE OFFLINE";
        GameTitleText = string.IsNullOrWhiteSpace(nextSnapshot.Dependencies.CaptureTargetProcessName)
            ? "NO GAME DETECTED"
            : nextSnapshot.Dependencies.CaptureTargetProcessName.ToUpperInvariant();
        PositionText = DescribePosition(nextSettings.TopBarPosition);
        FeelAndFitText = $"BG {(int)Math.Round(nextSettings.BackgroundOpacity * 100)}% · TXT {(int)Math.Round(nextSettings.TextOpacity * 100)}% · {nextSettings.TextSizePixels:0}px";
        ManualSummaryText = $"Manual matches {OverlayPresetCatalog.PresetDisplayName(SourcePresetForManual(nextSettings))} + your changes";
        EventLineText = BuildEventLine(nextSnapshot);
        EventLineShort = BuildEventLineShort(nextSnapshot);

        var detect = ControlShellTextBuilder.BuildGameDetectSummary(nextSnapshot, nextSnapshot.Dependencies);
        CaptureHealth = nextSnapshot.Dependencies.CaptureServiceConnected
            ? nextSnapshot.Dependencies.CapturePayloadAgeMilliseconds > 2000 ? CompanionHealth.Limited : CompanionHealth.Healthy
            : CompanionHealth.Offline;
        GameHealth = detect.Headline switch
        {
            "Active" => CompanionHealth.Healthy,
            "Ready" => CompanionHealth.Healthy,
            "Limited" => CompanionHealth.Limited,
            _ => CompanionHealth.Offline
        };
        WidgetHealth = nextSnapshot.Dependencies.WidgetClientCount > 0 ? CompanionHealth.Healthy : CompanionHealth.Limited;
        CaptureFooterText = nextSnapshot.Dependencies.CaptureServiceConnected ? "Capture online" : "Capture offline";
        GameFooterText = detect.Headline == "Active" ? "Game detect active" : detect.Headline == "Ready" ? "Game detect ready" : "Game detect limited";
        WidgetFooterText = nextSnapshot.Dependencies.WidgetClientCount > 0 ? "Companion linked" : "Companion standalone";

        if (nextSnapshot.Npu.Present)
        {
            NpuHeadline = nextSnapshot.Npu.AdapterName;
            NpuDetail = $"{BuildVendorLabel(nextSnapshot.Npu.Vendor)} detected. Utilization unavailable on current Windows.";
        }
        else
        {
            NpuHeadline = "NPU not detected";
            NpuDetail = "Utilization unavailable on current Windows.";
        }

        presetCards = BuildPresetCards(nextSettings, nextSnapshot);
        OnPropertyChanged(nameof(PresetCards));
    }

    public IReadOnlyList<string> CreateManualSeed()
    {
        if (settings.ActivePreset == OverlayPreset.Custom && settings.EnabledMetricIds.Count > 0)
        {
            return settings.EnabledMetricIds.ToArray();
        }

        return ResolveActiveMetricIds(settings).ToArray();
    }

    public void SetPositionFlyoutOpen(bool isOpen) => IsPositionFlyoutOpen = isOpen;
    public void SetFeelAndFitOpen(bool isOpen) => IsFeelAndFitOpen = isOpen;
    public void SetManualSheetOpen(bool isOpen) => IsManualSheetOpen = isOpen;

    private IReadOnlyList<string> ResolveActiveMetricIds(AppSettings current)
    {
        if (OverlayViewModel is not null && OverlayViewModel.TopBarMetricIds.Count > 0)
        {
            return OverlayViewModel.TopBarMetricIds.ToArray();
        }

        return OverlayPresetCatalog.GetMetricIds(current.ActivePreset, current.EnabledMetricIds);
    }

    private List<CompanionPresetCardModel> BuildPresetCards(AppSettings current, TelemetrySnapshot currentSnapshot)
    {
        return new List<CompanionPresetCardModel>
        {
            BuildPresetCard(current, currentSnapshot, OverlayPreset.Minimal, "L1"),
            BuildPresetCard(current, currentSnapshot, OverlayPreset.Standard, "L2"),
            BuildPresetCard(current, currentSnapshot, OverlayPreset.Tuner, "L3"),
            BuildPresetCard(current, currentSnapshot, OverlayPreset.Full, "L4")
        };
    }

    private static CompanionPresetCardModel BuildPresetCard(AppSettings current, TelemetrySnapshot currentSnapshot, OverlayPreset preset, string eyebrow)
    {
        var metrics = OverlayPresetCatalog.GetMetricIds(preset, current.EnabledMetricIds).ToArray();
        return new CompanionPresetCardModel(
            preset,
            eyebrow,
            OverlayPresetCatalog.PresetDisplayName(preset),
            BuildMiniPreview(metrics, currentSnapshot),
            $"{metrics.Length} metric{(metrics.Length == 1 ? string.Empty : "s")}",
            current.ActivePreset == preset);
    }

    private static string BuildMiniPreview(IReadOnlyList<string> metricIds, TelemetrySnapshot currentSnapshot)
    {
        var parts = new List<string>();
        foreach (var id in metricIds.Take(4))
        {
            parts.Add($"{MetricLabel(id)} {MetricPreviewValue(id, currentSnapshot)}");
        }

        return string.Join(" · ", parts);
    }

    private static string MetricLabel(string id) => id switch
    {
        var value when value == OverlayPresetCatalog.Fps => "FPS",
        var value when value == OverlayPresetCatalog.AvgFps => "AVG",
        var value when value == OverlayPresetCatalog.OnePercentLow => "1%",
        var value when value == OverlayPresetCatalog.ZeroPointOneLow => "0.1%",
        var value when value == OverlayPresetCatalog.FrameTime => "FT",
        var value when value == OverlayPresetCatalog.CpuUsage => "CPU",
        var value when value == OverlayPresetCatalog.CpuTemp => "CPU",
        var value when value == OverlayPresetCatalog.CpuPower => "CPU",
        var value when value == OverlayPresetCatalog.CpuClock => "CPU",
        var value when value == OverlayPresetCatalog.GpuUsage => "GPU",
        var value when value == OverlayPresetCatalog.GpuTemp => "GPU",
        var value when value == OverlayPresetCatalog.GpuClock => "GPU",
        var value when value == OverlayPresetCatalog.GpuPower => "GPU",
        var value when value == OverlayPresetCatalog.Ram => "RAM",
        var value when value == OverlayPresetCatalog.Vram => "VRAM",
        var value when value == OverlayPresetCatalog.StorageTemp => "SSD",
        var value when value == OverlayPresetCatalog.StorageWear => "SSD",
        var value when value == OverlayPresetCatalog.DeviceTemp => "SYS",
        var value when value == OverlayPresetCatalog.GpuFan => "FAN",
        var value when value == OverlayPresetCatalog.RefreshRate => "HZ",
        var value when value == OverlayPresetCatalog.Battery => "BAT",
        var value when value == OverlayPresetCatalog.TotalPower => "SYS",
        _ => id.ToUpperInvariant()
    };

    private static string MetricPreviewValue(string id, TelemetrySnapshot currentSnapshot)
    {
        var value = MetricFormatterCompact.FormatValue(id, currentSnapshot);
        return value == "--" ? "--" : value;
    }

    private static string BuildEventLine(TelemetrySnapshot currentSnapshot)
    {
        var timestamp = currentSnapshot.TimestampUnixMilliseconds > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(currentSnapshot.TimestampUnixMilliseconds).ToLocalTime().ToString("HH:mm:ss")
            : "--:--:--";
        var pid = currentSnapshot.Dependencies.CaptureTargetProcessId > 0
            ? $"pid {currentSnapshot.Dependencies.CaptureTargetProcessId}"
            : "pid --";
        var exe = string.IsNullOrWhiteSpace(currentSnapshot.Dependencies.CaptureTargetProcessName)
            ? "NO_GAME"
            : currentSnapshot.Dependencies.CaptureTargetProcessName.ToUpperInvariant();
        var frameTime = currentSnapshot.AvailableMetrics.HasFlag(MetricFlags.Fps)
            ? $"{currentSnapshot.Performance.FrameTimeMilliseconds:0.0} ms"
            : "-- ms";
        var hz = currentSnapshot.AvailableMetrics.HasFlag(MetricFlags.Display)
            ? $"{currentSnapshot.Display.RefreshRateHertz:0} Hz"
            : "-- Hz";
        return $"{timestamp} · {pid} · {exe} · frametime {frameTime} · {hz}";
    }

    private static string BuildEventLineShort(TelemetrySnapshot currentSnapshot)
    {
        var timestamp = currentSnapshot.TimestampUnixMilliseconds > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(currentSnapshot.TimestampUnixMilliseconds).ToLocalTime().ToString("HH:mm:ss")
            : "--:--:--";
        var pid = currentSnapshot.Dependencies.CaptureTargetProcessId > 0
            ? $"{currentSnapshot.Dependencies.CaptureTargetProcessId}"
            : "--";
        var exe = string.IsNullOrWhiteSpace(currentSnapshot.Dependencies.CaptureTargetProcessName)
            ? "NO_GAME"
            : currentSnapshot.Dependencies.CaptureTargetProcessName.ToUpperInvariant();
        return $"{timestamp} · pid {pid} · {exe}";
    }

    private static OverlayPreset SourcePresetForManual(AppSettings current) => current.ActivePreset switch
    {
        OverlayPreset.Custom => current.ManualSourcePreset,
        _ => current.ActivePreset
    };

    private static string DescribePosition(TopBarPosition position) => position switch
    {
        TopBarPosition.TopThin => "Top · 1 line",
        TopBarPosition.TopTall => "Top · 2 lines",
        TopBarPosition.BottomThin => "Bottom · 1 line",
        TopBarPosition.BottomTall => "Bottom · 2 lines",
        TopBarPosition.LeftDock => "Left dock",
        TopBarPosition.RightDock => "Right dock",
        _ => "Top · 1 line"
    };

    private static string BuildVendorLabel(NpuVendor vendor) => vendor switch
    {
        NpuVendor.Intel => "Intel AI Boost",
        NpuVendor.Amd => "AMD Ryzen AI",
        NpuVendor.Qualcomm => "Qualcomm Hexagon",
        NpuVendor.Other => "NPU",
        _ => "NPU"
    };
}

public sealed record CompanionPresetCardModel(
    OverlayPreset Preset,
    string Eyebrow,
    string Name,
    string Preview,
    string CountText,
    bool IsActive)
{
    public Visibility ActiveVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;
}

public enum CompanionHealth
{
    Offline = 0,
    Limited = 1,
    Healthy = 2
}
