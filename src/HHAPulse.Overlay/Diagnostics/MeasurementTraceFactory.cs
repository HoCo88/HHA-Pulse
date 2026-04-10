using HHAPulse.Overlay.Settings;
using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Diagnostics;

public static class MeasurementTraceFactory
{
    public static List<MeasurementTrace> Create(TelemetrySnapshot snapshot, IReadOnlyList<MetricStatus> statuses)
    {
        var statusLookup = BuildStatusLookup(statuses);
        var traceLookup = BuildTraceLookup(snapshot.MeasurementTraces);
        var traces = new List<MeasurementTrace>();

        foreach (var metricId in OverlayPresetCatalog.AllMetricIds)
        {
            if (traceLookup.TryGetValue(metricId, out var runtimeTrace))
            {
                traces.Add(runtimeTrace);
                continue;
            }

            var status = statusLookup.GetValueOrDefault(metricId) ?? new MetricStatus
            {
                MetricId = metricId,
                Source = "unknown collector",
                StatusMessage = "No status available."
            };

            traces.Add(UnknownTrace(metricId, status));
        }

        foreach (var trace in snapshot.MeasurementTraces)
        {
            if (!traces.Any(item => string.Equals(item.MetricId, trace.MetricId, StringComparison.OrdinalIgnoreCase)))
            {
                traces.Add(trace);
            }
        }

        return traces;
    }

    private static Dictionary<string, MetricStatus> BuildStatusLookup(IEnumerable<MetricStatus> statuses)
    {
        var lookup = new Dictionary<string, MetricStatus>(StringComparer.OrdinalIgnoreCase);
        foreach (var status in statuses)
        {
            lookup[status.MetricId] = status;
        }

        return lookup;
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

    private static MeasurementTrace UnknownTrace(string metricId, MetricStatus status)
    {
        return new MeasurementTrace
        {
            MetricId = metricId,
            Collector = "unknown collector",
            Source = "unknown collector",
            IsAvailable = status.IsAvailable,
            ValueText = status.IsAvailable ? "available, provenance missing" : "--",
            PipelineText = "No runtime provenance trace was recorded for this metric.",
            StatusMessage = string.IsNullOrWhiteSpace(status.StatusMessage)
                ? "No collector reported provenance for this metric."
                : status.StatusMessage,
            ValidationState = status.IsAvailable ? TelemetryValidationState.Verified : TelemetryValidationState.Unavailable,
            Reason = status.IsAvailable
                ? "Metric flag is available, but no collector reported runtime provenance."
                : "No collector reported provenance for this metric."
        };
    }
}
