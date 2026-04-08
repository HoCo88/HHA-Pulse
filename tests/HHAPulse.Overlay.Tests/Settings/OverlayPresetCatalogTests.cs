using HHAPulse.Overlay.Settings;
using Xunit;

namespace HHAPulse.Overlay.Tests.Settings;

public sealed class OverlayPresetCatalogTests
{
    [Fact]
    public void GetMetricIds_HudReturnsSingleOperationalMetricSet()
    {
        var metrics = OverlayPresetCatalog.GetMetricIds(OverlayPreset.Hud, Array.Empty<string>());

        Assert.Equal(new[]
        {
            OverlayPresetCatalog.Fps,
            OverlayPresetCatalog.OnePercentLow,
            OverlayPresetCatalog.FrameTime,
            OverlayPresetCatalog.CpuUsage,
            OverlayPresetCatalog.GpuUsage,
            OverlayPresetCatalog.Ram,
            OverlayPresetCatalog.Vram,
            OverlayPresetCatalog.RefreshRate,
            OverlayPresetCatalog.Battery
        }, metrics);
    }

    [Fact]
    public void GetMetricIds_OffReturnsNoMetrics()
    {
        Assert.Empty(OverlayPresetCatalog.GetMetricIds(OverlayPreset.Off, Array.Empty<string>()));
    }

    [Fact]
    public void NextPreset_TogglesHudAndOff()
    {
        Assert.Equal(OverlayPreset.Off, OverlayPresetCatalog.NextPreset(OverlayPreset.Hud));
        Assert.Equal(OverlayPreset.Hud, OverlayPresetCatalog.NextPreset(OverlayPreset.Off));
    }
}
