namespace HHAPulse.Overlay.Profiles;

public sealed class GameProfile
{
    public string Name { get; set; } = string.Empty;

    public string ExecutableName { get; set; } = string.Empty;

    public bool OverlayEnabled { get; set; } = true;
}
