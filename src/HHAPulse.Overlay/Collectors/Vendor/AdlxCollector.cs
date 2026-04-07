using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Collectors.Vendor;

public sealed class AdlxCollector : IMetricCollector
{
    public string Name => "AMD ADLX";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        snapshot.AvailableMetrics |= MetricFlags.GpuTemperature;
        return Task.CompletedTask;
    }
}
