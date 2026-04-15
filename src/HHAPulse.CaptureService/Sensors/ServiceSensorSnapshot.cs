namespace HHAPulse.CaptureService.Sensors;

public sealed class ServiceSensorSnapshot
{
    public long TimestampUnixMilliseconds { get; set; }

    public double CpuTemperatureCelsius { get; set; }

    public string CpuTemperatureSource { get; set; } = string.Empty;

    public string CpuTemperatureStatusMessage { get; set; } = string.Empty;

    public int[] FanRpms { get; set; } = Array.Empty<int>();

    public string DeviceFanSource { get; set; } = string.Empty;

    public string DeviceFanStatusMessage { get; set; } = string.Empty;

    public double DeviceTemperatureCelsius { get; set; }

    public string DeviceTemperatureSource { get; set; } = string.Empty;

    public string DeviceTemperatureStatusMessage { get; set; } = string.Empty;

    public byte StorageWearPercentUsed { get; set; }

    public uint StoragePowerOnHours { get; set; }

    public string StorageDeviceModel { get; set; } = string.Empty;

    public string StorageReliabilityStatusMessage { get; set; } = string.Empty;

    public bool StorageReliabilityAvailable { get; set; }
}
