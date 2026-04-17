using HHAPulse.Overlay.Collectors.Fps;
using HHAPulse.Shared.Models;
using Xunit;

namespace HHAPulse.Overlay.Tests.Collectors.Fps;

public sealed class CaptureServiceCollectorTests
{
    [Fact]
    public void ApplyFrameGenVendor_NoneVendor_LeavesFlagClear()
    {
        var snapshot = new TelemetrySnapshot();

        CaptureServiceCollector.ApplyFrameGenVendor(snapshot, FrameGenVendor.None);

        Assert.False(snapshot.AvailableMetrics.HasFlag(MetricFlags.FrameGen));
        Assert.Equal(FrameGenVendor.None, snapshot.Performance.FrameGenVendor);
    }

    [Fact]
    public void ApplyFrameGenVendor_AmdAFMF_SetsFlagAndVendor()
    {
        var snapshot = new TelemetrySnapshot();

        CaptureServiceCollector.ApplyFrameGenVendor(snapshot, FrameGenVendor.AmdAFMF);

        Assert.True(snapshot.AvailableMetrics.HasFlag(MetricFlags.FrameGen));
        Assert.Equal(FrameGenVendor.AmdAFMF, snapshot.Performance.FrameGenVendor);
    }

    [Fact]
    public void ApplyFrameGenVendor_IntelXeFG_SetsFlagAndVendor()
    {
        var snapshot = new TelemetrySnapshot();

        CaptureServiceCollector.ApplyFrameGenVendor(snapshot, FrameGenVendor.IntelXeFG);

        Assert.True(snapshot.AvailableMetrics.HasFlag(MetricFlags.FrameGen));
        Assert.Equal(FrameGenVendor.IntelXeFG, snapshot.Performance.FrameGenVendor);
    }
}
