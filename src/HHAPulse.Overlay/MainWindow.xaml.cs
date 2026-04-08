using System.ComponentModel;
using HHAPulse.Overlay.Interop;
using HHAPulse.Overlay.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace HHAPulse.Overlay;

public sealed partial class MainWindow : Window
{
    private const int TopBarHeight = 28;

    private OverlayViewModel? _viewModel;
    private bool isOverlayVisible = true;

    public MainWindow()
    {
        InitializeComponent();
        Activated += OnActivated;
    }

    public void AttachViewModel(OverlayViewModel vm)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = vm;
        TopBar.ViewModel = vm;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyLayoutState();
    }

    public void ToggleOverlayVisibility()
    {
        SetOverlayVisible(!isOverlayVisible);
    }

    public void SetOverlayVisible(bool visible)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        NativeMethods.ShowWindow(hwnd, visible ? NativeMethods.SW_SHOWNOACTIVATE : NativeMethods.SW_HIDE);

        if (visible)
        {
            NativeMethods.SetWindowPos(
                hwnd,
                NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
        }

        isOverlayVisible = visible;
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        TransparentWindowHelper.MakeOverlay(this);
        ApplyLayoutState();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(OverlayViewModel.TopBarMetricIds) ||
            args.PropertyName == nameof(OverlayViewModel.ActivePreset))
        {
            DispatcherQueue.TryEnqueue(ApplyLayoutState);
        }
    }

    private void ApplyLayoutState()
    {
        ResizeOverlayWindow();
    }

    private void ResizeOverlayWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);

        int width = displayArea.WorkArea.Width;
        int height = TopBarHeight;

        var bounds = new RectInt32(displayArea.WorkArea.X, displayArea.WorkArea.Y, width, height);
        appWindow.MoveAndResize(bounds);
    }
}
