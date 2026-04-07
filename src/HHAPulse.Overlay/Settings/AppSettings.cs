namespace HHAPulse.Overlay.Settings;

public sealed class AppSettings
{
    public OverlayPreset ActivePreset { get; set; }

    public OverlayEdge TopBarPosition { get; set; }

    public PanelEdge SidePanelPosition { get; set; }

    public double BackgroundOpacity { get; set; }

    public double TextOpacity { get; set; }

    public OverlayFontSize FontSize { get; set; }

    public TimeSpan UpdateInterval { get; set; }

    public int SidePanelWidthPixels { get; set; }

    public List<string> EnabledMetricIds { get; set; } = new();

    public static AppSettings CreateDefault()
    {
        return new AppSettings
        {
            ActivePreset = OverlayPreset.Minimal,
            TopBarPosition = OverlayEdge.Top,
            SidePanelPosition = PanelEdge.Right,
            BackgroundOpacity = 0.85,
            TextOpacity = 1.0,
            FontSize = OverlayFontSize.Medium,
            UpdateInterval = TimeSpan.FromSeconds(1),
            SidePanelWidthPixels = 260,
            EnabledMetricIds = new List<string> { "fps", "battery" }
        };
    }
}

public enum OverlayPreset
{
    Minimal = 0,
    Standard = 1,
    Tuner = 2,
    Diagnostic = 3,
    Custom = 4,
    Off = 5
}

public enum OverlayEdge
{
    Top = 0,
    Bottom = 1
}

public enum PanelEdge
{
    Left = 0,
    Right = 1
}

public enum OverlayFontSize
{
    Small = 0,
    Medium = 1,
    Large = 2
}
