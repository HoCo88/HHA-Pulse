using HHAPulse.Overlay.Helpers;
using HHAPulse.Overlay.Settings;
using HHAPulse.Shared.Models;
using Xunit;

namespace HHAPulse.Overlay.Tests.Helpers;

public sealed class MetricFormatterTests
{
    [Fact]
    public void FormatMetric_ShowsDashOnlyWhenMetricFlagIsMissing()
    {
        var snapshot = new TelemetrySnapshot();

        Assert.Equal("FPS --", MetricFormatter.FormatMetric(OverlayPresetCatalog.Fps, snapshot));
        Assert.Equal("1% --", MetricFormatter.FormatMetric(OverlayPresetCatalog.OnePercentLow, snapshot));
        Assert.Equal("FT --", MetricFormatter.FormatMetric(OverlayPresetCatalog.FrameTime, snapshot));
    }

    [Fact]
    public void FormatMetric_ShowsZeroGpuUsageWhenMetricIsAvailable()
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.GpuUsage
        };

        Assert.Equal("GPU 0%", MetricFormatter.FormatMetric(OverlayPresetCatalog.GpuUsage, snapshot));
    }

    [Fact]
    public void CompactFormatter_ShowsGpuPowerOnlyAsWatts()
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.GpuPower,
            Gpu = { PowerWatts = 18.4 }
        };

        Assert.Equal("18.4W", MetricFormatterCompact.FormatValue(OverlayPresetCatalog.GpuPower, snapshot));
    }

    [Fact]
    public void CompactFormatter_ShowsGpuFanOnlyWhenAvailable()
    {
        var unavailable = new TelemetrySnapshot
        {
            Gpu = { FanRpm = 2400 }
        };
        var available = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.Fan,
            Gpu = { FanRpm = 2400 }
        };

        Assert.Equal("--", MetricFormatterCompact.FormatValue(OverlayPresetCatalog.GpuFan, unavailable));
        Assert.Equal("2400rpm", MetricFormatterCompact.FormatValue(OverlayPresetCatalog.GpuFan, available));
    }
}
