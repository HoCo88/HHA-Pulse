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

### Option C: Custom SystemBackdrop (recommended for clean code)
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

## Always-On-Top

**Use `SetWindowPos(HWND_TOPMOST)` — NOT `IsAlwaysOnTop`!**

`OverlappedPresenter.IsAlwaysOnTop` has a KNOWN BUG: breaks `WS_EX_TRANSPARENT` click-through.

```csharp
SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
```

## Click-Through

For a **small top bar** (~40px): no click-through needed — window only covers the bar area.

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
- Named pipe callbacks fire on worker threads — ALWAYS dispatch
- `TryEnqueue` returns bool, not Task — use `TaskCompletionSource` if you need await

## XAML Memory Leaks

- **NEVER** use `x:Bind` in MainWindow.xaml (WinUI 3 leak: ~1000 objects per window)
- `x:Bind` is SAFE in child Pages/UserControls
- Subscribe events in `Loaded`, unsubscribe in `Unloaded`
- Graphs: imperative Canvas drawing, NOT XAML data binding

## Deployment

- `WindowsAppSDKSelfContained=true` — eliminates runtime dependency (+200MB but reliable)
- Unpackaged WinUI 3 silently fails if SDK runtime missing — self-contained avoids this
- Use `dotnet publish` for EXE, `msbuild` for MSIX (NOT `dotnet build` for UWP/wapproj)

## Reference Projects (April 2026)
- GuildOfCalamity/Transparency — best current transparent window example
- castorix/WinUI3_SwapChainPanel_Layered — full D3D pipeline with click-through
- WinUICommunity — production-quality backdrop helpers
- WinUIEx NuGet — TransparentTintBackdrop + window extensions
