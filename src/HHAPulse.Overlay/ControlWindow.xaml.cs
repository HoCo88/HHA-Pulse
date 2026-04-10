using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Overlay.Settings;
using HHAPulse.Overlay.ViewModels;
using HHAPulse.Shared.Models;
using HHAPulse.Shared.Protocol;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using WinRT.Interop;

namespace HHAPulse.Overlay;

public sealed partial class ControlWindow : Window
{
    private const int WindowWidth = 980;
    private const int WindowHeight = 740;
    private OverlayViewModel? viewModel;
    private AppSettings currentSettings = AppSettings.CreateDefault();
    private bool suppressToggleEvents;

    public ControlWindow()
    {
        InitializeComponent();
        Title = "HHA Pulse Settings";
        Activated += OnActivated;
        BuildShell();
        LogPathText.Text = $"Log path: {AppLogger.LogPath}";
        ApplySettings(currentSettings);
        ApplyTelemetryStatus();
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
            }

            ApplyTelemetryStatus();
        }
    }

    public void ApplySettings(AppSettings settings)
    {
        currentSettings = settings;

        suppressToggleEvents = true;
        BgOpacitySlider.Value = settings.BackgroundOpacity * 100;
        BgOpacityValue.Text = $"Current background opacity: {(int)BgOpacitySlider.Value}%";
        TextOpacitySlider.Value = settings.TextOpacity * 100;
        TextOpacityValue.Text = $"Current text opacity: {(int)TextOpacitySlider.Value}%";
        TextSizeSlider.Value = settings.TextSizePixels > 0 ? settings.TextSizePixels : 15;
        TextSizeValue.Text = $"Current HUD text size: {(int)TextSizeSlider.Value}px";
        ApplyToggleStates(settings.EnabledMetricIds);
        suppressToggleEvents = false;

        OverlayLayoutHintText.Text = "Positioning and future layout controls will keep building on the existing topmost overlay path.";
        GeneralPresetText.Text = $"{OverlayPresetCatalog.PresetDisplayName(settings.ActivePreset)} preset";
        GeneralQuickStateText.Text = $"Show mode: {ShowModeText(settings.ShowMode)}. Text size: {(int)TextSizeSlider.Value}px.";
        ShowModeDescription.Text = settings.ShowMode == OverlayShowMode.InGameOnly
            ? "Overlay is visible only when a game is in the foreground."
            : "Overlay is always visible.";

        PresetDescription.Text = settings.ActivePreset switch
        {
            OverlayPreset.Minimal => "Minimal keeps the HUD glanceable: FPS and battery.",
            OverlayPreset.Standard => "Standard balances play and tuning: FPS, 1% low, frame time, CPU, GPU, and battery.",
            OverlayPreset.Tuner => "Tuner expands into live hardware detail without inventing new telemetry semantics.",
            OverlayPreset.Custom => "Custom is opt-in power-user mode with grouped metric toggles.",
            OverlayPreset.Off => "Off hides the HUD while keeping the app and settings shell available.",
            _ => "Standard keeps the HUD balanced."
        };
        MetricsPresetSummaryText.Text = "Minimal, Standard, and Tuner are the default path. Custom only opens when you explicitly opt into individual metric control.";
        CustomModeStateText.Text = settings.ActivePreset == OverlayPreset.Custom
            ? "Custom mode is active. Toggle metrics below to shape the HUD."
            : "Custom mode is currently off. Presets remain the default experience.";
        CustomModeHintText.Text = settings.ActivePreset == OverlayPreset.Custom
            ? "Changes here persist to settings.json and only affect the Custom preset."
            : "Switch to Custom before editing individual metrics. Presets stay first so setup stays fast.";
        SwitchToCustomButton.Visibility = settings.ActivePreset == OverlayPreset.Custom ? Visibility.Collapsed : Visibility.Visible;
        foreach (var toggle in metricToggles.Values)
        {
            toggle.IsEnabled = settings.ActivePreset == OverlayPreset.Custom;
        }
        CustomModePanel.Opacity = settings.ActivePreset == OverlayPreset.Custom ? 1.0 : 0.72;
        HotkeyStatusText.Text = "Launch-command hotkeys are active: --toggle, --next-preset, --off, and --menu. Full rebinding is still a follow-up.";

        SetButtonState(PresetMinimalButton, settings.ActivePreset == OverlayPreset.Minimal);
        SetButtonState(PresetStandardButton, settings.ActivePreset == OverlayPreset.Standard);
        SetButtonState(PresetTunerButton, settings.ActivePreset == OverlayPreset.Tuner);
        SetButtonState(PresetCustomButton, settings.ActivePreset == OverlayPreset.Custom);
        SetButtonState(PresetOffButton, settings.ActivePreset == OverlayPreset.Off);
        SetButtonState(ShowAlwaysButton, settings.ShowMode == OverlayShowMode.Always);
        SetButtonState(ShowInGameButton, settings.ShowMode == OverlayShowMode.InGameOnly);
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

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var section = args.SelectedItemContainer?.Tag as string ?? "general";
        GeneralSection.Visibility = section == "general" ? Visibility.Visible : Visibility.Collapsed;
        OverlaySection.Visibility = section == "overlay" ? Visibility.Visible : Visibility.Collapsed;
        MetricsSection.Visibility = section == "metrics" ? Visibility.Visible : Visibility.Collapsed;
        FpsCaptureSection.Visibility = section == "capture" ? Visibility.Visible : Visibility.Collapsed;
        WidgetSection.Visibility = section == "widget" ? Visibility.Visible : Visibility.Collapsed;
        AboutSection.Visibility = section == "about" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(true, true);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(OverlayViewModel.CurrentSnapshot)
            or nameof(OverlayViewModel.ActivePreset)
            or nameof(OverlayViewModel.TopBarMetricIds))
        {
            DispatcherQueue.TryEnqueue(ApplyTelemetryStatus);
        }
    }

    private void ApplyTelemetryStatus()
    {
        var snapshot = viewModel?.CurrentSnapshot ?? new TelemetrySnapshot();
        var statuses = snapshot.MetricStatuses.Count > 0 ? snapshot.MetricStatuses : MetricStatusFactory.Create(snapshot);
        var dependencies = snapshot.Dependencies;

        GeneralRuntimeText.Text = $"Capture: {Blank(dependencies.CaptureServiceStatusMessage)} Widget: {Blank(dependencies.WidgetPipeStatusMessage)}";
        CaptureStatusDot.Fill = dependencies.CaptureServiceConnected ? ConnectedBrush : DisconnectedBrush;
        CaptureStatusText.Text = dependencies.CaptureServiceConnected ? "Connected" : "Disconnected";
        CaptureFreshnessText.Text = dependencies.CapturePayloadAgeMilliseconds > 0
            ? $"Payload freshness: {dependencies.CapturePayloadAgeMilliseconds} ms"
            : "Payload freshness: waiting for the first frame sample.";
        TargetStatusText.Text = dependencies.CaptureTargetProcessId == 0
            ? "Target: none"
            : $"Target: {Blank(dependencies.CaptureTargetProcessName)} ({dependencies.CaptureTargetProcessId})";
        CaptureProtocolText.Text = $"Protocol v{PipeConstants.ProtocolVersion}. Control pipe {PipeConstants.CaptureControlPipeLocalName}; output pipe {PipeConstants.CaptureOutputPipeLocalName}.";
        RuntimeStatusText.Text = $"Shared contract: v{Blank(dependencies.SharedAssemblyVersion)} | valid={dependencies.TelemetryContractValid}. {Blank(dependencies.TelemetryContractStatusMessage)}";
        GpuStatusText.Text = $"GPU telemetry: {BuildGpuTelemetrySummary(dependencies)} {BuildMetricTraceSummary(snapshot, OverlayPresetCatalog.GpuPower)}";
        CaptureHelpText.Text = $"{BuildCaptureHelpText(snapshot, dependencies)} {BuildMetricTraceSummary(snapshot, OverlayPresetCatalog.Fps)}";

        WidgetConnectionText.Text = dependencies.WidgetClientCount > 0 ? "Enhanced companion last observed" : "Standalone companion only";
        WidgetSummaryText.Text = $"{Blank(dependencies.WidgetPipeStatusMessage)} Count is last observed during broadcast writes, not a heartbeat.";
        WidgetPackagingText.Text = $"Read-only pipe: {PipeConstants.PipeLocalName}. Widget identity: HandheldAlly.HHAPulseWidget. Separate MSIX and Game Bar activation path.";

        var assembly = Assembly.GetExecutingAssembly();
        var overlayPath = assembly.Location;
        var nativePath = Path.Combine(AppContext.BaseDirectory, "HHAPulse.Native.dll");
        AboutVersionText.Text = $"Overlay {assembly.GetName().Version} at {overlayPath}";
        AboutContractText.Text = $"Shared: v{Blank(dependencies.SharedAssemblyVersion)} | MVID {Blank(dependencies.SharedAssemblyMvid)} | {Blank(dependencies.SharedAssemblyPath)}";
        AboutNativeBridgeText.Text = File.Exists(nativePath)
            ? $"Native bridge present at {nativePath}. ADLX, IGCL, and EMI still require build and hardware proof."
            : $"Native bridge missing at {nativePath}. Vendor-native collectors remain quarantined.";
        AboutTraceabilityText.Text = "Telemetry traceability is documented in docs/telemetry-traceability.md. ETW, PDH, DXGI, D3DKMT, battery, and display stay primary; ADLX, IGCL, and EMI remain disputed until validated.";
        DiagnosticsDumpText.Text = BuildDiagnosticsDump(snapshot, statuses, snapshot.MeasurementTraces);

        RebuildMetricStatusCards(statuses);
        UpdateMetricPickerDetails(statuses);
    }

    private void RebuildMetricStatusCards(IReadOnlyList<MetricStatus> statuses)
    {
        MetricStatusPanel.Children.Clear();
        var lookup = statuses.ToDictionary(status => status.MetricId, StringComparer.OrdinalIgnoreCase);

        foreach (var metricId in OverlayPresetCatalog.AllMetricIds)
        {
            var status = lookup.GetValueOrDefault(metricId) ?? new MetricStatus
            {
                MetricId = metricId,
                Source = "Waiting for runtime data",
                StatusMessage = "No telemetry has been collected yet."
            };

            var (badgeText, badgeBrush) = TruthBadge(status);
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            header.Children.Add(new TextBlock
            {
                Text = MetricDisplayNames.GetValueOrDefault(metricId, metricId),
                Foreground = Brush("ShellStrongBrush"),
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            header.Children.Add(new Border
            {
                Background = badgeBrush,
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(8, 2, 8, 2),
                Child = new TextBlock
                {
                    Text = badgeText,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                    FontSize = 11
                }
            });

            var body = new StackPanel { Spacing = 4 };
            body.Children.Add(header);
            body.Children.Add(CreateMuted($"Source: {status.Source}"));
            body.Children.Add(CreateMuted(status.StatusMessage));

            MetricStatusPanel.Children.Add(new Border
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 23, 25, 35)),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 77, 85, 106)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12),
                Child = body
            });
        }
    }

    private void UpdateMetricPickerDetails(IReadOnlyList<MetricStatus> statuses)
    {
        var lookup = statuses.ToDictionary(status => status.MetricId, StringComparer.OrdinalIgnoreCase);
        foreach (var metricId in OverlayPresetCatalog.AllMetricIds)
        {
            if (!metricDetailBlocks.TryGetValue(metricId, out var detail))
            {
                continue;
            }

            var status = lookup.GetValueOrDefault(metricId);
            if (status is null)
            {
                detail.Text = "Waiting for runtime validation.";
                continue;
            }

            var (badgeText, _) = TruthBadge(status);
            detail.Text = $"{badgeText} | {status.Source} | {status.StatusMessage}";
        }
    }

    private static string Blank(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "--" : value;
    }

    private static string BuildGpuTelemetrySummary(DependencyState dependencies)
    {
        var parts = new List<string>();

        AddGpuSource(parts, "temp", dependencies.GpuTemperatureSource);
        AddGpuSource(parts, "clock", dependencies.GpuClockSource);
        AddGpuSource(parts, "power", dependencies.GpuPowerSource);
        AddGpuSource(parts, "fan", dependencies.GpuFanSource);

        return parts.Count > 0
            ? string.Join(", ", parts)
            : dependencies.GpuTelemetryStatusMessage;
    }

    private static void AddGpuSource(List<string> parts, string metricName, string source)
    {
        if (!string.IsNullOrWhiteSpace(source))
        {
            parts.Add($"{metricName}={source}");
        }
    }

    private static string BuildCaptureHelpText(TelemetrySnapshot snapshot, DependencyState dependencies)
    {
        var routingText = dependencies.CaptureServiceConnected
            ? "Foreground target routing stays overlay -> capture control pipe -> ETW capture output."
            : "Enable or install the capture service if you want FPS metrics. Non-FPS telemetry continues locally.";

        var frameGenText = !dependencies.CaptureServiceConnected
            ? "Frame generation: unavailable until FPS capture is connected."
            : dependencies.CapturePayloadAgeMilliseconds > 2000
                ? "Frame generation: unavailable while capture data is stale."
                : snapshot.Performance.HybridPresentDetected
                    ? "Frame generation: detected from explicit Intel-PresentMon ETW evidence."
                    : "Frame generation: not detected in the current capture window.";

        return $"{routingText} {frameGenText}";
    }

    private static string BuildMetricTraceSummary(TelemetrySnapshot snapshot, string metricId)
    {
        var trace = snapshot.MeasurementTraces.FirstOrDefault(item => string.Equals(item.MetricId, metricId, StringComparison.OrdinalIgnoreCase));
        if (trace is null)
        {
            return string.Empty;
        }

        return $"Trace: {trace.ValueText} via {trace.Source}.";
    }

    private void OnPresetMinimalClicked(object sender, RoutedEventArgs args) => PresetChanged?.Invoke(OverlayPreset.Minimal);
    private void OnPresetStandardClicked(object sender, RoutedEventArgs args) => PresetChanged?.Invoke(OverlayPreset.Standard);
    private void OnPresetTunerClicked(object sender, RoutedEventArgs args) => PresetChanged?.Invoke(OverlayPreset.Tuner);
    private void OnPresetCustomClicked(object sender, RoutedEventArgs args) => PresetChanged?.Invoke(OverlayPreset.Custom);
    private void OnPresetOffClicked(object sender, RoutedEventArgs args) => PresetChanged?.Invoke(OverlayPreset.Off);
    private void OnCyclePresetClicked(object sender, RoutedEventArgs args) => PresetChanged?.Invoke(OverlayPresetCatalog.NextPreset(currentSettings.ActivePreset));

    private void OnToggleOverlayClicked(object sender, RoutedEventArgs args) => ToggleOverlayRequested?.Invoke();
    private void OnExitClicked(object sender, RoutedEventArgs args) => ExitRequested?.Invoke();

    private void OnShowAlwaysClicked(object sender, RoutedEventArgs args)
    {
        ShowModeChanged?.Invoke(OverlayShowMode.Always);
    }

    private void OnShowInGameClicked(object sender, RoutedEventArgs args)
    {
        ShowModeChanged?.Invoke(OverlayShowMode.InGameOnly);
    }

    private void OnEnableCaptureClicked(object sender, RoutedEventArgs args) => EnableCaptureRequested?.Invoke();

    private void OnOpenLogClicked(object sender, RoutedEventArgs args)
    {
        var directory = Path.GetDirectoryName(AppLogger.LogPath);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = directory,
            UseShellExecute = true
        });
    }

    private void OnCopyDiagnosticsClicked(object sender, RoutedEventArgs args)
    {
        var package = new DataPackage();
        package.SetText(DiagnosticsDumpText.Text ?? string.Empty);
        Clipboard.SetContent(package);
    }

    private void OnOpenSupportClicked(object sender, RoutedEventArgs args) => OpenUrl("https://handheldally.com/support");
    private void OnOpenCommunityClicked(object sender, RoutedEventArgs args) => OpenUrl("https://handheldally.com/games");
    private void OnOpenWebsiteClicked(object sender, RoutedEventArgs args) => OpenUrl("https://handheldally.com");

    private void OnBgOpacityChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (suppressToggleEvents) return;
        BgOpacityValue.Text = $"Current background opacity: {(int)args.NewValue}%";
        OpacityChanged?.Invoke(args.NewValue / 100.0, TextOpacitySlider.Value / 100.0);
    }

    private void OnTextOpacityChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (suppressToggleEvents) return;
        TextOpacityValue.Text = $"Current text opacity: {(int)args.NewValue}%";
        OpacityChanged?.Invoke(BgOpacitySlider.Value / 100.0, args.NewValue / 100.0);
    }

    private void OnTextSizeChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (suppressToggleEvents) return;
        TextSizeValue.Text = $"Current HUD text size: {(int)args.NewValue}px";
        TextSizeChanged?.Invoke(args.NewValue);
    }

    private static void SetButtonState(Button button, bool active)
    {
        button.IsEnabled = !active;
        button.Opacity = active ? 0.72 : 1.0;
    }

    private static string ShowModeText(OverlayShowMode mode) => mode == OverlayShowMode.InGameOnly ? "In-game only" : "Always";

    private static (string Text, SolidColorBrush Brush) TruthBadge(MetricStatus status)
    {
        if (string.Equals(status.ValidationState, TelemetryValidationState.Rejected, StringComparison.OrdinalIgnoreCase))
        {
            return ("Rejected", RejectedBrush);
        }

        if (string.Equals(status.ValidationState, TelemetryValidationState.HardwareValidationPending, StringComparison.OrdinalIgnoreCase))
        {
            return ("Hardware validation pending", PendingBrush);
        }

        if (!status.IsAvailable)
        {
            return ("Unavailable", UnavailableBrush);
        }

        if (IsDisputed(status))
        {
            return ("Hardware validation pending", PendingBrush);
        }

        return ("Verified", VerifiedBrush);
    }

    private static bool IsDisputed(MetricStatus status)
    {
        return status.MetricId == OverlayPresetCatalog.CpuPower
            || status.Source.Contains("ADLX", StringComparison.OrdinalIgnoreCase)
            || status.Source.Contains("IGCL", StringComparison.OrdinalIgnoreCase)
            || status.StatusMessage.Contains("hardware validation", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDiagnosticsDump(TelemetrySnapshot snapshot, IReadOnlyList<MetricStatus> statuses, IReadOnlyList<MeasurementTrace> traces)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var nativePath = Path.Combine(AppContext.BaseDirectory, "HHAPulse.Native.dll");
        var sb = new StringBuilder();
        sb.AppendLine("HHA Pulse Diagnostics");
        sb.AppendLine($"Overlay: {assembly.GetName().Version} | {assembly.Location}");
        sb.AppendLine($"Shared: {Blank(snapshot.Dependencies.SharedAssemblyVersion)} | MVID {Blank(snapshot.Dependencies.SharedAssemblyMvid)}");
        sb.AppendLine($"Shared path: {Blank(snapshot.Dependencies.SharedAssemblyPath)}");
        sb.AppendLine($"Native bridge: {(File.Exists(nativePath) ? "present" : "missing")} | {nativePath}");
        sb.AppendLine($"Protocol: v{PipeConstants.ProtocolVersion} | {PipeConstants.PipeLocalName}");
        sb.AppendLine($"Capture: {Blank(snapshot.Dependencies.CaptureServiceStatusMessage)} | age={snapshot.Dependencies.CapturePayloadAgeMilliseconds}ms");
        sb.AppendLine($"Target: {Blank(snapshot.Dependencies.CaptureTargetProcessName)} ({snapshot.Dependencies.CaptureTargetProcessId})");
        sb.AppendLine($"Widget: {Blank(snapshot.Dependencies.WidgetPipeStatusMessage)}");
        sb.AppendLine("Last observed boundary state:");
        sb.AppendLine($"- overlay -> capture control pipe: {PipeConstants.CaptureControlPipeLocalName} | target={Blank(snapshot.Dependencies.CaptureTargetProcessName)} ({snapshot.Dependencies.CaptureTargetProcessId})");
        sb.AppendLine($"- capture service -> overlay output pipe: {PipeConstants.CaptureOutputPipeLocalName} | connected={snapshot.Dependencies.CaptureServiceConnected} | age={snapshot.Dependencies.CapturePayloadAgeMilliseconds}ms");
        sb.AppendLine($"- collector orchestrator -> TelemetrySnapshot: timestamp={snapshot.TimestampUnixMilliseconds} | flags={snapshot.AvailableMetrics}");
        sb.AppendLine($"- overlay -> widget pipe: {PipeConstants.PipeLocalName} | clientsLastObserved={snapshot.Dependencies.WidgetClientCount}");
        sb.AppendLine("Disputed sources: ADLX, IGCL, and EMI remain quarantined until build and hardware proof clears them.");
        sb.AppendLine("Metrics:");
        foreach (var status in statuses)
        {
            var truth = TruthBadge(status).Text;
            sb.AppendLine($"- {status.MetricId}: {truth} | {status.Source} | {status.StatusMessage}");
        }

        sb.AppendLine("Measurement traces:");
        foreach (var trace in traces)
        {
            var availability = trace.IsAvailable ? "available" : "unavailable";
            sb.AppendLine($"- {trace.MetricId}: {availability} | state={Blank(trace.ValidationState)} | collector={trace.Collector} | source={trace.Source} | value={trace.ValueText}");
            sb.AppendLine($"  pipeline: {trace.PipelineText}");
            sb.AppendLine($"  evidence: api={Blank(trace.ApiContract)} | raw={Blank(trace.RawValue)} {Blank(trace.RawUnit)} | conversion={Blank(trace.ConversionRule)} | converted={Blank(trace.ConvertedValue)} {Blank(trace.ConvertedUnit)}");
            sb.AppendLine($"  status: {trace.StatusMessage}");
            sb.AppendLine($"  reason: {Blank(trace.Reason)}");
        }

        return sb.ToString().TrimEnd();
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
