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

## Plan B Lessons (2026-04-15 — companion redesign + 6-position HUD)

These rules are load-bearing and captured after real bugs landed during Plan B execution. Each lesson cites the report that caught it and the Microsoft doc that explains *why*.

### Rule 12: `RebuildMetricViews` must build a fresh container per layout mode

`UIElementCollection.Add` throws `ArgumentException` when the element already has a parent. If `TopBarControl.RebuildMetricViews` builds a `singleRow` and adds every `perf`/`hw` element to it **before** branching into Thin / Tall / SideDock modes, the Tall and SideDock branches crash at runtime the moment they try to re-add those elements to fresh containers.

**Required pattern:** build the container lazily inside each branch. The Thin branch is the only one that gets to own `singleRow`; Tall and SideDock construct their layouts directly from the `perf` and `hw` element lists.

- Caught by: `docs/superpowers/research/planb-implementation-audit-backend.md` finding #1 (selecting `TopTall`, `BottomTall`, `LeftDock`, or `RightDock` would have crashed the overlay).
- Microsoft source: `UIElementCollection.Add` exception semantics on `learn.microsoft.com`.

### Rule 13: `VisualStateManager` + `AdaptiveTrigger` is first-match-wins top-down

List `AdaptiveTrigger` states **from largest `MinWindowWidth` to smallest**. WinUI evaluates top-down and the first matching trigger wins. If you list `Sm (1024)` before `Md (1280)`, then at 1500 dip the `Sm` state wins and `Md` is ignored — a silent layout bug.

**Canonical order for HHA Pulse:** `Lg (1600) → Md (1280) → Sm (1024) → Xs (0)`.

- Caught by: `docs/superpowers/research/planb-winui3-platform-april2026.md` Gotcha #1, reiterated in the spec §13.3 VSM example.
- Microsoft source: `AdaptiveTrigger` docs on `learn.microsoft.com/en-us/uwp/api/windows.ui.xaml.adaptivetrigger`.

### Rule 14: Theme dictionaries must ship with `Dark` + `Light` + `HighContrast` keys from day one

Even if `HHAPulse.Overlay` only has `Dark` content today, the `ResourceDictionary.ThemeDictionaries` block must declare all three keys. Skipping any key causes brush leakage across subtrees with different `RequestedTheme` when a Light or HighContrast branch is added later. Ship stubs for `Light` and `HighContrast` on day one — the cost is 6 extra lines of XAML; the alternative is a week of debugging brush-resolution bugs down the road.

**Required XAML shape:**

```xml
<ResourceDictionary.ThemeDictionaries>
  <ResourceDictionary x:Key="Dark">  <!-- real content --> </ResourceDictionary>
  <ResourceDictionary x:Key="Light"> <!-- stub pointing at Dark values --> </ResourceDictionary>
  <ResourceDictionary x:Key="HighContrast"> <!-- system color references --> </ResourceDictionary>
</ResourceDictionary.ThemeDictionaries>
```

Inside a theme dictionary: use `{StaticResource}`. At call sites (pages / controls): use `{ThemeResource}`.

- Caught by: `docs/superpowers/research/planb-winui3-platform-april2026.md` Gotcha #2.
- Microsoft source: `learn.microsoft.com/en-us/windows/apps/develop/platform/xaml/xaml-theme-resources`.

### Rule 15: Flyout content does not inherit `RequestedTheme` from the parent page

`Flyout` content sits in a popup-tree sibling, not in the parent page's visual subtree. Setting `RequestedTheme = ActualTheme` on the flyout's inner content is cargo-cult — it doesn't propagate through the popup boundary. Flyouts resolve `{ThemeResource}` against the **app-level** dictionary chain, not the page-level one.

**Correct approach:** define all theme resources at `App.xaml` (or a merged dictionary at app scope, which HHA Pulse already does via `HhapTheme.xaml`). Do not scatter theme resources into page-level `Resources` blocks and then try to patch flyout inheritance with a `RequestedTheme` assignment — it won't work and the workaround is a smell that masks the real architecture mistake.

