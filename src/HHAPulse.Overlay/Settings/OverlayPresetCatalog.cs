namespace HHAPulse.Overlay.Settings;

public static class OverlayPresetCatalog
{
    public const string Fps = "fps";
    public const string OnePercentLow = "one_percent_low";
    public const string FrameTime = "frametime";
    public const string Battery = "battery";
    public const string CpuUsage = "cpu";
    public const string GpuUsage = "gpu";
    public const string Ram = "ram";
    public const string Vram = "vram";
    public const string RefreshRate = "refresh_rate";

    private static readonly string[] HudMetrics =
    {
        Fps,
        OnePercentLow,
        FrameTime,
        CpuUsage,
        GpuUsage,
        Ram,
        Vram,
        RefreshRate,
        Battery
    };

    public static IReadOnlyList<string> GetMetricIds(OverlayPreset preset, IReadOnlyList<string> customMetricIds)
    {
        return preset switch
        {
            OverlayPreset.Hud => HudMetrics,
            OverlayPreset.Off => Array.Empty<string>(),
            _ => HudMetrics
        };
    }

    public static OverlayLayout GetLayout(OverlayPreset preset, IReadOnlyList<string> customMetricIds)
    {
        var metrics = GetMetricIds(preset, customMetricIds);
        return new OverlayLayout(metrics);
    }

    public static OverlayPreset NextPreset(OverlayPreset preset)
    {
        return preset == OverlayPreset.Off ? OverlayPreset.Hud : OverlayPreset.Off;
    }
}

public sealed record OverlayLayout(IReadOnlyList<string> TopBarMetricIds);
