using System.Runtime.InteropServices;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Shared.Models;
using Microsoft.Win32.SafeHandles;

namespace HHAPulse.Overlay.Collectors.Cpu;

/// <summary>
/// Reads CPU package power via the Windows Energy Meter Interface (EMI).
/// EMI exposes RAPL energy counters on battery-equipped devices through
/// device I/O controls. We enumerate EMI devices, find one whose name
/// contains "CPU" or "Package", and compute watts from energy deltas.
/// No elevation required; EMI devices are readable from user mode.
/// </summary>
public sealed class CpuPowerCollector : IMetricCollector, IDisposable
{
    private SafeFileHandle? _deviceHandle;
    private bool _initialized;
    private bool _loggedFailure;
    private bool _disposed;
    private ushort _emiVersion;
    private ushort _channelCount;
    private int _channelIndex = -1;
    private string _channelName = string.Empty;
    private EmiMeasurementPoint _previousMeasurement;
    private bool _hasBaseline;

    public string Name => "CPU Power (EMI)";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Guard: only activate if a battery is present (handheld device).
            if (!CheckBatteryPresent())
            {
                LogOnce("CPU Power: No battery detected; EMI collector disabled.");
                return Task.CompletedTask;
            }

            // Enumerate EMI device interfaces.
            var emiGuid = EmiContract.DeviceInterfaceGuid;
            var deviceInfoSet = SetupDiGetClassDevs(
                ref emiGuid,
                null,
                IntPtr.Zero,
                DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);

            if (deviceInfoSet == InvalidHandleValue)
            {
                LogOnce("CPU Power: SetupDiGetClassDevs failed; no EMI devices.");
                return Task.CompletedTask;
            }

            try
            {
                // Iterate EMI device interfaces looking for a CPU/Package channel.
                for (uint i = 0; i < 64; i++)
                {
                    var interfaceData = new SP_DEVICE_INTERFACE_DATA
                    {
                        cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>()
                    };

                    if (!SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref emiGuid, i, ref interfaceData))
                        break;

                    // Get required buffer size for detail.
                    uint requiredSize = 0;
                    SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, ref requiredSize, IntPtr.Zero);

                    if (requiredSize == 0)
                        continue;

                    var detailBuffer = Marshal.AllocHGlobal((int)requiredSize);
                    try
                    {
                        // Set cbSize to the fixed part size (platform-dependent).
                        Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);

