using System.Runtime.InteropServices;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Shared.Models;
using NvAPIWrapper;
using NvAPIWrapper.GPU;

namespace HHAPulse.Overlay.Collectors.Gpu;

/// <summary>
/// Reads GPU temperature, fan RPM, and clock from NVIDIA GPUs via NvAPIWrapper.Net,
/// and GPU power (watts) from NVML via P/Invoke.
/// NVAPI: temp (ThermalSensors), clocks (Graphics domain), fan (true tach RPM).
/// NVML: power watts (milliwatts / 1000) — more reliable than NVAPI for wattage.
/// Both APIs work as standard user — no admin needed.
/// </summary>
public sealed class NvApiGpuCollector : IMetricCollector, IDisposable
{
    private PhysicalGPU? _gpu;
    private bool _initialized;
    private bool _loggedFailure;
    private bool _disposed;

    // NVML state.
    private bool _nvmlInitialized;
    private IntPtr _nvmlDevice;
    private IntPtr _nvmlLibHandle;

    /// <summary>
    /// Known install path for nvml.dll (ships with NVIDIA drivers).
    /// </summary>
    private const string NvmlDriverPath = @"C:\Program Files\NVIDIA Corporation\NVSMI\nvml.dll";

    public string Name => "GPU Sensors (NvAPI)";

