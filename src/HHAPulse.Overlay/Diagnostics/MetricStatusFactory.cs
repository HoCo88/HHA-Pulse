using HHAPulse.Overlay.Settings;
using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Diagnostics;

public static class MetricStatusFactory
{
    public static List<MetricStatus> Create(TelemetrySnapshot snapshot)
    {
        var now = snapshot.TimestampUnixMilliseconds;
        var statuses = new List<MetricStatus>
        {
            Status(OverlayPresetCatalog.Fps, "ETW capture service", snapshot.AvailableMetrics.HasFlag(MetricFlags.Fps), now, snapshot.Dependencies.CaptureServiceStatusMessage),
            Status(OverlayPresetCatalog.FrameTime, "ETW capture service", snapshot.AvailableMetrics.HasFlag(MetricFlags.FrameTime), now, snapshot.Dependencies.CaptureServiceStatusMessage),
            Status(OverlayPresetCatalog.FrameGenFps, "ETW capture service", snapshot.AvailableMetrics.HasFlag(MetricFlags.FrameGen), now, "Frame generation is shown only when ETW reports it."),
            Status(OverlayPresetCatalog.InputLatency, "ETW capture service", snapshot.AvailableMetrics.HasFlag(MetricFlags.InputLatency), now, "Input latency requires PresentMon-grade ETW evidence."),
            Status(OverlayPresetCatalog.Battery, "CallNtPowerInformation", snapshot.AvailableMetrics.HasFlag(MetricFlags.Battery), now, "Battery status from Windows power APIs."),
            Status(OverlayPresetCatalog.CpuUsage, "GetSystemTimes", snapshot.AvailableMetrics.HasFlag(MetricFlags.CpuUsage), now, "CPU usage from Windows system time deltas."),
            Status(OverlayPresetCatalog.CpuPower, "Optional privileged sensor lane", snapshot.AvailableMetrics.HasFlag(MetricFlags.CpuPower), now, "CPU power is hidden until a validated privileged/vendor collector exists."),
            Status(OverlayPresetCatalog.GpuUsage, "PDH GPU Engine", snapshot.AvailableMetrics.HasFlag(MetricFlags.GpuUsage), now, "GPU usage from PDH 3D engine counters."),
            Status(OverlayPresetCatalog.GpuTemp, "D3DKMT adapter perf data", snapshot.AvailableMetrics.HasFlag(MetricFlags.GpuTemperature), now, snapshot.Dependencies.GpuTelemetryStatusMessage),
            Status(OverlayPresetCatalog.GpuPower, "Optional true-watts GPU sensor lane", snapshot.AvailableMetrics.HasFlag(MetricFlags.GpuPower), now, "GPU watts are hidden until a validated watt source exists."),
            Status(OverlayPresetCatalog.GpuFan, "D3DKMT adapter perf data", snapshot.AvailableMetrics.HasFlag(MetricFlags.Fan), now, snapshot.Dependencies.GpuTelemetryStatusMessage),
            Status(OverlayPresetCatalog.Ram, "GlobalMemoryStatusEx", snapshot.AvailableMetrics.HasFlag(MetricFlags.Memory), now, "RAM usage from Windows memory status."),
            Status(OverlayPresetCatalog.Vram, "DXGI QueryVideoMemoryInfo", snapshot.AvailableMetrics.HasFlag(MetricFlags.Vram), now, "VRAM from DXGI local-memory budget."),
            Status(OverlayPresetCatalog.TotalPower, "Battery discharge or component sum", snapshot.AvailableMetrics.HasFlag(MetricFlags.SystemPower), now, "System power is shown only when a validated power source exists."),
            Status(OverlayPresetCatalog.RefreshRate, "EnumDisplaySettings", snapshot.AvailableMetrics.HasFlag(MetricFlags.Display), now, "Display refresh rate from Windows display settings.")
        };

        return statuses;
    }

    private static MetricStatus Status(string metricId, string source, bool available, long now, string message)
    {
        return new MetricStatus
        {
            MetricId = metricId,
            Source = source,
            IsAvailable = available,
            LastSuccessUnixMilliseconds = available ? now : 0,
            StatusMessage = string.IsNullOrWhiteSpace(message)
                ? (available ? "Available." : "Unavailable until a real source reports data.")
                : message
        };
    }
}
