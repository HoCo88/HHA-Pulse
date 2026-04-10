using System.Runtime.InteropServices;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Overlay.Settings;
using HHAPulse.Shared.Models;
using Microsoft.Win32.SafeHandles;

namespace HHAPulse.Overlay.Collectors.Battery;

public sealed class BatteryCollector : IMetricCollector
{
    public string Name => "Battery";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        var state = new SYSTEM_BATTERY_STATE();
        int status = CallNtPowerInformation(
            SystemBatteryStateLevel,
            IntPtr.Zero,
            0,
            out state,
            (uint)Marshal.SizeOf<SYSTEM_BATTERY_STATE>());

        if (status != 0 || !state.BatteryPresent)
        {
            return Task.CompletedTask;
        }

        snapshot.AvailableMetrics |= MetricFlags.Battery;
        snapshot.Battery.IsCharging = state.Charging || state.AcOnLine;

        if (state.MaxCapacity > 0)
        {
            snapshot.Battery.ChargePercent = ComputeChargePercent(state.RemainingCapacity, state.MaxCapacity);
        }

        if (state.Rate < 0)
        {
            snapshot.Battery.DischargeWatts = state.Rate / -1000.0;
        }
        else if (state.Rate > 0)
        {
            snapshot.Battery.ChargeWatts = state.Rate / 1000.0;
        }

        if (state.EstimatedTime != 0xFFFFFFFF && state.EstimatedTime > 0)
        {
            snapshot.Battery.EstimatedMinutesRemaining = state.EstimatedTime / 60.0;
        }

        var batteryIoctlStatus = TryPopulateBatteryIoctlFields(snapshot, state);
        RecordBatteryTrace(snapshot, state, batteryIoctlStatus);

