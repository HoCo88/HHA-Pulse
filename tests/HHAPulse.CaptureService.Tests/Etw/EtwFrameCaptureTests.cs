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
}
