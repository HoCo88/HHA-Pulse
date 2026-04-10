using HHAPulse.Overlay.Interop;
using HHAPulse.Shared.Models;
using Xunit;

namespace HHAPulse.Overlay.Tests.Interop;

public sealed class InGameVisibilityGateTests
{
    [Fact]
    public void StaysHiddenWithoutLiveFps()
    {
        var gate = new InGameVisibilityGate();
        var snapshot = new TelemetrySnapshot();

        Assert.False(gate.ShouldBeVisible(snapshot, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void StaysVisibleInsideGraceWindowAfterLiveFps()
    {
        var gate = new InGameVisibilityGate();
        var now = DateTimeOffset.Parse("2026-04-10T18:00:00Z");

        var live = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.Fps,
            Performance = { FramesPerSecond = 37 },
            Dependencies = { CapturePayloadAgeMilliseconds = 900 }
        };
        Assert.True(gate.ShouldBeVisible(live, now));

        var stale = new TelemetrySnapshot();
        Assert.True(gate.ShouldBeVisible(stale, now.AddSeconds(1)));
        Assert.False(gate.ShouldBeVisible(stale, now.AddSeconds(3)));
    }
}
