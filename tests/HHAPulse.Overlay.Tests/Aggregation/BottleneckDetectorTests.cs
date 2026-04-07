using HHAPulse.Overlay.Aggregation;
using HHAPulse.Shared.Models;
using Xunit;

namespace HHAPulse.Overlay.Tests.Aggregation;

public sealed class BottleneckDetectorTests
{
    [Fact]
    public void Detect_ReturnsGpuBoundWhenGpuBusyMatchesFrameTime()
    {
        var result = BottleneckDetector.Detect(frameTimeMilliseconds: 13.7, gpuBusyMilliseconds: 13.0, gpuBusyReliable: true);

        Assert.Equal(BottleneckKind.GpuBound, result.Kind);
        Assert.Equal(ConfidenceLevel.High, result.Confidence);
    }

    [Fact]
    public void Detect_ReturnsCpuBoundWhenGpuBusyIsMuchLowerThanFrameTime()
    {
        var result = BottleneckDetector.Detect(frameTimeMilliseconds: 20, gpuBusyMilliseconds: 8, gpuBusyReliable: true);

        Assert.Equal(BottleneckKind.CpuBound, result.Kind);
        Assert.Equal(ConfidenceLevel.High, result.Confidence);
    }

    [Fact]
    public void Detect_ReturnsUnknownWhenGpuBusyIsUnreliable()
    {
        var result = BottleneckDetector.Detect(frameTimeMilliseconds: 20, gpuBusyMilliseconds: 8, gpuBusyReliable: false);

        Assert.Equal(BottleneckKind.Unknown, result.Kind);
        Assert.Equal(ConfidenceLevel.Low, result.Confidence);
    }
}
