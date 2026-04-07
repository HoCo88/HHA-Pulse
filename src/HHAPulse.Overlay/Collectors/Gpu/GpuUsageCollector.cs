using System.Runtime.InteropServices;
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
            return Task.CompletedTask;

        status = PdhAddEnglishCounter(
            _queryHandle,
            @"\GPU Engine(*engtype_3D*)\Utilization Percentage",
            IntPtr.Zero,
            out _counterHandle);

        if (status != 0)
        {
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

        status = PdhGetFormattedCounterValue(
            _counterHandle,
            PdhFmtDouble,
            out _,
            out var counterValue);

        if (status != 0)
            return Task.CompletedTask;

        snapshot.AvailableMetrics |= MetricFlags.GpuUsage;
        snapshot.Gpu.UsagePercent = Math.Clamp(counterValue.doubleValue, 0.0, 100.0);

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
