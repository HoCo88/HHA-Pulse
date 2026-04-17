using System.Reflection;
using HHAPulse.CaptureService.Etw;
using HHAPulse.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HHAPulse.CaptureService.Tests.Etw;

public sealed class EtwFrameCaptureTests
{
    [Fact]
    public void SetTarget_ClearsFrameGenerationEvidenceWhenPidChanges()
    {
        var capture = new EtwFrameCapture(new CaptureCoordinator(), NullLogger<EtwFrameCapture>.Instance);
        var field = typeof(EtwFrameCapture).GetField("frameGenDetectedThisInterval", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        field!.SetValue(capture, true);

        capture.SetTarget(new CaptureTarget
        {
            ProcessId = 77,
            ProcessName = "Game.exe"
        });

        Assert.False((bool)field.GetValue(capture)!);
    }

    [Fact]
    public void SetTarget_ClearsPublishWindowFrameTimesWhenPidChanges()
    {
        var capture = new EtwFrameCapture(new CaptureCoordinator(), NullLogger<EtwFrameCapture>.Instance);
        var field = typeof(EtwFrameCapture).GetField("publishWindowFrameTimes", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        var samples = Assert.IsType<List<double>>(field!.GetValue(capture));
        samples.Add(8.3);
        samples.Add(8.4);

        capture.SetTarget(new CaptureTarget
        {
            ProcessId = 88,
            ProcessName = "Game.exe"
        });

        Assert.Empty(samples);
    }

    [Fact]
    public void AverageFpsWindow_IsFiveSecondsForHudResponsiveness()
    {
        var field = typeof(EtwFrameCapture).GetField("AverageFpsWindowSeconds", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Equal(5, field!.GetValue(null));
    }

    [Fact]
    public void ComputeAppAndPresentFps_NoFrameGenEvents_ReturnsZero()
    {
        var (app, present) = EtwFrameCapture.ComputeAppAndPresentFps(0, 0, 1.0);

        Assert.Equal(0, app);
        Assert.Equal(0, present);
    }

    [Fact]
    public void ComputeAppAndPresentFps_OnlyOriginalFrames_AppEqualsPresent()
    {
        var (app, present) = EtwFrameCapture.ComputeAppAndPresentFps(60, 0, 1.0);

        Assert.Equal(60, app);
        Assert.Equal(60, present);
    }

    [Fact]
    public void ComputeAppAndPresentFps_MixedFrames_SplitCorrect()
    {
        var (app, present) = EtwFrameCapture.ComputeAppAndPresentFps(60, 60, 1.0);

        Assert.Equal(60, app);
        Assert.Equal(120, present);
    }

    [Fact]
    public void ComputeAppAndPresentFps_ZeroInterval_ReturnsZero()
    {
        var (app, present) = EtwFrameCapture.ComputeAppAndPresentFps(60, 60, 0);

        Assert.Equal(0, app);
        Assert.Equal(0, present);
    }
}
