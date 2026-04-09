using System.Runtime.InteropServices;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Collectors.Gpu;

/// <summary>
/// Reads GPU temperature, power, and fan speed directly from the Windows
/// kernel via D3DKMTQueryAdapterInfo (KMTQAITYPE_ADAPTERPERFDATA).
/// This is the same API Windows Task Manager uses — no vendor SDK needed.
/// Works on any WDDM 2.4+ driver (AMD, Intel, NVIDIA).
/// Runs from user mode, no elevation required.
/// </summary>
public sealed class GpuPerfDataCollector : IMetricCollector, IDisposable
{
    private uint _adapterHandle;
    private bool _initialized;
    private bool _loggedFailure;
    private bool _disposed;
    private string _statusMessage = "D3DKMT GPU perf data not initialized.";

    // Capabilities queried once at init.
    private double _temperatureMaxCelsius;
    private double _temperatureWarningCelsius;

    public string Name => "GPU Perf Data (D3DKMT)";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Enumerate adapters to get the primary GPU handle.
            var enumAdapters = new D3DKMT_ENUMADAPTERS2 { NumAdapters = 0, pAdapters = IntPtr.Zero };

            // First call: get adapter count.
            int status = D3DKMTEnumAdapters2(ref enumAdapters);
            if (status != 0 && status != StatusBufferTooSmall)
            {
                LogOnce($"GPU Perf: D3DKMTEnumAdapters2 count query failed 0x{status:X8}.");
                return Task.CompletedTask;
            }

            if (enumAdapters.NumAdapters == 0)
            {
                LogOnce("GPU Perf: No adapters found.");
                return Task.CompletedTask;
            }

