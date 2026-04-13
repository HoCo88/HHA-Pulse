using System.Reflection;
using System.Text;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Overlay.Settings;
using HHAPulse.Shared.Models;
using HHAPulse.Shared.Protocol;

namespace HHAPulse.Overlay.Views;

internal static class ControlShellTextBuilder
{
    public static string Blank(string value) =>
        string.IsNullOrWhiteSpace(value) ? "--" : value;

    public static string FriendlyPresetName(OverlayPreset preset) => preset switch
    {
        OverlayPreset.Tuner => "Performance",
        OverlayPreset.Off => "Off",
        _ => OverlayPresetCatalog.PresetDisplayName(preset)
    };

    public static string ShowModeText(OverlayShowMode mode) =>
        mode == OverlayShowMode.InGameOnly ? "In-game" : "On";

    public static string FormatBatterySummary(TelemetrySnapshot snapshot)
    {
        if (!snapshot.AvailableMetrics.HasFlag(MetricFlags.Battery))
        {
            return "waiting for battery telemetry";
        }

        return snapshot.Battery.IsCharging
            ? $"{snapshot.Battery.ChargePercent:0}% charging"
            : $"{snapshot.Battery.ChargePercent:0}% battery";
    }

    public static string BuildHubRuntimeSummary(TelemetrySnapshot snapshot, DependencyState dependencies)
    {
        var capture = dependencies.CaptureServiceConnected
            ? dependencies.CaptureTargetProcessId == 0
                ? "Capture ready"
                : $"Reading {Blank(dependencies.CaptureTargetProcessName)}"
            : "Capture limited";
        var battery = snapshot.AvailableMetrics.HasFlag(MetricFlags.Battery)
            ? FormatBatterySummary(snapshot)
            : "battery waiting";
        var companion = dependencies.WidgetClientCount > 0 ? "Companion linked" : "Companion optional";
        return $"{capture}  |  {battery}  |  {companion}";
    }

    public static string BuildCaptureHelpText(TelemetrySnapshot snapshot, DependencyState dependencies)
    {
        var routingText = dependencies.CaptureServiceConnected
            ? "Pulse is connected and ready to read frame data when a supported game is active."
            : "Enable or install the capture service if you want FPS metrics. Non-FPS telemetry continues locally.";

        var frameGenText = !dependencies.CaptureServiceConnected
            ? "Frame generation status is unavailable until capture is connected."
            : dependencies.CapturePayloadAgeMilliseconds > 2000
                ? "Frame generation status is waiting on fresher capture data."
                : snapshot.Performance.HybridPresentDetected
                    ? "Frame generation was detected in the current capture window."
                    : "Frame generation was not detected in the current capture window.";

        return $"{routingText} {frameGenText}";
    }

    public static (string Headline, string Summary) BuildGameDetectSummary(TelemetrySnapshot snapshot, DependencyState dependencies)
    {
        if (!dependencies.CaptureServiceConnected)
        {
            return ("Limited", "Capture is not connected yet, so Pulse can still show local telemetry but not live FPS.");
        }

        if (dependencies.CaptureTargetProcessId == 0)
        {
            return ("Ready", "Capture is connected and waiting for a supported game window.");
        }

        if (dependencies.CapturePayloadAgeMilliseconds > 2000)
        {
            return ("Limited", $"Pulse found {Blank(dependencies.CaptureTargetProcessName)}, but the frame data is stale right now.");
        }

        return ("Active", $"Pulse is reading live frame data from {Blank(dependencies.CaptureTargetProcessName)}.");
    }

    public static string BuildGpuTelemetrySummary(DependencyState dependencies)
    {
        var parts = new List<string>();
        AddGpuSource(parts, "temp", dependencies.GpuTemperatureSource);
        AddGpuSource(parts, "clock", dependencies.GpuClockSource);
        AddGpuSource(parts, "power", dependencies.GpuPowerSource);
        AddGpuSource(parts, "fan", dependencies.GpuFanSource);
        return parts.Count > 0 ? string.Join(", ", parts) : dependencies.GpuTelemetryStatusMessage;
    }

    private static void AddGpuSource(List<string> parts, string metricName, string source)
    {
        if (!string.IsNullOrWhiteSpace(source))
        {
            parts.Add($"{metricName}={source}");
        }
    }

    public static string BuildMetricTraceSummary(TelemetrySnapshot snapshot, string metricId)
    {
        var trace = snapshot.MeasurementTraces.FirstOrDefault(item => string.Equals(item.MetricId, metricId, StringComparison.OrdinalIgnoreCase));
        if (trace is null)
        {
            return string.Empty;
        }

        return $"Trace: {trace.ValueText} via {trace.Source}.";
    }

