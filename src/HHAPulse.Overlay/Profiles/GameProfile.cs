using HHAPulse.Overlay.Settings;

namespace HHAPulse.Overlay.Profiles;

public sealed class GameProfile
{
    public string Name { get; set; } = string.Empty;

    public string ExecutableName { get; set; } = string.Empty;

    public OverlayPreset Preset { get; set; } = OverlayPreset.Minimal;

    public List<string> EnabledMetricIds { get; set; } = new();
}
