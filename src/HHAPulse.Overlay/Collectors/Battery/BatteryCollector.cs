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
        var state = new SystemBatteryState();
        int status = CallNtPowerInformation(
            SystemBatteryState,
            IntPtr.Zero,
            0,
            out state,
            (uint)Marshal.SizeOf<SystemBatteryState>());

        if (status != 0)
            return Task.CompletedTask;

        if (!state.BatteryPresent)
            return Task.CompletedTask;

        snapshot.AvailableMetrics |= MetricFlags.Battery;

        snapshot.Battery.IsCharging = state.Charging || state.AcOnLine;

        if (state.MaxCapacity > 0)
        {
            snapshot.Battery.ChargePercent = state.RemainingCapacity * 100.0 / state.MaxCapacity;
        }

        // Rate is signed: negative = discharging (mW). Convert to positive watts.
        if (state.Rate < 0)
        {
            snapshot.Battery.DischargeWatts = state.Rate / -1000.0;
        }
        else
        {
            snapshot.Battery.DischargeWatts = 0;
        }

        // EstimatedTime is in seconds, 0xFFFFFFFF means unknown
        if (state.EstimatedTime != 0xFFFFFFFF && state.EstimatedTime > 0)
        {
            snapshot.Battery.EstimatedMinutesRemaining = state.EstimatedTime / 60.0;
        }

        return Task.CompletedTask;
    }

    private const int SystemBatteryState = 5;

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern int CallNtPowerInformation(
        int informationLevel,
        IntPtr inputBuffer,
        uint inputBufferLength,
        out SystemBatteryState outputBuffer,
        uint outputBufferLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemBatteryState
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
