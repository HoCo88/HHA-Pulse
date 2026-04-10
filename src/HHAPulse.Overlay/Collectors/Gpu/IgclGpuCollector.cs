using System.Runtime.InteropServices;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Collectors.Gpu;

/// <summary>
/// Reads GPU temperature, power, fan speed, and clock from Intel GPUs
/// via the IGCL native wrapper in HHAPulse.Native.dll.
/// Power is pre-computed as watts by the native layer (energy deltas).
/// Fan is typically unavailable on integrated GPUs.
/// </summary>
public sealed class IgclGpuCollector : IMetricCollector, IDisposable
{
    private bool _initialized;
    private bool _loggedFailure;
    private bool _disposed;
    private bool _firstTick = true;
    private bool _loggedValidFlagsDiagnostic;

    public string Name => "GPU Sensors (IGCL)";

    public bool IsAvailable { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            int result = HhaPulseIgclInit();
            if (result == 0)
            {
                _initialized = true;
                IsAvailable = true;
                AppLogger.Info("IGCL: Initialized successfully.");
            }
            else
            {
                LogOnce($"IGCL: Init returned {result}. Intel GPU telemetry unavailable.");
            }
        }
        catch (DllNotFoundException)
        {
            LogOnce("IGCL: HHAPulse.Native.dll not found. Intel GPU telemetry unavailable.");
        }
        catch (Exception ex)
        {
            LogOnce($"IGCL: Init exception: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        if (!_initialized)
            return Task.CompletedTask;

        var reading = new HhaPulseGpuReading();
        int result = HhaPulseIgclReadGpu(ref reading);
        if (result != 0)
        {
            LogOnce($"IGCL: ReadGpu returned {result}.");
            return Task.CompletedTask;
        }

        snapshot.Dependencies.GpuTelemetryAvailable = true;
        snapshot.Dependencies.GpuTelemetrySource = "IGCL";
        snapshot.Dependencies.GpuTelemetryStatusMessage = "IGCL GPU telemetry initialized.";

        // One-shot diagnostic: on the first successful read, log which IGCL
        // telemetry fields the native layer actually populated AND which
        // ctl_power_telemetry_t.*bSupported flags the driver reported as
        // true. The extended diagnostic is essential because on Lunar Lake
        // Arc 140V the primary fields (gpuCurrentTemperature, gpuEnergyCounter,
        // fanSpeed) return bSupported=false, but the secondary fields
        // (gpuVrTemp, saVrTemp, totalCardEnergyCounter) might still work.
        // The log shows which specific secondary path produced the reading.
        if (!_loggedValidFlagsDiagnostic)
        {
            _loggedValidFlagsDiagnostic = true;
            var present = new List<string>();
            if ((reading.ValidFlags & ValidFlagTemp) != 0) present.Add("TEMP");
            if ((reading.ValidFlags & ValidFlagPower) != 0) present.Add("POWER");
            if ((reading.ValidFlags & ValidFlagFan) != 0) present.Add("FAN");
            if ((reading.ValidFlags & ValidFlagClock) != 0) present.Add("CLOCK");
            var presentList = present.Count > 0 ? string.Join("+", present) : "<none>";

            var supportedFields = new List<string>();
            if ((reading.IgclFieldSupportMask & IgclFieldGpuCurrentTemp) != 0) supportedFields.Add("gpuCurrentTemperature");
            if ((reading.IgclFieldSupportMask & IgclFieldGpuVrTemp) != 0) supportedFields.Add("gpuVrTemp");
            if ((reading.IgclFieldSupportMask & IgclFieldSaVrTemp) != 0) supportedFields.Add("saVrTemp");
            if ((reading.IgclFieldSupportMask & IgclFieldGpuEnergyCounter) != 0) supportedFields.Add("gpuEnergyCounter");
            if ((reading.IgclFieldSupportMask & IgclFieldTotalCardEnergy) != 0) supportedFields.Add("totalCardEnergyCounter");
            if ((reading.IgclFieldSupportMask & IgclFieldGpuCurrentClock) != 0) supportedFields.Add("gpuCurrentClockFrequency");
            if ((reading.IgclFieldSupportMask & IgclFieldGpuEffectiveClock) != 0) supportedFields.Add("gpuEffectiveClock");
            if ((reading.IgclFieldSupportMask & IgclFieldFanSpeedAny) != 0) supportedFields.Add("fanSpeed[*]");
            if ((reading.IgclFieldSupportMask & IgclFieldDedicatedTempApi) != 0) supportedFields.Add("ctlTemperatureGetState");
            if ((reading.IgclFieldSupportMask & IgclFieldDedicatedFanApi) != 0) supportedFields.Add("ctlFanGetState");
            var supportedList = supportedFields.Count > 0 ? string.Join(", ", supportedFields) : "<none>";

            var tempSourceName = reading.TemperatureSourceKind switch
            {
                TempSourceGpuCurrent => "gpuCurrentTemperature",
                TempSourceGpuVr => "gpuVrTemp",
                TempSourceSaVr => "saVrTemp",
                TempSourceDedicatedSensorEnum => "ctlTemperatureGetState",
                _ => "<none>"
            };

            AppLogger.Info($"IGCL: First successful read. validFlags=0x{reading.ValidFlags:X2} present=[{presentList}] supportMask=0x{reading.IgclFieldSupportMask:X4} bSupported=[{supportedList}] tempSource={tempSourceName} rawTemp={reading.TemperatureCelsius:0.00}C rawPower={reading.PowerWatts:0.000}W rawFan={reading.FanRpm}rpm rawClock={reading.ClockMegahertz:0.0}MHz powerSourceKind={reading.PowerSourceKind}.");
        }

        if ((reading.ValidFlags & ValidFlagTemp) != 0)
        {
            snapshot.AvailableMetrics |= MetricFlags.GpuTemperature;
            snapshot.Gpu.TemperatureCelsius = reading.TemperatureCelsius;
            snapshot.Dependencies.GpuTemperatureSource = "IGCL";
            var tempSource = reading.TemperatureSourceKind == TempSourceDedicatedSensorEnum
                ? "ctlTemperatureGetState"
                : "ctlPowerTelemetryGet";
            snapshot.Dependencies.GpuTemperatureStatusMessage = $"GPU temperature from Intel IGCL {tempSource}.";
            MeasurementTraceRecorder.Record(snapshot, "gpu_temp", nameof(IgclGpuCollector), $"IGCL {tempSource}", true, $"{reading.TemperatureCelsius:0.0}C", $"validFlags=0x{reading.ValidFlags:X}; tempSourceKind={reading.TemperatureSourceKind}; supportMask=0x{reading.IgclFieldSupportMask:X}", snapshot.Dependencies.GpuTemperatureStatusMessage, tempSource, $"{reading.TemperatureCelsius:0.000}", "C", "IGCL validated Celsius unit", $"{reading.TemperatureCelsius:0.000}", "C", TelemetryValidationState.HardwareValidationPending, "IGCL returned a bounded temperature; handheld hardware validation still pending.");
        }

        // First tick after init has no energy delta — skip power to avoid
        // reporting zero watts as a real reading.
        if (_firstTick)
        {
            _firstTick = false;
        }
        else if ((reading.ValidFlags & ValidFlagPower) != 0)
        {
            snapshot.AvailableMetrics |= MetricFlags.GpuPower;
            snapshot.Gpu.PowerWatts = reading.PowerWatts;
            snapshot.Dependencies.GpuPowerSource = "IGCL";
            snapshot.Dependencies.GpuPowerStatusMessage = $"GPU power from Intel IGCL gpuEnergyCounter/timeStamp delta ({reading.PowerWatts:0.0}W).";
            MeasurementTraceRecorder.Record(
                snapshot,
                "gpu_power",
                nameof(IgclGpuCollector),
                "IGCL gpuEnergyCounter/timeStamp",
                true,
                $"{reading.PowerWatts:0.0}W",
                $"validFlags=0x{reading.ValidFlags:X}; powerSourceKind={reading.PowerSourceKind}",
                snapshot.Dependencies.GpuPowerStatusMessage,
                "ctlPowerTelemetryGet",
                $"{reading.PowerWatts:0.000}",
                "W",
                "gpuEnergyCounter joules delta / timeStamp seconds delta",
                $"{reading.PowerWatts:0.000}",
                "W",
                TelemetryValidationState.HardwareValidationPending,
                "IGCL produced a valid positive energy/time watt delta; handheld hardware validation still pending.");
        }

        // Fan is typically unavailable on iGPU (validFlags bit 2 = 0).
        if ((reading.ValidFlags & ValidFlagFan) != 0)
        {
            snapshot.AvailableMetrics |= MetricFlags.Fan;
            snapshot.Gpu.FanRpm = reading.FanRpm;
            snapshot.Dependencies.FanRpms = new[] { reading.FanRpm };
            snapshot.Dependencies.DeviceFanSource = "IGCL";
            snapshot.Dependencies.DeviceFanStatusMessage = "Device fan tachometer from Intel IGCL.";
            snapshot.Dependencies.GpuFanSource = "IGCL";
            snapshot.Dependencies.GpuFanStatusMessage = snapshot.Dependencies.DeviceFanStatusMessage;
            var fanSource = (reading.IgclFieldSupportMask & IgclFieldDedicatedFanApi) != 0 ? "ctlFanGetState" : "ctlPowerTelemetryGet";
            MeasurementTraceRecorder.Record(snapshot, "gpu_fan", nameof(IgclGpuCollector), $"IGCL {fanSource}", true, $"{reading.FanRpm}rpm", $"validFlags=0x{reading.ValidFlags:X}; supportMask=0x{reading.IgclFieldSupportMask:X}", snapshot.Dependencies.GpuFanStatusMessage, fanSource, reading.FanRpm.ToString(), "rpm", "IGCL validated RPM unit", reading.FanRpm.ToString(), "rpm", TelemetryValidationState.HardwareValidationPending, "IGCL returned a positive fan RPM; handheld hardware validation still pending.");
        }

        if ((reading.ValidFlags & ValidFlagClock) != 0)
        {
            snapshot.AvailableMetrics |= MetricFlags.GpuClock;
            snapshot.Gpu.ClockMegahertz = reading.ClockMegahertz;
            snapshot.Dependencies.GpuClockSource = "IGCL";
            snapshot.Dependencies.GpuClockStatusMessage = "GPU clock from Intel IGCL.";
            MeasurementTraceRecorder.Record(snapshot, "gpu_clock", nameof(IgclGpuCollector), "IGCL gpuCurrentClockFrequency", true, $"{reading.ClockMegahertz:0.0}MHz", $"validFlags=0x{reading.ValidFlags:X}", snapshot.Dependencies.GpuClockStatusMessage, "ctlPowerTelemetryGet", $"{reading.ClockMegahertz:0.000}", "MHz", "IGCL validated MHz unit", $"{reading.ClockMegahertz:0.000}", "MHz", TelemetryValidationState.HardwareValidationPending, "IGCL returned a positive clock; handheld hardware validation still pending.");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_initialized)
        {
            try
            {
                HhaPulseIgclShutdown();
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    private void LogOnce(string message)
    {
        if (_loggedFailure)
            return;

        _loggedFailure = true;
        AppLogger.Info(message);
    }

    // ── Valid-flag bitmask constants (match NativeTelemetry.h) ──

    private const uint ValidFlagTemp  = 0x01;
    private const uint ValidFlagPower = 0x02;
    private const uint ValidFlagFan   = 0x04;
    private const uint ValidFlagClock = 0x08;

    // ── Temperature source constants (match NativeTelemetry.h HHAPULSE_GPU_TEMP_SOURCE_*) ──

    private const uint TempSourceNone = 0;
    private const uint TempSourceGpuCurrent = 1;
    private const uint TempSourceGpuVr = 2;
    private const uint TempSourceSaVr = 3;
    private const uint TempSourceDedicatedSensorEnum = 4;

    // ── IGCL field bSupported bitmap constants (match NativeTelemetry.h HHAPULSE_IGCL_FIELD_*) ──

    private const uint IgclFieldGpuCurrentTemp = 0x01;
    private const uint IgclFieldGpuVrTemp = 0x02;
    private const uint IgclFieldSaVrTemp = 0x04;
    private const uint IgclFieldGpuEnergyCounter = 0x08;
    private const uint IgclFieldTotalCardEnergy = 0x10;
    private const uint IgclFieldGpuCurrentClock = 0x20;
    private const uint IgclFieldGpuEffectiveClock = 0x40;
    private const uint IgclFieldFanSpeedAny = 0x80;
    private const uint IgclFieldDedicatedTempApi = 0x100;
    private const uint IgclFieldDedicatedFanApi = 0x200;

    // ── P/Invoke ──

    [DllImport("HHAPulse.Native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int HhaPulseIgclInit();

    [DllImport("HHAPulse.Native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int HhaPulseIgclReadGpu(ref HhaPulseGpuReading reading);

    [DllImport("HHAPulse.Native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void HhaPulseIgclShutdown();

    // ── Shared struct (matches NativeTelemetry.h layout) ──

    [StructLayout(LayoutKind.Sequential)]
    private struct HhaPulseGpuReading
    {
        public double TemperatureCelsius;  // offset 0
        public double PowerWatts;          // offset 8
        public int FanRpm;                 // offset 16
        // 4 bytes padding (natural alignment for next double)
        public double ClockMegahertz;      // offset 24
        public uint ValidFlags;            // offset 32
        public uint PowerSourceKind;       // offset 36
        public uint TemperatureSourceKind; // offset 40
        public uint IgclFieldSupportMask;  // offset 44
    }
}
