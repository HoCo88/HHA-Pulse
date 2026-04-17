using MessagePack;

namespace HHAPulse.Shared.Models;

[MessagePackObject]
public sealed class CaptureFrameMetrics
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
    public uint GameProcessId { get; set; }

    [Key(7)]
    public string GameProcessName { get; set; } = string.Empty;

    [Key(8)]
    public long TimestampUnixMilliseconds { get; set; }

    [Key(9)]
    public double AppFramesPerSecond { get; set; }

    [Key(10)]
    public double PresentFramesPerSecond { get; set; }

    [Key(11)]
    public double DisplayFramesPerSecond { get; set; }

    [Key(12)]
    public bool HybridPresentDetected { get; set; }

    [Key(13)]
    public bool HasFrameMetrics { get; set; }

    [Key(14)]
    public long ServiceTelemetryTimestampUnixMilliseconds { get; set; }

    [Key(15)]
    public double CpuTemperatureCelsius { get; set; }

    [Key(16)]
    public string CpuTemperatureSource { get; set; } = string.Empty;

    [Key(17)]
    public string CpuTemperatureStatusMessage { get; set; } = string.Empty;

    [Key(18)]
    public int[] FanRpms { get; set; } = Array.Empty<int>();

    [Key(19)]
    public string DeviceFanSource { get; set; } = string.Empty;

    [Key(20)]
    public string DeviceFanStatusMessage { get; set; } = string.Empty;

    [Key(21)]
    public double DeviceTemperatureCelsius { get; set; }

    [Key(22)]
    public string DeviceTemperatureSource { get; set; } = string.Empty;

    [Key(23)]
    public string DeviceTemperatureStatusMessage { get; set; } = string.Empty;

    [Key(24)]
    public byte StorageWearPercentUsed { get; set; }

    [Key(25)]
    public uint StoragePowerOnHours { get; set; }

    [Key(26)]
    public string StorageDeviceModel { get; set; } = string.Empty;

    [Key(27)]
    public string StorageReliabilityStatusMessage { get; set; } = string.Empty;

    [Key(28)]
    public bool StorageReliabilityAvailable { get; set; }

    [Key(29)]
    public FrameGenVendor FrameGenVendor { get; set; }
}
