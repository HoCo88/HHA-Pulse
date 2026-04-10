using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Interop;

internal sealed class ForegroundCaptureTargetRouter
{
    internal static readonly TimeSpan DefaultHoldWindow = TimeSpan.FromSeconds(10);

    private static readonly HashSet<string> HoldProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "dwm",
        "explorer",
        "ShellExperienceHost",
        "SearchHost",
        "StartMenuExperienceHost",
        "SystemSettings",
        "ApplicationFrameHost",
        "TextInputHost",
        "LockApp",
        "HHAPulse.Overlay",
        "steam",
        "steamwebhelper",
        "GameBar",
        "GameBarFTServer",
        "Gamebar_Widget",
        "XboxGameBarWidgets"
    };

    private static readonly HashSet<string> ClearProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Code",
        "Code - Insiders",
        "devenv",
        "notepad",
        "powershell",
        "pwsh",
        "cmd",
        "conhost",
        "WindowsTerminal",
        "chrome",
        "msedge",
        "firefox",
        "IntelGraphicsSoftware",
        "MSI Center M",
        "MSI_Center_M_Server_GameLibrary"
    };

    private readonly TimeSpan holdWindow;
    private CaptureTarget? lastTarget;
    private DateTimeOffset lastTargetSeenAt;

    public ForegroundCaptureTargetRouter()
        : this(DefaultHoldWindow)
    {
    }

    public ForegroundCaptureTargetRouter(TimeSpan holdWindow)
    {
        this.holdWindow = holdWindow;
    }

    public CaptureTargetRoute Resolve(ForegroundWindowInfo? foreground, DateTimeOffset now)
    {
        if (foreground is null)
        {
            return HoldOrClear(now, "no foreground window");
        }

        if (!HasUsableSize(foreground))
        {
            return HoldOrClear(now, $"foreground {foreground.ProcessName}({foreground.ProcessId}) is too small ({foreground.Width}x{foreground.Height})");
        }

        if (IsHoldProcess(foreground.ProcessName))
        {
            return HoldOrClear(now, $"foreground {foreground.ProcessName}({foreground.ProcessId}) is an overlay/shell process");
        }

        if (IsClearProcess(foreground.ProcessName))
        {
            lastTarget = null;
            return CaptureTargetRoute.Cleared($"foreground {foreground.ProcessName}({foreground.ProcessId}) is a non-game app; target cleared");
        }

        lastTarget = foreground.ToCaptureTarget(now);
        lastTargetSeenAt = now;
        return CaptureTargetRoute.Foreground(lastTarget, $"foreground {foreground.ProcessName}({foreground.ProcessId}) accepted as capture target");
    }

    public static bool IsForegroundGameCandidate(ForegroundWindowInfo foreground)
    {
        return HasUsableSize(foreground)
            && !IsHoldProcess(foreground.ProcessName)
            && !IsClearProcess(foreground.ProcessName);
    }

    private CaptureTargetRoute HoldOrClear(DateTimeOffset now, string reason)
    {
        if (lastTarget is not null)
        {
            var age = now - lastTargetSeenAt;
            if (age <= holdWindow)
            {
                return CaptureTargetRoute.Held(lastTarget, age, $"{reason}; holding last target {lastTarget.ProcessName}({lastTarget.ProcessId})");
            }
        }

        lastTarget = null;
        return CaptureTargetRoute.Cleared($"{reason}; no capture target held");
    }

    private static bool HasUsableSize(ForegroundWindowInfo foreground)
    {
        return foreground.Width >= 1024 && foreground.Height >= 576;
    }

    private static bool IsHoldProcess(string processName)
    {
        return HoldProcesses.Contains(processName);
    }

    private static bool IsClearProcess(string processName)
    {
        return ClearProcesses.Contains(processName);
    }
}

internal sealed record CaptureTargetRoute(
    CaptureTarget? Target,
    string Source,
    TimeSpan HeldAge,
    string Reason)
{
    public static CaptureTargetRoute Foreground(CaptureTarget target, string reason)
    {
        return new CaptureTargetRoute(target, "foreground", TimeSpan.Zero, reason);
    }

    public static CaptureTargetRoute Held(CaptureTarget target, TimeSpan heldAge, string reason)
    {
        return new CaptureTargetRoute(target, "held", heldAge, reason);
    }

    public static CaptureTargetRoute Cleared(string reason)
    {
        return new CaptureTargetRoute(null, "cleared", TimeSpan.Zero, reason);
    }

    public string ToStatusText()
    {
        return Target is null
            ? $"targetSource={Source}; {Reason}"
            : $"targetSource={Source}; target={Target.ProcessName}({Target.ProcessId}); heldAgeMs={(long)Math.Max(0, HeldAge.TotalMilliseconds)}; {Reason}";
    }
}