    public static bool IsDisputed(MetricStatus status)
    {
        return status.MetricId == OverlayPresetCatalog.CpuPower
            || status.Source.Contains("ADLX", StringComparison.OrdinalIgnoreCase)
            || status.Source.Contains("IGCL", StringComparison.OrdinalIgnoreCase)
            || status.StatusMessage.Contains("hardware validation", StringComparison.OrdinalIgnoreCase);
    }

    public static string TruthBadgeText(MetricStatus status)
    {
        if (string.Equals(status.ValidationState, TelemetryValidationState.Rejected, StringComparison.OrdinalIgnoreCase))
        {
            return "Rejected";
        }

        if (string.Equals(status.ValidationState, TelemetryValidationState.HardwareValidationPending, StringComparison.OrdinalIgnoreCase))
        {
            return "Hardware validation pending";
        }

        if (!status.IsAvailable)
        {
            return "Unavailable";
        }

        if (IsDisputed(status))
        {
            return "Hardware validation pending";
        }

        return "Verified";
    }

    public static string BuildDiagnosticsDump(TelemetrySnapshot snapshot, IReadOnlyList<MetricStatus> statuses, IReadOnlyList<MeasurementTrace> traces)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var nativePath = Path.Combine(AppContext.BaseDirectory, "HHAPulse.Native.dll");
        var sb = new StringBuilder();
        sb.AppendLine("HHA Pulse Diagnostics");
        sb.AppendLine($"Overlay: {assembly.GetName().Version} | {assembly.Location}");
        sb.AppendLine($"Shared: {Blank(snapshot.Dependencies.SharedAssemblyVersion)} | MVID {Blank(snapshot.Dependencies.SharedAssemblyMvid)}");
        sb.AppendLine($"Shared path: {Blank(snapshot.Dependencies.SharedAssemblyPath)}");
        sb.AppendLine($"Native bridge: {(File.Exists(nativePath) ? "present" : "missing")} | {nativePath}");
        sb.AppendLine($"Protocol: v{PipeConstants.ProtocolVersion} | {PipeConstants.PipeLocalName}");
        sb.AppendLine($"Capture: {Blank(snapshot.Dependencies.CaptureServiceStatusMessage)} | age={snapshot.Dependencies.CapturePayloadAgeMilliseconds}ms");
        sb.AppendLine($"Target: {Blank(snapshot.Dependencies.CaptureTargetProcessName)} ({snapshot.Dependencies.CaptureTargetProcessId})");
        sb.AppendLine($"Widget: {Blank(snapshot.Dependencies.WidgetPipeStatusMessage)}");
        sb.AppendLine("Last observed boundary state:");
        sb.AppendLine($"- overlay -> capture control pipe: {PipeConstants.CaptureControlPipeLocalName} | target={Blank(snapshot.Dependencies.CaptureTargetProcessName)} ({snapshot.Dependencies.CaptureTargetProcessId})");
        sb.AppendLine($"- capture service -> overlay output pipe: {PipeConstants.CaptureOutputPipeLocalName} | connected={snapshot.Dependencies.CaptureServiceConnected} | age={snapshot.Dependencies.CapturePayloadAgeMilliseconds}ms");
        sb.AppendLine($"- collector orchestrator -> TelemetrySnapshot: timestamp={snapshot.TimestampUnixMilliseconds} | flags={snapshot.AvailableMetrics}");
        sb.AppendLine($"- overlay -> widget pipe: {PipeConstants.PipeLocalName} | clientsLastObserved={snapshot.Dependencies.WidgetClientCount}");
        sb.AppendLine("Disputed sources: ADLX, IGCL, and EMI remain quarantined until build and hardware proof clears them.");
        sb.AppendLine("Metrics:");
        foreach (var status in statuses)
        {
            sb.AppendLine($"- {status.MetricId}: {TruthBadgeText(status)} | {status.Source} | {status.StatusMessage}");
        }

        sb.AppendLine("Measurement traces:");
        foreach (var trace in traces)
        {
            var availability = trace.IsAvailable ? "available" : "unavailable";
            sb.AppendLine($"- {trace.MetricId}: {availability} | state={Blank(trace.ValidationState)} | collector={trace.Collector} | source={trace.Source} | value={trace.ValueText}");
            sb.AppendLine($"  pipeline: {trace.PipelineText}");
            sb.AppendLine($"  evidence: api={Blank(trace.ApiContract)} | raw={Blank(trace.RawValue)} {Blank(trace.RawUnit)} | conversion={Blank(trace.ConversionRule)} | converted={Blank(trace.ConvertedValue)} {Blank(trace.ConvertedUnit)}");
            sb.AppendLine($"  status: {trace.StatusMessage}");
            sb.AppendLine($"  reason: {Blank(trace.Reason)}");
        }

        return sb.ToString().TrimEnd();
    }
}
