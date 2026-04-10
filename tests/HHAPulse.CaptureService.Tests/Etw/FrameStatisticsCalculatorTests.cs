using HHAPulse.CaptureService.Etw;
using Xunit;

namespace HHAPulse.CaptureService.Tests.Etw;

public sealed class FrameStatisticsCalculatorTests
{
    [Fact]
    public void CalculateFramesPerSecond_UsesOnlySuppliedWindow()
    {
        var currentWindow = new[] { 16.6667, 16.6667, 16.6667 };

        var fps = FrameStatisticsCalculator.CalculateFramesPerSecond(currentWindow);

        Assert.InRange(fps, 59.9, 60.1);
    }

    [Fact]
    public void PercentileLowFps_UsesLowHistoryWindow()
    {
        var lowHistory = new[] { 16.6667, 16.6667, 16.6667, 100.0 };

        var onePercentLow = FrameStatisticsCalculator.PercentileLowFps(lowHistory, 0.99);

        Assert.InRange(onePercentLow, 9.9, 10.1);
    }
}
