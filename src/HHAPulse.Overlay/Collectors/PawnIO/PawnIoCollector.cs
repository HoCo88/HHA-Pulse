using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Collectors.PawnIO;

public sealed class PawnIoCollector : IMetricCollector
{
    public string Name => "PawnIO";

    public bool IsAvailable { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        IsAvailable = false;
        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        snapshot.Dependencies.PawnIoAvailable = IsAvailable;
        snapshot.Dependencies.PawnIoStatusMessage = "PawnIO is optional and was not detected.";
        return Task.CompletedTask;
    }
}
