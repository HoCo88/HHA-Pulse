using HHAPulse.Overlay.Diagnostics;
using System.Runtime.InteropServices;
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

                var localInfo = adapter3.QueryVideoMemoryInfo(0, MemorySegmentGroup.Local);
                var nonLocalInfo = adapter3.QueryVideoMemoryInfo(0, MemorySegmentGroup.NonLocal);
                var selection = SelectDisplayMemory(desc.DedicatedVideoMemory, desc.SharedSystemMemory, localInfo.Budget);
                var adapterPdh = TryReadAdapterPdh(desc);
                var processPdh = snapshot.Dependencies.CaptureTargetProcessId > 0
                    ? TryReadProcessPdh(desc, snapshot.Dependencies.CaptureTargetProcessId)
                    : PdhVramSnapshot.Unavailable("capture target unavailable");
                var usedSelection = SelectUsedMemoryBytes(selection, localInfo.CurrentUsage, adapterPdh, processPdh);

                snapshot.AvailableMetrics |= MetricFlags.Vram;
                snapshot.Gpu.VramTotalMegabytes = selection.TotalBytes / (1024.0 * 1024.0);
                snapshot.Gpu.VramUsedMegabytes = usedSelection.UsedBytes / (1024.0 * 1024.0);
                MeasurementTraceRecorder.Record(
                    snapshot,
                    "vram",
                    nameof(VramCollector),
                    usedSelection.Source,
                    true,
                    $"{snapshot.Gpu.VramUsedMegabytes / 1024:0.0}/{snapshot.Gpu.VramTotalMegabytes / 1024:0.0}G",
                    $"displayMode={selection.DisplayMode}; selectedUsedBytes={usedSelection.UsedBytes}; selectedUsedSource={usedSelection.Source}; localUsageBytes={localInfo.CurrentUsage}; localBudgetBytes={localInfo.Budget}; nonLocalUsageBytes={nonLocalInfo.CurrentUsage}; nonLocalBudgetBytes={nonLocalInfo.Budget}; dedicatedBytes={desc.DedicatedVideoMemory}; sharedSystemBytes={desc.SharedSystemMemory}; selectedTotalBytes={selection.TotalBytes}; pdhAdapter={adapterPdh}; pdhProcess={processPdh}",
                    selection.StatusMessage,
                    "PDH GPU memory counters + IDXGIAdapter3.QueryVideoMemoryInfo + DXGI_ADAPTER_DESC1",
                    $"selectedUsed={usedSelection.UsedBytes}; localUsage={localInfo.CurrentUsage}; localBudget={localInfo.Budget}; nonLocalUsage={nonLocalInfo.CurrentUsage}; nonLocalBudget={nonLocalInfo.Budget}; dedicated={desc.DedicatedVideoMemory}; sharedSystem={desc.SharedSystemMemory}",
                    "bytes",
                    "bytes / 1024 / 1024",
                    $"usedMb={snapshot.Gpu.VramUsedMegabytes:0.000}; totalMb={snapshot.Gpu.VramTotalMegabytes:0.000}",
                    "MB",
                    TelemetryValidationState.Verified,
                    selection.Reason);
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

    internal static VramDisplaySelection SelectDisplayMemory(ulong dedicatedBytes, ulong sharedSystemBytes, ulong localBudgetBytes)
    {
        if (dedicatedBytes > 256UL * 1024UL * 1024UL)
        {
            return new VramDisplaySelection(
                dedicatedBytes,
                "dedicated VRAM",
                false,
                "VRAM total from DXGI adapter dedicated memory; usage from local memory current usage.",
                "DXGI returned local memory usage and a dedicated VRAM total.");
        }

        ulong totalBytes = sharedSystemBytes > 0 ? sharedSystemBytes : localBudgetBytes;
        return new VramDisplaySelection(
            totalBytes,
            "iGPU shared memory",
            true,
            "iGPU memory from PDH GPU memory counters over DXGI shared system memory capacity; dedicated aperture is diagnostic-only.",
            "DXGI reported a UMA/iGPU adapter, so the HUD shows shared GPU memory capacity and PDH-reported GPU memory usage instead of hiding the tile or treating the small dedicated aperture as total VRAM.");
    }

    internal static VramUsedSelection SelectUsedMemoryBytes(
        VramDisplaySelection displaySelection,
        ulong dxgiLocalUsageBytes,
        PdhVramSnapshot adapterPdh,
        PdhVramSnapshot processPdh)
    {
        if (displaySelection.IsUmaIgpu)
        {
            if (TrySelectPdhUsage(processPdh, "PDH GPU Process Memory", out var processBytes, out var processSource))
            {
                return new VramUsedSelection(processBytes, processSource);
            }

            if (TrySelectPdhUsage(adapterPdh, "PDH GPU Adapter Memory", out var adapterBytes, out var adapterSource))
            {
                return new VramUsedSelection(adapterBytes, adapterSource);
            }
        }

        return new VramUsedSelection(dxgiLocalUsageBytes, "DXGI local memory current usage");
    }

    private static bool TrySelectPdhUsage(PdhVramSnapshot snapshot, string sourcePrefix, out ulong bytes, out string source)
    {
        if (TryPositiveBytes(snapshot.TotalCommitted, out bytes))
        {
            source = $"{sourcePrefix} Total Committed";
            return true;
        }

        if (TryPositiveBytes(snapshot.LocalUsage, out bytes))
        {
            source = $"{sourcePrefix} Local Usage";
            return true;
        }

        if (TryPositiveBytes(snapshot.SharedUsage, out bytes))
        {
            source = $"{sourcePrefix} Shared Usage";
            return true;
        }

        source = string.Empty;
        return false;
    }

    private static bool TryPositiveBytes(double? value, out ulong bytes)
    {
        if (value is > 0 and < ulong.MaxValue)
        {
            bytes = (ulong)value.Value;
            return true;
        }

        bytes = 0;
        return false;
    }

    private static PdhVramSnapshot TryReadAdapterPdh(AdapterDescription1 desc)
    {
        string prefix = $@"\GPU Adapter Memory({LuidInstanceName(desc)})\";
        return new PdhVramSnapshot(
            TryReadPdhDouble(prefix + "Shared Usage"),
            TryReadPdhDouble(prefix + "Dedicated Usage"),
            null,
            null,
            TryReadPdhDouble(prefix + "Total Committed"));
    }

    private static PdhVramSnapshot TryReadProcessPdh(AdapterDescription1 desc, uint processId)
    {
        string prefix = $@"\GPU Process Memory(pid_{processId}_{LuidInstanceName(desc)})\";
        return new PdhVramSnapshot(
            TryReadPdhDouble(prefix + "Shared Usage"),
            TryReadPdhDouble(prefix + "Dedicated Usage"),
            TryReadPdhDouble(prefix + "Local Usage"),
            TryReadPdhDouble(prefix + "Non Local Usage"),
            TryReadPdhDouble(prefix + "Total Committed"));
    }

    private static string LuidInstanceName(AdapterDescription1 desc)
    {
        return $"luid_0x{desc.Luid.HighPart:X8}_0x{desc.Luid.LowPart:X8}_phys_0";
    }

    private static double? TryReadPdhDouble(string counterPath)
    {
        IntPtr queryHandle = IntPtr.Zero;
        IntPtr counterHandle = IntPtr.Zero;
        try
        {
            if (PdhOpenQuery(null, IntPtr.Zero, out queryHandle) != 0 || queryHandle == IntPtr.Zero)
                return null;

            if (PdhAddEnglishCounter(queryHandle, counterPath, IntPtr.Zero, out counterHandle) != 0 || counterHandle == IntPtr.Zero)
                return null;

            PdhCollectQueryData(queryHandle);
            PdhCollectQueryData(queryHandle);

            return PdhGetFormattedCounterValue(counterHandle, PdhFmtDouble, out _, out var value) == 0 && value.CStatus == 0
                ? value.DoubleValue
                : null;
        }
        finally
        {
            if (queryHandle != IntPtr.Zero)
            {
                PdhCloseQuery(queryHandle);
            }
        }
    }

    private const uint PdhFmtDouble = 0x00000200;

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern int PdhOpenQuery(
        string? szDataSource,
        IntPtr dwUserData,
        out IntPtr phQuery);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern int PdhAddEnglishCounter(
        IntPtr hQuery,
        string szFullCounterPath,
        IntPtr dwUserData,
        out IntPtr phCounter);

    [DllImport("pdh.dll")]
    private static extern int PdhCollectQueryData(IntPtr hQuery);

    [DllImport("pdh.dll")]
    private static extern int PdhGetFormattedCounterValue(
        IntPtr hCounter,
        uint dwFormat,
        out uint lpdwType,
        out PDH_FMT_COUNTERVALUE pValue);

    [DllImport("pdh.dll")]
    private static extern int PdhCloseQuery(IntPtr hQuery);

    internal sealed record VramDisplaySelection(
        ulong TotalBytes,
        string DisplayMode,
        bool IsUmaIgpu,
        string StatusMessage,
        string Reason);

    internal sealed record VramUsedSelection(
        ulong UsedBytes,
        string Source);

    internal sealed record PdhVramSnapshot(
        double? SharedUsage,
        double? DedicatedUsage,
        double? LocalUsage,
        double? NonLocalUsage,
        double? TotalCommitted,
        string? UnavailableReason = null)
    {
        public static PdhVramSnapshot Unavailable(string reason)
        {
            return new PdhVramSnapshot(null, null, null, null, null, reason);
        }

        public override string ToString()
        {
            if (UnavailableReason is not null)
            {
                return UnavailableReason;
            }

            return $"shared={Format(SharedUsage)}; dedicated={Format(DedicatedUsage)}; local={Format(LocalUsage)}; nonLocal={Format(NonLocalUsage)}; totalCommitted={Format(TotalCommitted)}";
        }

        private static string Format(double? value)
        {
            return value.HasValue ? $"{value.Value:0}" : "--";
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PDH_FMT_COUNTERVALUE
    {
        [FieldOffset(0)]
        public uint CStatus;

        [FieldOffset(8)]
        public double DoubleValue;
    }
}
