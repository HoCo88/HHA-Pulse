using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Collectors.Cpu;

public sealed class CpuUsageCollector : IMetricCollector
{
    public string Name => "CPU Usage";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        snapshot.AvailableMetrics |= MetricFlags.CpuUsage;
        return Task.CompletedTask;
    }
}
