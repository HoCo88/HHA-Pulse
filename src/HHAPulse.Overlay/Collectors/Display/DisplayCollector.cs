using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Collectors.Display;

public sealed class DisplayCollector : IMetricCollector
{
    public string Name => "Display";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        snapshot.AvailableMetrics |= MetricFlags.Display;
        return Task.CompletedTask;
    }
}