                        if (!SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, detailBuffer, requiredSize, ref requiredSize, IntPtr.Zero))
                            continue;

                        // DevicePath starts at offset 4 in the detail struct.
                        string devicePath = Marshal.PtrToStringUni(detailBuffer + 4) ?? string.Empty;

                        if (string.IsNullOrEmpty(devicePath))
                            continue;

                        // Open the EMI device and check its metadata.
                        var handle = CreateFile(
                            devicePath,
                            FileAccessGenericRead,
                            FileShareRead | FileShareWrite,
                            IntPtr.Zero,
                            OpenExisting,
                            0,
                            IntPtr.Zero);

                        if (handle.IsInvalid)
                            continue;

                        // Query metadata version first.
                        if (!TryGetVersion(handle, out ushort version) ||
                            !TryGetMetadataSize(handle, out uint metadataSize) ||
                            metadataSize == 0 ||
                            metadataSize > 64 * 1024)
                        {
                            handle.Dispose();
                            continue;
                        }

                        byte[] metadata = new byte[metadataSize];
                        if (!DeviceIoControl(
                                handle,
                                EmiContract.IoctlGetMetadata,
                                IntPtr.Zero,
                                0,
                                metadata,
                                metadataSize,
                                out uint bytesReturned,
                                IntPtr.Zero))
                        {
                            handle.Dispose();
                            continue;
                        }

                        EmiSelection? selection = EmiContract.TryParseSelection(version, metadata.AsSpan(0, (int)bytesReturned));
                        if (selection is not null)
                        {
                            _deviceHandle = handle;
                            _initialized = true;
                            _emiVersion = selection.Version;
                            _channelCount = selection.ChannelCount;
                            _channelIndex = selection.ChannelIndex;
                            _channelName = selection.ChannelName;
                            AppLogger.Info($"CPU Power: Selected EMI channel '{_channelName}' from {devicePath}.");
                            return Task.CompletedTask;
                        }

                        handle.Dispose();
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detailBuffer);
                    }
                }

                LogOnce("CPU Power: No CPU energy channel found among EMI devices.");
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }
        }
        catch (Exception ex)
        {
            LogOnce($"CPU Power: Init exception: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        if (!_initialized || _deviceHandle is null || _deviceHandle.IsInvalid)
            return Task.CompletedTask;

        uint measurementSize = _emiVersion <= EmiContract.VersionV1
            ? (uint)(sizeof(ulong) * 2)
            : (uint)(_channelCount * (sizeof(ulong) * 2));

        byte[] measurementBuffer = new byte[measurementSize];
        if (!DeviceIoControl(
                _deviceHandle,
                EmiContract.IoctlGetMeasurement,
                IntPtr.Zero,
                0,
                measurementBuffer,
                measurementSize,
                out uint bytesReturned,
                IntPtr.Zero))
            return Task.CompletedTask;

        if (!EmiContract.TryReadMeasurement(_emiVersion, _channelCount, _channelIndex, measurementBuffer.AsSpan(0, (int)bytesReturned), out EmiMeasurementPoint measurement))
            return Task.CompletedTask;

        if (_hasBaseline)
        {
            double? watts = EmiContract.TryComputeWatts(_previousMeasurement, measurement);
            if (watts is > 0 and < 500)
            {
                snapshot.Cpu.PowerWatts = watts.Value;
                snapshot.AvailableMetrics |= MetricFlags.CpuPower;
            }
        }

        _previousMeasurement = measurement;
        _hasBaseline = true;

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _deviceHandle?.Dispose();
        _deviceHandle = null;
    }

    // ── Helpers ──

    private static bool CheckBatteryPresent()
    {
        var state = new SYSTEM_BATTERY_STATE();
        int status = CallNtPowerInformation(
            SystemBatteryStateLevel,
            IntPtr.Zero,
            0,
            out state,
            (uint)Marshal.SizeOf<SYSTEM_BATTERY_STATE>());

        return status == 0 && state.BatteryPresent;
    }

    private static bool TryGetVersion(SafeFileHandle handle, out ushort version)
    {
        version = 0;
        bool ok = DeviceIoControl(
            handle,
            EmiContract.IoctlGetVersion,
            IntPtr.Zero,
            0,
            out EMI_VERSION emiVersion,
            (uint)Marshal.SizeOf<EMI_VERSION>(),
            out _,
            IntPtr.Zero);

        version = emiVersion.EmiVersion;
        return ok && version >= EmiContract.VersionV1;
    }

    private static bool TryGetMetadataSize(SafeFileHandle handle, out uint metadataSize)
    {
        metadataSize = 0;
        bool ok = DeviceIoControl(
            handle,
            EmiContract.IoctlGetMetadataSize,
            IntPtr.Zero,
            0,
            out EMI_METADATA_SIZE size,
            (uint)Marshal.SizeOf<EMI_METADATA_SIZE>(),
            out _,
            IntPtr.Zero);

        metadataSize = size.MetadataSize;
        return ok && metadataSize > 0;
    }

    private void LogOnce(string message)
    {
        if (_loggedFailure)
            return;

        _loggedFailure = true;
        AppLogger.Info(message);
    }

    // ── Constants ──

    private const int SystemBatteryStateLevel = 5;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    // Official EMI device interface GUID: {45BD8344-7ED6-49cf-A440-C276C933B053}
    private const uint DIGCF_PRESENT = 0x00000002;
    private const uint DIGCF_DEVICEINTERFACE = 0x00000010;
    private const uint FileAccessGenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;

    // ── P/Invoke ──

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern int CallNtPowerInformation(
        int informationLevel,
        IntPtr inputBuffer,
        uint inputBufferLength,
        out SYSTEM_BATTERY_STATE outputBuffer,
        uint outputBufferLength);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
        string? enumerator,
        IntPtr hwndParent,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet,
        IntPtr deviceInfoData,
        ref Guid interfaceClassGuid,
        uint memberIndex,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize,
        ref uint requiredSize,
        IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

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
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        [Out] byte[] lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        out EMI_VERSION lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        out EMI_METADATA_SIZE lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    // ── Structs ──

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_BATTERY_STATE
    {
        [MarshalAs(UnmanagedType.U1)]
        public bool AcOnLine;
        [MarshalAs(UnmanagedType.U1)]
        public bool BatteryPresent;
        [MarshalAs(UnmanagedType.U1)]
        public bool Charging;
        [MarshalAs(UnmanagedType.U1)]
        public bool Discharging;

        public byte Spare1;
        public byte Spare2;
        public byte Spare3;
        public byte Spare4;

        public uint MaxCapacity;
        public uint RemainingCapacity;
        public int Rate;
        public uint EstimatedTime;
        public uint DefaultAlert1;
        public uint DefaultAlert2;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVICE_INTERFACE_DATA
    {
        public int cbSize;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EMI_VERSION
    {
        public ushort EmiVersion;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EMI_METADATA_SIZE
    {
        public uint MetadataSize;
    }
}
