using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Interop;

internal sealed class InGameVisibilityGate
{
    private static readonly TimeSpan LiveFrameWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HideGraceWindow = TimeSpan.FromSeconds(2);
    private DateTimeOffset lastLiveFrameAt;

    public bool ShouldBeVisible(TelemetrySnapshot snapshot, DateTimeOffset now)
    {
        if (HasLiveFrameMetrics(snapshot))
        {
            lastLiveFrameAt = now;
            return true;
        }

        return lastLiveFrameAt != default && now - lastLiveFrameAt <= HideGraceWindow;
    }

    public void Reset()
    {
        lastLiveFrameAt = default;
    }

    private static bool HasLiveFrameMetrics(TelemetrySnapshot snapshot)
    {
        return snapshot.AvailableMetrics.HasFlag(MetricFlags.Fps)
            && snapshot.Performance.FramesPerSecond > 0
            && snapshot.Dependencies.CapturePayloadAgeMilliseconds <= LiveFrameWindow.TotalMilliseconds;
    }
}
