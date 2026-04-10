using System.Runtime.InteropServices;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Collectors.Display;

public sealed class DisplayCollector : IMetricCollector
{
    public string Name => "Display";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        var dm = new DEVMODE();
        dm.dmSize = (short)Marshal.SizeOf<DEVMODE>();

        if (!EnumDisplaySettings(null, EnumCurrentSettings, ref dm))
            return Task.CompletedTask;

        snapshot.AvailableMetrics |= MetricFlags.Display;
        snapshot.Display.WidthPixels = dm.dmPelsWidth;
        snapshot.Display.HeightPixels = dm.dmPelsHeight;
        snapshot.Display.RefreshRateHertz = dm.dmDisplayFrequency;
        var validRefresh = dm.dmDisplayFrequency > 1;
        MeasurementTraceRecorder.Record(
            snapshot,
            "refresh_rate",
            nameof(DisplayCollector),
            "EnumDisplaySettings",
            validRefresh,
            validRefresh ? $"{dm.dmDisplayFrequency:0}Hz" : "--",
            $"width={dm.dmPelsWidth}; height={dm.dmPelsHeight}; rawFrequency={dm.dmDisplayFrequency}; vrrSupported=not probed; vrrActive=not probed",
            validRefresh
                ? "Display refresh rate from Windows display settings."
                : "Display refresh raw value is 0 or 1, which Windows treats as hardware default rather than a literal Hz value.",
            "EnumDisplaySettings(DEVMODE.dmDisplayFrequency)",
            dm.dmDisplayFrequency.ToString(),
            "Hz or hardware-default sentinel",
            "accept only values > 1",
            validRefresh ? dm.dmDisplayFrequency.ToString() : "--",
            "Hz",
            validRefresh ? TelemetryValidationState.Verified : TelemetryValidationState.Rejected,
            validRefresh ? "Windows returned a concrete refresh rate." : "Raw refresh 0/1 is a sentinel and is hidden.");

        return Task.CompletedTask;
    }

    private const int EnumCurrentSettings = -1;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(
        string? lpszDeviceName,
        int iModeNum,
        ref DEVMODE lpDevMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;

        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;

        // Position union
        public int dmPositionX;
        public int dmPositionY;

        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;

        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;

        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;

        public int dmDisplayFlags;
        public int dmDisplayFrequency;

        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }
}
