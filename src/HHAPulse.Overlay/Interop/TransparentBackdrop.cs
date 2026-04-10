using System;
using HHAPulse.Overlay.Diagnostics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace HHAPulse.Overlay.Interop;

/// <summary>
/// Forces the WinUI 3 window's composition target clear color to fully
/// transparent so XAML content with <c>alpha &lt; 255</c> composites through
/// to the desktop.
///
/// Without this backdrop, WinUI 3's SwapChain defaults to an opaque black
/// clear color, so the HUD background at 0% opacity still renders black even
/// with <see cref="TransparentWindowHelper"/>'s DWM glass-frame setup. DWM
/// blur-behind only makes the window chrome transparent; it does not clear
/// the SwapChain interior. Three layers compose the final pixel:
/// <list type="number">
///   <item>DWM glass frame — handled by <c>DwmExtendFrameIntoClientArea</c> +
///         <c>DwmEnableBlurBehindWindow</c> in <see cref="TransparentWindowHelper"/>.</item>
///   <item>WinUI composition target / SwapChain clear color — handled here
///         by assigning a fully-transparent composition color brush.</item>
///   <item>XAML content — handled by the <c>RootBorder.Background</c> alpha
///         in <c>TopBarControl.ApplyOpacity</c>.</item>
/// </list>
/// All three must be transparent for a true see-through HUD.
/// </summary>
/// <remarks>
/// <para>
/// <b>WinAppSDK 1.8 type projection trap (learned the hard way, documented
/// for future-me):</b> in WinAppSDK 1.8,
/// <see cref="ICompositionSupportsSystemBackdrop.SystemBackdrop"/> is typed
/// as <c>Windows.UI.Composition.CompositionBrush</c> (the legacy WinRT
/// Composition namespace). But every XAML-side API that produces a
/// <c>Compositor</c> returns a <see cref="Compositor"/> from
/// <c>Microsoft.UI.Composition</c> instead — that includes both
/// <see cref="CompositionTarget.GetCompositorForCurrentThread"/> and
/// <see cref="Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual"/>.
/// The two Compositor types are <i>not</i> the same WinRT COM interface —
/// a <see cref="WinRT.CastExtensions.As{T}"/> between them throws
/// <see cref="InvalidCastException"/> at runtime (see HHAP-0.4 crash log).
/// </para>
/// <para>
/// The only way to obtain a legacy <c>Windows.UI.Composition.Compositor</c>
/// in WinAppSDK 1.8 desktop is to construct one directly with
/// <c>new global::Windows.UI.Composition.Compositor()</c>. That type has a
/// public COM-activatable constructor and the Windows.UI.Composition runtime
/// is always available. The standalone compositor is independent of the XAML
/// one, but the <see cref="CompositionColorBrush"/> it produces is a valid
/// <c>Windows.UI.Composition.CompositionBrush</c> and assignable to the
/// <see cref="ICompositionSupportsSystemBackdrop.SystemBackdrop"/> setter.
/// </para>
/// <para>
/// The compositor is kept alive as a field so the brush it created does not
/// become invalid after GC. Both connect/disconnect paths are wrapped in
/// try/catch because the overlay must still launch and show its HUD if the
/// transparency plumbing ever fails.
/// </para>
/// </remarks>
internal sealed class TransparentBackdrop : SystemBackdrop
{
    private global::Windows.UI.Composition.Compositor? _compositor;

    protected override void OnTargetConnected(
        ICompositionSupportsSystemBackdrop connectedTarget,
        XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        try
        {
            _compositor ??= new global::Windows.UI.Composition.Compositor();
            global::Windows.UI.Composition.CompositionColorBrush transparentBrush =
                _compositor.CreateColorBrush(Color.FromArgb(0, 0, 0, 0));
            connectedTarget.SystemBackdrop = transparentBrush;
        }
        catch (Exception ex)
        {
            AppLogger.Error(
                "TransparentBackdrop: Failed to install transparent SwapChain brush. " +
                "The HUD will still render but 0% background opacity may show as black. " +
                "See exception for details.",
                ex);
        }
    }

    protected override void OnTargetDisconnected(
        ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        try
        {
            disconnectedTarget.SystemBackdrop = null;
        }
        catch (Exception ex)
        {
            AppLogger.Error("TransparentBackdrop: Failed to clear SwapChain brush on disconnect.", ex);
        }

        _compositor?.Dispose();
        _compositor = null;

        base.OnTargetDisconnected(disconnectedTarget);
    }
}
