using HHAPulse.Overlay.Controls;
using HHAPulse.Overlay.Settings;
using Xunit;

namespace HHAPulse.Overlay.Tests.Controls;

public sealed class TopBarControlTests
{
    [Fact]
    public void ValueMinCharCount_ReservesStableSpaceForWideMetrics()
    {
        Assert.True(TopBarControl.ValueMinCharCount(OverlayPresetCatalog.Fps) >= 6);
        Assert.True(TopBarControl.ValueMinCharCount(OverlayPresetCatalog.Battery) >= 10);
        Assert.True(TopBarControl.ValueMinCharCount(OverlayPresetCatalog.GpuFan) >= 7);
    }

    [Theory]
    [InlineData(TopBarPosition.TopThin, 1, TopBarLayoutMode.Thin)]
    [InlineData(TopBarPosition.BottomThin, 1, TopBarLayoutMode.Thin)]
    [InlineData(TopBarPosition.TopTall, 2, TopBarLayoutMode.Tall)]
    [InlineData(TopBarPosition.BottomTall, 2, TopBarLayoutMode.Tall)]
    [InlineData(TopBarPosition.LeftDock, 1, TopBarLayoutMode.SideDock)]
    [InlineData(TopBarPosition.RightDock, 1, TopBarLayoutMode.SideDock)]
    public void ResolveLayoutMode_MatchesPlanBPositions(TopBarPosition position, int lineCount, TopBarLayoutMode expected)
    {
        Assert.Equal(expected, TopBarControl.ResolveLayoutMode(position, lineCount));
    }
}
