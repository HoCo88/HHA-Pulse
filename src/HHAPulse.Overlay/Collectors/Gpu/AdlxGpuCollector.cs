using System.Runtime.InteropServices;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Collectors.Gpu;

/// <summary>
/// Reads GPU temperature, power, fan speed, and clock from AMD GPUs
/// via the ADLX native wrapper in HHAPulse.Native.dll.
/// </summary>
public sealed class AdlxGpuCollector : IMetricCollector, IDisposable
{
    private bool _initialized;
    private bool _loggedFailure;
    private bool _disposed;

    public string Name => "GPU Sensors (ADLX)";

    public bool IsAvailable { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            int result = HhaPulseAdlxInit();
            if (result == 0)
            {
                _initialized = true;
                IsAvailable = true;
                AppLogger.Info("ADLX: Initialized successfully.");
            }
            else
            {
                LogOnce($"ADLX: Init returned {result}. AMD GPU telemetry unavailable.");
            }
        }
        catch (DllNotFoundException)
        {
            LogOnce("ADLX: HHAPulse.Native.dll not found. AMD GPU telemetry unavailable.");
        }
        catch (Exception ex)
        {
            LogOnce($"ADLX: Init exception: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        if (!_initialized)
            return Task.CompletedTask;

        var reading = new HhaPulseGpuReading();
        int result = HhaPulseAdlxReadGpu(ref reading);
        if (result != 0)
        {
            LogOnce($"ADLX: ReadGpu returned {result}.");
            return Task.CompletedTask;
        }

        snapshot.Dependencies.GpuTelemetryAvailable = true;
        snapshot.Dependencies.GpuTelemetrySource = "ADLX";
        snapshot.Dependencies.GpuTelemetryStatusMessage = "ADLX GPU telemetry initialized.";

        if ((reading.ValidFlags & ValidFlagTemp) != 0)
        {
            snapshot.AvailableMetrics |= MetricFlags.GpuTemperature;
            snapshot.Gpu.TemperatureCelsius = reading.TemperatureCelsius;
            snapshot.Dependencies.GpuTemperatureSource = "ADLX";
            snapshot.Dependencies.GpuTemperatureStatusMessage = "GPU temperature from AMD ADLX.";
            MeasurementTraceRecorder.Record(snapshot, "gpu_temp", nameof(AdlxGpuCollector), "ADLX GPUTemperature", true, $"{reading.TemperatureCelsius:0.0}C", $"validFlags=0x{reading.ValidFlags:X}", snapshot.Dependencies.GpuTemperatureStatusMessage, "IADLXGPUMetrics::GPUTemperature", $"{reading.TemperatureCelsius:0.000}", "C", "ADLX reports Celsius directly", $"{reading.TemperatureCelsius:0.000}", "C", TelemetryValidationState.HardwareValidationPending, "ADLX returned a bounded temperature; handheld hardware validation still pending.");
        }

        if ((reading.ValidFlags & ValidFlagPower) != 0)
        {
            snapshot.AvailableMetrics |= MetricFlags.GpuPower;
            snapshot.Gpu.PowerWatts = reading.PowerWatts;
            snapshot.Dependencies.GpuPowerSource = "ADLX";
            var sourceName = reading.PowerSourceKind == PowerSourceAdlxTotalBoard
                ? "ADLX GPUTotalBoardPower"
                : "ADLX GPUPower";
            snapshot.Dependencies.GpuPowerStatusMessage = reading.PowerSourceKind == PowerSourceAdlxTotalBoard
                ? "GPU/APU board power from AMD ADLX GPUTotalBoardPower."
                : "GPU silicon power from AMD ADLX GPUPower.";
            MeasurementTraceRecorder.Record(
                snapshot,
                "gpu_power",
                nameof(AdlxGpuCollector),
                sourceName,
                true,
                $"{reading.PowerWatts:0.0}W",
                $"validFlags=0x{reading.ValidFlags:X}; powerSourceKind={reading.PowerSourceKind}; source={sourceName}",
                snapshot.Dependencies.GpuPowerStatusMessage,
                sourceName,
                $"{reading.PowerWatts:0.000}",
                "W",
                "ADLX reports watts directly",
                $"{reading.PowerWatts:0.000}",
                "W",
                TelemetryValidationState.HardwareValidationPending,
                "ADLX produced a positive watt value; handheld hardware validation still pending.");
        }

        if ((reading.ValidFlags & ValidFlagFan) != 0)
        {
            snapshot.AvailableMetrics |= MetricFlags.Fan;
            snapshot.Gpu.FanRpm = reading.FanRpm;
            snapshot.Dependencies.GpuFanSource = "ADLX";
            snapshot.Dependencies.GpuFanStatusMessage = "GPU fan speed from AMD ADLX.";
            MeasurementTraceRecorder.Record(snapshot, "gpu_fan", nameof(AdlxGpuCollector), "ADLX GPUFanSpeed", true, $"{reading.FanRpm}rpm", $"validFlags=0x{reading.ValidFlags:X}", snapshot.Dependencies.GpuFanStatusMessage, "IADLXGPUMetrics::GPUFanSpeed", reading.FanRpm.ToString(), "rpm", "ADLX reports RPM directly", reading.FanRpm.ToString(), "rpm", TelemetryValidationState.HardwareValidationPending, "ADLX returned a positive fan RPM; handheld hardware validation still pending.");
        }

        if ((reading.ValidFlags & ValidFlagClock) != 0)
        {
            snapshot.AvailableMetrics |= MetricFlags.GpuClock;
            snapshot.Gpu.ClockMegahertz = reading.ClockMegahertz;
            snapshot.Dependencies.GpuClockSource = "ADLX";
            snapshot.Dependencies.GpuClockStatusMessage = "GPU clock from AMD ADLX.";
            MeasurementTraceRecorder.Record(snapshot, "gpu_clock", nameof(AdlxGpuCollector), "ADLX GPUClockSpeed", true, $"{reading.ClockMegahertz:0.0}MHz", $"validFlags=0x{reading.ValidFlags:X}", snapshot.Dependencies.GpuClockStatusMessage, "IADLXGPUMetrics::GPUClockSpeed", $"{reading.ClockMegahertz:0.000}", "MHz", "ADLX reports MHz directly", $"{reading.ClockMegahertz:0.000}", "MHz", TelemetryValidationState.HardwareValidationPending, "ADLX returned a positive clock; handheld hardware validation still pending.");
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
                HhaPulseAdlxShutdown();
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
    private const uint PowerSourceAdlxTotalBoard = 2;

    // ── P/Invoke ──

    [DllImport("HHAPulse.Native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int HhaPulseAdlxInit();

    [DllImport("HHAPulse.Native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int HhaPulseAdlxReadGpu(ref HhaPulseGpuReading reading);

    [DllImport("HHAPulse.Native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void HhaPulseAdlxShutdown();

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
        public uint PowerSourceKind;        // offset 36
    }
}
