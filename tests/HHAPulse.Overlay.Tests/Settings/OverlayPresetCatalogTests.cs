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

        Assert.Equal(11, metrics.Count);
        Assert.Contains(OverlayPresetCatalog.Fps, metrics);
        Assert.Contains(OverlayPresetCatalog.OnePercentLow, metrics);
        Assert.Contains(OverlayPresetCatalog.FrameTime, metrics);
        Assert.Contains(OverlayPresetCatalog.CpuTemp, metrics);
        Assert.Contains(OverlayPresetCatalog.CpuPower, metrics);
        Assert.Contains(OverlayPresetCatalog.GpuTemp, metrics);
        Assert.Contains(OverlayPresetCatalog.RefreshRate, metrics);
        Assert.Contains(OverlayPresetCatalog.Battery, metrics);
        Assert.DoesNotContain(OverlayPresetCatalog.GpuClock, metrics);
        Assert.DoesNotContain(OverlayPresetCatalog.GpuFan, metrics);
        Assert.DoesNotContain(OverlayPresetCatalog.DeviceTemp, metrics);
        Assert.DoesNotContain(OverlayPresetCatalog.Vram, metrics);
        Assert.DoesNotContain(OverlayPresetCatalog.InputLatency, metrics);
        Assert.DoesNotContain(OverlayPresetCatalog.GpuPower, metrics);
    }

    [Fact]
    public void GetMetricIds_FullReturnsNineteenMetrics()
    {
        var metrics = OverlayPresetCatalog.GetMetricIds(OverlayPreset.Full, Array.Empty<string>());

        Assert.Equal(19, metrics.Count);
        Assert.Contains(OverlayPresetCatalog.AvgFps, metrics);
        Assert.Contains(OverlayPresetCatalog.ZeroPointOneLow, metrics);
        Assert.Contains(OverlayPresetCatalog.CpuPower, metrics);
        Assert.Contains(OverlayPresetCatalog.GpuPower, metrics);
        Assert.Contains(OverlayPresetCatalog.GpuFan, metrics);
        Assert.Contains(OverlayPresetCatalog.Vram, metrics);
        Assert.Contains(OverlayPresetCatalog.TotalPower, metrics);
        Assert.Contains(OverlayPresetCatalog.DeviceTemp, metrics);
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

        Assert.Equal(11, metrics.Count);
    }

    [Fact]
    public void AllMetricIds_ContainsPlanAManualOnlyTelemetry()
    {
        Assert.Contains(OverlayPresetCatalog.StorageTemp, OverlayPresetCatalog.AllMetricIds);
        Assert.Contains(OverlayPresetCatalog.StorageWear, OverlayPresetCatalog.AllMetricIds);
        Assert.Contains(OverlayPresetCatalog.CpuClock, OverlayPresetCatalog.AllMetricIds);
    }

    [Fact]
    public void NextPreset_CyclesMinimalStandardAdvancedFullOff()
    {
        Assert.Equal(OverlayPreset.Standard, OverlayPresetCatalog.NextPreset(OverlayPreset.Minimal));
        Assert.Equal(OverlayPreset.Tuner, OverlayPresetCatalog.NextPreset(OverlayPreset.Standard));
        Assert.Equal(OverlayPreset.Full, OverlayPresetCatalog.NextPreset(OverlayPreset.Tuner));
        Assert.Equal(OverlayPreset.Off, OverlayPresetCatalog.NextPreset(OverlayPreset.Full));
        Assert.Equal(OverlayPreset.Minimal, OverlayPresetCatalog.NextPreset(OverlayPreset.Off));
    }

    [Fact]
    public void PresetDisplayName_UsesPlanBLabels()
    {
        Assert.Equal("Advanced", OverlayPresetCatalog.PresetDisplayName(OverlayPreset.Tuner));
        Assert.Equal("Manual", OverlayPresetCatalog.PresetDisplayName(OverlayPreset.Custom));
        Assert.Equal("Full", OverlayPresetCatalog.PresetDisplayName(OverlayPreset.Full));
    }
}
