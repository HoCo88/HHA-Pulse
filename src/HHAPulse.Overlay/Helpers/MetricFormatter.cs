using HHAPulse.Overlay.Settings;
using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Helpers;

public static class MetricFormatter
{
    public static string Fps(double value) => value > 0 ? $"{value:0} FPS" : "-- FPS";

    public static string Temperature(double value) => value > 0 ? $"{value:0}C" : "--C";

    public static string Watts(double value) => value > 0 ? $"{value:0.0}W" : "--W";

    public static string Percent(double value) => value >= 0 ? $"{value:0}%" : "--%";

    public static string Minutes(double value)
    {
        if (value <= 0)
        {
            return "--";
        }

        var totalMinutes = (int)Math.Round(value);
        return $"{totalMinutes / 60}h {totalMinutes % 60:00}m";
    }

    public static string FormatMetric(string metricId, TelemetrySnapshot snapshot)
    {
        return metricId switch
        {
            OverlayPresetCatalog.Fps => FormatAvailable(snapshot, MetricFlags.Fps, $"FPS {FpsValue(snapshot.Performance.FramesPerSecond)}", "FPS --"),
            OverlayPresetCatalog.OnePercentLow => FormatAvailable(snapshot, MetricFlags.Fps, $"1% {FpsValue(snapshot.Performance.OnePercentLowFramesPerSecond)}", "1% --"),
            OverlayPresetCatalog.FrameTime => FormatAvailable(snapshot, MetricFlags.FrameTime, $"FT {snapshot.Performance.FrameTimeMilliseconds:0.0} ms", "FT --"),
            OverlayPresetCatalog.Battery => FormatAvailable(snapshot, MetricFlags.Battery, FormatBatteryPercent(snapshot), "BAT --"),
            OverlayPresetCatalog.CpuUsage => FormatAvailable(snapshot, MetricFlags.CpuUsage, $"CPU {Percent(snapshot.Cpu.UsagePercent)}", "CPU --"),
            OverlayPresetCatalog.GpuUsage => FormatAvailable(snapshot, MetricFlags.GpuUsage, $"GPU {Percent(snapshot.Gpu.UsagePercent)}", "GPU --"),
            OverlayPresetCatalog.GpuTemp => FormatAvailable(snapshot, MetricFlags.GpuTemperature, $"GPU {Temperature(snapshot.Gpu.TemperatureCelsius)}", "GPU --"),
            OverlayPresetCatalog.GpuClock => FormatAvailable(snapshot, MetricFlags.GpuClock, $"GPU {snapshot.Gpu.ClockMegahertz:0} MHz", "GPU --"),
            OverlayPresetCatalog.GpuPower => FormatAvailable(snapshot, MetricFlags.GpuPower, $"GPU {Watts(snapshot.Gpu.PowerWatts)}", "GPU --"),
            OverlayPresetCatalog.GpuFan => FormatAvailable(snapshot, MetricFlags.Fan, $"FAN {snapshot.Gpu.FanRpm:0} RPM", "FAN --"),
            OverlayPresetCatalog.Ram => FormatAvailable(snapshot, MetricFlags.Memory, $"RAM {snapshot.Memory.RamUsedMegabytes:0}/{snapshot.Memory.RamTotalMegabytes:0} MB", "RAM --"),
            OverlayPresetCatalog.Vram => FormatAvailable(snapshot, MetricFlags.Vram, $"VRAM {snapshot.Gpu.VramUsedMegabytes:0}/{snapshot.Gpu.VramTotalMegabytes:0} MB", "VRAM --"),
            OverlayPresetCatalog.RefreshRate => FormatAvailable(snapshot, MetricFlags.Display, $"Hz {snapshot.Display.RefreshRateHertz:0}", "Hz --"),
            _ => metricId
        };
    }

    public static string DependencySummary(TelemetrySnapshot snapshot)
    {
        var parts = new List<string>();
        if (snapshot.Dependencies.CaptureServiceConnected)
            parts.Add("FPS capture connected");
        if (snapshot.Dependencies.GpuTelemetryAvailable)
            parts.Add("GPU telemetry connected");
        return parts.Count > 0 ? string.Join(" | ", parts) : "Using built-in Windows telemetry";
    }

    private static string FormatAvailable(TelemetrySnapshot snapshot, MetricFlags requiredMetric, string value, string unavailableText)
    {
        return snapshot.AvailableMetrics.HasFlag(requiredMetric) ? value : unavailableText;
    }

    private static string FpsValue(double value)
    {
        return value > 0 ? $"{value:0}" : "--";
    }

    private static string FormatBatteryPercent(TelemetrySnapshot snapshot)
    {
        var pct = Percent(snapshot.Battery.ChargePercent);
        return snapshot.Battery.IsCharging ? $"BAT {pct} AC" : $"BAT {pct}";
    }

    private static string FormatBatteryWatts(TelemetrySnapshot snapshot)
    {
        if (snapshot.Battery.IsCharging && snapshot.Battery.ChargeWatts > 0)
        {
            return $"BAT +{Watts(snapshot.Battery.ChargeWatts)}";
        }

        return $"BAT {Watts(snapshot.Battery.DischargeWatts)}";
    }
}
