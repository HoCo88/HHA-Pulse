using HHAPulse.Overlay.Collectors.Gpu;
using HHAPulse.Overlay.Helpers;
using HHAPulse.Overlay.Settings;
using HHAPulse.Shared.Models;
using Xunit;

namespace HHAPulse.Overlay.Tests.Collectors;

public sealed class VramCollectorTests
{
    [Fact]
    public void SelectDisplayMemory_IntelIgpuUsesSharedSystemMemory()
    {
        var selection = VramCollector.SelectDisplayMemory(
            dedicatedBytes: 128UL * 1024UL * 1024UL,
            sharedSystemBytes: 16UL * 1024UL * 1024UL * 1024UL,
            localBudgetBytes: 8UL * 1024UL * 1024UL * 1024UL);

        Assert.Equal("iGPU shared memory", selection.DisplayMode);
        Assert.True(selection.IsUmaIgpu);
        Assert.Equal(16UL * 1024UL * 1024UL * 1024UL, selection.TotalBytes);
    }

    [Fact]
    public void SelectDisplayMemory_DedicatedGpuUsesDedicatedMemory()
    {
        var selection = VramCollector.SelectDisplayMemory(
            dedicatedBytes: 8UL * 1024UL * 1024UL * 1024UL,
            sharedSystemBytes: 16UL * 1024UL * 1024UL * 1024UL,
            localBudgetBytes: 6UL * 1024UL * 1024UL * 1024UL);

        Assert.Equal("dedicated VRAM", selection.DisplayMode);
        Assert.False(selection.IsUmaIgpu);
        Assert.Equal(8UL * 1024UL * 1024UL * 1024UL, selection.TotalBytes);
    }

    [Fact]
    public void SelectUsedMemoryBytes_IgpuPrefersTargetProcessPdhTotalCommitted()
    {
        var display = VramCollector.SelectDisplayMemory(
            dedicatedBytes: 128UL * 1024UL * 1024UL,
            sharedSystemBytes: 16UL * 1024UL * 1024UL * 1024UL,
            localBudgetBytes: 8UL * 1024UL * 1024UL * 1024UL);

        var used = VramCollector.SelectUsedMemoryBytes(
            display,
            dxgiLocalUsageBytes: 64UL * 1024UL * 1024UL,
            adapterPdh: new VramCollector.PdhVramSnapshot(2_000_000_000, 0, null, null, 3_000_000_000),
            processPdh: new VramCollector.PdhVramSnapshot(4_500_000_000, 0, 4_600_000_000, 0, 4_700_000_000));

        Assert.Equal(4_700_000_000UL, used.UsedBytes);
        Assert.Equal("PDH GPU Process Memory Total Committed", used.Source);
    }

    [Fact]
    public void SelectUsedMemoryBytes_IgpuFallsBackToAdapterPdh()
    {
        var display = VramCollector.SelectDisplayMemory(
            dedicatedBytes: 128UL * 1024UL * 1024UL,
            sharedSystemBytes: 16UL * 1024UL * 1024UL * 1024UL,
            localBudgetBytes: 8UL * 1024UL * 1024UL * 1024UL);

        var used = VramCollector.SelectUsedMemoryBytes(
            display,
            dxgiLocalUsageBytes: 64UL * 1024UL * 1024UL,
            adapterPdh: new VramCollector.PdhVramSnapshot(2_000_000_000, 0, null, null, 3_000_000_000),
            processPdh: VramCollector.PdhVramSnapshot.Unavailable("capture target unavailable"));

        Assert.Equal(3_000_000_000UL, used.UsedBytes);
        Assert.Equal("PDH GPU Adapter Memory Total Committed", used.Source);
    }

    [Fact]
    public void CompactFormatter_ShowsIgpuSharedVramWhenFlagAvailable()
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.Vram,
            Gpu =
            {
                VramUsedMegabytes = 1536,
                VramTotalMegabytes = 16384
            }
        };

        Assert.Equal("1.5/16.0G", MetricFormatterCompact.FormatValue(OverlayPresetCatalog.Vram, snapshot));
    }
}
