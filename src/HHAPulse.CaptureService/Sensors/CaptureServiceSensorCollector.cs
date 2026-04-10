using System.Management;
using HHAPulse.Shared.Models;
using Microsoft.Extensions.Logging;

namespace HHAPulse.CaptureService.Sensors;

public sealed class CaptureServiceSensorCollector : IDisposable
{
    private const string WmiNamespace = @"\\.\root\WMI";
    private const string CpuTempQuery = "SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature";
    private const string MsiAcpiClassPath = @"\\.\root\WMI:MSI_ACPI";
    private const string Package32ClassPath = @"\\.\root\WMI:Package_32";

    private static readonly string[] CpuKeywords = { "CPUZ", "PROC", "PKG", "CORE", "CPU", "THRM" };
    private static readonly string[] GpuKeywords = { "GFX", "GPU", "VGA", "IGPU" };
    private static readonly object MsiWmiCallLock = new();

    private readonly ILogger<CaptureServiceSensorCollector> logger;
    private ManagementObject? msiInstance;
    private ManagementClass? packageClass;
    private bool msiInitialized;
    private bool msiUnavailableLogged;
    private bool cpuUnavailableLogged;

    public CaptureServiceSensorCollector(ILogger<CaptureServiceSensorCollector> logger)
    {
        this.logger = logger;
    }

