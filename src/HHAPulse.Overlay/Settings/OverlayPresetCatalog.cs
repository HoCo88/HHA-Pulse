namespace HHAPulse.Overlay.Settings;

public static class OverlayPresetCatalog
{
    // ── Performance ──
    public const string Fps = "fps";
    public const string AvgFps = "avg_fps";
    public const string OnePercentLow = "one_percent_low";
    public const string ZeroPointOneLow = "zero_point_one_low";
    public const string FrameTime = "frametime";
    public const string FrameGenFps = "framegen_fps";
    public const string InputLatency = "input_latency";

    // ── CPU ──
    public const string CpuUsage = "cpu";
    public const string CpuPower = "cpu_power";

    // ── GPU ──
    public const string GpuUsage = "gpu";
    public const string GpuTemp = "gpu_temp";
    public const string GpuPower = "gpu_power";
    public const string GpuFan = "gpu_fan";
    public const string Vram = "vram";

    // ── System ──
    public const string Ram = "ram";
    public const string TotalPower = "total_power";
    public const string Battery = "battery";
    public const string RefreshRate = "refresh_rate";

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
        GpuUsage,
        GpuTemp,
        GpuFan,
        Ram,
        Vram,
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
        FrameGenFps,
        InputLatency,
        CpuUsage,
        CpuPower,
        GpuUsage,
        GpuTemp,
        GpuPower,
        GpuFan,
        Ram,
        Vram,
        TotalPower,
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
            OverlayPreset.Custom => customMetricIds.Count > 0 ? customMetricIds : TunerMetrics,
            OverlayPreset.Off => Array.Empty<string>(),
            _ => StandardMetrics
        };
    }

    public static OverlayLayout GetLayout(OverlayPreset preset, IReadOnlyList<string> customMetricIds)
    {
        var metrics = GetMetricIds(preset, customMetricIds);
        return new OverlayLayout(metrics);
    }

    /// <summary>
    /// Cycles presets: Minimal → Standard → Tuner → Off → Minimal.
    /// Custom is not part of the cycle.
    /// </summary>
    public static OverlayPreset NextPreset(OverlayPreset preset)
    {
        return preset switch
        {
            OverlayPreset.Minimal => OverlayPreset.Standard,
            OverlayPreset.Standard => OverlayPreset.Tuner,
            OverlayPreset.Tuner => OverlayPreset.Off,
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
            OverlayPreset.Tuner => "Tuner",
            OverlayPreset.Custom => "Custom",
            OverlayPreset.Off => "Off",
            _ => "Standard"
        };
    }
}

public sealed record OverlayLayout(IReadOnlyList<string> TopBarMetricIds);
