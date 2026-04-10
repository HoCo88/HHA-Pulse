using System;
using HHAPulse.Overlay.Diagnostics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace HHAPulse.Overlay.Interop;

internal static class TransparentWindowHelper
{
    internal static void MakeOverlay(Window window)
    {
        // 0. Install a transparent SwapChain backdrop. Without this, WinUI 3
        //    clears the DirectX SwapChain to opaque black every frame, so any
        //    XAML content with alpha < 255 composites on top of that black
        //    surface and the HUD "transparent" background renders solid black.
        //    The DWM glass-frame work below (layer 1) only makes the window
        //    chrome transparent — it does NOT clear the SwapChain interior
        //    (layer 2). Both layers must be transparent for the final pixel to
        //    show through to the desktop. See
        //    memory-bank/best_practices/winui3_overlay.md "Option C".
        //
        //    Wrapped in try/catch because the SystemBackdrop setter raises the
        //    ICompositionSupportsSystemBackdrop.OnTargetConnected hook
        //    synchronously from inside the setter, and any exception from the
        //    backdrop's composition code path must NOT prevent the rest of
        //    MakeOverlay from running — otherwise the window would appear
        //    with its full system chrome (title bar, borders) and show up as
        //    a "squared" un-styled window, which is exactly the crash failure
        //    mode observed in HHAP-0.4.
        try
        {
            window.SystemBackdrop = new TransparentBackdrop();
        }
        catch (Exception ex)
        {
            AppLogger.Error("TransparentWindowHelper: Failed to assign TransparentBackdrop. Chrome stripping will continue but the HUD background at 0% opacity may render black. See exception for details.", ex);
        }

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

        // 4a. Strip all non-client chrome from the normal style so window rect == client rect.
        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE);
        style &= ~(NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME | NativeMethods.WS_SYSMENU);
        style |= NativeMethods.WS_POPUP;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE, style);

        // 4b. Set extended window styles: tool window, no-activate, layered + transparent (click-through).
        var exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_TOOLWINDOW
                 | NativeMethods.WS_EX_NOACTIVATE
                 | NativeMethods.WS_EX_LAYERED
                 | NativeMethods.WS_EX_TRANSPARENT;
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

        // 8. Square corners — remove Windows 11 rounded pill shape.
        var cornerPref = NativeMethods.DWMWCP_DONOTROUND;
        NativeMethods.DwmSetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
            in cornerPref,
            sizeof(int));
    }
}
