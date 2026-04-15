using System.Linq;
using System.ComponentModel;
using HHAPulse.Overlay.Settings;
using HHAPulse.Overlay.ViewModels;
using HHAPulse.Shared.Models;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;

namespace HHAPulse.Overlay;

public sealed partial class ControlWindow : Window
{
    private const int PreferredWindowWidth = 1024;
    private const int PreferredWindowHeight = 700;
    private const int MinimumWindowWidth = 760;
    private const int MinimumWindowHeight = 520;

    private readonly CompanionViewModel companionViewModel = new();
    private OverlayViewModel? viewModel;
    private AppSettings currentSettings = AppSettings.CreateDefault();
    private TelemetrySnapshot lastSnapshot = new();

    public ControlWindow()
    {
        InitializeComponent();
        Title = "Handheld Ally Pulse";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);

        CompanionViewControl.ViewModel = companionViewModel;
        CompanionViewControl.ToggleOverlayRequested += OnToggleOverlayRequested;
        CompanionViewControl.ExitRequested += OnExitRequestedRequested;
        CompanionViewControl.ShowModeChanged += OnShowModeRequested;
        CompanionViewControl.PresetRequested += OnPresetRequested;
        CompanionViewControl.ManualRequested += OnManualRequested;
        CompanionViewControl.CustomMetricsChanged += OnCustomMetricsRequested;
        CompanionViewControl.OpacityChanged += OnOpacityRequested;
        CompanionViewControl.TextSizeChanged += OnTextSizeRequested;
        CompanionViewControl.PositionChanged += OnPositionRequested;

        Activated += OnFirstActivated;
        Closed += OnClosed;
        ApplySettings(currentSettings);
    }

    public event Action? ToggleOverlayRequested;
    public event Action? ExitRequested;
    public event Action<OverlayShowMode>? ShowModeChanged;
    public event Action<List<string>>? CustomMetricsChanged;
    public event Action<OverlayPreset>? PresetChanged;
    public event Action<double, double>? OpacityChanged;
    public event Action<double>? TextSizeChanged;
    public event Action<TopBarPosition>? PositionChanged;

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
            companionViewModel.OverlayViewModel = value;
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
        companionViewModel.Apply(settings, lastSnapshot);
        CompanionViewControl.ViewModel = companionViewModel;
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
        lastSnapshot = viewModel?.CurrentSnapshot ?? new TelemetrySnapshot();
        companionViewModel.OverlayViewModel = viewModel;
        companionViewModel.Apply(currentSettings, lastSnapshot);
        CompanionViewControl.ViewModel = companionViewModel;
    }

    private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        ConfigureAppWindow();
    }

    private void ConfigureAppWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        var width = Math.Clamp(PreferredWindowWidth, MinimumWindowWidth, Math.Max(MinimumWindowWidth, workArea.Width - 32));
        var height = Math.Clamp(PreferredWindowHeight, MinimumWindowHeight, Math.Max(MinimumWindowHeight, workArea.Height - 32));
        appWindow.Resize(new SizeInt32(width, height));

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 170, 182, 214);
            titleBar.ButtonHoverBackgroundColor = Color.FromArgb(28, 255, 255, 255);
            titleBar.ButtonPressedBackgroundColor = Color.FromArgb(40, 255, 255, 255);
        }

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
            presenter.PreferredMinimumWidth = MinimumWindowWidth;
            presenter.PreferredMinimumHeight = MinimumWindowHeight;
            presenter.SetBorderAndTitleBar(true, true);
        }
    }

    private void OnManualRequested()
    {
        var previousPreset = currentSettings.ActivePreset;
        var previousMetricIds = currentSettings.EnabledMetricIds.ToList();
        var sourcePreset = ResolveManualSourcePreset(currentSettings);
        var metricIds = companionViewModel.CreateManualSeed().ToList();

        currentSettings.ManualSourcePreset = sourcePreset;
        currentSettings.EnabledMetricIds = metricIds;
        currentSettings.ActivePreset = OverlayPreset.Custom;
        companionViewModel.SetManualSheetOpen(true);
        companionViewModel.Apply(currentSettings, lastSnapshot);
        CompanionViewControl.ViewModel = companionViewModel;
        CompanionViewControl.ShowManualSheet(metricIds);

        if (previousPreset != OverlayPreset.Custom || !previousMetricIds.SequenceEqual(metricIds))
        {
            CustomMetricsChanged?.Invoke(metricIds);
            PresetChanged?.Invoke(OverlayPreset.Custom);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        CompanionViewControl.ToggleOverlayRequested -= OnToggleOverlayRequested;
        CompanionViewControl.ExitRequested -= OnExitRequestedRequested;
        CompanionViewControl.ShowModeChanged -= OnShowModeRequested;
        CompanionViewControl.PresetRequested -= OnPresetRequested;
        CompanionViewControl.ManualRequested -= OnManualRequested;
        CompanionViewControl.CustomMetricsChanged -= OnCustomMetricsRequested;
        CompanionViewControl.OpacityChanged -= OnOpacityRequested;
        CompanionViewControl.TextSizeChanged -= OnTextSizeRequested;
        CompanionViewControl.PositionChanged -= OnPositionRequested;
        Closed -= OnClosed;
        ViewModel = null;
    }

    private static OverlayPreset ResolveManualSourcePreset(AppSettings settings)
    {
        return settings.ActivePreset switch
        {
            OverlayPreset.Minimal or OverlayPreset.Standard or OverlayPreset.Tuner or OverlayPreset.Full => settings.ActivePreset,
            OverlayPreset.Custom when settings.ManualSourcePreset is OverlayPreset.Minimal or OverlayPreset.Standard or OverlayPreset.Tuner or OverlayPreset.Full => settings.ManualSourcePreset,
            _ => OverlayPreset.Standard
        };
    }

    private void OnToggleOverlayRequested() => ToggleOverlayRequested?.Invoke();
    private void OnExitRequestedRequested() => ExitRequested?.Invoke();
    private void OnShowModeRequested(OverlayShowMode mode) => ShowModeChanged?.Invoke(mode);
    private void OnPresetRequested(OverlayPreset preset) => PresetChanged?.Invoke(preset);
    private void OnCustomMetricsRequested(List<string> metrics) => CustomMetricsChanged?.Invoke(metrics);
    private void OnOpacityRequested(double backgroundOpacity, double textOpacity) => OpacityChanged?.Invoke(backgroundOpacity, textOpacity);
    private void OnTextSizeRequested(double size) => TextSizeChanged?.Invoke(size);
    private void OnPositionRequested(TopBarPosition position) => PositionChanged?.Invoke(position);
}
