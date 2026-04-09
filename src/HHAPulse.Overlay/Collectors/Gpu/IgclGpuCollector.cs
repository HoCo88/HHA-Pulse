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

        if ((reading.ValidFlags & ValidFlagTemp) != 0)
        {
            snapshot.AvailableMetrics |= MetricFlags.GpuTemperature;
            snapshot.Gpu.TemperatureCelsius = reading.TemperatureCelsius;
            snapshot.Dependencies.GpuTemperatureSource = "IGCL";
            snapshot.Dependencies.GpuTemperatureStatusMessage = "GPU temperature from Intel IGCL.";
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
            snapshot.Dependencies.GpuPowerStatusMessage = "GPU power from Intel IGCL energy telemetry.";
        }

        // Fan is typically unavailable on iGPU (validFlags bit 2 = 0).
        if ((reading.ValidFlags & ValidFlagFan) != 0)
        {
            snapshot.AvailableMetrics |= MetricFlags.Fan;
            snapshot.Gpu.FanRpm = reading.FanRpm;
            snapshot.Dependencies.GpuFanSource = "IGCL";
            snapshot.Dependencies.GpuFanStatusMessage = "GPU fan speed from Intel IGCL.";
        }

        if ((reading.ValidFlags & ValidFlagClock) != 0)
        {
            snapshot.AvailableMetrics |= MetricFlags.GpuClock;
            snapshot.Gpu.ClockMegahertz = reading.ClockMegahertz;
            snapshot.Dependencies.GpuClockSource = "IGCL";
            snapshot.Dependencies.GpuClockStatusMessage = "GPU clock from Intel IGCL.";
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
    }
}
