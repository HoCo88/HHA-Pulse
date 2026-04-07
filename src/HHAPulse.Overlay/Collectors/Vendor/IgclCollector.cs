using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Collectors.Vendor;

public sealed class IgclCollector : IMetricCollector
{
    public string Name => "Intel IGCL";

    public bool IsAvailable => OperatingSystem.IsWindows() && Environment.Is64BitProcess;

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