            // Second call: fill adapter info.
            int structSize = Marshal.SizeOf<D3DKMT_ADAPTERINFO>();
            var buffer = Marshal.AllocHGlobal(structSize * (int)enumAdapters.NumAdapters);
            try
            {
                enumAdapters.pAdapters = buffer;
                status = D3DKMTEnumAdapters2(ref enumAdapters);
                if (status != 0)
                {
                    LogOnce($"GPU Perf: D3DKMTEnumAdapters2 fill failed 0x{status:X8}.");
                    return Task.CompletedTask;
                }

                // Prefer the display-attached adapter so D3DKMT perf data lines up
                // with the same primary adapter DXGI uses for VRAM.
                var adapterInfo = Marshal.PtrToStructure<D3DKMT_ADAPTERINFO>(buffer);
                for (var i = 0; i < enumAdapters.NumAdapters; i++)
                {
                    var current = Marshal.PtrToStructure<D3DKMT_ADAPTERINFO>(buffer + (i * structSize));
                    if (current.NumOfSources > 0)
                    {
                        adapterInfo = current;
                        break;
                    }
                }

                _adapterHandle = adapterInfo.hAdapter;
                AppLogger.Info($"GPU Perf: Selected adapter LUID={adapterInfo.AdapterLuid.HighPart:X8}:{adapterInfo.AdapterLuid.LowPart:X8}, Sources={adapterInfo.NumOfSources}.");
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            // Query perf data caps (static — max temp, warning temp).
            var caps = new D3DKMT_ADAPTER_PERFDATACAPS { PhysicalAdapterIndex = 0 };
            var capsQuery = new D3DKMT_QUERYADAPTERINFO
            {
                hAdapter = _adapterHandle,
                Type = KMTQAITYPE_ADAPTERPERFDATACAPS,
                pPrivateDriverData = IntPtr.Zero,
                PrivateDriverDataSize = (uint)Marshal.SizeOf<D3DKMT_ADAPTER_PERFDATACAPS>()
            };

            var capsPtr = Marshal.AllocHGlobal((int)capsQuery.PrivateDriverDataSize);
            try
            {
                Marshal.StructureToPtr(caps, capsPtr, false);
                capsQuery.pPrivateDriverData = capsPtr;
                status = D3DKMTQueryAdapterInfo(ref capsQuery);
                if (status == 0)
                {
                    caps = Marshal.PtrToStructure<D3DKMT_ADAPTER_PERFDATACAPS>(capsPtr);
                    _temperatureMaxCelsius = caps.TemperatureMax / 10.0;
                    _temperatureWarningCelsius = caps.TemperatureWarning / 10.0;
                    AppLogger.Info($"GPU Perf: Caps loaded. MaxTemp={_temperatureMaxCelsius:0.0}C, WarnTemp={_temperatureWarningCelsius:0.0}C.");
                }
                else
                {
                    AppLogger.Info($"GPU Perf: Caps query returned 0x{status:X8} (non-fatal).");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(capsPtr);
            }

            // Try a test perf data query to confirm the driver supports it.
            var testPerf = new D3DKMT_ADAPTER_PERFDATA { PhysicalAdapterIndex = 0 };
            var testQuery = new D3DKMT_QUERYADAPTERINFO
            {
                hAdapter = _adapterHandle,
                Type = KMTQAITYPE_ADAPTERPERFDATA,
                pPrivateDriverData = IntPtr.Zero,
                PrivateDriverDataSize = (uint)Marshal.SizeOf<D3DKMT_ADAPTER_PERFDATA>()
            };

            var testPtr = Marshal.AllocHGlobal((int)testQuery.PrivateDriverDataSize);
            try
            {
                Marshal.StructureToPtr(testPerf, testPtr, false);
                testQuery.pPrivateDriverData = testPtr;
                status = D3DKMTQueryAdapterInfo(ref testQuery);

                if (status == 0)
                {
                    testPerf = Marshal.PtrToStructure<D3DKMT_ADAPTER_PERFDATA>(testPtr);
                    _initialized = true;
                    _statusMessage = "D3DKMT GPU perf data initialized.";
                    AppLogger.Info($"GPU Perf: D3DKMT initialized. VendorHint=adapter0. Test read: Temp={testPerf.Temperature / 10.0:0.0}C, PowerRaw={testPerf.Power}, FanRPM={testPerf.FanRPM}, MemFreq={testPerf.MemoryFrequency}Hz.");
                }
                else
                {
                    LogOnce($"GPU Perf: D3DKMTQueryAdapterInfo PERFDATA failed 0x{status:X8}. Driver may not support it.");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(testPtr);
            }
        }
        catch (Exception ex)
        {
            LogOnce($"GPU Perf: Init exception: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        if (!_initialized)
        {
            snapshot.Dependencies.GpuTelemetryAvailable = false;
            snapshot.Dependencies.GpuTelemetryStatusMessage = _statusMessage;
            return Task.CompletedTask;
        }

        snapshot.Dependencies.GpuTelemetryAvailable = true;
        snapshot.Dependencies.GpuTelemetryStatusMessage = _statusMessage;

        var perfData = new D3DKMT_ADAPTER_PERFDATA { PhysicalAdapterIndex = 0 };
        var query = new D3DKMT_QUERYADAPTERINFO
        {
            hAdapter = _adapterHandle,
            Type = KMTQAITYPE_ADAPTERPERFDATA,
            pPrivateDriverData = IntPtr.Zero,
            PrivateDriverDataSize = (uint)Marshal.SizeOf<D3DKMT_ADAPTER_PERFDATA>()
        };

        var ptr = Marshal.AllocHGlobal((int)query.PrivateDriverDataSize);
        try
        {
            Marshal.StructureToPtr(perfData, ptr, false);
            query.pPrivateDriverData = ptr;

            int status = D3DKMTQueryAdapterInfo(ref query);
            if (status != 0)
                return Task.CompletedTask;

            perfData = Marshal.PtrToStructure<D3DKMT_ADAPTER_PERFDATA>(ptr);

            // Temperature: stored in deci-Celsius (1 = 0.1°C).
            if (perfData.Temperature > 0)
            {
                snapshot.AvailableMetrics |= MetricFlags.GpuTemperature;
                snapshot.Gpu.TemperatureCelsius = perfData.Temperature / 10.0;

                if (_temperatureMaxCelsius > 0)
                {
                    snapshot.Gpu.MaxTemperatureCelsius = _temperatureMaxCelsius;
                }
            }

            // Power: D3DKMT exposes a raw driver power field, but not a
            // validated watt value. Do not surface it as GPU watts.
            if (perfData.Power > 0)
            {
                snapshot.Dependencies.GpuTelemetryStatusMessage =
                    $"{_statusMessage} D3DKMT PowerRaw={perfData.Power} is hidden until a true GPU watt source is available.";
            }

            // Fan RPM.
            if (perfData.FanRPM > 0)
            {
                snapshot.AvailableMetrics |= MetricFlags.Fan;
                snapshot.Gpu.FanRpm = (int)perfData.FanRPM;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_adapterHandle != 0)
        {
            var closeAdapter = new D3DKMT_CLOSEADAPTER { hAdapter = _adapterHandle };
            D3DKMTCloseAdapter(ref closeAdapter);
            _adapterHandle = 0;
        }
    }

    private void LogOnce(string message)
    {
        _statusMessage = message;
        if (_loggedFailure)
            return;

        _loggedFailure = true;
        AppLogger.Info(message);
    }

    // ── D3DKMT Query Types ──

    private const int KMTQAITYPE_ADAPTERPERFDATA = 62;
    private const int KMTQAITYPE_ADAPTERPERFDATACAPS = 63;

    // STATUS_BUFFER_TOO_SMALL
    private const int StatusBufferTooSmall = unchecked((int)0xC0000023);

    // ── P/Invoke ──

    [DllImport("gdi32.dll", EntryPoint = "D3DKMTQueryAdapterInfo", SetLastError = false)]
    private static extern int D3DKMTQueryAdapterInfo(ref D3DKMT_QUERYADAPTERINFO info);

    [DllImport("gdi32.dll", EntryPoint = "D3DKMTEnumAdapters2", SetLastError = false)]
    private static extern int D3DKMTEnumAdapters2(ref D3DKMT_ENUMADAPTERS2 adapters);

    [DllImport("gdi32.dll", EntryPoint = "D3DKMTCloseAdapter", SetLastError = false)]
    private static extern int D3DKMTCloseAdapter(ref D3DKMT_CLOSEADAPTER adapter);

    // ── Structs ──

    [StructLayout(LayoutKind.Sequential)]
    private struct D3DKMT_QUERYADAPTERINFO
    {
        public uint hAdapter;
        public int Type;         // KMTQUERYADAPTERINFOTYPE
        public IntPtr pPrivateDriverData;
        public uint PrivateDriverDataSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3DKMT_ENUMADAPTERS2
    {
        public uint NumAdapters;
        public IntPtr pAdapters;  // D3DKMT_ADAPTERINFO[]
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3DKMT_ADAPTERINFO
    {
        public uint hAdapter;
        public LUID AdapterLuid;
        public uint NumOfSources;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bPrecisePresentRegionsPreferred;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3DKMT_CLOSEADAPTER
    {
        public uint hAdapter;
    }

    /// <summary>
    /// D3DKMT_ADAPTER_PERFDATA — performance data per adapter.
    /// Temperature in deci-Celsius (1 = 0.1°C).
    /// Power in tenths of percentage of TDP (1 = 0.1%) per MS docs.
    /// FanRPM in RPM.
    /// Explicit layout matches the D3DKMT_ALIGN64 macro on ULONGLONG fields
    /// in the Windows header, which forces 8-byte alignment with padding
    /// after PhysicalAdapterIndex (UINT32 → 4 bytes padding → ULONGLONG).
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    private struct D3DKMT_ADAPTER_PERFDATA
    {
        [FieldOffset(0)]  public uint PhysicalAdapterIndex;
        // 4 bytes padding (D3DKMT_ALIGN64)
        [FieldOffset(8)]  public ulong MemoryFrequency;       // Hz
        [FieldOffset(16)] public ulong MaxMemoryFrequency;    // Hz
        [FieldOffset(24)] public ulong MaxMemoryFrequencyOC;  // Hz (overclocked)
        [FieldOffset(32)] public ulong MemoryBandwidth;       // bytes/sec
        [FieldOffset(40)] public ulong PCIEBandwidth;         // bytes/sec
        [FieldOffset(48)] public uint FanRPM;
        [FieldOffset(52)] public uint Power;                  // tenths of % of TDP per MS docs
        [FieldOffset(56)] public uint Temperature;            // deci-Celsius
        [FieldOffset(60)] public byte PowerStateOverride;
    }

    /// <summary>
    /// D3DKMT_ADAPTER_PERFDATACAPS — static caps queried once.
    /// Explicit layout for D3DKMT_ALIGN64 on ULONGLONG fields.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    private struct D3DKMT_ADAPTER_PERFDATACAPS
    {
        [FieldOffset(0)]  public uint PhysicalAdapterIndex;
        // 4 bytes padding (D3DKMT_ALIGN64)
        [FieldOffset(8)]  public ulong MaxMemoryBandwidth;
        [FieldOffset(16)] public ulong MaxPCIEBandwidth;
        [FieldOffset(24)] public uint MaxFanRPM;
        [FieldOffset(28)] public uint TemperatureMax;      // deci-Celsius
        [FieldOffset(32)] public uint TemperatureWarning;  // deci-Celsius
    }
}
