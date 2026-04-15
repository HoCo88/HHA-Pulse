using HHAPulse.Overlay.Settings;
using HHAPulse.Overlay.ViewModels;
using HHAPulse.Shared.Models;
using Xunit;

namespace HHAPulse.Overlay.Tests.ViewModels;

public sealed class OverlayViewModelTests
{
    [Fact]
    public void ApplySettings_DefaultUsesStandardPreset()
    {
        var viewModel = new OverlayViewModel();
        var settings = AppSettings.CreateDefault();

        viewModel.ApplySettings(settings);

        Assert.Equal(OverlayPreset.Standard, viewModel.ActivePreset);
        Assert.Contains(OverlayPresetCatalog.Fps, viewModel.TopBarMetricIds);
        Assert.Contains(OverlayPresetCatalog.Battery, viewModel.TopBarMetricIds);
    }

    [Theory]
    [InlineData(OverlayPreset.Minimal, 2)]
    [InlineData(OverlayPreset.Standard, 6)]
    [InlineData(OverlayPreset.Tuner, 11)]
    [InlineData(OverlayPreset.Full, 19)]
    public void ApplySettings_PresetControlsMetricCount(OverlayPreset preset, int expectedCount)
    {
        var viewModel = new OverlayViewModel();
        var settings = AppSettings.CreateDefault();
        settings.ActivePreset = preset;

        viewModel.ApplySettings(settings);

        Assert.Equal(preset, viewModel.ActivePreset);
        Assert.Equal(expectedCount, viewModel.TopBarMetricIds.Count);
    }

    [Theory]
    [InlineData(TopBarPosition.TopThin, 1)]
    [InlineData(TopBarPosition.BottomThin, 1)]
    [InlineData(TopBarPosition.TopTall, 2)]
    [InlineData(TopBarPosition.BottomTall, 2)]
    [InlineData(TopBarPosition.LeftDock, 1)]
    [InlineData(TopBarPosition.RightDock, 1)]
    public void ApplySettings_UpdatesPositionAndLineCount(TopBarPosition position, int expectedLineCount)
    {
        var viewModel = new OverlayViewModel();
        var settings = AppSettings.CreateDefault();
        settings.TopBarPosition = position;

        viewModel.ApplySettings(settings);

        Assert.Equal(position, viewModel.Position);
        Assert.Equal(expectedLineCount, viewModel.LineCount);
    }

    [Fact]
    public void ApplyTelemetry_AddsGraphSamplesOnlyWhenMetricsAreAvailable()
    {
        var viewModel = new OverlayViewModel();

        viewModel.ApplyTelemetry(new TelemetrySnapshot
        {
            Performance = { FramesPerSecond = 90, FrameTimeMilliseconds = 11.1 }
        });

        Assert.Empty(viewModel.FpsHistory);
        Assert.Empty(viewModel.FrameTimeHistory);

        viewModel.ApplyTelemetry(new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.Fps | MetricFlags.FrameTime,
            Performance = { FramesPerSecond = 90, FrameTimeMilliseconds = 11.1 }
        });

        Assert.Equal(new[] { 90d }, viewModel.FpsHistory);
        Assert.Equal(new[] { 11.1d }, viewModel.FrameTimeHistory);
    }
}