    public bool IsAvailable { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            NVIDIA.Initialize();

            var gpus = PhysicalGPU.GetPhysicalGPUs();
            if (gpus.Length == 0)
            {
                LogOnce("NvAPI: No NVIDIA GPUs found.");
                return Task.CompletedTask;
            }

            _gpu = gpus[0];
            _initialized = true;
            IsAvailable = true;
            AppLogger.Info($"NvAPI: Initialized — {_gpu.FullName}.");

            // Try to initialize NVML for power reading.
            InitializeNvml();
        }
        catch (DllNotFoundException)
        {
            LogOnce("NvAPI: nvapi64.dll not found. NVIDIA GPU telemetry unavailable.");
        }
        catch (Exception ex)
        {
            LogOnce($"NvAPI: Init exception: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        if (!_initialized || _gpu is null)
            return Task.CompletedTask;

        snapshot.Dependencies.GpuTelemetryAvailable = true;
        snapshot.Dependencies.GpuTelemetrySource = "NvAPI";
        snapshot.Dependencies.GpuTelemetryStatusMessage = _nvmlInitialized
            ? "NvAPI + NVML GPU telemetry initialized."
            : "NvAPI GPU telemetry initialized. NVML power unavailable.";

        try
        {
            // Temperature: first thermal sensor (NvAPI_GPU_GetThermalSettings).
            var sensors = _gpu.ThermalInformation.ThermalSensors;
            foreach (var sensor in sensors)
            {
                snapshot.AvailableMetrics |= MetricFlags.GpuTemperature;
                snapshot.Gpu.TemperatureCelsius = sensor.CurrentTemperature;
                snapshot.Dependencies.GpuTemperatureSource = "NvAPI";
                snapshot.Dependencies.GpuTemperatureStatusMessage = "GPU temperature from NVIDIA NvAPI.";
                MeasurementTraceRecorder.Record(snapshot, "gpu_temp", nameof(NvApiGpuCollector), "NvAPI thermal sensor", true, $"{sensor.CurrentTemperature:0.0}C", "first NvAPI thermal sensor accepted", snapshot.Dependencies.GpuTemperatureStatusMessage, "NvAPI_GPU_GetThermalSettings", $"{sensor.CurrentTemperature:0.000}", "C", "NvAPIWrapper reports Celsius", $"{sensor.CurrentTemperature:0.000}", "C", TelemetryValidationState.Verified, "NvAPI returned a thermal sensor reading.");
                break;
            }
        }
        catch (Exception ex)
        {
            LogOnce($"NvAPI: Temperature read failed: {ex.Message}");
        }

        try
        {
            // Clock: graphics domain frequency (NvAPI_GPU_GetAllClockFrequencies).
            // NvAPIWrapper returns frequency in kHz — divide by 1000 for MHz.
            var clocks = _gpu.CurrentClockFrequencies;
            var coreClock = clocks.GraphicsClock;
            if (coreClock.Frequency > 0)
            {
                snapshot.AvailableMetrics |= MetricFlags.GpuClock;
                snapshot.Gpu.ClockMegahertz = coreClock.Frequency / 1000.0;
                snapshot.Dependencies.GpuClockSource = "NvAPI";
                snapshot.Dependencies.GpuClockStatusMessage = "GPU clock from NVIDIA NvAPI.";
                MeasurementTraceRecorder.Record(snapshot, "gpu_clock", nameof(NvApiGpuCollector), "NvAPI graphics clock", true, $"{snapshot.Gpu.ClockMegahertz:0.0}MHz", $"rawFrequency={coreClock.Frequency}", snapshot.Dependencies.GpuClockStatusMessage, "NvAPIWrapper CurrentClockFrequencies.GraphicsClock", $"{coreClock.Frequency:0.000}", "kHz", "kHz / 1000", $"{snapshot.Gpu.ClockMegahertz:0.000}", "MHz", TelemetryValidationState.Verified, "NvAPIWrapper.Net 0.8.1.101 frequency value is treated as kHz by this collector.");
            }
        }
        catch (Exception ex)
        {
            LogOnce($"NvAPI: Clock read failed: {ex.Message}");
        }

        try
        {
            // Fan RPM: true tachometer reading (NvAPI_GPU_GetTachReading).
            // NVML only gives intended %, not actual RPM — NVAPI is authoritative here.
            var coolers = _gpu.CoolerInformation.Coolers;
            foreach (var cooler in coolers)
            {
                snapshot.AvailableMetrics |= MetricFlags.Fan;
                snapshot.Gpu.FanRpm = (int)cooler.CurrentFanSpeedInRPM;
                snapshot.Dependencies.FanRpms = new[] { snapshot.Gpu.FanRpm };
                snapshot.Dependencies.DeviceFanSource = "NvAPI";
                snapshot.Dependencies.DeviceFanStatusMessage = "Device fan tachometer from NVIDIA NvAPI cooler tach.";
                snapshot.Dependencies.GpuFanSource = "NvAPI";
                snapshot.Dependencies.GpuFanStatusMessage = snapshot.Dependencies.DeviceFanStatusMessage;
                MeasurementTraceRecorder.Record(snapshot, "gpu_fan", nameof(NvApiGpuCollector), "NvAPI tach reading", true, $"{snapshot.Gpu.FanRpm}rpm", $"rawRpm={cooler.CurrentFanSpeedInRPM}", snapshot.Dependencies.GpuFanStatusMessage, "NvAPI GPU cooler tach", cooler.CurrentFanSpeedInRPM.ToString(), "rpm", "direct RPM", snapshot.Gpu.FanRpm.ToString(), "rpm", TelemetryValidationState.Verified, "NvAPI returned a tachometer fan reading.");
                break;
            }
        }
        catch (Exception ex)
        {
            LogOnce($"NvAPI: Fan read failed: {ex.Message}");
        }

        // Power from NVML (milliwatts -> watts).
        // NVML nvmlDeviceGetPowerUsage is more reliable for wattage than NVAPI.
        if (_nvmlInitialized && _nvmlDevice != IntPtr.Zero)
        {
            try
            {
                int result = NvmlDeviceGetPowerUsage(_nvmlDevice, out uint milliwatts);
                if (result == 0 && milliwatts > 0)
                {
                    snapshot.AvailableMetrics |= MetricFlags.GpuPower;
                    snapshot.Gpu.PowerWatts = milliwatts / 1000.0;
                    snapshot.Dependencies.GpuPowerSource = "NVML";
                    snapshot.Dependencies.GpuPowerStatusMessage = "GPU power from NVIDIA NVML.";
                    MeasurementTraceRecorder.Record(
                        snapshot,
                        "gpu_power",
                        nameof(NvApiGpuCollector),
                        "NVML nvmlDeviceGetPowerUsage",
                        true,
                        $"{snapshot.Gpu.PowerWatts:0.0}W",
                        $"rawMilliwatts={milliwatts}; flagGpuPower=True",
                        snapshot.Dependencies.GpuPowerStatusMessage,
                        "nvmlDeviceGetPowerUsage",
                        milliwatts.ToString(),
                        "mW",
                        "milliwatts / 1000",
                        $"{snapshot.Gpu.PowerWatts:0.000}",
                        "W",
                        TelemetryValidationState.Verified,
                        "NVML returned a positive milliwatt reading.");
                }
            }
            catch (Exception ex)
            {
                LogOnce($"NVML: Power read failed: {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_nvmlInitialized)
        {
            try
            {
                NvmlShutdown();
            }
            catch
            {
                // Best-effort cleanup.
            }

            _nvmlInitialized = false;
        }

        if (_nvmlLibHandle != IntPtr.Zero)
        {
            NativeLibrary.Free(_nvmlLibHandle);
            _nvmlLibHandle = IntPtr.Zero;
        }
    }

    private void InitializeNvml()
    {
        if (_gpu is null)
            return;

        try
        {
            // Pre-load nvml.dll from the known NVIDIA driver path if it's not
            // already on the DLL search path. This ensures the P/Invoke calls
            // below can resolve the library.
            if (!NativeLibrary.TryLoad("nvml.dll", out _nvmlLibHandle))
            {
                if (!NativeLibrary.TryLoad(NvmlDriverPath, out _nvmlLibHandle))
                {
                    AppLogger.Info("NVML: nvml.dll not found. GPU power unavailable.");
                    return;
                }
            }

            int result = NvmlInit();
            if (result != 0)
            {
                AppLogger.Info($"NVML: nvmlInit_v2 returned {result}. GPU power unavailable.");
                return;
            }

            _nvmlInitialized = true;

            // Match NVML device by PCI bus ID from NVAPI.
            // NVML expects format like "0000:01:00.0".
            string nvmlBusId = $"0000:{_gpu.BusInformation.BusId:X2}:{_gpu.BusInformation.BusSlot:X2}.0";

            result = NvmlDeviceGetHandleByPciBusId(nvmlBusId, out _nvmlDevice);
            if (result != 0)
            {
                // Try without leading domain.
                string shortBusId = $"{_gpu.BusInformation.BusId:X2}:{_gpu.BusInformation.BusSlot:X2}.0";
                result = NvmlDeviceGetHandleByPciBusId(shortBusId, out _nvmlDevice);
            }

            if (result != 0)
            {
                int countResult = NvmlDeviceGetCount(out uint deviceCount);
                if (countResult == 0 && deviceCount == 1)
                {
                    result = NvmlDeviceGetHandleByIndex(0, out _nvmlDevice);
                }
            }

            if (result == 0)
            {
                AppLogger.Info("NVML: Device handle acquired. GPU power available.");
            }
            else
            {
                AppLogger.Info($"NVML: Could not get device handle (result={result}). GPU power unavailable.");
                _nvmlDevice = IntPtr.Zero;
            }
        }
        catch (DllNotFoundException)
        {
            AppLogger.Info("NVML: nvml.dll not found. GPU power unavailable.");
            _nvmlInitialized = false;
        }
        catch (Exception ex)
        {
            AppLogger.Info($"NVML: Init exception: {ex.Message}");
            _nvmlInitialized = false;
        }
    }

    private void LogOnce(string message)
    {
        if (_loggedFailure)
            return;

        _loggedFailure = true;
        AppLogger.Info(message);
    }

    // ── NVML P/Invoke ──
    // nvml.dll ships with NVIDIA drivers at C:\Program Files\NVIDIA Corporation\NVSMI\nvml.dll.
    // Pre-loaded via NativeLibrary.TryLoad in InitializeNvml before these are called.

    private const string NvmlLib = "nvml.dll";

    [DllImport(NvmlLib, EntryPoint = "nvmlInit_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int NvmlInit();

    [DllImport(NvmlLib, EntryPoint = "nvmlShutdown", CallingConvention = CallingConvention.Cdecl)]
    private static extern int NvmlShutdown();

    [DllImport(NvmlLib, EntryPoint = "nvmlDeviceGetHandleByPciBusId_v2", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int NvmlDeviceGetHandleByPciBusId([MarshalAs(UnmanagedType.LPStr)] string pciBusId, out IntPtr device);

    [DllImport(NvmlLib, EntryPoint = "nvmlDeviceGetHandleByIndex_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int NvmlDeviceGetHandleByIndex(uint index, out IntPtr device);

    [DllImport(NvmlLib, EntryPoint = "nvmlDeviceGetCount_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int NvmlDeviceGetCount(out uint deviceCount);

    [DllImport(NvmlLib, EntryPoint = "nvmlDeviceGetPowerUsage", CallingConvention = CallingConvention.Cdecl)]
    private static extern int NvmlDeviceGetPowerUsage(IntPtr device, out uint powerMilliwatts);
}
