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
    public void CompactFormatter_ShowsMultipleDeviceFans()
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.Fan,
            Dependencies = { FanRpms = new[] { 2462, 2424 } }
        };

        Assert.Equal("2462/2424rpm", MetricFormatterCompact.FormatValue(OverlayPresetCatalog.GpuFan, snapshot));
    }

    [Fact]
    public void CompactFormatter_ShowsDeviceTemperatureOnlyWhenAvailable()
    {
        var unavailable = new TelemetrySnapshot
        {
            Dependencies = { DeviceTemperatureCelsius = 64 }
        };
        var available = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.DeviceTemperature,
            Dependencies = { DeviceTemperatureCelsius = 64 }
        };

        Assert.Equal("--", MetricFormatterCompact.FormatValue(OverlayPresetCatalog.DeviceTemp, unavailable));
        Assert.Equal("64\u00B0C", MetricFormatterCompact.FormatValue(OverlayPresetCatalog.DeviceTemp, available));
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

    [Fact]
    public void CompactFormatter_DoesNotShowRaplPackagePlusDramAsDevicePower()
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.Battery | MetricFlags.CpuPower | MetricFlags.SystemPower,
            Battery = { IsCharging = true },
            Cpu = { PowerWatts = 16.2 },
            Dependencies = { DramPowerWatts = 0.4 }
        };

        HHAPulse.Overlay.Diagnostics.SystemPowerValidator.ApplyTo(snapshot);

        Assert.False(snapshot.AvailableMetrics.HasFlag(MetricFlags.SystemPower));
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
    public void CompactFormatter_CpuTempShowsCelsiusWhenFlagAvailable()
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.CpuTemperature,
            Cpu = { TemperatureCelsius = 62.4 }
        };

        Assert.Equal("62\u00B0C", MetricFormatterCompact.FormatValue(OverlayPresetCatalog.CpuTemp, snapshot));
    }

    [Fact]
    public void CompactFormatter_CpuTempFallsBackWhenFlagMissing()
    {
        var snapshot = new TelemetrySnapshot
        {
            Cpu = { TemperatureCelsius = 62.4 }
        };

        Assert.Equal("--", MetricFormatterCompact.FormatValue(OverlayPresetCatalog.CpuTemp, snapshot));
    }

    [Fact]
    public void CompactFormatter_FpsCellShowsTotalOverAppWhenFrameGenActive()
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.Fps | MetricFlags.FrameGen,
            Performance =
            {
                FramesPerSecond = 120,
                AppFramesPerSecond = 60
            }
        };

        Assert.Equal("120/60fps", MetricFormatterCompact.FormatValue(OverlayPresetCatalog.Fps, snapshot));
    }

    [Fact]
    public void CompactFormatter_FpsCellCollapsesWhenFrameGenInactive()
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.Fps,
            Performance =
            {
                FramesPerSecond = 60,
                AppFramesPerSecond = 60
            }
        };

        Assert.Equal("60fps", MetricFormatterCompact.FormatValue(OverlayPresetCatalog.Fps, snapshot));
    }
}
