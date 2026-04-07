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
        snapshot.AvailableMetrics |= MetricFlags.Memory;
        return Task.CompletedTask;
    }
}
