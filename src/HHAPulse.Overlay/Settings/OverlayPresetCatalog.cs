namespace HHAPulse.Overlay.Settings;

public static class OverlayPresetCatalog
{
    // ── Performance ──
    public const string Fps = "fps";
    public const string AvgFps = "avg_fps";
    public const string OnePercentLow = "one_percent_low";
    public const string ZeroPointOneLow = "zero_point_one_low";
    public const string FrameTime = "frametime";
    public const string InputLatency = "input_latency"; // Reserved until real input-to-display latency exists.

    // ── CPU ──
    public const string CpuUsage = "cpu";
    public const string CpuTemp = "cpu_temp";
    public const string CpuPower = "cpu_power";
    public const string CpuClock = "cpu_clock";

    // ── GPU ──
    public const string GpuUsage = "gpu";
    public const string GpuTemp = "gpu_temp";
    public const string GpuClock = "gpu_clock";
    public const string GpuPower = "gpu_power";
    public const string GpuFan = "gpu_fan";
    public const string Vram = "vram";

    // ── System ──
    public const string Ram = "ram";
    public const string TotalPower = "total_power";
    public const string Battery = "battery";
    public const string RefreshRate = "refresh_rate";
    public const string DeviceTemp = "device_temp";
    public const string StorageTemp = "storage_temp";
    public const string StorageWear = "storage_wear";

    // ── Presets ──
    // Minimal: glanceable essentials.
    private static readonly string[] MinimalMetrics =
    {
        Fps,
        Battery
    };

    // Standard: what most gamers want.
    private static readonly string[] StandardMetrics =
    {
        Fps,
        OnePercentLow,
        FrameTime,
        CpuUsage,
        GpuUsage,
        Battery
    };

    // Tuner: everything with a real data source and a meaningful display unit.
    private static readonly string[] TunerMetrics =
    {
        Fps,
        OnePercentLow,
        FrameTime,
        CpuUsage,
        CpuTemp,
        CpuPower,
        GpuUsage,
        GpuTemp,
        Ram,
        RefreshRate,
        Battery
    };

    private static readonly string[] FullMetrics =
    {
        Fps,
        AvgFps,
        OnePercentLow,
        ZeroPointOneLow,
        FrameTime,
        CpuUsage,
        CpuTemp,
        CpuPower,
        GpuUsage,
        GpuTemp,
        GpuClock,
        GpuPower,
        GpuFan,
        Ram,
        Vram,
        TotalPower,
        DeviceTemp,
        RefreshRate,
        Battery
    };

    /// <summary>
    /// Every known metric in display order (for Custom mode picker).
    /// </summary>
    public static readonly string[] AllMetricIds =
    {
        Fps,
        AvgFps,
        OnePercentLow,
        ZeroPointOneLow,
        FrameTime,
        CpuUsage,
        CpuTemp,
        CpuPower,
        CpuClock,
        GpuUsage,
        GpuTemp,
        GpuClock,
        GpuPower,
        GpuFan,
        Ram,
        Vram,
        StorageTemp,
        StorageWear,
        TotalPower,
        DeviceTemp,
        RefreshRate,
        Battery
    };

    public static IReadOnlyList<string> GetMetricIds(OverlayPreset preset, IReadOnlyList<string> customMetricIds)
    {
        return preset switch
        {
            OverlayPreset.Minimal => MinimalMetrics,
            OverlayPreset.Standard => StandardMetrics,
            OverlayPreset.Tuner => TunerMetrics,
            OverlayPreset.Full => FullMetrics,
            OverlayPreset.Custom => customMetricIds.Count > 0 ? customMetricIds : TunerMetrics,
            OverlayPreset.Off => Array.Empty<string>(),
            _ => StandardMetrics
        };
    }

    public static OverlayLayout GetLayout(OverlayPreset preset, IReadOnlyList<string> customMetricIds, TopBarPosition position = TopBarPosition.TopThin)
    {
        var metrics = GetMetricIds(preset, customMetricIds);
        return new OverlayLayout(metrics, position, LineCountFor(position));
    }

    /// <summary>
    /// Cycles presets: Minimal → Standard → Tuner → Full → Off → Minimal.
    /// Custom is not part of the cycle.
    /// </summary>
    public static OverlayPreset NextPreset(OverlayPreset preset)
    {
        return preset switch
        {
            OverlayPreset.Minimal => OverlayPreset.Standard,
            OverlayPreset.Standard => OverlayPreset.Tuner,
            OverlayPreset.Tuner => OverlayPreset.Full,
            OverlayPreset.Full => OverlayPreset.Off,
            OverlayPreset.Off => OverlayPreset.Minimal,
            OverlayPreset.Custom => OverlayPreset.Minimal,
            _ => OverlayPreset.Standard
        };
    }

    public static string PresetDisplayName(OverlayPreset preset)
    {
        return preset switch
        {
            OverlayPreset.Minimal => "Minimal",
            OverlayPreset.Standard => "Standard",
            OverlayPreset.Tuner => "Advanced",
            OverlayPreset.Custom => "Manual",
            OverlayPreset.Off => "Off",
            OverlayPreset.Full => "Full",
            _ => "Standard"
        };
    }

    public static int LineCountFor(TopBarPosition position)
    {
        return position is TopBarPosition.TopTall or TopBarPosition.BottomTall ? 2 : 1;
    }
}

public sealed record OverlayLayout(IReadOnlyList<string> TopBarMetricIds, TopBarPosition Position, int LineCount);
