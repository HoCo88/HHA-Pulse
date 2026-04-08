using System.Runtime.InteropServices;

namespace HHAPulse.Overlay.Interop;

internal static partial class NativeMethods
{
    // ── Window styles ──────────────────────────────────────────────────
    internal const int GWL_EXSTYLE = -20;
    internal const int WS_EX_LAYERED = 0x00080000;
    internal const int WS_EX_TRANSPARENT = 0x00000020;
    internal const int WS_EX_TOOLWINDOW = 0x00000080;
    internal const int WS_EX_NOACTIVATE = 0x08000000;

    // ── SetWindowPos flags ─────────────────────────────────────────────
    internal static readonly nint HWND_TOPMOST = new(-1);
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_SHOWWINDOW = 0x0040;
    internal const int SW_HIDE = 0;
    internal const int SW_SHOWNOACTIVATE = 4;

    // ── DWM constants ──────────────────────────────────────────────────
    internal const int DWMWA_EXCLUDED_FROM_PEEK = 12;

    // ── Structs ────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    internal struct MARGINS
    {
        public int LeftWidth;
        public int RightWidth;
        public int TopHeight;
        public int BottomHeight;

        public static MARGINS Extend() => new() { LeftWidth = -1, RightWidth = -1, TopHeight = -1, BottomHeight = -1 };
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DWM_BLURBEHIND
    {
        public uint DwFlags;
        public int FEnable;
        public nint HRgnBlur;
        public int FTransitionOnMaximized;

        internal const uint DWM_BB_ENABLE = 0x00000001;
        internal const uint DWM_BB_BLURREGION = 0x00000002;
    }

    // ── User32 ─────────────────────────────────────────────────────────

    [LibraryImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(nint hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(nint hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // ── Dwmapi ─────────────────────────────────────────────────────────

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmExtendFrameIntoClientArea(nint hWnd, in MARGINS pMarInset);

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmEnableBlurBehindWindow(nint hWnd, in DWM_BLURBEHIND pBlurBehind);

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmSetWindowAttribute(nint hWnd, int dwAttribute, in int pvAttribute, int cbAttribute);

    // ── GDI32 ──────────────────────────────────────────────────────────

    [LibraryImport("gdi32.dll")]
    internal static partial nint CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(nint hObject);
}
