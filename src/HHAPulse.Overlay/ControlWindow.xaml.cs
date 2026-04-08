using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Overlay.Settings;
using HHAPulse.Overlay.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace HHAPulse.Overlay;

public sealed partial class ControlWindow : Window
{
    private const int WindowWidth = 520;
    private const int WindowHeight = 360;
    private OverlayViewModel? viewModel;

    public ControlWindow()
    {
        InitializeComponent();
        Activated += OnActivated;
        LogPathText.Text = $"Log: {AppLogger.LogPath}";
    }

    public event Action? ToggleOverlayRequested;

    public event Action? ExitRequested;

    public event Action<OverlayShowMode>? ShowModeChanged;

    public event Action? EnableCaptureRequested;

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
        CaptureStatusText.Text = dependencies.CaptureServiceConnected
            ? $"FPS capture connected: {dependencies.CaptureTargetProcessName}".TrimEnd()
            : "Capture service not connected.";
    }

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
}
