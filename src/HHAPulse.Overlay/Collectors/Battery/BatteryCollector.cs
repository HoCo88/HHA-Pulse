using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Collectors.Battery;

public sealed class BatteryCollector : IMetricCollector
{
    public string Name => "Battery";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        snapshot.AvailableMetrics |= MetricFlags.Battery;
        return Task.CompletedTask;
    }
}