        return Task.CompletedTask;
    }

    internal static double ComputeChargePercent(uint remainingCapacity, uint maxCapacity)
    {
        if (maxCapacity == 0)
        {
            return 0d;
        }

        double rawPercent = remainingCapacity * 100.0 / maxCapacity;
        return Math.Clamp(rawPercent, 0d, 100d);
    }

    private const int SystemBatteryStateLevel = 5;
    private const uint DIGCF_PRESENT = 0x00000002;
    private const uint DIGCF_DEVICEINTERFACE = 0x00000010;
    private const uint FileAccessGenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint IoctlBatteryQueryTag = 0x00294040;
    private const uint IoctlBatteryQueryInformation = 0x00294044;
    private const uint BatteryCapacityRelative = 0x40000000;
    private static readonly Guid BatteryInterfaceGuid = new("72631E54-78A4-11D0-BCF7-00AA00B7B32A");
    private static readonly IntPtr InvalidHandleValue = new(-1);

    private static string TryPopulateBatteryIoctlFields(TelemetrySnapshot snapshot, SYSTEM_BATTERY_STATE state)
    {
        var batteryInterfaceGuid = BatteryInterfaceGuid;
        var deviceInfoSet = SetupDiGetClassDevs(
            ref batteryInterfaceGuid,
            null,
            IntPtr.Zero,
            DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);

        if (deviceInfoSet == InvalidHandleValue)
        {
            return "Battery class IOCTL unavailable; using SystemBatteryState percent/rate only.";
        }

        try
        {
            for (uint index = 0; index < 16; index++)
            {
                var interfaceData = new SP_DEVICE_INTERFACE_DATA
                {
                    cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>()
                };

                if (!SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref batteryInterfaceGuid, index, ref interfaceData))
                {
                    break;
                }

                uint requiredSize = 0;
                SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, ref requiredSize, IntPtr.Zero);
                    if (requiredSize == 0)
                    {
                        continue;
                }

                var detailBuffer = Marshal.AllocHGlobal((int)requiredSize);
                try
                {
                    Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, detailBuffer, requiredSize, ref requiredSize, IntPtr.Zero))
                    {
                        continue;
                    }

                    var devicePath = Marshal.PtrToStringUni(detailBuffer + 4) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(devicePath))
                    {
                        continue;
                    }

                    using var handle = CreateFile(
                        devicePath,
                        FileAccessGenericRead,
                        FileShareRead | FileShareWrite,
                        IntPtr.Zero,
                        OpenExisting,
                        0,
                        IntPtr.Zero);

                    if (handle.IsInvalid)
                    {
                        continue;
                    }

                    uint tag = 0;
                    uint waitMilliseconds = 0;
                    if (!DeviceIoControl(handle, IoctlBatteryQueryTag, ref waitMilliseconds, (uint)sizeof(uint), ref tag, (uint)sizeof(uint), out _, IntPtr.Zero) || tag == 0)
                    {
                        continue;
                    }

                    var query = new BATTERY_QUERY_INFORMATION
                    {
                        BatteryTag = tag,
                        InformationLevel = BatteryQueryInformationLevel.BatteryInformation
                    };

                    if (!DeviceIoControl(
                            handle,
                            IoctlBatteryQueryInformation,
                            ref query,
                            (uint)Marshal.SizeOf<BATTERY_QUERY_INFORMATION>(),
                            out BATTERY_INFORMATION info,
                            (uint)Marshal.SizeOf<BATTERY_INFORMATION>(),
                            out _,
                            IntPtr.Zero))
                    {
                        continue;
                    }

                    if ((info.Capabilities & BatteryCapacityRelative) != 0)
                    {
                        return $"Battery class IOCTL rejected Wh capacity conversion because BATTERY_CAPACITY_RELATIVE is set (Capabilities=0x{info.Capabilities:X8}).";
                    }

                    if (info.DesignedCapacity > 0)
                    {
                        snapshot.Battery.DesignCapacityWattHours = info.DesignedCapacity / 1000.0;
                    }

                    if (info.FullChargedCapacity > 0)
                    {
                        snapshot.Battery.HealthPercent = info.DesignedCapacity > 0
                            ? Math.Clamp(info.FullChargedCapacity * 100.0 / info.DesignedCapacity, 0, 100)
                            : 0;
                    }

                    if (state.RemainingCapacity > 0)
                    {
                        snapshot.Battery.CurrentCapacityWattHours = state.RemainingCapacity / 1000.0;
                    }

                    snapshot.Battery.CycleCount = info.CycleCount > int.MaxValue ? 0 : (int)info.CycleCount;
                    TryPopulateBatteryEstimatedTime(handle, tag, snapshot);
                    return "Battery class IOCTL returned absolute mWh capacity data.";
                }
                finally
                {
                    Marshal.FreeHGlobal(detailBuffer);
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }

        return "Battery class IOCTL capacity data unavailable; using SystemBatteryState percent/rate only.";
    }

    private static void TryPopulateBatteryEstimatedTime(SafeFileHandle handle, uint tag, TelemetrySnapshot snapshot)
    {
        var query = new BATTERY_QUERY_INFORMATION
        {
            BatteryTag = tag,
            InformationLevel = BatteryQueryInformationLevel.BatteryEstimatedTime,
            AtRate = 0
        };

        if (DeviceIoControl(
                handle,
                IoctlBatteryQueryInformation,
                ref query,
                (uint)Marshal.SizeOf<BATTERY_QUERY_INFORMATION>(),
                out uint seconds,
                (uint)sizeof(uint),
                out _,
                IntPtr.Zero) &&
            seconds is > 0 and not 0xFFFFFFFF)
        {
            snapshot.Battery.EstimatedMinutesRemaining = seconds / 60.0;
        }
    }

    private static void RecordBatteryTrace(TelemetrySnapshot snapshot, SYSTEM_BATTERY_STATE state, string batteryIoctlStatus)
    {
        MeasurementTraceRecorder.Record(
            snapshot,
            OverlayPresetCatalog.Battery,
            nameof(BatteryCollector),
            "CallNtPowerInformation(SystemBatteryState) + battery class IOCTL",
            true,
            FormatBatteryValue(snapshot),
            $"chargePercent={snapshot.Battery.ChargePercent:0.0}; dischargeWatts={snapshot.Battery.DischargeWatts:0.000}; chargeWatts={snapshot.Battery.ChargeWatts:0.000}; remainingWh={snapshot.Battery.CurrentCapacityWattHours:0.000}; designWh={snapshot.Battery.DesignCapacityWattHours:0.000}; cycleCount={snapshot.Battery.CycleCount}; {batteryIoctlStatus}",
            "Battery percent, charge/discharge watts, and runtime from Windows power APIs. Capacity fields require absolute mWh units.",
            "SYSTEM_BATTERY_STATE and IOCTL_BATTERY_QUERY_INFORMATION",
            $"Rate={state.Rate}; RemainingCapacity={state.RemainingCapacity}; MaxCapacity={state.MaxCapacity}; EstimatedTime={state.EstimatedTime}",
            "mW/mWh/seconds",
            "Rate mW -> W; capacity mWh -> Wh; estimated seconds -> minutes",
            FormatBatteryValue(snapshot),
            "mixed",
            TelemetryValidationState.Verified,
            $"Battery is present and Windows returned a valid battery state. {batteryIoctlStatus}");
    }

    private static string FormatBatteryValue(TelemetrySnapshot snapshot)
    {
        if (snapshot.Battery.IsCharging)
        {
            return snapshot.Battery.ChargeWatts > 0
                ? $"{snapshot.Battery.ChargePercent:0.0}% +{snapshot.Battery.ChargeWatts:0.0}W"
                : $"{snapshot.Battery.ChargePercent:0.0}% AC";
        }

        if (snapshot.Battery.EstimatedMinutesRemaining > 0)
        {
            return $"{snapshot.Battery.ChargePercent:0.0}% {snapshot.Battery.EstimatedMinutesRemaining:0}m {snapshot.Battery.DischargeWatts:0.0}W";
        }

        return snapshot.Battery.DischargeWatts > 0
            ? $"{snapshot.Battery.ChargePercent:0.0}% {snapshot.Battery.DischargeWatts:0.0}W"
            : $"{snapshot.Battery.ChargePercent:0.0}%";
    }

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
        ref uint lpInBuffer,
        uint nInBufferSize,
        ref uint lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        ref BATTERY_QUERY_INFORMATION lpInBuffer,
        uint nInBufferSize,
        out BATTERY_INFORMATION lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        ref BATTERY_QUERY_INFORMATION lpInBuffer,
        uint nInBufferSize,
        out uint lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

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

    private enum BatteryQueryInformationLevel
    {
        BatteryInformation = 0,
        BatteryEstimatedTime = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BATTERY_QUERY_INFORMATION
    {
        public uint BatteryTag;
        public BatteryQueryInformationLevel InformationLevel;
        public int AtRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BATTERY_INFORMATION
    {
        public uint Capabilities;
        public byte Technology;
        public byte Reserved0;
        public byte Reserved1;
        public byte Reserved2;
        public byte Chemistry0;
        public byte Chemistry1;
        public byte Chemistry2;
        public byte Chemistry3;
        public uint DesignedCapacity;
        public uint FullChargedCapacity;
        public uint DefaultAlert1;
        public uint DefaultAlert2;
        public uint CriticalBias;
        public uint CycleCount;
    }
}
