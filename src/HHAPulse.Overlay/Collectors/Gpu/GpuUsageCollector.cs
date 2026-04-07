using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Collectors.Gpu;

public sealed class GpuUsageCollector : IMetricCollector
{
    public string Name => "GPU Usage";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        snapshot.AvailableMetrics |= MetricFlags.GpuUsage;
        return Task.CompletedTask;
    }
}
