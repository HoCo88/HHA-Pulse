using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Collectors.PresentMon;

public sealed class PresentMonCollector : IMetricCollector
{
    private readonly PresentMonLoader loader;

    public PresentMonCollector(PresentMonLoader loader)
    {
        this.loader = loader;
    }

    public string Name => "PresentMon";

    public bool IsAvailable => loader.IsAvailable;

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        loader.TryInitialize();
        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        snapshot.Dependencies.PresentMonAvailable = loader.IsAvailable;
        snapshot.Dependencies.PresentMonStatusMessage = loader.StatusMessage;

        if (!loader.IsAvailable)
        {
            return Task.CompletedTask;
        }

        snapshot.AvailableMetrics |= MetricFlags.Fps | MetricFlags.FrameTime | MetricFlags.Latency | MetricFlags.FrameGeneration | MetricFlags.Bottleneck;
        return Task.CompletedTask;
    }
}
