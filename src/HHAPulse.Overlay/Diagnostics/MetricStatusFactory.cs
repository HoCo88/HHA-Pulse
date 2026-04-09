using HHAPulse.Overlay.Settings;
using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Diagnostics;

public static class MetricStatusFactory
{
    public static List<MetricStatus> Create(TelemetrySnapshot snapshot)
    {
        var now = snapshot.TimestampUnixMilliseconds;
        var dependencies = snapshot.Dependencies;
        var statuses = new List<MetricStatus>
        {
            Status(OverlayPresetCatalog.Fps, "ETW capture service", snapshot.AvailableMetrics.HasFlag(MetricFlags.Fps), now, dependencies.CaptureServiceStatusMessage),
            Status(OverlayPresetCatalog.FrameTime, "ETW capture service", snapshot.AvailableMetrics.HasFlag(MetricFlags.FrameTime), now, dependencies.CaptureServiceStatusMessage),
            Status(OverlayPresetCatalog.FrameGenFps, "ETW capture service", snapshot.AvailableMetrics.HasFlag(MetricFlags.FrameGen), now, "Frame generation is shown only when ETW reports it."),
            Status(OverlayPresetCatalog.InputLatency, "ETW capture service", snapshot.AvailableMetrics.HasFlag(MetricFlags.InputLatency), now, "Input latency requires PresentMon-grade ETW evidence."),
            Status(OverlayPresetCatalog.Battery, "CallNtPowerInformation", snapshot.AvailableMetrics.HasFlag(MetricFlags.Battery), now, "Battery status from Windows power APIs."),
            Status(OverlayPresetCatalog.CpuUsage, "GetSystemTimes", snapshot.AvailableMetrics.HasFlag(MetricFlags.CpuUsage), now, "CPU usage from Windows system time deltas."),
            Status(OverlayPresetCatalog.CpuPower, "EMI energy deltas", snapshot.AvailableMetrics.HasFlag(MetricFlags.CpuPower), now, "CPU power from Windows Energy Meter Interface (EMI)."),
            Status(OverlayPresetCatalog.GpuUsage, "PDH GPU Engine", snapshot.AvailableMetrics.HasFlag(MetricFlags.GpuUsage), now, "GPU usage from PDH 3D engine counters."),
            Status(OverlayPresetCatalog.GpuTemp, GpuSource(dependencies.GpuTemperatureSource, "D3DKMT fallback"), snapshot.AvailableMetrics.HasFlag(MetricFlags.GpuTemperature), now, GpuMessage(dependencies.GpuTemperatureStatusMessage, dependencies.GpuTelemetryStatusMessage, "GPU temperature is unavailable until D3DKMT or a vendor SDK reports it.")),
            Status(OverlayPresetCatalog.GpuClock, GpuSource(dependencies.GpuClockSource, "Vendor GPU telemetry"), snapshot.AvailableMetrics.HasFlag(MetricFlags.GpuClock), now, GpuMessage(dependencies.GpuClockStatusMessage, dependencies.GpuTelemetryStatusMessage, "GPU clock requires a vendor SDK (ADLX, IGCL, or NvAPI).")),
            Status(OverlayPresetCatalog.GpuPower, GpuSource(dependencies.GpuPowerSource, "Vendor GPU telemetry"), snapshot.AvailableMetrics.HasFlag(MetricFlags.GpuPower), now, GpuMessage(dependencies.GpuPowerStatusMessage, dependencies.GpuTelemetryStatusMessage, "GPU watts require a vendor SDK (ADLX, IGCL, or NVML).")),
            Status(OverlayPresetCatalog.GpuFan, GpuSource(dependencies.GpuFanSource, "D3DKMT fallback"), snapshot.AvailableMetrics.HasFlag(MetricFlags.Fan), now, GpuMessage(dependencies.GpuFanStatusMessage, dependencies.GpuTelemetryStatusMessage, "GPU fan speed is unavailable until D3DKMT or a vendor SDK reports it.")),
            Status(OverlayPresetCatalog.Ram, "GlobalMemoryStatusEx", snapshot.AvailableMetrics.HasFlag(MetricFlags.Memory), now, "RAM usage from Windows memory status."),
            Status(OverlayPresetCatalog.Vram, "DXGI QueryVideoMemoryInfo", snapshot.AvailableMetrics.HasFlag(MetricFlags.Vram), now, "VRAM from DXGI local-memory budget."),
            Status(OverlayPresetCatalog.TotalPower, "Battery discharge or component sum", snapshot.AvailableMetrics.HasFlag(MetricFlags.SystemPower), now, "System power is shown only when a validated power source exists."),
            Status(OverlayPresetCatalog.RefreshRate, "EnumDisplaySettings", snapshot.AvailableMetrics.HasFlag(MetricFlags.Display), now, "Display refresh rate from Windows display settings.")
        };

        return statuses;
    }

    private static string GpuSource(string specificSource, string fallbackSource)
    {
        return string.IsNullOrWhiteSpace(specificSource) ? fallbackSource : specificSource;
    }

    private static string GpuMessage(string specificMessage, string aggregateMessage, string fallbackMessage)
    {
        if (!string.IsNullOrWhiteSpace(specificMessage))
        {
            return specificMessage;
        }

        if (!string.IsNullOrWhiteSpace(aggregateMessage))
        {
            return aggregateMessage;
        }

        return fallbackMessage;
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
