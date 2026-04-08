using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace HHAPulse.Overlay.Interop;

internal sealed class TransparentBackdrop : SystemBackdrop
{
    protected override void OnTargetConnected(
        ICompositionSupportsSystemBackdrop connectedTarget,
        XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        var controller = new DesktopAcrylicController
        {
            TintColor = Windows.UI.Color.FromArgb(0, 0, 0, 0),
            TintOpacity = 0,
            LuminosityOpacity = 0,
            FallbackColor = Windows.UI.Color.FromArgb(0, 0, 0, 0)
        };
        controller.AddSystemBackdropTarget(connectedTarget);
        controller.SetSystemBackdropConfiguration(
            GetDefaultSystemBackdropConfiguration(connectedTarget, xamlRoot));
    }

    protected override void OnTargetDisconnected(
        ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);
    }
}
