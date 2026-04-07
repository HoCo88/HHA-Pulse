using System.Runtime.InteropServices;
using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Collectors.Memory;

public sealed class RamCollector : IMetricCollector
{
    public string Name => "RAM";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        var status = new MEMORYSTATUSEX();
        if (!GlobalMemoryStatusEx(status))
            return Task.CompletedTask;

        snapshot.AvailableMetrics |= MetricFlags.Memory;

        double totalMb = status.ullTotalPhys / (1024.0 * 1024.0);
        double availMb = status.ullAvailPhys / (1024.0 * 1024.0);

        snapshot.Memory.RamTotalMegabytes = totalMb;
        snapshot.Memory.RamUsedMegabytes = totalMb - availMb;

        return Task.CompletedTask;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    // Must be a class (not struct) because dwLength is set in the constructor
    // and marshalled by reference.
    [StructLayout(LayoutKind.Sequential)]
    private sealed class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
        }
    }
}
