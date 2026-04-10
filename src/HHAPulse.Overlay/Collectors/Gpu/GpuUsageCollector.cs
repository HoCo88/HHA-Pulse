using System.Runtime.InteropServices;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Collectors.Gpu;

public sealed class GpuUsageCollector : IMetricCollector, IDisposable
{
    private IntPtr _queryHandle;
    private IntPtr _counterHandle;
    private bool _initialized;
    private int _collectCount;
    private bool _disposed;

    public string Name => "GPU Usage";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        int status = PdhOpenQuery(null, IntPtr.Zero, out _queryHandle);
        if (status != 0)
        {
            AppLogger.Info($"GPU Usage: PdhOpenQuery failed with status 0x{status:X8}.");
            return Task.CompletedTask;
        }

        status = PdhAddEnglishCounter(
            _queryHandle,
            @"\GPU Engine(*engtype_3D*)\Utilization Percentage",
            IntPtr.Zero,
            out _counterHandle);

        if (status != 0)
        {
            AppLogger.Info($"GPU Usage: PdhAddEnglishCounter failed with status 0x{status:X8}.");
            PdhCloseQuery(_queryHandle);
            _queryHandle = IntPtr.Zero;
            return Task.CompletedTask;
        }

        // Must collect twice before first read to establish baseline.
        PdhCollectQueryData(_queryHandle);
        PdhCollectQueryData(_queryHandle);
        _collectCount = 2;
        _initialized = true;

        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        if (!_initialized || _queryHandle == IntPtr.Zero)
            return Task.CompletedTask;

        int status = PdhCollectQueryData(_queryHandle);
        if (status != 0)
            return Task.CompletedTask;

        _collectCount++;

        if (_collectCount < 3)
            return Task.CompletedTask;

        // Use PdhGetFormattedCounterArray to enumerate ALL matching instances
        // and sum their utilization. The wildcard counter matches multiple
        // per-process engtype_3D instances. PdhGetFormattedCounterValue only
        // returns one and often yields 0 on Intel iGPUs.
        uint bufferSize = 0;
        uint itemCount = 0;

        // First call: get required buffer size.
        status = PdhGetFormattedCounterArray(
            _counterHandle,
            PdhFmtDouble,
            ref bufferSize,
            out itemCount,
            IntPtr.Zero);

        // PDH_MORE_DATA (0x800007D2) means buffer too small — expected on first call.
        if (status != PdhMoreData && status != 0)
            return Task.CompletedTask;

        if (bufferSize == 0 || itemCount == 0)
            return Task.CompletedTask;

        var buffer = Marshal.AllocHGlobal((int)bufferSize);
        try
        {
            status = PdhGetFormattedCounterArray(
                _counterHandle,
                PdhFmtDouble,
                ref bufferSize,
                out itemCount,
                buffer);

            if (status != 0)
                return Task.CompletedTask;

            double totalUtilization = 0;
            int structSize = Marshal.SizeOf<PDH_FMT_COUNTERVALUE_ITEM>();

            for (int i = 0; i < itemCount; i++)
            {
                var itemPtr = buffer + (i * structSize);
                var item = Marshal.PtrToStructure<PDH_FMT_COUNTERVALUE_ITEM>(itemPtr);
                if (item.FmtValue.CStatus == 0 && item.FmtValue.doubleValue > 0)
                {
                    totalUtilization += item.FmtValue.doubleValue;
                }
            }

            snapshot.AvailableMetrics |= MetricFlags.GpuUsage;
            snapshot.Gpu.UsagePercent = Math.Clamp(totalUtilization, 0.0, 100.0);
            MeasurementTraceRecorder.Record(
                snapshot,
                "gpu",
                nameof(GpuUsageCollector),
                "PDH GPU Engine",
                true,
                $"{snapshot.Gpu.UsagePercent:0.0}%",
                $"counter=\\GPU Engine(*engtype_3D*)\\Utilization Percentage; instanceCount={itemCount}; summed3D={totalUtilization:0.000}",
                "GPU usage from PDH 3D engine counters.",
                "PdhGetFormattedCounterArray",
                $"{totalUtilization:0.000}",
                "%",
                "sum matching engtype_3D instances; clamp 0..100",
                $"{snapshot.Gpu.UsagePercent:0.000}",
                "%",
                TelemetryValidationState.Verified,
                "PDH returned formatted 3D engine counter instances.");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_queryHandle != IntPtr.Zero)
        {
            PdhCloseQuery(_queryHandle);
            _queryHandle = IntPtr.Zero;
        }
    }

    private const uint PdhFmtDouble = 0x00000200;
    private const int PdhMoreData = unchecked((int)0x800007D2);

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
    private static extern int PdhGetFormattedCounterArray(
        IntPtr hCounter,
        uint dwFormat,
        ref uint lpdwBufferSize,
        out uint lpdwItemCount,
        IntPtr ItemBuffer);

    [DllImport("pdh.dll")]
    private static extern int PdhCloseQuery(IntPtr hQuery);

    [StructLayout(LayoutKind.Sequential)]
    private struct PDH_FMT_COUNTERVALUE_ITEM
    {
        public IntPtr szName;
        public PDH_FMT_COUNTERVALUE FmtValue;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PDH_FMT_COUNTERVALUE
    {
        [FieldOffset(0)]
        public uint CStatus;

        [FieldOffset(8)]
        public int longValue;

        [FieldOffset(8)]
        public double doubleValue;

        [FieldOffset(8)]
        public long largeValue;

        [FieldOffset(8)]
        public IntPtr AnsiStringValue;

        [FieldOffset(8)]
        public IntPtr WideStringValue;
    }
}
