# WinUI 3 Overlay Best Practices (April 2026)

## Transparent Window (PROVEN approach)

WinUI 3 has NO native transparent window support. Must use Win32 interop.

### Option A: WinUIEx NuGet (simplest)
```
NuGet: WinUIEx (v2.9.0+)
Use TransparentTintBackdrop class
```

### Option B: Manual DWM Interop
```csharp
// 1. Get HWND
var hwnd = WindowNative.GetWindowHandle(this);

// 2. DWM extend frame
DwmExtendFrameIntoClientArea(hwnd, new MARGINS(-1));

// 3. DWM blur behind (trick for transparency)
var rgn = CreateRectRgn(-2, -2, -1, -1);
DwmEnableBlurBehindWindow(hwnd, blurBehind with region);

// 4. Composition brush with Alpha=0
compositor.CreateColorBrush(Color.FromArgb(0, 255, 255, 255));
```

### Option C: Transparent SystemBackdrop (recommended for clean code)
```csharp
internal class TransparentBackdrop : SystemBackdrop
{
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target, XamlRoot root)
    {
        target.SystemBackdrop = CompositionTarget.GetCompositorForCurrentThread()
            .CreateColorBrush(Color.FromArgb(0, 255, 255, 255));
    }
}
// XAML: <Window SystemBackdrop="{local:TransparentBackdrop}" />
```

## Chromeless Overlay Window (CRITICAL)

`OverlappedPresenter.SetBorderAndTitleBar(false, false)` is NOT enough for a true chromeless window. WinUI 3 keeps `WS_CAPTION | WS_THICKFRAME | WS_SYSMENU` in the window style, reserving invisible non-client area that steals ~8px from content height.

**Required steps for pixel-accurate chromeless overlay:**
```csharp
// 1. Strip all non-client chrome from GWL_STYLE
var style = GetWindowLongPtr(hwnd, GWL_STYLE);
style &= ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU);
style |= WS_POPUP;
SetWindowLongPtr(hwnd, GWL_STYLE, style);

// 2. Square corners on Windows 11 (removes pill/rounded shape)
DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE (33), DWMWCP_DONOTROUND (1));

// 3. Size with Win32 SetWindowPos, NOT AppWindow.MoveAndResize
//    MoveAndResize internally compensates for non-client area that no longer exists.
SetWindowPos(hwnd, HWND_TOPMOST, x, y, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
```

**Do NOT use** `ExtendsContentIntoTitleBar = true` alongside Win32 chrome stripping — they conflict and add internal drag region padding.

## Slim HUD Bar Sizing

For the runtime HUD bar, the overlay window must size from measured content, not from hand-written metric width estimates.

Required pattern:

```csharp
// 1. Render current metric values into TextBlocks first.
UpdateMetricValues();

// 2. Measure cells with an unbounded size.
element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
var contentWidth = element.DesiredSize.Width;

// 3. Add root padding, publish the measured width, then resize the HWND.
EstimatedWidth = (int)Math.Ceiling(contentWidth + RootBorder.Padding.Left + RootBorder.Padding.Right);
LayoutMetricsChanged?.Invoke(this, EventArgs.Empty);
SetWindowPos(hwnd, HWND_TOPMOST, x, y, EstimatedWidth, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
```

Rules:

- Measure after live formatted values are written. Battery, RAM, VRAM, time remaining, watts, and used/total memory strings can grow beyond any static estimate.
- Re-evaluate one-row vs two-row layout from measured element widths when content exceeds the display work area.
- Keep `SetWindowPos` in `MainWindow` as the final window sizing mechanism.
- Preserve the slim HUD look: no nested cards, no side panel, no group separators, no large graph chrome.
- Preserve compact label styling: Segoe UI abbreviations where text is clearer, Segoe Fluent Icons for power/temperature/refresh/battery glyphs, current metric colors, and compact sparkline visuals.

## Always-On-Top

**Use `SetWindowPos(HWND_TOPMOST)` â€” NOT `IsAlwaysOnTop`!**

`OverlappedPresenter.IsAlwaysOnTop` has a KNOWN BUG: breaks `WS_EX_TRANSPARENT` click-through.

```csharp
SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
```

## Click-Through

For a **small top bar** (~40px): no click-through needed â€” window only covers the bar area.

For **full-screen transparent overlay**:
```csharp
SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
```

For **partial click-through** (bar interactive, rest passes through):
```csharp
var rgn = CreateRectRgn(0, 0, windowWidth, 40); // only top 40px interactive
SetWindowRgn(hwnd, rgn, true);
```

## Hide from Taskbar + Don't Steal Focus
```csharp
SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
AppWindow.IsShownInSwitchers = false;
```

## Threading

- ALL UI updates must go through `DispatcherQueue.TryEnqueue()`
- Named pipe callbacks fire on worker threads â€” ALWAYS dispatch
- `TryEnqueue` returns bool, not Task â€” use `TaskCompletionSource` if you need await

## XAML Memory Leaks

- **NEVER** use `x:Bind` in MainWindow.xaml (WinUI 3 leak: ~1000 objects per window)
- `x:Bind` is SAFE in child Pages/UserControls
- Subscribe events in `Loaded`, unsubscribe in `Unloaded`
- Graphs: imperative Canvas drawing, NOT XAML data binding

## Deployment

- `WindowsAppSDKSelfContained=true` â€” eliminates runtime dependency (+200MB but reliable)
- Unpackaged WinUI 3 silently fails if SDK runtime missing â€” self-contained avoids this
- Use `dotnet publish` for EXE, `msbuild` for MSIX (NOT `dotnet build` for UWP/wapproj)

## Reference Projects (April 2026)
- GuildOfCalamity/Transparency â€” best current transparent window example
- castorix/WinUI3_SwapChainPanel_Layered â€” full D3D pipeline with click-through
- WinUICommunity â€” production-quality backdrop helpers
- WinUIEx NuGet â€” TransparentTintBackdrop + window extensions

