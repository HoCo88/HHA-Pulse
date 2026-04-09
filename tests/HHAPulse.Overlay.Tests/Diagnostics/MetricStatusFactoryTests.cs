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

        var statuses = MetricStatusFactory.Create(snapshot);

        Assert.Equal("D3DKMT fallback", statuses.Single(status => status.MetricId == OverlayPresetCatalog.GpuTemp).Source);
        Assert.Equal("NvAPI", statuses.Single(status => status.MetricId == OverlayPresetCatalog.GpuClock).Source);
        Assert.Equal("NVML", statuses.Single(status => status.MetricId == OverlayPresetCatalog.GpuPower).Source);
        Assert.Equal("NvAPI", statuses.Single(status => status.MetricId == OverlayPresetCatalog.GpuFan).Source);
    }

    [Fact]
    public void Create_UsesVendorFallbackMessagesForUnavailableMetrics()
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
        Assert.Equal("Vendor GPU telemetry", gpuClock.Source);
        Assert.Equal("no vendor SDK", gpuPower.StatusMessage);
    }
}
