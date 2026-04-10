using MessagePack;

namespace HHAPulse.Shared.Models;

[MessagePackObject]
public sealed class TelemetrySnapshot
{
    [Key(0)]
    public long TimestampUnixMilliseconds { get; set; }

    [Key(1)]
    public MetricFlags AvailableMetrics { get; set; }

    [Key(2)]
    public PerformanceMetrics Performance { get; set; } = new();

    [Key(3)]
    public CpuMetrics Cpu { get; set; } = new();

    [Key(4)]
    public GpuMetrics Gpu { get; set; } = new();

    [Key(5)]
    public BatteryMetrics Battery { get; set; } = new();

    [Key(6)]
    public MemoryMetrics Memory { get; set; } = new();

    [Key(7)]
    public DisplayMetrics Display { get; set; } = new();

    [Key(8)]
    public DependencyState Dependencies { get; set; } = new();

    [Key(9)]
    public List<MetricStatus> MetricStatuses { get; set; } = new();

    [Key(10)]
    public List<MeasurementTrace> MeasurementTraces { get; set; } = new();
}

[MessagePackObject]
public sealed class PerformanceMetrics
{
    [Key(0)]
    public double FramesPerSecond { get; set; }

    [Key(1)]
    public double AverageFramesPerSecond { get; set; }

    [Key(2)]
    public double OnePercentLowFramesPerSecond { get; set; }

    [Key(3)]
    public double ZeroPointOnePercentLowFramesPerSecond { get; set; }

    [Key(4)]
    public double FrameTimeMilliseconds { get; set; }

    [Key(5)]
    public double GpuBusyMilliseconds { get; set; }

    [Key(6)]
    public double AppFramesPerSecond { get; set; }

    [Key(7)]
    public double PresentFramesPerSecond { get; set; }

    [Key(8)]
    public double DisplayFramesPerSecond { get; set; }

    [Key(9)]
    public bool HybridPresentDetected { get; set; }
}

[MessagePackObject]
public sealed class CpuMetrics
{
    [Key(0)]
    public double UsagePercent { get; set; }

    [Key(1)]
    public double TemperatureCelsius { get; set; }

    [Key(2)]
    public double MaxTemperatureCelsius { get; set; }

    [Key(3)]
    public double ClockMegahertz { get; set; }

    [Key(4)]
    public double PowerWatts { get; set; }

    [Key(5)]
    public int FanRpm { get; set; }
}

[MessagePackObject]
public sealed class GpuMetrics
{
    [Key(0)]
    public double UsagePercent { get; set; }

    [Key(1)]
    public double TemperatureCelsius { get; set; }

    [Key(2)]
    public double MaxTemperatureCelsius { get; set; }

    [Key(3)]
    public double ClockMegahertz { get; set; }

    [Key(4)]
    public double PowerWatts { get; set; }

    [Key(5)]
    public double VramUsedMegabytes { get; set; }

    [Key(6)]
    public double VramTotalMegabytes { get; set; }

    [Key(7)]
    public int FanRpm { get; set; }
}

[MessagePackObject]
public sealed class BatteryMetrics
{
    [Key(0)]
    public double ChargePercent { get; set; }

    [Key(1)]
    public double EstimatedMinutesRemaining { get; set; }

    [Key(2)]
    public double DischargeWatts { get; set; }

    [Key(3)]
    public double HealthPercent { get; set; }

    [Key(4)]
    public double CurrentCapacityWattHours { get; set; }

    [Key(5)]
    public double DesignCapacityWattHours { get; set; }

    [Key(6)]
    public int CycleCount { get; set; }

    [Key(7)]
    public bool IsCharging { get; set; }

    [Key(8)]
    public double ChargeWatts { get; set; }
}

[MessagePackObject]
public sealed class MemoryMetrics
{
    [Key(0)]
    public double RamUsedMegabytes { get; set; }

    [Key(1)]
    public double RamTotalMegabytes { get; set; }
}

[MessagePackObject]
public sealed class DisplayMetrics
{
    [Key(0)]
    public int WidthPixels { get; set; }

    [Key(1)]
    public int HeightPixels { get; set; }

    [Key(2)]
    public double RefreshRateHertz { get; set; }

    [Key(3)]
    public bool VrrSupported { get; set; }

    [Key(4)]
    public bool VrrActive { get; set; }

    [Key(5)]
    public string PresentMode { get; set; } = string.Empty;
}

[MessagePackObject]
public sealed class DependencyState
{
    [Key(0)]
    public bool CaptureServiceConnected { get; set; }

    [Key(1)]
    public bool PawnIoAvailable { get; set; }

    [Key(2)]
    public string CaptureServiceStatusMessage { get; set; } = string.Empty;

    [Key(3)]
    public string PawnIoStatusMessage { get; set; } = string.Empty;

    [Key(4)]
    public bool GpuTelemetryAvailable { get; set; }

