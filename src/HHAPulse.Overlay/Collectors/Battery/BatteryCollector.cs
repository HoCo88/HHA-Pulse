using System.Runtime.InteropServices;
using HHAPulse.Shared.Models;

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

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern int CallNtPowerInformation(
        int informationLevel,
        IntPtr inputBuffer,
        uint inputBufferLength,
        out SYSTEM_BATTERY_STATE outputBuffer,
        uint outputBufferLength);

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
}
