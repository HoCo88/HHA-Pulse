using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Collectors;

public sealed class CollectorOrchestrator
{
    private readonly IReadOnlyList<IMetricCollector> collectors;

    public CollectorOrchestrator(IEnumerable<IMetricCollector> collectors)
    {
        this.collectors = collectors.ToArray();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        foreach (var collector in collectors)
        {
            try
            {
                await collector.InitializeAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Individual collectors must not prevent the app from starting.
            }
        }
    }

    public async Task<TelemetrySnapshot> CollectAsync(CancellationToken cancellationToken)
    {
        var snapshot = new TelemetrySnapshot
        {
            TimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        foreach (var collector in collectors)
        {
            if (!collector.IsAvailable)
            {
                continue;
            }

            try
            {
                await collector.CollectAsync(snapshot, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Failure isolation is deliberate. Surface per-collector status later via dependency state.
            }
        }

        return snapshot;
    }
}
