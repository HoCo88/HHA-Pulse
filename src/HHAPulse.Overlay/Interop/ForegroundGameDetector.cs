using System.Diagnostics;
using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Interop;

internal static class ForegroundGameDetector
{
    private static readonly HashSet<string> ShellProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer",
        "ShellExperienceHost",
        "SearchHost",
        "StartMenuExperienceHost",
        "SystemSettings",
        "ApplicationFrameHost",
        "TextInputHost",
        "LockApp",
        "HHAPulse.Overlay"
    };

    public static bool IsGameInForeground()
    {
        return TryGetForegroundGameTarget() is not null;
    }

    public static CaptureTarget? TryGetForegroundGameTarget()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == 0)
            return null;

        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
            return null;

        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
            return null;

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width < 1024 || height < 576)
            return null;

        try
        {
            using var process = Process.GetProcessById((int)pid);
            var name = process.ProcessName;
            if (ShellProcesses.Contains(name))
                return null;

            return new CaptureTarget
            {
                ProcessId = pid,
                ProcessName = name,
                TimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }
        catch
        {
            return null;
        }
    }
}
