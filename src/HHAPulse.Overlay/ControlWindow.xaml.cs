using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Overlay.Settings;
using HHAPulse.Overlay.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace HHAPulse.Overlay;

public sealed partial class ControlWindow : Window
{
    private const int WindowWidth = 520;
    private const int WindowHeight = 560;
    private OverlayViewModel? viewModel;

    private static readonly SolidColorBrush ConnectedBrush = new(Colors.LimeGreen);
    private static readonly SolidColorBrush DisconnectedBrush = new(Color.FromArgb(255, 85, 85, 102));

    // Friendly names for the metric picker.
    private static readonly Dictionary<string, string> MetricDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        [OverlayPresetCatalog.Fps] = "FPS",
        [OverlayPresetCatalog.AvgFps] = "Average FPS",
        [OverlayPresetCatalog.OnePercentLow] = "1% Low FPS",
        [OverlayPresetCatalog.ZeroPointOneLow] = "0.1% Low FPS",
        [OverlayPresetCatalog.FrameTime] = "Frame Time",
        [OverlayPresetCatalog.FrameGenFps] = "Frame Gen FPS",
        [OverlayPresetCatalog.InputLatency] = "Input Latency",
        [OverlayPresetCatalog.CpuUsage] = "CPU Usage",
        [OverlayPresetCatalog.CpuPower] = "CPU Power",
        [OverlayPresetCatalog.GpuUsage] = "GPU Usage",
        [OverlayPresetCatalog.GpuTemp] = "GPU Temperature",
        [OverlayPresetCatalog.GpuPower] = "GPU Power",
        [OverlayPresetCatalog.Ram] = "RAM",
        [OverlayPresetCatalog.Vram] = "VRAM",
        [OverlayPresetCatalog.TotalPower] = "System Power",
        [OverlayPresetCatalog.RefreshRate] = "Refresh Rate",
        [OverlayPresetCatalog.Battery] = "Battery",
    };

    private readonly Dictionary<string, ToggleSwitch> metricToggles = new(StringComparer.OrdinalIgnoreCase);
    private bool suppressToggleEvents;

    public ControlWindow()
    {
        InitializeComponent();
        Activated += OnActivated;
        LogPathText.Text = $"Log: {AppLogger.LogPath}";
        BuildMetricPicker();
    }

    public event Action? ToggleOverlayRequested;
    public event Action? ExitRequested;
    public event Action<OverlayShowMode>? ShowModeChanged;
    public event Action? EnableCaptureRequested;
    public event Action<OverlayPreset>? PresetChanged;
    public event Action<List<string>>? CustomMetricsChanged;
    public event Action<double, double>? OpacityChanged;
    public event Action<double>? TextSizeChanged;

    public OverlayViewModel? ViewModel
    {
        get => viewModel;
        set
        {
            if (ReferenceEquals(viewModel, value))
            {
                return;
            }

            if (viewModel is not null)
            {
                viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            viewModel = value;
            if (viewModel is not null)
            {
                viewModel.PropertyChanged += OnViewModelPropertyChanged;
                ApplyTelemetryStatus();
            }
        }
    }

    public void ApplySettings(AppSettings settings)
    {
        ShowModeDescription.Text = settings.ShowMode == OverlayShowMode.InGameOnly
            ? "Overlay is visible only when a game is in the foreground."
            : "Overlay is always visible.";

        PresetDescription.Text = settings.ActivePreset switch
        {
            OverlayPreset.Minimal => "Minimal: FPS and battery.",
            OverlayPreset.Standard => "Standard: FPS, 1% low, frametime, CPU, GPU, battery.",
            OverlayPreset.Tuner => "Tuner: all metrics \u2014 FPS graph, frame gen, power, temps, latency.",
            OverlayPreset.Custom => "Custom: tap metrics below to toggle them on or off.",
            OverlayPreset.Off => "Overlay is hidden.",
            _ => "Standard: FPS, 1% low, frametime, CPU, GPU, battery."
        };

        // Show/hide the metric picker based on preset.
        MetricPickerSection.Visibility = settings.ActivePreset == OverlayPreset.Custom
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Sync toggle states to settings.
        ApplyToggleStates(settings.EnabledMetricIds);

        // Sync opacity sliders.
        suppressToggleEvents = true;
        BgOpacitySlider.Value = settings.BackgroundOpacity * 100;
        BgOpacityValue.Text = $"{(int)(settings.BackgroundOpacity * 100)}%";
        TextOpacitySlider.Value = settings.TextOpacity * 100;
        TextOpacityValue.Text = $"{(int)(settings.TextOpacity * 100)}%";
        TextSizeSlider.Value = settings.TextSizePixels > 0 ? settings.TextSizePixels : 15;
        TextSizeValue.Text = $"{(int)TextSizeSlider.Value}";
        suppressToggleEvents = false;
    }

    private void BuildMetricPicker()
    {
        metricToggles.Clear();
        MetricPickerPanel.Children.Clear();

        foreach (var metricId in OverlayPresetCatalog.AllMetricIds)
        {
            var displayName = MetricDisplayNames.GetValueOrDefault(metricId, metricId);

            var toggle = new ToggleSwitch
            {
                Header = displayName,
                IsOn = false,
                OnContent = "On",
                OffContent = "Off",
                MinWidth = 200,
            };

            // Capture metricId in closure.
            var capturedId = metricId;
            toggle.Toggled += (_, _) =>
            {
                if (!suppressToggleEvents)
                {
                    OnMetricToggled();
                }
            };

            metricToggles[metricId] = toggle;
            MetricPickerPanel.Children.Add(toggle);
        }
    }

    private void ApplyToggleStates(List<string> enabledMetricIds)
    {
        suppressToggleEvents = true;
        try
        {
            var enabledSet = new HashSet<string>(enabledMetricIds, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in metricToggles)
            {
                pair.Value.IsOn = enabledSet.Contains(pair.Key);
            }
        }
        finally
        {
            suppressToggleEvents = false;
        }
    }

    private void OnMetricToggled()
    {
        var enabledIds = new List<string>();
        foreach (var metricId in OverlayPresetCatalog.AllMetricIds)
        {
            if (metricToggles.TryGetValue(metricId, out var toggle) && toggle.IsOn)
            {
                enabledIds.Add(metricId);
            }
        }

        CustomMetricsChanged?.Invoke(enabledIds);
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(true, true);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(OverlayViewModel.CurrentSnapshot))
        {
            DispatcherQueue.TryEnqueue(ApplyTelemetryStatus);
        }
    }

    private void ApplyTelemetryStatus()
    {
        if (viewModel is null)
        {
            return;
        }

        var dependencies = viewModel.CurrentSnapshot.Dependencies;
        var connected = dependencies.CaptureServiceConnected;

        CaptureStatusDot.Fill = connected ? ConnectedBrush : DisconnectedBrush;
        CaptureStatusText.Text = connected
            ? $"Connected: {dependencies.CaptureTargetProcessName}".TrimEnd()
            : "Not connected";
    }

    // Preset handlers.
    private void OnPresetMinimalClicked(object sender, RoutedEventArgs args) => PresetChanged?.Invoke(OverlayPreset.Minimal);
    private void OnPresetStandardClicked(object sender, RoutedEventArgs args) => PresetChanged?.Invoke(OverlayPreset.Standard);
    private void OnPresetTunerClicked(object sender, RoutedEventArgs args) => PresetChanged?.Invoke(OverlayPreset.Tuner);
    private void OnPresetCustomClicked(object sender, RoutedEventArgs args) => PresetChanged?.Invoke(OverlayPreset.Custom);
    private void OnPresetOffClicked(object sender, RoutedEventArgs args) => PresetChanged?.Invoke(OverlayPreset.Off);

    // Visibility handlers.
    private void OnToggleOverlayClicked(object sender, RoutedEventArgs args) => ToggleOverlayRequested?.Invoke();
    private void OnExitClicked(object sender, RoutedEventArgs args) => ExitRequested?.Invoke();

    private void OnShowAlwaysClicked(object sender, RoutedEventArgs args)
    {
        ShowModeDescription.Text = "Overlay is always visible.";
        ShowModeChanged?.Invoke(OverlayShowMode.Always);
    }

    private void OnShowInGameClicked(object sender, RoutedEventArgs args)
    {
        ShowModeDescription.Text = "Overlay is visible only when a game is in the foreground.";
        ShowModeChanged?.Invoke(OverlayShowMode.InGameOnly);
    }

    private void OnEnableCaptureClicked(object sender, RoutedEventArgs args) => EnableCaptureRequested?.Invoke();

    // Opacity handlers.
    private void OnBgOpacityChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (suppressToggleEvents) return;
        BgOpacityValue.Text = $"{(int)args.NewValue}%";
        OpacityChanged?.Invoke(args.NewValue / 100.0, TextOpacitySlider.Value / 100.0);
    }

    private void OnTextOpacityChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (suppressToggleEvents) return;
        TextOpacityValue.Text = $"{(int)args.NewValue}%";
        OpacityChanged?.Invoke(BgOpacitySlider.Value / 100.0, args.NewValue / 100.0);
    }

    private void OnTextSizeChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (suppressToggleEvents) return;
        TextSizeValue.Text = $"{(int)args.NewValue}";
        TextSizeChanged?.Invoke(args.NewValue);
    }
}