    [Key(5)]
    public string GpuTelemetryStatusMessage { get; set; } = string.Empty;

    [Key(6)]
    public uint CaptureTargetProcessId { get; set; }

    [Key(7)]
    public string CaptureTargetProcessName { get; set; } = string.Empty;

    [Key(8)]
    public string SharedAssemblyPath { get; set; } = string.Empty;

    [Key(9)]
    public string SharedAssemblyVersion { get; set; } = string.Empty;

    [Key(10)]
    public string SharedAssemblyMvid { get; set; } = string.Empty;

    [Key(11)]
    public bool TelemetryContractValid { get; set; } = true;

    [Key(12)]
    public string TelemetryContractStatusMessage { get; set; } = string.Empty;

    [Key(13)]
    public string GpuTelemetrySource { get; set; } = "D3DKMT";

    [Key(14)]
    public string GpuTemperatureSource { get; set; } = string.Empty;

    [Key(15)]
    public string GpuTemperatureStatusMessage { get; set; } = string.Empty;

    [Key(16)]
    public string GpuPowerSource { get; set; } = string.Empty;

    [Key(17)]
    public string GpuPowerStatusMessage { get; set; } = string.Empty;

    [Key(18)]
    public string GpuFanSource { get; set; } = string.Empty;

    [Key(19)]
    public string GpuFanStatusMessage { get; set; } = string.Empty;

    [Key(20)]
    public string GpuClockSource { get; set; } = string.Empty;

    [Key(21)]
    public string GpuClockStatusMessage { get; set; } = string.Empty;

    [Key(22)]
    public long CapturePayloadAgeMilliseconds { get; set; }

    [Key(23)]
    public int WidgetClientCount { get; set; }

    [Key(24)]
    public string WidgetPipeStatusMessage { get; set; } = string.Empty;

    [Key(25)]
    public double ComponentPowerSumWatts { get; set; }

    [Key(26)]
    public string ComponentPowerStatusMessage { get; set; } = string.Empty;

    // DRAM RAPL rail (Intel client SKUs).
    // Populated by DramPowerEmiCollector when the Intel Energy Meter
    // Interface exposes a "RAPL_Package<N>_DRAM" channel. Diagnostics
    // use it as a component rail only; it is not whole-device power.

    [Key(27)]
    public double DramPowerWatts { get; set; }

    [Key(28)]
    public string DramPowerSource { get; set; } = string.Empty;

    [Key(29)]
    public string DramPowerStatusMessage { get; set; } = string.Empty;

    // Device/chassis fan readings. Existing Gpu.FanRpm remains populated
    // for wire compatibility with older consumers that only know a single
    // fan field.
    [Key(30)]
    public int[] FanRpms { get; set; } = Array.Empty<int>();

    [Key(31)]
    public string DeviceFanSource { get; set; } = string.Empty;

    [Key(32)]
    public string DeviceFanStatusMessage { get; set; } = string.Empty;

    [Key(33)]
    public double DeviceTemperatureCelsius { get; set; }

    [Key(34)]
    public string DeviceTemperatureSource { get; set; } = string.Empty;

    [Key(35)]
    public string DeviceTemperatureStatusMessage { get; set; } = string.Empty;
}

[MessagePackObject]
public sealed class MetricStatus
{
    [Key(0)]
    public string MetricId { get; set; } = string.Empty;

    [Key(1)]
    public string Source { get; set; } = string.Empty;

    [Key(2)]
    public bool IsAvailable { get; set; }

    [Key(3)]
    public long LastSuccessUnixMilliseconds { get; set; }

    [Key(4)]
    public string StatusMessage { get; set; } = string.Empty;

    [Key(5)]
    public string ValidationState { get; set; } = string.Empty;
}

[MessagePackObject]
public sealed class MeasurementTrace
{
    [Key(0)]
    public string MetricId { get; set; } = string.Empty;

    [Key(1)]
    public string Collector { get; set; } = string.Empty;

    [Key(2)]
    public string Source { get; set; } = string.Empty;

    [Key(3)]
    public bool IsAvailable { get; set; }

    [Key(4)]
    public string ValueText { get; set; } = string.Empty;

    [Key(5)]
    public string PipelineText { get; set; } = string.Empty;

    [Key(6)]
    public string StatusMessage { get; set; } = string.Empty;

    [Key(7)]
    public string ApiContract { get; set; } = string.Empty;

    [Key(8)]
    public string RawValue { get; set; } = string.Empty;

    [Key(9)]
    public string RawUnit { get; set; } = string.Empty;

    [Key(10)]
    public string ConversionRule { get; set; } = string.Empty;

    [Key(11)]
    public string ConvertedValue { get; set; } = string.Empty;

    [Key(12)]
    public string ConvertedUnit { get; set; } = string.Empty;

    [Key(13)]
    public string ValidationState { get; set; } = string.Empty;

    [Key(14)]
    public string Reason { get; set; } = string.Empty;
}