- Caught by: `docs/superpowers/research/planb-implementation-audit-ui.md` finding #2 + `docs/superpowers/research/planb-winui3-platform-april2026.md` Gotcha #3.
- Microsoft source: `learn.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.flyout` (popup-tree placement documentation).

### Rule 16: `HotkeyService` is intentionally launch-based — do not replace with `RegisterHotKey`

`HHAPulse.Overlay/Hotkeys/HotkeyService.cs` uses a named event mutex + single-instance process model, not Win32 `RegisterHotKey`. This is an intentional architectural decision, not a stopgap. Rationale:

- **Sleep / resume resilient by design.** A new process launch is always fresh. No hotkey to re-register after a lid-close round-trip.
- **Fullscreen-exclusive game resilient by design.** The OS still spawns the new process regardless of the game's exclusive surface. A `RegisterHotKey` can be blocked by exclusive mode; a process launch cannot.
- **No focus steal vulnerability.** There is no global hotkey for another process to grab.

The user maps a handheld button to **launch `HHAPulse.Overlay.exe`** with one of five command args (`--toggle`, `--next-preset`, `--off`, `--menu` / `--settings`, or default). `SignalExistingInstance` at `HotkeyService.cs:29-55` writes the command to `%LOCALAPPDATA%\HHAPulse\overlay-command.txt`, opens the named event `Local\HHAPulse_Overlay_Toggle`, sets it, and exits. The running instance's listener at `:96-120` picks up the signal and dispatches via `App.HandleLaunchCommand`.

**Do not replace this with Win32 `RegisterHotKey` without reading spec §5.7.1.** The launch-based model is more resilient against the exact failure modes (stuck overlays, unresponsive hotkeys) that handheld-gamer research identifies as the #1 complaint about every competing overlay. The trade-off — cold-start latency of ~100-300 ms for the exe spawn on the first launch — is acknowledged and accepted.

- Architectural source: `docs/superpowers/specs/2026-04-15-hha-pulse-ux-redesign-design.md` §5.7.1.
- Real code: `src/HHAPulse.Overlay/Hotkeys/HotkeyService.cs:8-137`, `src/HHAPulse.Overlay/App.xaml.cs:309-414`, `src/HHAPulse.Overlay/MainWindow.xaml.cs:46-71`.
- Research: `docs/superpowers/research/handheld-gamer-voices.md` §2b.1 finding #1 ("the overlay must go away on demand") — real-user complaints across every competing handheld overlay are about hide/show reliability, and the launch-based model is the cleanest way to guarantee it.

### Rule 17: `TopBarPosition` enum values preserve JSON round-trip via ordering

When extending a persisted enum, assign new values **after** the existing values so old saved settings files continue to round-trip without a custom `JsonConverter`. `TopBarPosition` was extended from the old binary `OverlayEdge { Top = 0, Bottom = 1 }` by declaring `TopThin = 0, BottomThin = 1, TopTall = 2, BottomTall = 3, LeftDock = 4, RightDock = 5` — the first two values match the old schema byte-for-byte. A setting saved as `"TopBarPosition": 0` before Plan B loads as `TopThin` after Plan B, which is the correct migration.

**Rule:** when extending a persisted enum, never reorder or insert values into the middle of the existing range. Always append new values to the end (or reserve specific numeric values that don't collide). Same applies to `OverlayPreset.Full = 5` — it reserves `Off = 4` so existing `Off` settings deserialize correctly.

- Caught by: `docs/superpowers/research/planb-implementation-audit-backend.md` Area #1 verified; the reviewer's Round-1 plan had proposed a custom `JsonConverter` but the enum-ordering trick is simpler and was adopted.
- Microsoft source: `System.Text.Json` enum deserialization semantics (`JsonSerializer` treats unknown enum integers as exception-worthy if not in range, so appending is safe).

