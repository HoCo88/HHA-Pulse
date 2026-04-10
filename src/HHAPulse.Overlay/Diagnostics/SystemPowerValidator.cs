using HHAPulse.Shared.Models;
using HHAPulse.Overlay.Settings;

namespace HHAPulse.Overlay.Diagnostics;

public static class SystemPowerValidator
{
    public static void ApplyTo(TelemetrySnapshot snapshot)
    {
        var componentValidated = HasValidatedComponentSum(snapshot);
        snapshot.Dependencies.ComponentPowerSumWatts = componentValidated
            ? snapshot.Cpu.PowerWatts + snapshot.Gpu.PowerWatts
            : 0;
        snapshot.Dependencies.ComponentPowerStatusMessage = componentValidated
            ? "component_sum_watts is diagnostics-only and excludes display, memory, SSD, fans, radios, and platform losses."
            : "component_sum_watts unavailable until both CPU and GPU watts are validated.";

        if (HasValidatedBatteryPower(snapshot))
        {
            snapshot.AvailableMetrics |= MetricFlags.SystemPower;
            MeasurementTraceRecorder.Record(
                snapshot,
                OverlayPresetCatalog.TotalPower,
                nameof(SystemPowerValidator),
                "CallNtPowerInformation(SystemBatteryState.Rate)",
                true,
                $"{snapshot.Battery.DischargeWatts:0.0}W",
                $"batteryDischargeWatts={snapshot.Battery.DischargeWatts:0.000}; component_sum_watts={snapshot.Dependencies.ComponentPowerSumWatts:0.000}; whole-device battery discharge selected",
                "Whole-device power from battery discharge rate while unplugged.",
                "SYSTEM_BATTERY_STATE.Rate",
                $"{snapshot.Battery.DischargeWatts:0.000}",
                "W",
                "negative mW discharge rate / -1000",
                $"{snapshot.Battery.DischargeWatts:0.000}",
                "W",
                TelemetryValidationState.Verified,
                "Battery is discharging and Windows reported a positive discharge watt rate.");
            return;
        }

        snapshot.AvailableMetrics &= ~MetricFlags.SystemPower;
        MeasurementTraceRecorder.Record(
            snapshot,
            OverlayPresetCatalog.TotalPower,
            nameof(SystemPowerValidator),
            "CallNtPowerInformation(SystemBatteryState.Rate)",
            false,
            "--",
            $"batteryFlag={snapshot.AvailableMetrics.HasFlag(MetricFlags.Battery)}; charging={snapshot.Battery.IsCharging}; batteryDischargeWatts={snapshot.Battery.DischargeWatts:0.000}; component_sum_watts={snapshot.Dependencies.ComponentPowerSumWatts:0.000}",
            "Whole-device power is available only from battery discharge watts in this phase.",
            "SYSTEM_BATTERY_STATE.Rate",
            snapshot.Battery.DischargeWatts > 0 ? $"{snapshot.Battery.DischargeWatts:0.000}" : "--",
            snapshot.Battery.DischargeWatts > 0 ? "W" : "--",
            "no conversion accepted",
            "--",
            "W",
            TelemetryValidationState.Unavailable,
            snapshot.Battery.IsCharging
                ? "Device is on AC/charging; battery discharge is not a whole-device draw source."
                : "Windows did not report a positive battery discharge watt rate.");
    }

    public static bool HasValidatedBatteryPower(TelemetrySnapshot snapshot)
    {
        return snapshot.AvailableMetrics.HasFlag(MetricFlags.Battery)
            && !snapshot.Battery.IsCharging
            && snapshot.Battery.DischargeWatts > 0;
    }

    public static bool HasValidatedComponentSum(TelemetrySnapshot snapshot)
    {
        return snapshot.AvailableMetrics.HasFlag(MetricFlags.CpuPower)
            && snapshot.Cpu.PowerWatts > 0
            && snapshot.AvailableMetrics.HasFlag(MetricFlags.GpuPower)
            && snapshot.Gpu.PowerWatts > 0;
    }
}
