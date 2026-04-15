using System.Runtime.InteropServices;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Overlay.Settings;
using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Collectors.Cpu;

public sealed class CpuClockCollector : IMetricCollector
{
    private const int ProcessorInformationLevel = 11;
    private const uint PdhFmtDouble = 0x00000200;
    private const int ErrorSuccess = 0;
    private const string CounterPath = @"\Processor Information(_Total)\Processor Frequency";

    private IntPtr queryHandle;
    private IntPtr counterHandle;
    private bool initialized;

    public string Name => "CPU Clock";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows() || initialized)
        {
            return Task.CompletedTask;
        }

        if (PdhOpenQuery(null, IntPtr.Zero, out queryHandle) == ErrorSuccess)
        {
            if (PdhAddEnglishCounter(queryHandle, CounterPath, IntPtr.Zero, out counterHandle) != ErrorSuccess)
            {
                PdhCloseQuery(queryHandle);
                queryHandle = IntPtr.Zero;
            }
        }

        initialized = true;
        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.CompletedTask;
        }

        CollectAggregateClock(snapshot);
        CollectPerCoreNominal(snapshot);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (queryHandle != IntPtr.Zero)
        {
            PdhCloseQuery(queryHandle);
            queryHandle = IntPtr.Zero;
            counterHandle = IntPtr.Zero;
        }
    }

    internal static List<ProcessorCorePower> ParseProcessorPowerInformation(byte[] buffer)
    {
        var size = Marshal.SizeOf<ProcessorPowerInformation>();
        var count = buffer.Length / size;
        var result = new List<ProcessorCorePower>(count);
        if (count == 0)
        {
            return result;
        }

        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var basePtr = handle.AddrOfPinnedObject();
            for (var index = 0; index < count; index++)
            {
                var info = Marshal.PtrToStructure<ProcessorPowerInformation>(basePtr + (index * size));
                result.Add(new ProcessorCorePower
                {
                    CoreIndex = index,
                    CurrentMhz = info.CurrentMhz,
                    MaxMhz = info.MaxMhz,
                    MhzLimit = info.MhzLimit
                });
            }
        }
        finally
        {
            handle.Free();
        }

        return result;
    }

    private void CollectAggregateClock(TelemetrySnapshot snapshot)
    {
        if (queryHandle == IntPtr.Zero || counterHandle == IntPtr.Zero)
        {
            return;
        }

        if (PdhCollectQueryData(queryHandle) != ErrorSuccess)
        {
            return;
        }

        if (PdhGetFormattedCounterValue(counterHandle, PdhFmtDouble, out _, out var value) != ErrorSuccess)
        {
            return;
        }

        if (value.doubleValue <= 0)
        {
            return;
        }

        snapshot.AvailableMetrics |= MetricFlags.CpuClock;
        snapshot.Cpu.ClockMegahertz = value.doubleValue;
        snapshot.CpuDetail.AggregateEffectiveMhz = value.doubleValue;
        MeasurementTraceRecorder.Record(
            snapshot,
            OverlayPresetCatalog.CpuClock,
            nameof(CpuClockCollector),
            @"PDH \Processor Information(_Total)\Processor Frequency",
            true,
            value.doubleValue >= 1000 ? $"CPU ~{value.doubleValue / 1000.0:0.00}GHz" : $"CPU ~{value.doubleValue:0}MHz",
            "PDH query -> Processor Information(_Total) frequency counter",
            "Aggregate CPU clock from PDH processor-frequency counter.",
            @"\Processor Information(_Total)\Processor Frequency",
            $"{value.doubleValue:0.000}",
            "MHz",
            "PDH reports MHz directly",
            $"{value.doubleValue:0.000}",
            "MHz",
            TelemetryValidationState.Verified,
            "Aggregate CPU clock collected from PDH.");
    }

    private void CollectPerCoreNominal(TelemetrySnapshot snapshot)
    {
        var buffer = new byte[Math.Max(1, Environment.ProcessorCount) * Marshal.SizeOf<ProcessorPowerInformation>()];
        var status = CallNtPowerInformation(
            ProcessorInformationLevel,
            IntPtr.Zero,
            0,
            buffer,
            buffer.Length);

        if (status != 0)
        {
            return;
        }

        var perCore = ParseProcessorPowerInformation(buffer);
        if (perCore.Count == 0)
        {
            return;
        }

        snapshot.CpuDetail.PerCoreNominal = perCore;
        if (perCore.All(item => item.CurrentMhz > 0 && item.CurrentMhz == item.MaxMhz))
        {
            snapshot.CpuDetail.ClockStatusMessage = "live throttle-aware per-core frequency unavailable on this Windows build; PROCESSOR_POWER_INFORMATION returns nominal MaxMhz";
        }
        else
        {
            snapshot.CpuDetail.ClockStatusMessage = "Per-core nominal processor frequency from CallNtPowerInformation(ProcessorInformation).";
        }
    }

    [DllImport("PowrProf.dll", ExactSpelling = true)]
    private static extern int CallNtPowerInformation(
        int informationLevel,
        IntPtr inputBuffer,
        int inputBufferLength,
        [Out] byte[] outputBuffer,
        int outputBufferLength);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern int PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, EntryPoint = "PdhAddEnglishCounterW")]
    private static extern int PdhAddEnglishCounter(IntPtr query, string fullCounterPath, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern int PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll")]
    private static extern int PdhCloseQuery(IntPtr query);

    [DllImport("pdh.dll")]
    private static extern int PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out PdhFormattedCounterValue value);

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessorPowerInformation
    {
        public uint Number;
        public uint MaxMhz;
        public uint CurrentMhz;
        public uint MhzLimit;
        public uint MaxIdleState;
        public uint CurrentIdleState;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PdhFormattedCounterValue
    {
        public uint CStatus;
        public double doubleValue;
    }
}
