using HHAPulse.Overlay.Collectors.Fps;
using HHAPulse.Overlay.Interop;
using HHAPulse.Shared.Models;
using System.Reflection;
using Xunit;

namespace HHAPulse.Overlay.Tests.Interop;

public sealed class ForegroundCaptureTargetRouterTests
{
    [Theory]
    [InlineData("HHAPulse.Overlay")]
    [InlineData("steamwebhelper")]
    [InlineData("GameBar")]
    public void Resolve_HoldsLastGameForOverlayProcesses(string overlayProcess)
    {
        var router = new ForegroundCaptureTargetRouter();
        var now = DateTimeOffset.Parse("2026-04-10T14:00:00Z");

        var game = router.Resolve(new ForegroundWindowInfo(1234, "Helldivers2", 3440, 1440), now);
        var held = router.Resolve(new ForegroundWindowInfo(2222, overlayProcess, 3440, 1440), now.AddSeconds(3));

        Assert.Equal("foreground", game.Source);
        Assert.Equal("held", held.Source);
        Assert.NotNull(held.Target);
        Assert.Equal((uint)1234, held.Target!.ProcessId);
    }

    [Fact]
    public void Resolve_ClearsWhenDifferentNormalAppTakesForeground()
    {
        var router = new ForegroundCaptureTargetRouter();
        var now = DateTimeOffset.Parse("2026-04-10T14:00:00Z");

        router.Resolve(new ForegroundWindowInfo(1234, "Helldivers2", 3440, 1440), now);
        var cleared = router.Resolve(new ForegroundWindowInfo(3333, "Code - Insiders", 3440, 1440), now.AddSeconds(2));

        Assert.Equal("cleared", cleared.Source);
        Assert.Null(cleared.Target);
    }

    [Fact]
    public void Resolve_ClearsHeldTargetAfterGraceWindow()
    {
        var router = new ForegroundCaptureTargetRouter();
        var now = DateTimeOffset.Parse("2026-04-10T14:00:00Z");

        router.Resolve(new ForegroundWindowInfo(1234, "Helldivers2", 3440, 1440), now);
        var cleared = router.Resolve(new ForegroundWindowInfo(2222, "steamwebhelper", 3440, 1440), now.AddSeconds(11));

        Assert.Equal("cleared", cleared.Source);
        Assert.Null(cleared.Target);
    }

    [Fact]
    public async Task CaptureServiceCollector_DoesNotExposeFpsWithoutHeldOrFreshMetrics()
    {
        var collector = new CaptureServiceCollector();
        var snapshot = new TelemetrySnapshot();

        collector.SetTarget(null, "targetSource=cleared; no capture target held");
        await collector.CollectAsync(snapshot, CancellationToken.None);

        Assert.False(snapshot.AvailableMetrics.HasFlag(MetricFlags.Fps));
        Assert.Contains("targetSource=cleared", snapshot.Dependencies.CaptureServiceStatusMessage);
    }

    [Fact]
    public async Task CaptureServiceCollector_KeepsHeldTargetMetricsInsideGraceWindow()
    {
        var collector = new CaptureServiceCollector();
        var metricsField = typeof(CaptureServiceCollector).GetField("latestFrameMetrics", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(metricsField);
        metricsField!.SetValue(collector, new CaptureFrameMetrics
        {
            HasFrameMetrics = true,
            TimestampUnixMilliseconds = DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeMilliseconds(),
            FramesPerSecond = 57,
            AverageFramesPerSecond = 55,
            FrameTimeMilliseconds = 17.5,
            GameProcessId = 41464,
            GameProcessName = "KingdomCome"
        });

        collector.SetTarget(new CaptureTarget { ProcessId = 41464, ProcessName = "KingdomCome" }, "targetSource=held; target=KingdomCome(41464); heldAgeMs=5000");

        var snapshot = new TelemetrySnapshot();
        await collector.CollectAsync(snapshot, CancellationToken.None);

        Assert.True(snapshot.AvailableMetrics.HasFlag(MetricFlags.Fps));
        Assert.Contains("captureFreshness=held", snapshot.Dependencies.CaptureServiceStatusMessage);
    }

    [Fact]
    public async Task CaptureServiceCollector_AppliesServiceSensorsWithoutFrameMetrics()
    {
        var collector = new CaptureServiceCollector();
        var metricsField = typeof(CaptureServiceCollector).GetField("latestServiceTelemetry", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(metricsField);
        metricsField!.SetValue(collector, new CaptureFrameMetrics
        {
            HasFrameMetrics = false,
            ServiceTelemetryTimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            CpuTemperatureCelsius = 51,
            CpuTemperatureSource = "test cpu",
            CpuTemperatureStatusMessage = "cpu ok",
            FanRpms = new[] { 2424, 2462 },
            DeviceFanSource = "test fan",
            DeviceFanStatusMessage = "fan ok",
            DeviceTemperatureCelsius = 64,
            DeviceTemperatureSource = "test device temp",
            DeviceTemperatureStatusMessage = "device temp ok",
            StorageWearPercentUsed = 0,
            StoragePowerOnHours = 12,
            StorageDeviceModel = "Test NVMe",
            StorageReliabilityStatusMessage = "storage ok",
            StorageReliabilityAvailable = true
        });

        collector.SetTarget(null, "targetSource=cleared; no capture target held");

        var snapshot = new TelemetrySnapshot();
        await collector.CollectAsync(snapshot, CancellationToken.None);

        Assert.False(snapshot.AvailableMetrics.HasFlag(MetricFlags.Fps));
        Assert.True(snapshot.AvailableMetrics.HasFlag(MetricFlags.CpuTemperature));
        Assert.True(snapshot.AvailableMetrics.HasFlag(MetricFlags.Fan));
        Assert.True(snapshot.AvailableMetrics.HasFlag(MetricFlags.DeviceTemperature));
        Assert.True(snapshot.AvailableMetrics.HasFlag(MetricFlags.StorageWear));
        Assert.False(snapshot.AvailableMetrics.HasFlag(MetricFlags.GpuTemperature));
        Assert.Equal(64, snapshot.Dependencies.DeviceTemperatureCelsius);
        Assert.Equal(new[] { 2424, 2462 }, snapshot.Dependencies.FanRpms);
        Assert.Equal((byte)0, snapshot.Storage.WearPercentUsed);
        Assert.Equal((uint)12, snapshot.Storage.PowerOnHours);
        Assert.Equal("Test NVMe", snapshot.Storage.DeviceModel);
    }
}
