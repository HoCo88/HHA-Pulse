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
}
