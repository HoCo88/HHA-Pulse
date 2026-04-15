using System.Runtime.InteropServices;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Overlay.Settings;
using HHAPulse.Shared.Models;
using Microsoft.Win32.SafeHandles;

namespace HHAPulse.Overlay.Collectors.Storage;

public sealed class StorageTempCollector : IMetricCollector
{
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint GenericRead = 0x80000000;
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const uint IoctlStorageGetDeviceNumber = 0x002D1080;
    private const uint StorageDeviceTemperatureProperty = 52;
    private const uint PropertyStandardQuery = 0;
    private const uint InvalidDeviceNumber = 0xFFFFFFFF;

    public string Name => "Storage Temperature";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.CompletedTask;
        }

        var targetDeviceNumber = TryGetSystemDriveDeviceNumber();
        var temperature = TryReadTemperature(targetDeviceNumber);
        if (temperature is null && targetDeviceNumber != 0)
        {
            temperature = TryReadTemperature(0);
        }

        if (temperature is null)
        {
            return Task.CompletedTask;
        }

        snapshot.AvailableMetrics |= MetricFlags.StorageTemperature;
        snapshot.Storage.TemperatureCelsius = temperature.Value.TemperatureCelsius;
        snapshot.Storage.TemperatureMaxCelsius = temperature.Value.TemperatureMaxCelsius;
        MeasurementTraceRecorder.Record(
            snapshot,
            OverlayPresetCatalog.StorageTemp,
            nameof(StorageTempCollector),
            "IOCTL_STORAGE_QUERY_PROPERTY(StorageDeviceTemperatureProperty)",
            true,
            $"SSD {temperature.Value.TemperatureCelsius:0}\u00B0C",
            $"deviceNumber={temperature.Value.DeviceNumber}; infoCount={temperature.Value.InfoCount}",
            "Storage temperature from IOCTL storage temperature descriptor.",
            "IOCTL_STORAGE_QUERY_PROPERTY + STORAGE_TEMPERATURE_DATA_DESCRIPTOR",
            $"{temperature.Value.TemperatureCelsius:0}",
            "C",
            "signed short Celsius from sensor index 0",
            $"{temperature.Value.TemperatureCelsius:0}",
            "C",
            TelemetryValidationState.Verified,
            "Storage temperature collected from the primary/system drive.");

        if (snapshot.Storage.DeviceModel.Length == 0)
        {
            snapshot.Storage.DeviceModel = $"PhysicalDrive{temperature.Value.DeviceNumber}";
        }

        return Task.CompletedTask;
    }

    internal static StorageTemperatureReading? ParseTemperatureDescriptor(byte[] buffer, uint deviceNumber)
    {
        var headerSize = Marshal.SizeOf<StorageTemperatureDataDescriptorHeader>();
        if (buffer.Length < headerSize)
        {
            return null;
        }

        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var basePtr = handle.AddrOfPinnedObject();
            var header = Marshal.PtrToStructure<StorageTemperatureDataDescriptorHeader>(basePtr);
            if (header.InfoCount == 0 || header.Size < headerSize + Marshal.SizeOf<StorageTemperatureInfo>())
            {
                return null;
            }

            var infoPtr = basePtr + headerSize;
            var info = Marshal.PtrToStructure<StorageTemperatureInfo>(infoPtr);
            return new StorageTemperatureReading(deviceNumber, info.Temperature, header.WarningTemperature, header.InfoCount);
        }
        finally
        {
            handle.Free();
        }
    }

    private static StorageTemperatureReading? TryReadTemperature(uint deviceNumber)
    {
        using var handle = CreateFile(
            $@"\\.\PhysicalDrive{deviceNumber}",
            GenericRead,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            FileAttributeNormal,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return null;
        }

        var query = new StoragePropertyQuery
        {
            PropertyId = StorageDeviceTemperatureProperty,
            QueryType = PropertyStandardQuery
        };

        var output = new byte[512];
        if (!DeviceIoControl(
                handle,
                IoctlStorageQueryProperty,
                ref query,
                (uint)Marshal.SizeOf<StoragePropertyQuery>(),
                output,
                (uint)output.Length,
                out var bytesReturned,
                IntPtr.Zero) ||
            bytesReturned == 0)
        {
            return null;
        }

        return ParseTemperatureDescriptor(output, deviceNumber);
    }

    private static uint TryGetSystemDriveDeviceNumber()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory);
        if (string.IsNullOrWhiteSpace(root))
        {
            return 0;
        }

        var volumePath = $@"\\.\{root.TrimEnd('\\')}";
        using var handle = CreateFile(
            volumePath,
            0,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            FileAttributeNormal,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return 0;
        }

        if (!DeviceIoControl(
                handle,
                IoctlStorageGetDeviceNumber,
                IntPtr.Zero,
                0,
                out StorageDeviceNumber deviceNumber,
                (uint)Marshal.SizeOf<StorageDeviceNumber>(),
                out _,
                IntPtr.Zero))
        {
            return 0;
        }

        return deviceNumber.DeviceNumber == InvalidDeviceNumber ? 0 : deviceNumber.DeviceNumber;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        ref StoragePropertyQuery lpInBuffer,
        uint nInBufferSize,
        [Out] byte[] lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        out StorageDeviceNumber lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [StructLayout(LayoutKind.Sequential)]
    private struct StoragePropertyQuery
    {
        public uint PropertyId;
        public uint QueryType;
        public byte AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StorageDeviceNumber
    {
        public uint DeviceType;
        public uint DeviceNumber;
        public uint PartitionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StorageTemperatureDataDescriptorHeader
    {
        public uint Version;
        public uint Size;
        public short CriticalTemperature;
        public short WarningTemperature;
        public ushort InfoCount;
        public byte Reserved0_0;
        public byte Reserved0_1;
        public uint Reserved1_0;
        public uint Reserved1_1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StorageTemperatureInfo
    {
        public ushort Index;
        public short Temperature;
        public short OverThreshold;
        public short UnderThreshold;
        public byte OverThresholdChangable;
        public byte UnderThresholdChangable;
        public byte EventGenerated;
        public byte Reserved0;
        public uint Reserved1;
    }

    internal readonly record struct StorageTemperatureReading(uint DeviceNumber, short TemperatureCelsius, short TemperatureMaxCelsius, ushort InfoCount);
}
