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

    [Fact]
    public void CompactFormatter_ShowsTotalPowerFromBatteryWhenValidated()
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.Battery | MetricFlags.SystemPower,
            Battery =
            {
                DischargeWatts = 18.6,
                IsCharging = false
            }
        };

        Assert.Equal("18.6W", MetricFormatterCompact.FormatValue(OverlayPresetCatalog.TotalPower, snapshot));
    }

    [Fact]
    public void CompactFormatter_DoesNotShowComponentSumWithoutBothValidatedSources()
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.SystemPower | MetricFlags.CpuPower,
            Cpu = { PowerWatts = 12.5 },
            Gpu = { PowerWatts = 18.0 }
        };

        Assert.Equal("--", MetricFormatterCompact.FormatValue(OverlayPresetCatalog.TotalPower, snapshot));
    }

    [Fact]
    public void CompactFormatter_DoesNotShowComponentSumAsTotalPower()
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.CpuPower | MetricFlags.GpuPower,
            Cpu = { PowerWatts = 12.5 },
            Gpu = { PowerWatts = 18.0 }
        };

        HHAPulse.Overlay.Diagnostics.SystemPowerValidator.ApplyTo(snapshot);

        Assert.Equal("--", MetricFormatterCompact.FormatValue(OverlayPresetCatalog.TotalPower, snapshot));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void CompactFormatter_HidesRefreshSentinels(double refreshRate)
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.Display,
            Display = { RefreshRateHertz = refreshRate }
        };

        Assert.Equal("--", MetricFormatterCompact.FormatValue(OverlayPresetCatalog.RefreshRate, snapshot));
    }

    [Fact]
    public void CompactFormatter_ShowsBatteryChargeRateWhenCharging()
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.Battery,
            Battery =
            {
                ChargePercent = 72,
                ChargeWatts = 25.4,
                IsCharging = true
            }
        };

        Assert.Equal("72% +25.4W", MetricFormatterCompact.FormatValue(OverlayPresetCatalog.Battery, snapshot));
    }

    [Fact]
    public void CompactFormatter_KeepsFrameGenUnavailableWithoutMetricFlag()
    {
        var snapshot = new TelemetrySnapshot
        {
            Performance =
            {
                FramesPerSecond = 118,
                HybridPresentDetected = true
            }
        };

        Assert.Equal("--fps", MetricFormatterCompact.FormatValue(OverlayPresetCatalog.FrameGenFps, snapshot));
    }
}
