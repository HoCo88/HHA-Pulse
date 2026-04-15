using System.ComponentModel;
using HHAPulse.Overlay.Interop;
using HHAPulse.Overlay.Settings;
using HHAPulse.Overlay.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace HHAPulse.Overlay;

public sealed partial class MainWindow : Window
{
    private OverlayViewModel? _viewModel;
    private bool isOverlayVisible = true;
    private TopBarPosition overlayPosition = TopBarPosition.TopThin;

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

    public void ApplyPosition(TopBarPosition position)
    {
        overlayPosition = position;
        ResizeOverlayWindow();
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
            args.PropertyName == nameof(OverlayViewModel.ActivePreset) ||
            args.PropertyName == nameof(OverlayViewModel.Position) ||
            args.PropertyName == nameof(OverlayViewModel.LineCount))
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

        int dockWidth = ResolveSideDockWidth();
        bool isSideDock = overlayPosition is TopBarPosition.LeftDock or TopBarPosition.RightDock;
        int width = isSideDock
            ? Math.Min(dockWidth, displayArea.WorkArea.Width)
            : Math.Min(TopBar.EstimatedWidth, displayArea.WorkArea.Width);
        int height = isSideDock
            ? displayArea.WorkArea.Height
            : Math.Min(TopBar.EstimatedHeight, displayArea.WorkArea.Height);

        int x = displayArea.WorkArea.X;
        int y = displayArea.WorkArea.Y;
        switch (overlayPosition)
        {
            case TopBarPosition.BottomThin:
            case TopBarPosition.BottomTall:
                y = displayArea.WorkArea.Y + displayArea.WorkArea.Height - height;
                break;
            case TopBarPosition.RightDock:
                x = displayArea.WorkArea.X + displayArea.WorkArea.Width - width;
                break;
        }

        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            x,
            y,
            width,
            height,
            NativeMethods.SWP_NOACTIVATE);
    }

    private static int ResolveSideDockWidth()
    {
        try
        {
            if (Application.Current?.Resources.TryGetValue("HhapSideDockWidth", out var resource) == true)
            {
                return resource switch
                {
                    double value => (int)Math.Ceiling(value),
                    int value => value,
                    _ => 280
                };
            }
        }
        catch
        {
        }

        return 280;
    }
}
