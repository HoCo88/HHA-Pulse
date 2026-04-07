using System.Runtime.InteropServices;
using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Collectors.Cpu;

public sealed class CpuUsageCollector : IMetricCollector
{
    private long _prevIdle;
    private long _prevKernel;
    private long _prevUser;
    private bool _hasBaseline;

    public string Name => "CPU Usage";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        // Take baseline reading so first CollectAsync can compute a delta.
        GetSystemTimes(out var idle, out var kernel, out var user);
        _prevIdle = FileTimeToLong(idle);
        _prevKernel = FileTimeToLong(kernel);
        _prevUser = FileTimeToLong(user);
        _hasBaseline = true;

        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        if (!GetSystemTimes(out var idleFt, out var kernelFt, out var userFt))
            return Task.CompletedTask;

        long idle = FileTimeToLong(idleFt);
        long kernel = FileTimeToLong(kernelFt);
        long user = FileTimeToLong(userFt);

        if (_hasBaseline)
        {
            long idleDiff = idle - _prevIdle;
            long kernelDiff = kernel - _prevKernel;
            long userDiff = user - _prevUser;

            long totalSystem = kernelDiff + userDiff;
            if (totalSystem > 0)
            {
                // Kernel time INCLUDES idle time, so subtract idle from total.
                double cpuPercent = (totalSystem - idleDiff) * 100.0 / totalSystem;
                snapshot.Cpu.UsagePercent = Math.Clamp(cpuPercent, 0.0, 100.0);
                snapshot.AvailableMetrics |= MetricFlags.CpuUsage;
            }
        }

        _prevIdle = idle;
        _prevKernel = kernel;
        _prevUser = user;
        _hasBaseline = true;

        return Task.CompletedTask;
    }

    private static long FileTimeToLong(FILETIME ft)
    {
        // Cast dwLowDateTime to uint before combining to avoid sign-extension.
        return ((long)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(
        out FILETIME lpIdleTime,
        out FILETIME lpKernelTime,
        out FILETIME lpUserTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public int dwLowDateTime;
        public int dwHighDateTime;
    }
}
