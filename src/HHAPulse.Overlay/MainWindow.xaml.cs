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
    private const int SingleRowHeight = 28;
    private const int TallRowHeight = 46;
    private const int DoubleRowHeight = 52;

    private OverlayViewModel? _viewModel;
    private bool isOverlayVisible = true;

    public MainWindow()
    {
        InitializeComponent();
        TopBar.LayoutMetricsChanged += OnTopBarLayoutMetricsChanged;
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
        if (isOverlayVisible == visible)
        {
            return;
        }

        var hwnd = WindowNative.GetWindowHandle(this);
        NativeMethods.ShowWindow(hwnd, visible ? NativeMethods.SW_SHOWNOACTIVATE : NativeMethods.SW_HIDE);
        isOverlayVisible = visible;

        if (visible)
        {
            NativeMethods.SetWindowPos(
                hwnd,
                NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
            ResizeOverlayWindow();
        }
    }

    public void ApplyOpacity(double bgOpacity, double textOpacity)
    {
        TopBar.ApplyOpacity(bgOpacity, textOpacity);
    }

    public void ApplyTextSize(double size)
    {
        TopBar.ApplyTextSize(size);
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

    private void OnTopBarLayoutMetricsChanged(object? sender, EventArgs args)
    {
        DispatcherQueue.TryEnqueue(ApplyLayoutState);
    }

    private void ApplyLayoutState()
    {
        ResizeOverlayWindow();
    }

    private void ResizeOverlayWindow()
    {
        if (!isOverlayVisible)
        {
            return;
        }

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);

        int width = Math.Min(TopBar.EstimatedWidth, displayArea.WorkArea.Width);
        int rows = TopBar.RowCount;
        int height;
        if (rows > 1)
            height = DoubleRowHeight;
        else if (TopBar.FpsGroupIsTwoRow)
            height = TallRowHeight;
        else
            height = SingleRowHeight;

        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            displayArea.WorkArea.X, displayArea.WorkArea.Y, width, height,
            NativeMethods.SWP_NOACTIVATE);
    }
}
