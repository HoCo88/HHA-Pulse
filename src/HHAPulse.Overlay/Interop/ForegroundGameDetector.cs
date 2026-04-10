using System.Diagnostics;
using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Interop;

internal static class ForegroundGameDetector
{
    public static bool IsGameInForeground()
    {
        return TryGetForegroundWindowInfo() is { } foreground
            && ForegroundCaptureTargetRouter.IsForegroundGameCandidate(foreground);
    }

    public static CaptureTarget? TryGetForegroundGameTarget()
    {
        var foreground = TryGetForegroundWindowInfo();
        return foreground is not null && ForegroundCaptureTargetRouter.IsForegroundGameCandidate(foreground)
            ? foreground.ToCaptureTarget(DateTimeOffset.UtcNow)
            : null;
    }

    public static ForegroundWindowInfo? TryGetForegroundWindowInfo()
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

        try
        {
            using var process = Process.GetProcessById((int)pid);
            return new ForegroundWindowInfo(pid, process.ProcessName, width, height);
        }
        catch
        {
            return null;
        }
    }
}

internal sealed record ForegroundWindowInfo(uint ProcessId, string ProcessName, int Width, int Height)
{
    public CaptureTarget ToCaptureTarget(DateTimeOffset now)
    {
        return new CaptureTarget
        {
            ProcessId = ProcessId,
            ProcessName = ProcessName,
            TimestampUnixMilliseconds = now.ToUnixTimeMilliseconds()
        };
    }
}
