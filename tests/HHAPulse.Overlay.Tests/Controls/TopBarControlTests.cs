using HHAPulse.Overlay.Controls;
using HHAPulse.Overlay.Settings;
using Xunit;

namespace HHAPulse.Overlay.Tests.Controls;

public sealed class TopBarControlTests
{
    [Fact]
    public void ValueMinWidth_ReservesStableSpaceForWideMetrics()
    {
        Assert.True(TopBarControl.ValueMinWidth(OverlayPresetCatalog.Fps) >= 72);
        Assert.True(TopBarControl.ValueMinWidth(OverlayPresetCatalog.Battery) >= 144);
        Assert.True(TopBarControl.ValueMinWidth(OverlayPresetCatalog.GpuFan) >= 96);
    }
}
