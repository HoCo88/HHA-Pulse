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
            Status(OverlayPresetCatalog.Fps, now, traces),
            Status(OverlayPresetCatalog.AvgFps, now, traces),
            Status(OverlayPresetCatalog.OnePercentLow, now, traces),
            Status(OverlayPresetCatalog.ZeroPointOneLow, now, traces),
            Status(OverlayPresetCatalog.FrameTime, now, traces),
            Status(OverlayPresetCatalog.FrameGenFps, now, traces),
            Status(OverlayPresetCatalog.Battery, now, traces),
            Status(OverlayPresetCatalog.CpuUsage, now, traces),
            Status(OverlayPresetCatalog.CpuPower, now, traces),
            Status(OverlayPresetCatalog.GpuUsage, now, traces),
            Status(OverlayPresetCatalog.GpuTemp, now, traces),
            Status(OverlayPresetCatalog.GpuClock, now, traces),
            Status(OverlayPresetCatalog.GpuPower, now, traces),
            Status(OverlayPresetCatalog.GpuFan, now, traces),
            Status(OverlayPresetCatalog.Ram, now, traces),
            Status(OverlayPresetCatalog.Vram, now, traces),
            Status(OverlayPresetCatalog.TotalPower, now, traces),
            Status(OverlayPresetCatalog.RefreshRate, now, traces)
        };

        return statuses;
    }

    private static MetricStatus Status(string metricId, long now, IReadOnlyDictionary<string, MeasurementTrace> traces)
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

        return new MetricStatus
        {
            MetricId = metricId,
            Source = "unknown collector",
            IsAvailable = false,
            LastSuccessUnixMilliseconds = 0,
            StatusMessage = "No collector reported provenance for this metric.",
            ValidationState = TelemetryValidationState.Unavailable
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
