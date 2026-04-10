using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Overlay.Settings;
using HHAPulse.Shared.Models;
using Xunit;

namespace HHAPulse.Overlay.Tests.Diagnostics;

public sealed class MetricStatusFactoryTests
{
    [Fact]
    public void Create_UsesPerMetricGpuSources()
    {
        var snapshot = new TelemetrySnapshot
        {
            TimestampUnixMilliseconds = 123,
            AvailableMetrics = MetricFlags.GpuTemperature | MetricFlags.GpuClock | MetricFlags.GpuPower | MetricFlags.Fan
        };

        snapshot.Dependencies.GpuTelemetryStatusMessage = "aggregate";
        snapshot.Dependencies.GpuTemperatureSource = "D3DKMT fallback";
        snapshot.Dependencies.GpuTemperatureStatusMessage = "GPU temperature from D3DKMT fallback.";
        snapshot.Dependencies.GpuClockSource = "NvAPI";
        snapshot.Dependencies.GpuClockStatusMessage = "GPU clock from NVIDIA NvAPI.";
        snapshot.Dependencies.GpuPowerSource = "NVML";
        snapshot.Dependencies.GpuPowerStatusMessage = "GPU power from NVIDIA NVML.";
        snapshot.Dependencies.GpuFanSource = "NvAPI";
        snapshot.Dependencies.GpuFanStatusMessage = "GPU fan speed from NVIDIA NvAPI.";
        MeasurementTraceRecorder.Record(snapshot, OverlayPresetCatalog.GpuTemp, "GpuPerfDataCollector", "D3DKMT fallback", true, "72C", "test", "GPU temperature from D3DKMT fallback.", "D3DKMTQueryAdapterInfo", "720", "deci-C", "raw / 10", "72", "C", TelemetryValidationState.Verified, "test");
        MeasurementTraceRecorder.Record(snapshot, OverlayPresetCatalog.GpuClock, "NvApiGpuCollector", "NvAPI", true, "2000MHz", "test", "GPU clock from NVIDIA NvAPI.", "NvAPI", "2000000", "kHz", "kHz / 1000", "2000", "MHz", TelemetryValidationState.Verified, "test");
        MeasurementTraceRecorder.Record(snapshot, OverlayPresetCatalog.GpuPower, "NvApiGpuCollector", "NVML", true, "12W", "test", "GPU power from NVIDIA NVML.", "NVML", "12000", "mW", "mW / 1000", "12", "W", TelemetryValidationState.Verified, "test");
        MeasurementTraceRecorder.Record(snapshot, OverlayPresetCatalog.GpuFan, "NvApiGpuCollector", "NvAPI", true, "2400rpm", "test", "GPU fan speed from NVIDIA NvAPI.", "NvAPI", "2400", "rpm", "direct RPM", "2400", "rpm", TelemetryValidationState.Verified, "test");

        var statuses = MetricStatusFactory.Create(snapshot);

        Assert.Equal("D3DKMT fallback", statuses.Single(status => status.MetricId == OverlayPresetCatalog.GpuTemp).Source);
        Assert.Equal("NvAPI", statuses.Single(status => status.MetricId == OverlayPresetCatalog.GpuClock).Source);
        Assert.Equal("NVML", statuses.Single(status => status.MetricId == OverlayPresetCatalog.GpuPower).Source);
        Assert.Equal("NvAPI", statuses.Single(status => status.MetricId == OverlayPresetCatalog.GpuFan).Source);
    }

    [Fact]
    public void Create_ReportsUnknownCollectorWhenProvenanceIsMissing()
    {
        var snapshot = new TelemetrySnapshot
        {
            TimestampUnixMilliseconds = 456,
            Dependencies =
            {
                GpuTelemetryStatusMessage = "no vendor SDK"
            }
        };

        var statuses = MetricStatusFactory.Create(snapshot);

        var gpuPower = statuses.Single(status => status.MetricId == OverlayPresetCatalog.GpuPower);
        var gpuClock = statuses.Single(status => status.MetricId == OverlayPresetCatalog.GpuClock);

        Assert.False(gpuPower.IsAvailable);
        Assert.Equal("unknown collector", gpuClock.Source);
        Assert.Equal("No collector reported provenance for this metric.", gpuPower.StatusMessage);
    }

    [Fact]
    public void Create_KeepsMetricAvailableWhenFlagIsSetButProvenanceIsMissing()
    {
        var snapshot = new TelemetrySnapshot
        {
            TimestampUnixMilliseconds = 456,
            AvailableMetrics = MetricFlags.CpuUsage,
            Cpu = { UsagePercent = 31 }
        };

        var statuses = MetricStatusFactory.Create(snapshot);

        var cpu = statuses.Single(status => status.MetricId == OverlayPresetCatalog.CpuUsage);
        Assert.True(cpu.IsAvailable);
        Assert.Equal("unknown collector", cpu.Source);
        Assert.Equal(456, cpu.LastSuccessUnixMilliseconds);
        Assert.Equal("Metric is available, but no collector reported runtime provenance for it.", cpu.StatusMessage);
    }

    [Fact]
    public void Create_IncludesAllPresetMetricsIncludingFpsDerivatives()
    {
        var statuses = MetricStatusFactory.Create(new TelemetrySnapshot
        {
            TimestampUnixMilliseconds = 789
        });

        Assert.Contains(statuses, status => status.MetricId == OverlayPresetCatalog.AvgFps);
        Assert.Contains(statuses, status => status.MetricId == OverlayPresetCatalog.OnePercentLow);
        Assert.Contains(statuses, status => status.MetricId == OverlayPresetCatalog.ZeroPointOneLow);
        Assert.DoesNotContain(statuses, status => status.MetricId == OverlayPresetCatalog.InputLatency);
        Assert.Equal(19, statuses.Count);
    }
}
