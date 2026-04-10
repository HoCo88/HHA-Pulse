using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Overlay.Interop;
using HHAPulse.Shared.Models;
using Vortice.DXGI;

namespace HHAPulse.Overlay.Collectors.Gpu;

public sealed class VramCollector : IMetricCollector
{
    private IDXGIFactory1? factory;
    private bool loggedFailure;
    private bool loggedAdapter;

    public string Name => "VRAM";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        var result = DXGI.CreateDXGIFactory1(out factory);
        if (result.Failure)
        {
            LogOnce($"VRAM: CreateDXGIFactory1 failed with HRESULT 0x{result.Code:X8}.");
            factory = null;
        }

        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        if (factory is null)
            return Task.CompletedTask;

        if (!DxgiPrimaryAdapterSelector.TryGetPrimaryAdapter(out var adapter, out _) || adapter is null)
        {
            LogOnce("VRAM: Could not resolve the primary display-attached adapter.");
            return Task.CompletedTask;
        }

        using (adapter)
        {
            IDXGIAdapter3? adapter3 = null;
            try
            {
                adapter3 = adapter.QueryInterfaceOrNull<IDXGIAdapter3>();
                if (adapter3 is null)
                {
                    LogOnce("VRAM: IDXGIAdapter3 is not available for the primary adapter.");
                    return Task.CompletedTask;
                }

                var desc = adapter.Description1;
                if (!loggedAdapter)
                {
                    loggedAdapter = true;
                    AppLogger.Info($"VRAM: Selected adapter '{desc.Description}' VendorId=0x{desc.VendorId:X4}, DedicatedVRAM={desc.DedicatedVideoMemory / (1024 * 1024)}MB.");
                }

                // Intel iGPUs (vendor 0x8086) have no dedicated VRAM — skip.
                // AMD APUs report usable VRAM through the local segment.
                bool isIntelIgpu = desc.VendorId == 0x8086 && desc.DedicatedVideoMemory <= 256UL * 1024UL * 1024UL;
                if (isIntelIgpu)
                {
                    LogOnce($"VRAM: Intel iGPU detected (VendorId=0x{desc.VendorId:X4}, DedicatedVRAM={desc.DedicatedVideoMemory / (1024 * 1024)}MB). VRAM metric hidden.");
                    return Task.CompletedTask;
                }

                var info = adapter3.QueryVideoMemoryInfo(0, MemorySegmentGroup.Local);
                var totalBytes = info.Budget;
                if (desc.DedicatedVideoMemory > 256UL * 1024UL * 1024UL)
                {
                    totalBytes = desc.DedicatedVideoMemory;
                }

                snapshot.AvailableMetrics |= MetricFlags.Vram;
                snapshot.Gpu.VramTotalMegabytes = totalBytes / (1024.0 * 1024.0);
                snapshot.Gpu.VramUsedMegabytes = info.CurrentUsage / (1024.0 * 1024.0);
                MeasurementTraceRecorder.Record(
                    snapshot,
                    "vram",
                    nameof(VramCollector),
                    "DXGI QueryVideoMemoryInfo",
                    true,
                    $"{snapshot.Gpu.VramUsedMegabytes / 1024:0.0}/{snapshot.Gpu.VramTotalMegabytes / 1024:0.0}G",
                    $"currentUsageBytes={info.CurrentUsage}; budgetBytes={info.Budget}; dedicatedBytes={desc.DedicatedVideoMemory}; selectedTotalBytes={totalBytes}",
                    desc.DedicatedVideoMemory > 256UL * 1024UL * 1024UL
                        ? "VRAM total from DXGI adapter dedicated memory; usage from local memory current usage."
                        : "VRAM total from DXGI local memory budget; usage from local memory current usage.",
                    "IDXGIAdapter3.QueryVideoMemoryInfo + DXGI_ADAPTER_DESC1",
                    $"usage={info.CurrentUsage}; budget={info.Budget}; dedicated={desc.DedicatedVideoMemory}",
                    "bytes",
                    "bytes / 1024 / 1024",
                    $"usedMb={snapshot.Gpu.VramUsedMegabytes:0.000}; totalMb={snapshot.Gpu.VramTotalMegabytes:0.000}",
                    "MB",
                    TelemetryValidationState.Verified,
                    "DXGI returned local memory usage; total is dedicated memory when available, otherwise local budget.");
            }
            finally
            {
                adapter3?.Dispose();
            }
        }

        return Task.CompletedTask;
    }

    private void LogOnce(string message)
    {
        if (loggedFailure)
            return;

        loggedFailure = true;
        AppLogger.Info(message);
    }
}
