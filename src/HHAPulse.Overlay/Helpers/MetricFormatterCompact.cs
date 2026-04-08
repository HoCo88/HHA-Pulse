using HHAPulse.Overlay.Settings;
using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Helpers;

/// <summary>
/// Value-only formatter for the single-row HUD bar.
/// Labels are rendered separately by the TopBarControl.
/// </summary>
public static class MetricFormatterCompact
{
    public static string FormatValue(string metricId, TelemetrySnapshot snapshot)
    {
        return metricId switch
        {
            // ── Performance ──
            OverlayPresetCatalog.Fps => FormatAvailable(snapshot, MetricFlags.Fps,
                FpsValueWithUnit(snapshot.Performance.FramesPerSecond), "--fps"),

            OverlayPresetCatalog.AvgFps => FormatAvailable(snapshot, MetricFlags.Fps,
                FpsValueWithUnit(snapshot.Performance.AverageFramesPerSecond), "--fps"),

            OverlayPresetCatalog.OnePercentLow => FormatAvailable(snapshot, MetricFlags.Fps,
                FpsValueWithUnit(snapshot.Performance.OnePercentLowFramesPerSecond), "--fps"),

            OverlayPresetCatalog.ZeroPointOneLow => FormatAvailable(snapshot, MetricFlags.Fps,
                FpsValueWithUnit(snapshot.Performance.ZeroPointOnePercentLowFramesPerSecond), "--fps"),

            OverlayPresetCatalog.FrameTime => FormatAvailable(snapshot, MetricFlags.FrameTime,
                snapshot.Performance.FrameTimeMilliseconds > 0
                    ? $"{snapshot.Performance.FrameTimeMilliseconds:0.0}ms"
                    : "--",
                "--"),

            OverlayPresetCatalog.FrameGenFps => FormatAvailable(snapshot, MetricFlags.FrameGen,
                FpsValueWithUnit(snapshot.Performance.FramesPerSecond), "--fps"),

            OverlayPresetCatalog.InputLatency => FormatAvailable(snapshot, MetricFlags.InputLatency,
                snapshot.Performance.GpuBusyMilliseconds > 0
                    ? $"{snapshot.Performance.GpuBusyMilliseconds:0.0}ms"
                    : "--",
                "--"),

            // ── CPU ──
            OverlayPresetCatalog.CpuUsage => FormatAvailable(snapshot, MetricFlags.CpuUsage,
                $"{snapshot.Cpu.UsagePercent:0}%", "--%"),

            OverlayPresetCatalog.CpuPower => FormatAvailable(snapshot, MetricFlags.CpuPower,
                snapshot.Cpu.PowerWatts > 0
                    ? $"{snapshot.Cpu.PowerWatts:0.0}W"
                    : "--",
                "--"),

            // ── GPU ──
            OverlayPresetCatalog.GpuUsage => FormatAvailable(snapshot, MetricFlags.GpuUsage,
                $"{snapshot.Gpu.UsagePercent:0}%", "--%"),

            OverlayPresetCatalog.GpuTemp => FormatAvailable(snapshot, MetricFlags.GpuTemperature,
                snapshot.Gpu.TemperatureCelsius > 0
                    ? $"{snapshot.Gpu.TemperatureCelsius:0}\u00B0C"
                    : "--",
                "--"),

            // D3DKMT reports power as "tenths of %" per MS docs. Value/10 = % of TDP.
            // If value > 100 it's likely watts from a vendor driver, show as W.
            OverlayPresetCatalog.GpuPower => FormatAvailable(snapshot, MetricFlags.GpuPower,
                FormatGpuPower(snapshot.Gpu.PowerWatts),
                "--"),

            // ── Memory ──
            OverlayPresetCatalog.Ram => FormatAvailable(snapshot, MetricFlags.Memory,
                FormatMemoryCompact(snapshot.Memory.RamUsedMegabytes, snapshot.Memory.RamTotalMegabytes),
                "--"),

            OverlayPresetCatalog.Vram => FormatAvailable(snapshot, MetricFlags.Vram,
                FormatMemoryCompact(snapshot.Gpu.VramUsedMegabytes, snapshot.Gpu.VramTotalMegabytes),
                "--"),

            // ── System ──
            OverlayPresetCatalog.TotalPower => FormatAvailable(snapshot, MetricFlags.SystemPower,
                FormatTotalPower(snapshot),
                "--"),

            OverlayPresetCatalog.RefreshRate => FormatAvailable(snapshot, MetricFlags.Display,
                $"{snapshot.Display.RefreshRateHertz:0}", "--"),

            OverlayPresetCatalog.Battery => FormatAvailable(snapshot, MetricFlags.Battery,
                FormatBatteryCompact(snapshot), "--"),

            _ => "--"
        };
    }

    private static string FormatAvailable(TelemetrySnapshot snapshot, MetricFlags required, string value, string fallback)
    {
        return snapshot.AvailableMetrics.HasFlag(required) ? value : fallback;
    }

    private static string FpsValue(double value) => value > 0 ? $"{value:0}" : "--";

    private static string FormatGpuPower(double value)
    {
        if (value <= 0) return "--";
        // Heuristic: D3DKMT says "% of TDP". Values 0-100 are %.
        // If a vendor driver reports watts, values > 100 are common (e.g. 150W).
        // Below 100 → show as % TDP. Above 100 → likely watts.
        return value <= 100 ? $"{value:0}%" : $"{value:0.0}W";
    }

    private static string FpsValueWithUnit(double value) => value > 0 ? $"{value:0}fps" : "--fps";

    private static string FormatMemoryCompact(double usedMb, double totalMb)
    {
        if (totalMb >= 1024)
        {
            return $"{usedMb / 1024:0.0}/{totalMb / 1024:0.0}G";
        }

        return $"{usedMb:0}/{totalMb:0}M";
    }

    private static string FormatTotalPower(TelemetrySnapshot snapshot)
    {
        // If battery is discharging, that IS total system power.
        if (!snapshot.Battery.IsCharging && snapshot.Battery.DischargeWatts > 0)
        {
            return $"{snapshot.Battery.DischargeWatts:0.0}W";
        }

        // Sum available component power readings.
        var total = snapshot.Cpu.PowerWatts + snapshot.Gpu.PowerWatts;
        return total > 0 ? $"{total:0.0}W" : "--";
    }

    private static string FormatBatteryCompact(TelemetrySnapshot snapshot)
    {
        var pct = snapshot.Battery.ChargePercent >= 0
            ? $"{snapshot.Battery.ChargePercent:0}%"
            : "--%";

        if (snapshot.Battery.IsCharging)
        {
            if (snapshot.Battery.ChargeWatts > 0)
            {
                return $"{pct} +{snapshot.Battery.ChargeWatts:0.0}W";
            }

            return $"{pct} AC";
        }

        // Discharging: show time remaining if available, else watts.
        if (snapshot.Battery.EstimatedMinutesRemaining > 0)
        {
            var total = (int)Math.Round(snapshot.Battery.EstimatedMinutesRemaining);
            var hours = total / 60;
            var mins = total % 60;
            var time = hours > 0 ? $"{hours}h{mins:00}m" : $"{mins}m";

            if (snapshot.Battery.DischargeWatts > 0)
            {
                return $"{pct} {time} {snapshot.Battery.DischargeWatts:0.0}W";
            }

            return $"{pct} {time}";
        }

        if (snapshot.Battery.DischargeWatts > 0)
        {
            return $"{pct} {snapshot.Battery.DischargeWatts:0.0}W";
        }

        return pct;
    }
}
