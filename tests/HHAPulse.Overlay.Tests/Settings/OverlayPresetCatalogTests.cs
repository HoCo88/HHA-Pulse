using HHAPulse.Overlay.Settings;
using Xunit;

namespace HHAPulse.Overlay.Tests.Settings;

public sealed class OverlayPresetCatalogTests
{
    [Fact]
    public void GetMetricIds_MinimalReturnsFpsAndBattery()
    {
        var metrics = OverlayPresetCatalog.GetMetricIds(OverlayPreset.Minimal, Array.Empty<string>());

        Assert.Equal(new[]
        {
            OverlayPresetCatalog.Fps,
            OverlayPresetCatalog.Battery
        }, metrics);
    }

    [Fact]
    public void GetMetricIds_StandardReturnsSixMetrics()
    {
        var metrics = OverlayPresetCatalog.GetMetricIds(OverlayPreset.Standard, Array.Empty<string>());

        Assert.Equal(new[]
        {
            OverlayPresetCatalog.Fps,
            OverlayPresetCatalog.OnePercentLow,
            OverlayPresetCatalog.FrameTime,
            OverlayPresetCatalog.CpuUsage,
            OverlayPresetCatalog.GpuUsage,
            OverlayPresetCatalog.Battery
        }, metrics);
    }

    [Fact]
    public void GetMetricIds_TunerReturnsMetricsWithRealDataSources()
    {
        var metrics = OverlayPresetCatalog.GetMetricIds(OverlayPreset.Tuner, Array.Empty<string>());

        Assert.Equal(14, metrics.Count);
        Assert.Contains(OverlayPresetCatalog.Fps, metrics);
        Assert.Contains(OverlayPresetCatalog.OnePercentLow, metrics);
        Assert.Contains(OverlayPresetCatalog.FrameTime, metrics);
        Assert.Contains(OverlayPresetCatalog.CpuTemp, metrics);
        Assert.Contains(OverlayPresetCatalog.GpuTemp, metrics);
        Assert.Contains(OverlayPresetCatalog.GpuClock, metrics);
        Assert.Contains(OverlayPresetCatalog.GpuFan, metrics);
        Assert.Contains(OverlayPresetCatalog.DeviceTemp, metrics);
        Assert.Contains(OverlayPresetCatalog.Battery, metrics);
        // Metrics without collectors are excluded from Tuner.
        Assert.DoesNotContain(OverlayPresetCatalog.InputLatency, metrics);
        Assert.DoesNotContain(OverlayPresetCatalog.CpuPower, metrics);
        Assert.DoesNotContain(OverlayPresetCatalog.GpuPower, metrics);
    }

    [Fact]
    public void GetMetricIds_OffReturnsNoMetrics()
    {
        Assert.Empty(OverlayPresetCatalog.GetMetricIds(OverlayPreset.Off, Array.Empty<string>()));
    }

    [Fact]
    public void GetMetricIds_CustomUsesProvidedMetrics()
    {
        var custom = new List<string> { OverlayPresetCatalog.Fps, OverlayPresetCatalog.Battery };
        var metrics = OverlayPresetCatalog.GetMetricIds(OverlayPreset.Custom, custom);

        Assert.Equal(custom, metrics);
    }

    [Fact]
    public void GetMetricIds_CustomFallsBackToTunerWhenEmpty()
    {
        var metrics = OverlayPresetCatalog.GetMetricIds(OverlayPreset.Custom, Array.Empty<string>());

        Assert.Equal(14, metrics.Count);
    }

    [Fact]
    public void NextPreset_CyclesMinimalStandardTunerOff()
    {
        Assert.Equal(OverlayPreset.Standard, OverlayPresetCatalog.NextPreset(OverlayPreset.Minimal));
        Assert.Equal(OverlayPreset.Tuner, OverlayPresetCatalog.NextPreset(OverlayPreset.Standard));
        Assert.Equal(OverlayPreset.Off, OverlayPresetCatalog.NextPreset(OverlayPreset.Tuner));
        Assert.Equal(OverlayPreset.Minimal, OverlayPresetCatalog.NextPreset(OverlayPreset.Off));
    }
}
