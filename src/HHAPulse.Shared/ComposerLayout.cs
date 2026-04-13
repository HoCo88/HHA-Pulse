namespace HHAPulse.Shared;

/// <summary>
/// Layout shape the user picks in step 1 of the Custom composer.
/// </summary>
public enum ComposerLayout
{
    TopBar,
    BottomBar,
    DualLine,
    LeftDock,
    RightDock,
    SidePanel
}

public static class ComposerLayoutExtensions
{
    public static string DisplayName(this ComposerLayout layout)
    {
        switch (layout)
        {
            case ComposerLayout.TopBar: return "Top bar";
            case ComposerLayout.BottomBar: return "Bottom bar";
            case ComposerLayout.DualLine: return "Dual line";
            case ComposerLayout.LeftDock: return "Left dock";
            case ComposerLayout.RightDock: return "Right dock";
            case ComposerLayout.SidePanel: return "Side panel";
            default: return "Top bar";
        }
    }

    public static string Description(this ComposerLayout layout)
    {
        switch (layout)
        {
            case ComposerLayout.TopBar: return "Slim strip across the top of the screen";
            case ComposerLayout.BottomBar: return "Slim strip across the bottom of the screen";
            case ComposerLayout.DualLine: return "Two rows for denser metric counts";
            case ComposerLayout.LeftDock: return "Vertical dock on the left edge";
            case ComposerLayout.RightDock: return "Vertical dock on the right edge";
            case ComposerLayout.SidePanel: return "Wide side panel with room for graphs";
            default: return string.Empty;
        }
    }
}
