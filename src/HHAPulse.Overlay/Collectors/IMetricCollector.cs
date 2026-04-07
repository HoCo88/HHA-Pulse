using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Collectors;

public interface IMetricCollector
{
    string Name { get; }

    bool IsAvailable { get; }

    Task InitializeAsync(CancellationToken cancellationToken);

    Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken);
}
