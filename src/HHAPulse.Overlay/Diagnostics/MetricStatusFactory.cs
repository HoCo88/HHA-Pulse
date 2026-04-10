using HHAPulse.Overlay.Settings;
using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Diagnostics;

public static class MetricStatusFactory
{
    public static List<MetricStatus> Create(TelemetrySnapshot snapshot)
    {
        var now = snapshot.TimestampUnixMilliseconds;
        var traces = BuildTraceLookup(snapshot.MeasurementTraces);
        var statuses = new List<MetricStatus>
        {
            Status(OverlayPresetCatalog.Fps, snapshot, now, traces),
            Status(OverlayPresetCatalog.AvgFps, snapshot, now, traces),
            Status(OverlayPresetCatalog.OnePercentLow, snapshot, now, traces),
            Status(OverlayPresetCatalog.ZeroPointOneLow, snapshot, now, traces),
            Status(OverlayPresetCatalog.FrameTime, snapshot, now, traces),
            Status(OverlayPresetCatalog.Battery, snapshot, now, traces),
            Status(OverlayPresetCatalog.CpuUsage, snapshot, now, traces),
            Status(OverlayPresetCatalog.CpuTemp, snapshot, now, traces),
            Status(OverlayPresetCatalog.CpuPower, snapshot, now, traces),
            Status(OverlayPresetCatalog.GpuUsage, snapshot, now, traces),
            Status(OverlayPresetCatalog.GpuTemp, snapshot, now, traces),
            Status(OverlayPresetCatalog.GpuClock, snapshot, now, traces),
            Status(OverlayPresetCatalog.GpuPower, snapshot, now, traces),
            Status(OverlayPresetCatalog.GpuFan, snapshot, now, traces),
            Status(OverlayPresetCatalog.Ram, snapshot, now, traces),
            Status(OverlayPresetCatalog.Vram, snapshot, now, traces),
            Status(OverlayPresetCatalog.TotalPower, snapshot, now, traces),
            Status(OverlayPresetCatalog.DeviceTemp, snapshot, now, traces),
            Status(OverlayPresetCatalog.RefreshRate, snapshot, now, traces)
        };

        return statuses;
    }

    private static MetricStatus Status(string metricId, TelemetrySnapshot snapshot, long now, IReadOnlyDictionary<string, MeasurementTrace> traces)
    {
        if (traces.TryGetValue(metricId, out var trace))
        {
            return new MetricStatus
            {
                MetricId = metricId,
                Source = string.IsNullOrWhiteSpace(trace.Source) ? Blank(trace.Collector, "unknown collector") : trace.Source,
                IsAvailable = trace.IsAvailable,
                LastSuccessUnixMilliseconds = trace.IsAvailable ? now : 0,
                StatusMessage = Blank(Blank(trace.Reason, trace.StatusMessage), "No collector reported provenance details for this metric."),
                ValidationState = Blank(trace.ValidationState, trace.IsAvailable ? TelemetryValidationState.Verified : TelemetryValidationState.Unavailable)
            };
        }

        var available = IsMetricFlagAvailable(metricId, snapshot);
        return new MetricStatus
        {
            MetricId = metricId,
            Source = "unknown collector",
            IsAvailable = available,
            LastSuccessUnixMilliseconds = available ? now : 0,
            StatusMessage = available
                ? "Metric is available, but no collector reported runtime provenance for it."
                : "No collector reported provenance for this metric.",
            ValidationState = available ? TelemetryValidationState.Verified : TelemetryValidationState.Unavailable
        };
    }

    private static bool IsMetricFlagAvailable(string metricId, TelemetrySnapshot snapshot)
    {
        return metricId switch
        {
            OverlayPresetCatalog.Fps => snapshot.AvailableMetrics.HasFlag(MetricFlags.Fps),
            OverlayPresetCatalog.AvgFps => snapshot.AvailableMetrics.HasFlag(MetricFlags.Fps),
            OverlayPresetCatalog.OnePercentLow => snapshot.AvailableMetrics.HasFlag(MetricFlags.Fps),
            OverlayPresetCatalog.ZeroPointOneLow => snapshot.AvailableMetrics.HasFlag(MetricFlags.Fps),
            OverlayPresetCatalog.FrameTime => snapshot.AvailableMetrics.HasFlag(MetricFlags.FrameTime),
            OverlayPresetCatalog.Battery => snapshot.AvailableMetrics.HasFlag(MetricFlags.Battery),
            OverlayPresetCatalog.CpuUsage => snapshot.AvailableMetrics.HasFlag(MetricFlags.CpuUsage),
            OverlayPresetCatalog.CpuTemp => snapshot.AvailableMetrics.HasFlag(MetricFlags.CpuTemperature),
            OverlayPresetCatalog.CpuPower => snapshot.AvailableMetrics.HasFlag(MetricFlags.CpuPower),
            OverlayPresetCatalog.GpuUsage => snapshot.AvailableMetrics.HasFlag(MetricFlags.GpuUsage),
            OverlayPresetCatalog.GpuTemp => snapshot.AvailableMetrics.HasFlag(MetricFlags.GpuTemperature),
            OverlayPresetCatalog.GpuClock => snapshot.AvailableMetrics.HasFlag(MetricFlags.GpuClock),
            OverlayPresetCatalog.GpuPower => snapshot.AvailableMetrics.HasFlag(MetricFlags.GpuPower),
            OverlayPresetCatalog.GpuFan => snapshot.AvailableMetrics.HasFlag(MetricFlags.Fan),
            OverlayPresetCatalog.Ram => snapshot.AvailableMetrics.HasFlag(MetricFlags.Memory),
            OverlayPresetCatalog.Vram => snapshot.AvailableMetrics.HasFlag(MetricFlags.Vram),
            OverlayPresetCatalog.TotalPower => snapshot.AvailableMetrics.HasFlag(MetricFlags.SystemPower),
            OverlayPresetCatalog.DeviceTemp => snapshot.AvailableMetrics.HasFlag(MetricFlags.DeviceTemperature),
            OverlayPresetCatalog.RefreshRate => snapshot.AvailableMetrics.HasFlag(MetricFlags.Display),
            _ => false
        };
    }

    private static Dictionary<string, MeasurementTrace> BuildTraceLookup(IEnumerable<MeasurementTrace> traces)
    {
        var lookup = new Dictionary<string, MeasurementTrace>(StringComparer.OrdinalIgnoreCase);
        foreach (var trace in traces)
        {
            lookup[trace.MetricId] = trace;
        }

        return lookup;
    }

    private static string Blank(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