    public ServiceSensorSnapshot Collect()
    {
        var snapshot = new ServiceSensorSnapshot
        {
            TimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        CollectCpuTemperature(snapshot);
        CollectMsiSensors(snapshot);
        return snapshot;
    }

    public CaptureFrameMetrics Enrich(CaptureFrameMetrics metrics)
    {
        var sensors = Collect();
        metrics.ServiceTelemetryTimestampUnixMilliseconds = sensors.TimestampUnixMilliseconds;
        metrics.CpuTemperatureCelsius = sensors.CpuTemperatureCelsius;
        metrics.CpuTemperatureSource = sensors.CpuTemperatureSource;
        metrics.CpuTemperatureStatusMessage = sensors.CpuTemperatureStatusMessage;
        metrics.FanRpms = sensors.FanRpms;
        metrics.DeviceFanSource = sensors.DeviceFanSource;
        metrics.DeviceFanStatusMessage = sensors.DeviceFanStatusMessage;
        metrics.DeviceTemperatureCelsius = sensors.DeviceTemperatureCelsius;
        metrics.DeviceTemperatureSource = sensors.DeviceTemperatureSource;
        metrics.DeviceTemperatureStatusMessage = sensors.DeviceTemperatureStatusMessage;
        return metrics;
    }

    internal static int[] DecodeMsiFanRpms(byte[] raw)
    {
        if (raw.Length < 9 || raw[0] == 0)
        {
            return Array.Empty<int>();
        }

        var result = new List<int>(4);
        for (var index = 1; index <= 7; index += 2)
        {
            int tach = (raw[index] << 8) | raw[index + 1];
            if (tach <= 0)
            {
                continue;
            }

            int rpm = (int)Math.Round(480000.0 / tach);
            if (rpm is > 0 and < 20000)
            {
                result.Add(rpm);
            }
        }

        return result.ToArray();
    }

    internal static double DecodeMsiDeviceTemperatureCelsius(byte[] raw)
    {
        if (raw.Length < 2 || raw[0] == 0)
        {
            return 0;
        }

        int celsius = raw[1];
        return celsius is > 0 and < 125 ? celsius : 0;
    }

    private void CollectCpuTemperature(ServiceSensorSnapshot snapshot)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(WmiNamespace, CpuTempQuery);
            using var results = searcher.Get();

            double hottestMatchCelsius = double.NaN;
            string hottestInstance = string.Empty;
            var seenZones = new List<string>();
            var matchedZones = new List<string>();

            foreach (ManagementObject zone in results)
            {
                using (zone)
                {
                    var instanceName = Convert.ToString(zone["InstanceName"]) ?? string.Empty;
                    var rawTemp = zone["CurrentTemperature"];
                    if (rawTemp is null)
                    {
                        continue;
                    }

                    double deciKelvin = Convert.ToDouble(rawTemp);
                    double celsius = (deciKelvin / 10.0) - 273.15;
                    seenZones.Add($"{instanceName}={celsius:0.0}C");
                    if (celsius is < 0 or > 125)
                    {
                        continue;
                    }

                    var upper = instanceName.ToUpperInvariant();
                    if (ContainsAny(upper, GpuKeywords) || !ContainsAny(upper, CpuKeywords))
                    {
                        continue;
                    }

                    matchedZones.Add($"{instanceName}={celsius:0.0}C");
                    if (double.IsNaN(hottestMatchCelsius) || celsius > hottestMatchCelsius)
                    {
                        hottestMatchCelsius = celsius;
                        hottestInstance = instanceName;
                    }
                }
            }

            if (!double.IsNaN(hottestMatchCelsius))
            {
                snapshot.CpuTemperatureCelsius = hottestMatchCelsius;
                snapshot.CpuTemperatureSource = "WMI MSAcpi_ThermalZoneTemperature (capture service)";
                snapshot.CpuTemperatureStatusMessage = $"CPU temperature from ACPI thermal zone '{hottestInstance}'.";
                return;
            }

            snapshot.CpuTemperatureStatusMessage = seenZones.Count > 0
                ? $"Capture service saw ACPI thermal zones but none matched CPU keywords {string.Join("/", CpuKeywords)}. Seen: {string.Join(", ", seenZones)}."
                : "Capture service saw no ACPI thermal zones.";
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException)
        {
            snapshot.CpuTemperatureStatusMessage = $"Capture service CPU temperature WMI read failed: {ex.Message}.";
            LogCpuUnavailableOnce(snapshot.CpuTemperatureStatusMessage);
        }
    }

    private void CollectMsiSensors(ServiceSensorSnapshot snapshot)
    {
        if (!EnsureMsiInitialized())
        {
            return;
        }

        try
        {
            var fanRaw = InvokeMsiMethod("Get_Fan", 0x00);
            var rpms = DecodeMsiFanRpms(fanRaw);
            if (rpms.Length > 0)
            {
                snapshot.FanRpms = rpms;
                snapshot.DeviceFanSource = "MSI_ACPI.Get_Fan (capture service)";
                snapshot.DeviceFanStatusMessage = $"Device/chassis fan tachometers from MSI_ACPI.Get_Fan: {string.Join("/", rpms)} rpm.";
            }
            else
            {
                snapshot.DeviceFanStatusMessage = $"MSI_ACPI.Get_Fan returned no non-zero fan tachometer readings. raw={FormatRawBytes(fanRaw)}";
            }

            var tempRaw = InvokeMsiMethod("Get_Temperature", 0x00);
            var deviceTemp = DecodeMsiDeviceTemperatureCelsius(tempRaw);
            if (deviceTemp > 0)
            {
                snapshot.DeviceTemperatureCelsius = deviceTemp;
                snapshot.DeviceTemperatureSource = "MSI_ACPI.Get_Temperature subfeature 0x00 (capture service)";
                snapshot.DeviceTemperatureStatusMessage = $"Device/SoC temperature from MSI_ACPI.Get_Temperature subfeature 0x00: {deviceTemp:0}C. Sensor identity is OEM/EC-defined, not GPU-specific.";
            }
            else
            {
                snapshot.DeviceTemperatureStatusMessage = $"MSI_ACPI.Get_Temperature subfeature 0x00 returned no bounded temperature. raw={FormatRawBytes(tempRaw)}";
            }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException)
        {
            LogMsiUnavailableOnce($"MSI_ACPI read failed in capture service: {ex.Message}");
        }
    }

    private bool EnsureMsiInitialized()
    {
        if (msiInitialized)
        {
            return msiInstance is not null && packageClass is not null;
        }

        msiInitialized = true;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                WmiNamespace,
                "SELECT * FROM MSI_ACPI WHERE Active = TRUE");
            msiInstance = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            if (msiInstance is null)
            {
                LogMsiUnavailableOnce("MSI_ACPI WMI class is not active in capture service.");
                return false;
            }

            packageClass = new ManagementClass(Package32ClassPath);
            logger.LogInformation("MSI_ACPI read-only service sensor collector initialized.");
            return true;
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException)
        {
            LogMsiUnavailableOnce($"MSI_ACPI initialization failed in capture service: {ex.Message}");
            DisposeMsiObjects();
            return false;
        }
    }

    private byte[] InvokeMsiMethod(string methodName, byte subfeature)
    {
        if (msiInstance is null || packageClass is null)
        {
            throw new ManagementException("MSI_ACPI is not initialized.");
        }

        lock (MsiWmiCallLock)
        {
            using var input = packageClass.CreateInstance();
            var bytes = new byte[32];
            bytes[0] = subfeature;
            input["Bytes"] = bytes;
            using var parameters = msiInstance.GetMethodParameters(methodName);
            parameters["Data"] = input;
            using var output = msiInstance.InvokeMethod(methodName, parameters, null);
            return ExtractBytes(output);
        }
    }

    private static byte[] ExtractBytes(ManagementBaseObject output)
    {
        var data = output["Data"] as ManagementBaseObject
            ?? throw new ManagementException("MSI_ACPI method returned no Data object.");
        return data["Bytes"] as byte[]
            ?? throw new ManagementException("MSI_ACPI method returned no Bytes array.");
    }

    private void LogCpuUnavailableOnce(string message)
    {
        if (cpuUnavailableLogged)
        {
            return;
        }

        cpuUnavailableLogged = true;
        logger.LogInformation("{Message}", message);
    }

    private void LogMsiUnavailableOnce(string message)
    {
        if (msiUnavailableLogged)
        {
            return;
        }

        msiUnavailableLogged = true;
        logger.LogInformation("{Message}", message);
    }

    private static bool ContainsAny(string text, IEnumerable<string> needles)
    {
        return needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatRawBytes(byte[] raw)
    {
        return string.Join(" ", raw.Select(value => value.ToString("X2")));
    }

    private void DisposeMsiObjects()
    {
        msiInstance?.Dispose();
        msiInstance = null;
        packageClass?.Dispose();
        packageClass = null;
    }

    public void Dispose()
    {
        DisposeMsiObjects();
    }
}
