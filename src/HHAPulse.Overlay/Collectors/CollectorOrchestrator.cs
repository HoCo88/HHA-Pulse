using HHAPulse.Shared.Models;
using HHAPulse.Overlay.Diagnostics;

namespace HHAPulse.Overlay.Collectors;

public sealed class CollectorOrchestrator : IDisposable
{
    private readonly IReadOnlyList<IMetricCollector> collectors;
    private readonly HashSet<string> loggedCollectionFailures = new(StringComparer.Ordinal);
    private readonly HashSet<string> loggedInitializationFailures = new(StringComparer.Ordinal);

    public CollectorOrchestrator(IEnumerable<IMetricCollector> collectors)
    {
        this.collectors = collectors.ToArray();
    }

    public void Dispose()
    {
        foreach (var collector in collectors)
        {
            if (collector is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        foreach (var collector in collectors)
        {
            try
            {
                await collector.InitializeAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Individual collectors must not prevent the app from starting.
                LogInitializationFailureOnce(collector, ex);
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
            catch (OperationCanceledException)
            {
                throw; // Re-throw for clean shutdown.
            }
            catch (Exception ex)
            {
                // Failure isolation is deliberate. Surface per-collector status later via dependency state.
                LogCollectionFailureOnce(collector, ex);
            }
        }

        return snapshot;
    }

    private void LogInitializationFailureOnce(IMetricCollector collector, Exception exception)
    {
        if (loggedInitializationFailures.Add(collector.Name))
        {
            AppLogger.Error($"Collector '{collector.Name}' failed to initialize.", exception);
        }
    }

    private void LogCollectionFailureOnce(IMetricCollector collector, Exception exception)
    {
        if (loggedCollectionFailures.Add(collector.Name))
        {
            AppLogger.Error($"Collector '{collector.Name}' failed during collection.", exception);
        }
    }
}
