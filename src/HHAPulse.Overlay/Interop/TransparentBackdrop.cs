namespace HHAPulse.Overlay.Interop;

// Sets the WinUI SwapChain clear color to fully transparent so XAML content
// with alpha < 255 composites through to the desktop. Without this, the
// SwapChain defaults to an opaque black clear color and any "transparent"
// background renders black regardless of the alpha value in TopBarControl.
// This is the documented WinUI 3 transparency pattern — see
// memory-bank/best_practices/winui3_overlay.md "Option C: Transparent
// SystemBackdrop".
// Reserved for a future WinUI backdrop implementation. The active transparency
// fix is the overlay alpha floor in TopBarControl, which avoids the black DWM
// frame without relying on an extra composition interop shim.
