using System.Runtime.InteropServices;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Shared.Models;
using Microsoft.Win32.SafeHandles;

namespace HHAPulse.Overlay.Collectors.Cpu;

/// <summary>
/// Reads memory (DRAM) power via the Intel RAPL DRAM domain, exposed
/// as a <c>RAPL_Package&lt;N&gt;_DRAM</c> channel through the Windows
/// Energy Meter Interface (EMI).
///
/// <para>
/// <b>What RAPL DRAM is.</b> Intel's RAPL architecture defines a
/// dedicated energy counter for the memory subsystem (DIMMs + memory
/// controller on some SKUs). On client Core and Core Ultra parts the
/// counter reports watts consumed by the DRAM itself. Source: Intel
/// 64 and IA-32 Architectures Software Developer's Manual Vol 3B,
/// §15.10.3 "RAPL Domains and Platform Specific Information".
/// </para>
///
/// <para>
/// <b>How we read it.</b> The Intel Power Management driver
/// (ipmDrv) exposes every RAPL domain as a named EMI channel. On
/// Lunar Lake (Core Ultra 7 258V) the enumeration returns four
/// channels — <c>PKG</c>, <c>PP0</c>, <c>PP1</c>, <c>DRAM</c> — all
/// verified via the overlay's own init diagnostic log. This
/// collector opens the same EMI device class as
/// <see cref="CpuPowerCollector"/> and selects the DRAM channel
/// via <see cref="EmiChannelRole.Dram"/>. Power is derived from
/// picowatt-hour energy deltas over elapsed time, identical to the
/// CPU and iGPU collectors.
/// </para>
///
/// <para>
/// <b>Why we care.</b> On AC, <see cref="SystemPowerValidator"/>
/// uses (PKG package watts) + (DRAM watts) as the TotalPower tile
/// value — an honest SoC + memory proxy rather than PKG alone while
/// still being bounded to real Intel-documented measurements.
/// The reading still excludes display panel, SSD, radios, fans,
/// and platform conversion losses, and the diagnostic trace
/// records this caveat.
/// </para>
///
/// <para>
/// <b>No fake data.</b> If the EMI driver is not installed, if no
/// device exposes a DRAM channel, or if the energy delta is
/// invalid on a given tick, we fail closed — no flag is set and
/// the TotalPower tile remains <c>--</c>. The collector never
/// substitutes a guess for an unavailable reading.
/// </para>
/// </summary>
public sealed class DramPowerEmiCollector : IMetricCollector, IDisposable
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
    private string _statusMessage = "DRAM EMI power collector not initialized.";

    public string Name => "DRAM Power (EMI)";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            // The DRAM RAPL rail is a property of Intel client SoCs.
            // Guard on battery presence so we only probe on handhelds /
            // laptops, matching the pattern of CpuPowerCollector and
            // GpuPowerEmiCollector.
            if (!CheckBatteryPresent())
            {
                LogOnce("DRAM Power: No battery detected; EMI collector disabled.");
                return Task.CompletedTask;
            }

            var emiGuid = EmiContract.DeviceInterfaceGuid;
            var deviceInfoSet = SetupDiGetClassDevs(
                ref emiGuid,
                null,
                IntPtr.Zero,
                DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);

            if (deviceInfoSet == InvalidHandleValue)
            {
                LogOnce("DRAM Power: SetupDiGetClassDevs failed; no EMI devices.");
                return Task.CompletedTask;
            }

            try
            {
                uint enumeratedDeviceCount = 0;
                for (uint i = 0; i < 64; i++)
                {
                    var interfaceData = new SP_DEVICE_INTERFACE_DATA
                    {
                        cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>()
                    };

                    if (!SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref emiGuid, i, ref interfaceData))
                        break;

                    enumeratedDeviceCount++;

                    uint requiredSize = 0;
                    SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, ref requiredSize, IntPtr.Zero);

                    if (requiredSize == 0)
                        continue;

                    var detailBuffer = Marshal.AllocHGlobal((int)requiredSize);
                    try
                    {
                        Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);

                        if (!SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, detailBuffer, requiredSize, ref requiredSize, IntPtr.Zero))
                            continue;

                        string devicePath = Marshal.PtrToStringUni(detailBuffer + 4) ?? string.Empty;

                        if (string.IsNullOrEmpty(devicePath))
                            continue;

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

                        EmiSelection? selection = EmiContract.TryParseSelection(version, metadata.AsSpan(0, (int)bytesReturned), EmiChannelRole.Dram);
                        if (selection is not null)
                        {
                            _deviceHandle = handle;
                            _initialized = true;
                            _emiVersion = selection.Version;
                            _channelCount = selection.ChannelCount;
                            _channelIndex = selection.ChannelIndex;
                            _channelName = selection.ChannelName;
                            _statusMessage = $"Selected EMI channel '{_channelName}' (index {_channelIndex} of {_channelCount}). Intel RAPL DRAM = memory subsystem.";
                            AppLogger.Info($"DRAM Power: {_statusMessage} Device={devicePath}. EMI version={version}.");
                            return Task.CompletedTask;
                        }

                        handle.Dispose();
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detailBuffer);
                    }
                }

                if (enumeratedDeviceCount == 0)
                {
                    LogOnce("DRAM Power: No EMI device interfaces enumerated. Intel Power Management driver (ipmDrv) is not installed.");
                }
                else
                {
                    LogOnce($"DRAM Power: Enumerated {enumeratedDeviceCount} EMI device(s) but none exposed a channel whose name ends in _DRAM. This platform does not expose a DRAM RAPL rail via EMI.");
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }
        }
        catch (Exception ex)
        {
            LogOnce($"DRAM Power: Init exception: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        if (!_initialized || _deviceHandle is null || _deviceHandle.IsInvalid)
        {
            snapshot.Dependencies.DramPowerSource = string.Empty;
            snapshot.Dependencies.DramPowerStatusMessage = _statusMessage;
            MeasurementTraceRecorder.Record(
                snapshot,
                "dram_power",
                nameof(DramPowerEmiCollector),
                "Windows Energy Meter Interface (EMI) RAPL DRAM",
                false,
                "--",
                "DRAM EMI collector unavailable; no RAPL_Package<N>_DRAM channel accepted",
                _statusMessage,
                "EMI energy measurement IOCTLs",
                "--",
                "picowatt-hours",
                "energy delta over elapsed time",
                "--",
                "W",
                TelemetryValidationState.Unavailable,
                _statusMessage);
            return Task.CompletedTask;
        }

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
            // DRAM is typically a fraction of a watt to a few watts
            // even under load; clamp matches CPU / iGPU bounds.
            if (watts is >= 0 and < 500)
            {
                snapshot.Dependencies.DramPowerWatts = watts.Value;
                snapshot.Dependencies.DramPowerSource = "EMI RAPL DRAM";
                snapshot.Dependencies.DramPowerStatusMessage = $"DRAM power from Intel RAPL DRAM channel '{_channelName}'. Intel SDM Vol 3B §15.10.3.";
                MeasurementTraceRecorder.Record(
                    snapshot,
                    "dram_power",
                    nameof(DramPowerEmiCollector),
                    "Windows Energy Meter Interface (EMI) RAPL DRAM",
                    true,
                    $"{watts.Value:0.0}W",
                    $"channel={_channelName}; version={_emiVersion}; channelIndex={_channelIndex}; channelCount={_channelCount}",
                    "DRAM power from Intel RAPL DRAM energy deltas. Hardware validation is still pending.",
                    "EMI measurement channel",
                    $"{measurement.AbsoluteEnergyPicowattHours}",
                    "picowatt-hours",
                    "(delta picowatt-hours * 3.6e-9) / elapsed seconds",
                    $"{watts.Value:0.000}",
                    "W",
                    TelemetryValidationState.HardwareValidationPending,
                    "EMI reported a valid non-negative energy delta for the RAPL DRAM channel.");
            }
            else
            {
                snapshot.Dependencies.DramPowerWatts = 0;
                MeasurementTraceRecorder.Record(
                    snapshot,
                    "dram_power",
                    nameof(DramPowerEmiCollector),
                    "Windows Energy Meter Interface (EMI) RAPL DRAM",
                    false,
                    "--",
                    $"channel={_channelName}; version={_emiVersion}; channelIndex={_channelIndex}; invalid or first delta",
                    "DRAM power from EMI RAPL was rejected this tick.",
                    "EMI measurement channel",
                    $"{measurement.AbsoluteEnergyPicowattHours}",
                    "picowatt-hours",
                    "(delta picowatt-hours * 3.6e-9) / elapsed seconds",
                    "--",
                    "W",
                    TelemetryValidationState.Rejected,
                    "EMI did not produce a valid non-negative watt delta in the allowed range.");
            }
        }
        else
        {
            snapshot.Dependencies.DramPowerWatts = 0;
            MeasurementTraceRecorder.Record(
                snapshot,
                "dram_power",
                nameof(DramPowerEmiCollector),
                "Windows Energy Meter Interface (EMI) RAPL DRAM",
                false,
                "--",
                $"channel={_channelName}; version={_emiVersion}; baseline pending",
                "DRAM power requires two EMI samples before watts can be computed.",
                "EMI measurement channel",
                $"{measurement.AbsoluteEnergyPicowattHours}",
                "picowatt-hours",
                "baseline sample only",
                "--",
                "W",
                TelemetryValidationState.Unavailable,
                "First EMI sample captured as baseline.");
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
        _statusMessage = message;
        if (_loggedFailure)
            return;

        _loggedFailure = true;
        AppLogger.Info(message);
    }

    // ── Constants ──

    private const int SystemBatteryStateLevel = 5;
    private static readonly IntPtr InvalidHandleValue = new(-1);

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
