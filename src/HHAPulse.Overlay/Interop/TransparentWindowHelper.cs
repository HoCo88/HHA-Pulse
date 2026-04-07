using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace HHAPulse.Overlay.Interop;

internal static class TransparentWindowHelper
{
    internal static void MakeOverlay(Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

        // 1. Extend glass frame across the entire window for transparency.
        var margins = NativeMethods.MARGINS.Extend();
        NativeMethods.DwmExtendFrameIntoClientArea(hwnd, in margins);

        // 2. Enable blur behind with an empty region so the compositor
        //    renders the window as fully transparent.
        var emptyRegion = NativeMethods.CreateRectRgn(0, 0, 0, 0);
        try
        {
            var blur = new NativeMethods.DWM_BLURBEHIND
            {
                DwFlags = NativeMethods.DWM_BLURBEHIND.DWM_BB_ENABLE | NativeMethods.DWM_BLURBEHIND.DWM_BB_BLURREGION,
                FEnable = 1,
                HRgnBlur = emptyRegion
            };
            NativeMethods.DwmEnableBlurBehindWindow(hwnd, in blur);
        }
        finally
        {
            NativeMethods.DeleteObject(emptyRegion);
        }

        // 3. Pin the window to HWND_TOPMOST via SetWindowPos.
        //    We avoid AppWindow.IsAlwaysOnTop because it interferes with click-through.
        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

        // 4. Set extended window styles: tool window (hides from taskbar) + no-activate.
        var exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);

        // 5. Hide from Alt-Tab / Task View.
        var appWindow = AppWindow.GetFromWindowId(
            Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd));
        appWindow.IsShownInSwitchers = false;

        // 6. Remove title bar and borders.
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
        }

        // 7. Exclude from Peek.
        var excludeFromPeek = 1;
        NativeMethods.DwmSetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_EXCLUDED_FROM_PEEK,
            in excludeFromPeek,
            sizeof(int));
    }
}
