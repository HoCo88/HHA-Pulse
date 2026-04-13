using System.ComponentModel;
using HHAPulse.Overlay.Settings;
using HHAPulse.Overlay.ViewModels;
using HHAPulse.Overlay.Views;
using HHAPulse.Shared;
using HHAPulse.Shared.Models;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.System;
using Windows.UI;
using WinRT.Interop;

namespace HHAPulse.Overlay;

public sealed partial class ControlWindow : Window
{
    private const int PreferredWindowWidth = 1024;
    private const int PreferredWindowHeight = 700;
    private const int MinimumWindowWidth = 760;
    private const int MinimumWindowHeight = 520;

    private readonly HubView hubView;
    private readonly SupportView supportView;
    private OverlayViewModel? viewModel;
    private AppSettings currentSettings = AppSettings.CreateDefault();
    private TelemetrySnapshot lastSnapshot = new();
    private string currentSectionTag = "hub";
    private bool isSettingsPanelOpen;
    private bool isComposerOpen;

    public ControlWindow()
    {
        InitializeComponent();
        Title = "Handheld Ally Pulse";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);

        hubView = new HubView();
        hubView.ToggleOverlayRequested += () => ToggleOverlayRequested?.Invoke();
        hubView.CustomizeRequested += OpenComposer;
        hubView.PresetRequested += p => PresetChanged?.Invoke(p);
        hubView.ShowModeRequested += m => ShowModeChanged?.Invoke(m);
        hubView.PositionRequested += p => PositionChanged?.Invoke(p);

        supportView = new SupportView();
        supportView.ExitRequested += () => ExitRequested?.Invoke();

        SettingsSheetControl.CloseRequested += () => SetSettingsPanelOpen(false);
        SettingsSheetControl.OpacityChanged += (bg, txt) => OpacityChanged?.Invoke(bg, txt);
        SettingsSheetControl.TextSizeChanged += size => TextSizeChanged?.Invoke(size);
        SettingsSheetControl.PositionChanged += edge => PositionChanged?.Invoke(edge);

        ComposerOverlayControl.CloseRequested += () => SetComposerOpen(false);
        ComposerOverlayControl.PresetApplied += OnComposerPresetApplied;
        ComposerOverlayControl.LayoutChanged += OnComposerLayoutChanged;

        Activated += OnFirstActivated;

        ContentHost.Content = hubView;
        ApplySettings(currentSettings);
    }

    public event Action? ToggleOverlayRequested;
    public event Action? ExitRequested;
    public event Action<OverlayShowMode>? ShowModeChanged;
#pragma warning disable CS0067
    public event Action? EnableCaptureRequested;
#pragma warning restore CS0067
    public event Action<List<string>>? CustomMetricsChanged;
    public event Action<OverlayPreset>? PresetChanged;
    public event Action<double, double>? OpacityChanged;
    public event Action<double>? TextSizeChanged;
    public event Action<OverlayEdge>? PositionChanged;

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
        hubView.ApplyState(settings, lastSnapshot);
        SettingsSheetControl.ApplySettings(settings);
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
        hubView.ApplyState(currentSettings, lastSnapshot);
        supportView.ApplyTelemetryStatus(lastSnapshot);
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

    private void OnHubNavClicked(object sender, RoutedEventArgs e) => SelectSection("hub");
    private void OnSupportNavClicked(object sender, RoutedEventArgs e) => SelectSection("support");

    private void SelectSection(string section)
    {
        if (string.Equals(currentSectionTag, section, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        currentSectionTag = section;
        if (section == "hub")
        {
            ContentHost.Content = hubView;
            HubNavButton.Style = (Style)Application.Current.Resources["HhapPrimaryButtonStyle"];
            SupportNavButton.Style = (Style)Application.Current.Resources["HhapGhostButtonStyle"];
        }
        else
        {
            supportView.ApplyTelemetryStatus(lastSnapshot);
            ContentHost.Content = supportView;
            HubNavButton.Style = (Style)Application.Current.Resources["HhapGhostButtonStyle"];
            SupportNavButton.Style = (Style)Application.Current.Resources["HhapPrimaryButtonStyle"];
        }
    }

    private void OnSettingsButtonClicked(object sender, RoutedEventArgs e) => SetSettingsPanelOpen(!isSettingsPanelOpen);
    private void OnSettingsScrimTapped(object sender, TappedRoutedEventArgs e) => SetSettingsPanelOpen(false);

    private void SetSettingsPanelOpen(bool isOpen)
    {
        isSettingsPanelOpen = isOpen;
        SettingsSheetHost.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        if (isOpen)
        {
            SettingsSheetControl.ApplySettings(currentSettings);
        }
    }

    private void OpenComposer()
    {
        ComposerOverlayControl.ResetToStart();
        SetComposerOpen(true);
    }

    private void OnComposerPresetApplied(CustomPresetDraft draft)
    {
        CustomMetricsChanged?.Invoke(draft.OrderedMetricIds.ToList());
        PresetChanged?.Invoke(OverlayPreset.Custom);
        SetComposerOpen(false);
    }

    private void OnComposerLayoutChanged(ComposerLayout layout)
    {
        switch (layout)
        {
            case ComposerLayout.TopBar:
                PositionChanged?.Invoke(OverlayEdge.Top);
                break;
            case ComposerLayout.BottomBar:
                PositionChanged?.Invoke(OverlayEdge.Bottom);
                break;
        }
    }

    private void SetComposerOpen(bool isOpen)
    {
        isComposerOpen = isOpen;
        ComposerOverlayControl.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnShellKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape)
        {
            return;
        }

        if (isComposerOpen)
        {
            SetComposerOpen(false);
            e.Handled = true;
            return;
        }

        if (isSettingsPanelOpen)
        {
            SetSettingsPanelOpen(false);
            e.Handled = true;
        }
    }
}
