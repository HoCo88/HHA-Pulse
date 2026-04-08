using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Shared.Models;
using Vortice.DXGI;

namespace HHAPulse.Overlay.Collectors.Gpu;

public sealed class VramCollector : IMetricCollector
{
    private IDXGIFactory1? factory;
    private bool loggedFailure;

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

        var result = factory.EnumAdapters1(0, out var adapter);
        if (result.Failure || adapter is null)
        {
            LogOnce($"VRAM: EnumAdapters1 failed with HRESULT 0x{result.Code:X8}.");
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
