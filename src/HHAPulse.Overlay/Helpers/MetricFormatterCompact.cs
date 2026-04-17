using HHAPulse.Overlay.Settings;
using HHAPulse.Overlay.Diagnostics;
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
            //
            // When driver frame generation is active (FrameGen flag set +
            // a valid AppFramesPerSecond < FramesPerSecond), the FPS cell
            // shows total/base as "120/60fps XeFG" (or "AFMF") so the user
            // sees both the effective presented rate, the real app render
            // rate, and the detected vendor. When frame gen is off,
            // AppFramesPerSecond is 0 and the cell collapses to the single
            // "60fps" display.
            OverlayPresetCatalog.Fps => FormatAvailable(snapshot, MetricFlags.Fps,
                FormatTotalOverAppFps(snapshot), "--fps"),

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

            // ── CPU ──
            OverlayPresetCatalog.CpuUsage => FormatAvailable(snapshot, MetricFlags.CpuUsage,
                $"{snapshot.Cpu.UsagePercent:0}%", "--%"),

            OverlayPresetCatalog.CpuTemp => FormatAvailable(snapshot, MetricFlags.CpuTemperature,
                snapshot.Cpu.TemperatureCelsius > 0
                    ? $"{snapshot.Cpu.TemperatureCelsius:0}\u00B0C"
                    : "--",
                "--"),

            OverlayPresetCatalog.CpuPower => FormatAvailable(snapshot, MetricFlags.CpuPower,
                snapshot.Cpu.PowerWatts > 0
                    ? $"{snapshot.Cpu.PowerWatts:0.0}W"
                    : "--",
                "--"),

            OverlayPresetCatalog.CpuClock => FormatAvailable(snapshot, MetricFlags.CpuClock,
                FormatCpuClock(snapshot),
                "--"),

            // ── GPU ──
            OverlayPresetCatalog.GpuUsage => FormatAvailable(snapshot, MetricFlags.GpuUsage,
                $"{snapshot.Gpu.UsagePercent:0}%", "--%"),

            OverlayPresetCatalog.GpuTemp => FormatAvailable(snapshot, MetricFlags.GpuTemperature,
                snapshot.Gpu.TemperatureCelsius > 0
                    ? $"{snapshot.Gpu.TemperatureCelsius:0}\u00B0C"
                    : "--",
                "--"),

            OverlayPresetCatalog.GpuClock => FormatAvailable(snapshot, MetricFlags.GpuClock,
                snapshot.Gpu.ClockMegahertz > 0
                    ? $"{snapshot.Gpu.ClockMegahertz:0}MHz"
                    : "--",
                "--"),

            OverlayPresetCatalog.GpuPower => FormatAvailable(snapshot, MetricFlags.GpuPower,
                FormatGpuPower(snapshot),
                "--"),

            OverlayPresetCatalog.GpuFan => FormatAvailable(snapshot, MetricFlags.Fan,
                FormatFan(snapshot),
                "--"),

            // ── Memory ──
            OverlayPresetCatalog.Ram => FormatAvailable(snapshot, MetricFlags.Memory,
                FormatMemoryCompact(snapshot.Memory.RamUsedMegabytes, snapshot.Memory.RamTotalMegabytes),
                "--"),

            OverlayPresetCatalog.Vram => FormatAvailable(snapshot, MetricFlags.Vram,
                FormatMemoryCompact(snapshot.Gpu.VramUsedMegabytes, snapshot.Gpu.VramTotalMegabytes),
                "--"),

            OverlayPresetCatalog.StorageTemp => FormatAvailable(snapshot, MetricFlags.StorageTemperature,
                FormatStorageTemp(snapshot),
                "--"),

            OverlayPresetCatalog.StorageWear => FormatAvailable(snapshot, MetricFlags.StorageWear,
                FormatStorageWear(snapshot),
                "--"),

            // ── System ──
            OverlayPresetCatalog.TotalPower => FormatAvailable(snapshot, MetricFlags.SystemPower,
                FormatTotalPower(snapshot),
                "--"),

            OverlayPresetCatalog.DeviceTemp => FormatAvailable(snapshot, MetricFlags.DeviceTemperature,
                snapshot.Dependencies.DeviceTemperatureCelsius > 0
                    ? $"{snapshot.Dependencies.DeviceTemperatureCelsius:0}\u00B0C"
                    : "--",
                "--"),

            OverlayPresetCatalog.RefreshRate => FormatAvailable(snapshot, MetricFlags.Display,
                FormatRefreshRate(snapshot.Display.RefreshRateHertz), "--"),

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

    private static string FormatGpuPower(TelemetrySnapshot snapshot)
    {
        return snapshot.Gpu.PowerWatts > 0 ? $"{snapshot.Gpu.PowerWatts:0.0}W" : "--";
    }

    private static string FormatCpuClock(TelemetrySnapshot snapshot)
    {
        var mhz = snapshot.CpuDetail.AggregateEffectiveMhz;
        if (mhz <= 0)
        {
            return "--";
        }

        return mhz >= 1000
            ? $"~{mhz / 1000.0:0.00}GHz"
            : $"~{mhz:0}MHz";
    }

    private static string FormatStorageTemp(TelemetrySnapshot snapshot)
    {
        return snapshot.Storage.TemperatureCelsius > 0
            ? $"{snapshot.Storage.TemperatureCelsius:0}\u00B0C"
            : "--";
    }

    private static string FormatStorageWear(TelemetrySnapshot snapshot)
    {
        return $"{snapshot.Storage.WearPercentUsed:0}%";
    }

    private static string FormatRefreshRate(double refreshRateHertz)
    {
        return refreshRateHertz > 1 ? $"{refreshRateHertz:0}" : "--";
    }

    private static string FpsValueWithUnit(double value) => value > 0 ? $"{value:0}fps" : "--fps";

    /// <summary>
    /// Renders the primary FPS cell. When frame generation is active AND the
    /// base (app-rendered) FPS is measurably lower than the total FPS, shows
    /// <c>"{total}/{base}fps"</c> (e.g. <c>"120/60fps"</c>). Otherwise, shows
    /// just the total <c>"{fps}fps"</c>.
    /// </summary>
    private static string FormatTotalOverAppFps(TelemetrySnapshot snapshot)
    {
        double total = snapshot.Performance.FramesPerSecond;
        double app = snapshot.Performance.AppFramesPerSecond;
        bool frameGenActive =
            snapshot.AvailableMetrics.HasFlag(MetricFlags.FrameGen)
            && app > 0
            && app < total
            && total - app >= 1.0;   // ignore sub-1-fps noise between streams

        if (total <= 0)
        {
            return "--fps";
        }

        if (!frameGenActive)
        {
            return $"{total:0}fps";
        }

        var badge = VendorBadge(snapshot.Performance.FrameGenVendor);
        return badge.Length > 0
            ? $"{total:0}/{app:0}fps {badge}"
            : $"{total:0}/{app:0}fps";
    }

    private static string VendorBadge(FrameGenVendor vendor) => vendor switch
    {
        FrameGenVendor.IntelXeFG => "XeFG",
        FrameGenVendor.AmdAFMF => "AFMF",
        _ => string.Empty,
    };

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
        // Path 1 — battery discharge rate when unplugged. Whole-device.
        if (SystemPowerValidator.HasValidatedBatteryPower(snapshot))
        {
            return $"{snapshot.Battery.DischargeWatts:0.0}W";
        }

        // Path 2 — on AC, PKG + DRAM from Intel RAPL. Real measured
        // Intel-documented SoC + memory sum. Excludes display, SSD,
        // radios, fans, and platform losses. See SystemPowerValidator.
        return "--";
    }

    private static string FormatFan(TelemetrySnapshot snapshot)
    {
        var fans = snapshot.Dependencies.FanRpms
            .Where(rpm => rpm > 0)
            .ToArray();
        if (fans.Length > 0)
        {
            return string.Join("/", fans.Select(rpm => rpm.ToString("0"))) + "rpm";
        }

        return snapshot.Gpu.FanRpm > 0 ? $"{snapshot.Gpu.FanRpm:0}rpm" : "--";
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
