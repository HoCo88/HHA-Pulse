namespace HHAPulse.Overlay.Settings;

public sealed class AppSettings
{
    public OverlayPreset ActivePreset { get; set; }

    public OverlayPreset ManualSourcePreset { get; set; }

    public TopBarPosition TopBarPosition { get; set; }

    public double BackgroundOpacity { get; set; }

    public double TextOpacity { get; set; }

    public OverlayFontSize FontSize { get; set; }

    public double TextSizePixels { get; set; }

    public TimeSpan UpdateInterval { get; set; }

    public List<string> EnabledMetricIds { get; set; } = new();

    public OverlayShowMode ShowMode { get; set; }

    public static AppSettings CreateDefault()
    {
        return new AppSettings
        {
            ActivePreset = OverlayPreset.Standard,
            ManualSourcePreset = OverlayPreset.Standard,
            TopBarPosition = TopBarPosition.TopThin,
            BackgroundOpacity = 0.85,
            TextOpacity = 1.0,
            FontSize = OverlayFontSize.Medium,
            TextSizePixels = 15,
            UpdateInterval = TimeSpan.FromSeconds(1),
            EnabledMetricIds = new List<string>(),
            ShowMode = OverlayShowMode.Always
        };
    }
}

public enum OverlayPreset
{
    Minimal = 0,
    Standard = 1,
    Tuner = 2,
    Custom = 3,
    Off = 4,
    Full = 5
}

public enum TopBarPosition
{
    TopThin = 0,
    BottomThin = 1,
    TopTall = 2,
    BottomTall = 3,
    LeftDock = 4,
    RightDock = 5
}

public enum OverlayFontSize
{
    Small = 0,
    Medium = 1,
    Large = 2
}

public enum OverlayShowMode
{
    Always = 0,
    InGameOnly = 1
}
