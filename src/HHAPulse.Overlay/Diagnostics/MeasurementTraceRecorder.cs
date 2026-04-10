using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Diagnostics;

public static class MeasurementTraceRecorder
{
    public static void Record(
        TelemetrySnapshot snapshot,
        string metricId,
        string collector,
        string source,
        bool isAvailable,
        string valueText,
        string pipelineText,
        string statusMessage,
        string apiContract,
        string rawValue,
        string rawUnit,
        string conversionRule,
        string convertedValue,
        string convertedUnit,
        string validationState,
        string reason)
    {
        var trace = snapshot.MeasurementTraces.FirstOrDefault(item =>
            string.Equals(item.MetricId, metricId, StringComparison.OrdinalIgnoreCase));

        if (trace is null)
        {
            trace = new MeasurementTrace { MetricId = metricId };
            snapshot.MeasurementTraces.Add(trace);
        }

        trace.Collector = collector;
        trace.Source = source;
        trace.IsAvailable = isAvailable;
        trace.ValueText = valueText;
        trace.PipelineText = pipelineText;
        trace.StatusMessage = statusMessage;
        trace.ApiContract = apiContract;
        trace.RawValue = rawValue;
        trace.RawUnit = rawUnit;
        trace.ConversionRule = conversionRule;
        trace.ConvertedValue = convertedValue;
        trace.ConvertedUnit = convertedUnit;
        trace.ValidationState = validationState;
        trace.Reason = reason;
    }
}
