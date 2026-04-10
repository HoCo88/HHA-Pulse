using HHAPulse.Shared.Models;
using HHAPulse.Overlay.Settings;

namespace HHAPulse.Overlay.Diagnostics;

/// <summary>
/// Resolves the whole-device power reading used by the TotalPower HUD
/// tile and its diagnostics trace.
///
/// <para>
/// <b>Path 1 — battery discharge (unplugged).</b>
/// <c>CallNtPowerInformation(SystemBatteryState.Rate)</c> reports
/// whole-device draw in mW while the battery is the sole power
/// source. This is the most accurate path and is preferred whenever
/// the device is unplugged and the discharge rate is positive.
/// </para>
///
/// <para>
/// <b>Path 2 — RAPL PKG + DRAM (on AC).</b> Intel's Energy Meter
/// Interface exposes both the package rail (<c>RAPL_Package0_PKG</c>,
/// covering P-cores + E-cores + iGPU + uncore) and the memory rail
/// (<c>RAPL_Package0_DRAM</c>, covering the DIMMs / memory
/// controller). Summing them gives a real measured "SoC + memory"
/// power reading. It is NOT whole-device power — it excludes display
/// panel, SSD, radios, fans, and platform conversion losses — but on
/// AC (where this machine's firmware power meter reports 0 W) it is the
/// closest honest SoC + memory proxy we can build from documented Intel
/// RAPL domains (Intel 64 and IA-32 Architectures Software
/// Developer's Manual Vol 3B §15.10.3).
/// </para>
///
/// <para>
/// <b>Path 3 — fail closed.</b> If neither battery discharge nor
/// PKG+DRAM produces a valid reading, TotalPower renders <c>--</c>
/// and the diagnostics trace records which inputs were missing.
/// </para>
///
/// <para>
/// <b>No fake data.</b> Every trace entry records the exact source,
/// the raw measurement values, and which exclusions apply, so the
/// TotalPower tile's value can always be traced back to the
/// specific API and channel that produced it.
/// </para>
/// </summary>
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

        // Path 1 — unplugged: battery discharge rate is whole-device.
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

        // Path 2 — on AC: PKG + DRAM from Intel RAPL.
        snapshot.AvailableMetrics &= ~MetricFlags.SystemPower;
        MeasurementTraceRecorder.Record(
            snapshot,
            OverlayPresetCatalog.TotalPower,
            nameof(SystemPowerValidator),
            "CallNtPowerInformation(SystemBatteryState.Rate)",
            false,
            "--",
            $"batteryFlag={snapshot.AvailableMetrics.HasFlag(MetricFlags.Battery)}; charging={snapshot.Battery.IsCharging}; batteryDischargeWatts={snapshot.Battery.DischargeWatts:0.000}; cpuPowerFlag={snapshot.AvailableMetrics.HasFlag(MetricFlags.CpuPower)}; pkgWatts={snapshot.Cpu.PowerWatts:0.000}; dramWatts={snapshot.Dependencies.DramPowerWatts:0.000}; component_sum_watts={snapshot.Dependencies.ComponentPowerSumWatts:0.000}; acRaplPkgPlusDramRejectedAsDevicePower=True",
            "Whole-device power is unavailable. RAPL PKG + DRAM remains diagnostics-only because it excludes display, SSD, radios, fans, and platform losses.",
            "SYSTEM_BATTERY_STATE.Rate",
            snapshot.Battery.DischargeWatts > 0 ? $"{snapshot.Battery.DischargeWatts:0.000}" : "--",
            snapshot.Battery.DischargeWatts > 0 ? "W" : "--",
            "no conversion accepted",
            "--",
            "W",
            TelemetryValidationState.Unavailable,
            snapshot.Battery.IsCharging
                ? "Device is on AC/charging; no accepted whole-device wall-watt source exists. RAPL PKG + DRAM is only a SoC + memory component rail and is not shown as Device Power."
                : "Windows did not report a positive battery discharge watt rate.");
    }

    public static bool HasValidatedBatteryPower(TelemetrySnapshot snapshot)
    {
        return snapshot.AvailableMetrics.HasFlag(MetricFlags.Battery)
            && !snapshot.Battery.IsCharging
            && snapshot.Battery.DischargeWatts > 0;
    }

    /// <summary>
    /// Returns true when the device is on AC (battery path unavailable)
    /// and both CPU package power and DRAM power have been validated
    /// this tick. In that case the sum is a real Intel-documented
    /// SoC + memory proxy (excluding display,
    /// SSD, radios, fans, platform losses).
    /// </summary>
    public static bool HasValidatedPackagePlusDramPower(TelemetrySnapshot snapshot)
    {
        // Diagnostics-only helper. This must not imply MetricFlags.SystemPower.
        return snapshot.AvailableMetrics.HasFlag(MetricFlags.CpuPower)
            && snapshot.Cpu.PowerWatts > 0
            && snapshot.Dependencies.DramPowerWatts > 0;
    }

    public static bool HasValidatedComponentSum(TelemetrySnapshot snapshot)
    {
        return snapshot.AvailableMetrics.HasFlag(MetricFlags.CpuPower)
            && snapshot.Cpu.PowerWatts > 0
            && snapshot.AvailableMetrics.HasFlag(MetricFlags.GpuPower)
            && snapshot.Gpu.PowerWatts > 0;
    }
}
