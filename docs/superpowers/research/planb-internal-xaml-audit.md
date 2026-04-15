# Plan B — Internal HHA Pulse XAML Audit

**Date:** 2026-04-15
**Scope:** First-hand audit of existing XAML + code-behind in `src/HHAPulse.Overlay/` to ground the Plan B redesign (`CompanionView`, `PositionPillButton`, `PositionFlyout`, `FeelAndFitPillButton`, `FeelAndFitFlyout`, `PresetRow`, `ManualMetricSheet`, `EventLine`, `NpuStatusCard`) in real code patterns, not assumptions.

---

## 1. Theme token inventory (`Themes/HhapTheme.xaml`)

**Tokens already present:**

| Key | Type | Value | Role |
|---|---|---|---|
| `HhapBgBrush` | SolidColorBrush | `#FF050916` | Window base background |
| `HhapPanelBrush` | SolidColorBrush | `#E60C1223` | Card surface 1 |
| `HhapPanel2Brush` | SolidColorBrush | `#EB121B32` | Card surface 2 (hover / elevated) |
| `HhapPanel3Brush` | SolidColorBrush | `#F00A0F1C` | Pressed |
| `HhapLineBrush` | SolidColorBrush | `#297596FF` | Hairline dividers |
| `HhapLineStrongBrush` | SolidColorBrush | `#4756C7FF` | Card strokes |
| `HhapTextBrush` | SolidColorBrush | `#FFF4F7FF` | Headlines, numbers |
| `HhapMutedBrush` | SolidColorBrush | `#FF95A0BD` | Labels |
| `HhapMuted2Brush` | SolidColorBrush | `#FF7F89A9` | Dividers, placeholder |
| `HhapBlueBrush`, `HhapBlue2Brush` | SolidColorBrush | (cyan family) | Accent |
| `HhapAmberBrush`, `HhapAmber2Brush` | SolidColorBrush | (gold/amber) | Premium / warning |
| `HhapGreenBrush` | SolidColorBrush | (green) | Good |
| `HhapRedBrush` | SolidColorBrush | (red) | Warn |
| `HhapPurpleBrush` | SolidColorBrush | (purple) | — |
| `HhapFontFamily` | FontFamily | `Segoe UI` | Default UI font |
| `HhapFontHero` | x:Double | `54` | Hero size |
| `HhapFontH1/H2/H3/Body/Micro/HudValue/HudLabel` | x:Double | various | Font scale |

**Tokens Plan B must add:**

- `HhapAccent` — alias for `HhapBlueBrush` (spec-named key)
- `HhapAccentHi` — hover/focus variant (can alias `HhapBlue2Brush`)
- `HhapGold` — Founder-tier gold (can alias `HhapAmberBrush`)
- `HhapBandGood`, `HhapBandOkay`, `HhapBandWarn` — band-tier dots (support footer only)
- `HhapDurFast = 100`, `HhapDurBase = 200` — animation durations (ms as `x:Double`)
- `HhapSideDockWidth` — new; width constant for `LeftDock`/`RightDock` HUD layouts (recommendation ~280 dip, pending Steam-overlay measurement per Round-1 §10 open item)
- `HhapMonitorGridCell` — new; the faint cyan grid cell size in the Position flyout's monitor canvas
- `HhapTouchTargetMin = 44` — new; minimum hit-target dip (Fluent accessibility standard)

**Token coverage:** ~60% already in place. The spec's palette maps cleanly onto existing keys with new aliases. No destructive migration needed — extend `HhapTheme.xaml`, do not rewrite.

---

## 2. Control style inventory (`Themes/HhapStyles.xaml`)

**Styles already present:**

- `HhapCardStyle`, `HhapStatCardStyle`, `HhapTileStyle` — Border-targeted surface cards
- `HhapChipStyle` + variants (`HhapChipBlue/Amber/Green/Purple/ButtonStyle`) — pill badges
- `HhapPrimaryButtonStyle`, `HhapGhostButtonStyle`, `HhapChipButtonStyle` — button variants
- Typography: `HhapHeroStyle`, `HhapH1/H2/H3Style`, `HhapBodyStyle`, `HhapMutedStyle`, `HhapEyebrowStyle`, `HhapKickerStyle`, `HhapHudValueStyle`, `HhapHudLabelStyle`

**Styles Plan B must add (per spec §6.1):**

- `HhapPresetCardStyle` — preset card with selected/unselected visual states
- `HhapPositionButtonStyle` — pill-shaped position selector button for the Position flyout
- `HhapBandDotStyle` — small colored dot for the three support-footer health states (only legitimate consumer of band tokens)
- `HhapLivePulseStyle` — animated pulsing border for the `OVERLAY ON` state chip
- `HhapEventLineStyle` — mono text block for the operator event line

**Orphan to delete:** `HhapRailNavButtonStyle` (`HhapStyles.xaml:206-218`) — targets `ToggleButton`, zero consumers in any `Views/`. Confirmed unused by first-hand grep. Safe to remove as part of Plan B cleanup.

---

## 3. View-layer pattern catalog

### `ControlWindow.xaml` (companion window shell — stripped by Plan B)

- Custom title bar row with Hub / Support nav buttons + Overlay Settings button
- `ContentHost` swaps between `HubView` and `SupportView`
- `SettingsSheetHost` overlays a right-edge sheet
- `ComposerOverlayControl` full-screen drawer (deleted by Plan B)
- `ControlWindow.xaml.cs` exposes these events back to `App.xaml.cs`: `PresetChanged`, `OpacityChanged`, `PositionChanged`, `ShowModeChanged`, `ToggleOverlayRequested`, `ExitRequested`, plus `OnComposerPresetApplied` / `OnComposerLayoutChanged` (deleted). `App.xaml.cs` subscribes at the lines where it constructs `controlWindow` and calls `ApplySettings()` at `:376-382`.
- **Load-bearing pattern:** `ControlWindow.ViewModel` setter (the `ViewModel` property) receives the `OverlayViewModel` instance — `ControlWindow` does not construct its own view model. The same pattern must be preserved for `CompanionView`.

### `HubView.xaml` (deleted by Plan B)

- Hero card with large preset name, status chips, toggle + customize buttons
- Live preview card (the one rejected in Round 03)
- Preset row (`Minimal`/`Standard`/`Performance`/`Custom`)
- Visibility + Position utility cards
- Collapsed `LiveStripCard` dead code
- `HubView.xaml.cs` uses `ControlShellTextBuilder` for status strings; these helper strings may need to survive in Plan B's `CompanionView`

### `SupportView.xaml` (folded into CompanionView footer by Plan B)

- Capture / Game Detect / Companion status cards
- Copy report / Open log / Website / Community action buttons
- Advanced diagnostics expander (deleted by Plan B)
- Tip jar card (deleted by Plan B)
- Close overlay button (deleted — closing is via window `×`)

### `SettingsSheet.xaml` (deleted; content moves to `FeelAndFitFlyout`)

- Background opacity slider
- Text opacity slider
- HUD text size slider
- Top / Bottom position chips
- `SettingsSheet.xaml.cs` raises `OpacityChanged`, `TextSizeChanged`, `PositionChanged` — these event contracts must survive on the new `FeelAndFitFlyout` (or on `CompanionViewModel`) so `App.xaml.cs` subscription keeps working

### `ComposerOverlay.xaml` (deleted entirely — nothing survives)

- Wizard with Layout / Modules / Detail / Reorder steps
- `ComposerViewModel.cs` is instantiated fresh per open (no persistence)
- `App.xaml.cs` has handlers for `OnComposerPresetApplied` and `OnComposerLayoutChanged` — these can be deleted once `ComposerOverlay` goes

### `MainWindow.xaml` + `TopBarControl.xaml` + `SparklineGraph.xaml` (HUD, stays structurally)

- `MainWindow` hosts `TopBar` (`TopBarControl` instance)
- `MainWindow.xaml.cs` lifecycle and listeners already verified in the Round-1 plan's Phase 1 (read in full): `ToggleOverlayVisibility` / `SetOverlayVisible` / `OnViewModelPropertyChanged` / `OnTopBarLayoutMetricsChanged` / `ResizeOverlayWindow` / `ApplyEdge`
- `TopBarControl.xaml.cs` uses programmatic `RebuildMetricViews()` to build rows (verified in the Round-1 plan's Phase 1 read)

---

## 4. `ControlWindow` → `CompanionView` migration map

| Current surface | Survives | How it maps into `CompanionView` |
|---|---|---|
| Title bar with Hub/Support nav buttons | Partially | Title bar stays (Mica backdrop, custom `×`/`–`), nav buttons deleted |
| `HubView.HeroCard` | No | Hero is deleted — spec §4 IA has no hero |
| `HubView.LivePreview` | No | Rejected in Round 03 |
| `HubView.PresetRow` | Yes (rebuilt) | Becomes `PresetRow.xaml` with 4 cards + mini HUD previews |
| `HubView.PositionChips` (top/bottom) | Yes (rebuilt) | Becomes `PositionPillButton` + `PositionFlyout` |
| `SettingsSheet` sliders | Yes (rebuilt) | Becomes `FeelAndFitPillButton` + `FeelAndFitFlyout` |
| `SupportView` status cards | Yes | Becomes the 3-dot support footer in `CompanionView` |
| `SupportView` action buttons | Yes | Becomes the 4-button support footer row |
| `SupportView` advanced diagnostics expander | No | Deleted |
| `SupportView` tip jar | No | Deleted |
| `ComposerOverlay` (full wizard) | No | Replaced by the Manual link + inline `ManualMetricSheet` |

**Event contracts that must survive:**

- `PresetChanged` → App.xaml.cs still needs to catch this to call `SetPreset()` and save settings
- `OpacityChanged` → same, calls `window.ApplyOpacity()`
- `PositionChanged` → same, calls `window.ApplyEdge()` (to be renamed `ApplyPosition`)
- `ShowModeChanged` → same
- `ToggleOverlayRequested` → same, calls `window.ToggleOverlayVisibility()`
- `ExitRequested` → same, closes windows

These events must be raised by **either** `CompanionView` **or** `CompanionViewModel` so `App.xaml.cs:436-440` subscription logic continues working with minimal rewiring.

---

## 5. `CompanionViewModel` integration pattern — recommended: **wrap, do not inherit**

Rationale from first-hand reads of `OverlayViewModel.cs` and `App.xaml.cs`:

1. `OverlayViewModel` is the HUD-side VM: it owns `CurrentSnapshot`, `ActivePreset`, `TopBarMetricIds`, and 5 sparkline `RingBuffer<double>` histories. It's driven by `AppHost` telemetry ticks via `ApplyTelemetry(snapshot)`.
2. `OverlayViewModel` is shared between `MainWindow` (HUD) and `ControlWindow` (companion) — both windows' setters receive the *same* instance, constructed once in `App.xaml.cs` startup.
3. `CompanionViewModel` needs UI-only state that doesn't belong to the HUD VM: flyout open/closed, selected preset chip highlight, foreground game title chip, capture state chip, Manual sheet expanded state, event line fields.

**Recommended shape:**

```csharp
public sealed class CompanionViewModel : ViewModelBase
{
    public OverlayViewModel? Overlay { get; set; }  // injected by App.xaml.cs, same instance as HUD
    
    // UI state not shared with HUD VM
    public bool IsFeelAndFitOpen { get; set; }
    public bool IsPositionFlyoutOpen { get; set; }
    public bool IsManualSheetOpen { get; set; }
    public string ForegroundGameTitle { get; set; } = "NO GAME DETECTED";
    public CompanionCaptureState CaptureState { get; set; } = CompanionCaptureState.Offline;
    
    // Events the host subscribes to (match ControlWindow's existing contract)
    public event Action<OverlayPreset>? PresetChanged;
    public event Action<double, double>? OpacityChanged;
    public event Action<TopBarPosition>? PositionChanged;
    public event Action? ToggleOverlayRequested;
}
```

**Integration in `App.xaml.cs`:**

```csharp
companionView = new CompanionView();
companionViewModel = new CompanionViewModel { Overlay = overlayViewModel };
companionView.ViewModel = companionViewModel;
companionViewModel.PresetChanged += p => SetPreset(p);
companionViewModel.PositionChanged += p => window?.ApplyPosition(p);
companionViewModel.OpacityChanged += (bg, txt) => window?.ApplyOpacity(bg, txt);
companionViewModel.ToggleOverlayRequested += ToggleOverlayVisibility;
```

**Why wrap, not inherit:** `OverlayViewModel` is the telemetry-side VM. `CompanionViewModel` is the UI-side VM. Inheriting would couple flyout state to the HUD pipeline, polluting both. Composition is cleaner and matches the existing `ControlWindow.ViewModel = overlayViewModel` pattern (which is just "set a reference to the HUD VM and read from it").

---

## 6. `MainWindow.ApplyEdge` → `ApplyPosition` expansion impact

Current (read first-hand):

- `MainWindow.xaml.cs:83-87` — `ApplyEdge(OverlayEdge)` stores and calls `ResizeOverlayWindow()`
- `MainWindow.xaml.cs:115-139` — `ResizeOverlayWindow()` computes `width = min(TopBar.EstimatedWidth, WorkArea.Width)`, `height = min(TopBar.EstimatedHeight, WorkArea.Height)`, anchors at `WorkArea.Y` (top) or `WorkArea.Y + WorkArea.Height - height` (bottom) via `SetWindowPos`.

**For Plan B's 6-position enum:**

- `TopThin` / `TopTall` — anchor at `WorkArea.Y`, width = `TopBar.EstimatedWidth`, height = `TopBar.EstimatedHeight`. Behavior matches today's `Top` except `TopTall` uses two-row metric rendering.
- `BottomThin` / `BottomTall` — anchor at `WorkArea.Y + WorkArea.Height - height`, same width/height computation. Matches today's `Bottom`.
- `LeftDock` — anchor at `WorkArea.X`, width = `HhapSideDockWidth` (new token, ~280 dip), height = `WorkArea.Height`.
- `RightDock` — anchor at `WorkArea.X + WorkArea.Width - HhapSideDockWidth`, width = `HhapSideDockWidth`, height = `WorkArea.Height`.

**`TopBarControl.RebuildMetricViews` extension needed:**

- Today the method builds horizontal `StackPanel` children inside `RowsPanel` (which is vertical with horizontal rows).
- For side-dock mode, `RebuildMetricViews` must build a vertical stack of metrics (one per row). The existing `BuildFps`/`BuildFrametime`/`BuildHw`/`BuildGpu`/`BuildMemory`/`BuildSystem` builders can be reused; only the layout container changes.
- The measured-sizing pattern (`element.Measure(Unbounded)` at `:283` → `Notify(rows, width, height)` at `:291,:302` → `MainWindow.OnTopBarLayoutMetricsChanged` → `ResizeOverlayWindow`) works unchanged for the new mode.

**`TransparentWindowHelper.MakeOverlay(this)`** — does not care about anchor position. No change needed.

---

## 7. Asset bundling — current state

- **No `Assets/Fonts/` directory exists.** Grep for `ms-appx:///Assets/Fonts` across the repo returns zero hits.
- All text uses system fonts: `new("Segoe UI")` and `new("Segoe Fluent Icons")`.
- `HhapFontFamily` in `HhapTheme.xaml` is `"Segoe UI"`.
- Plan B introduces `Inter`, `JetBrains Mono`, and (optionally) `Space Grotesk` per the spec §3.3.

**Action for Plan B:**

1. Create `src/HHAPulse.Overlay/Assets/Fonts/` directory.
2. Drop static-weight `.ttf` files (`Inter-Regular.ttf`, `Inter-Medium.ttf`, `Inter-SemiBold.ttf`, `Inter-Bold.ttf`, `JetBrainsMono-Regular.ttf`, `JetBrainsMono-SemiBold.ttf`, plus Space Grotesk if used).
3. Mark `.csproj` `<Content Include="Assets/Fonts/*.ttf">` with `CopyToOutputDirectory=PreserveNewest`.
4. Reference via `FontFamily="ms-appx:///Assets/Fonts/Inter-SemiBold.ttf#Inter"` — the `#` fragment is the **internal family name**, verified per Round-1 `winui3-modern-patterns.md`.
5. Expose as `FontFamily` resources in `HhapTheme.xaml` so styles can reference by key.

---

## 8. Gotchas, orphans, dead code found during audit

- `HhapRailNavButtonStyle` — orphan. Safe to delete.
- `MetricFormatter.cs` — exists but NOT referenced in production paths. `MetricFormatterCompact.cs` is the one the HUD uses. If Plan B needs non-compact formatting for the companion event line, consider consolidating.
- `Helpers/ControlShellTextBuilder.cs` — used by `HubView` and `SupportView` for status strings. If `CompanionView` inlines the strings instead, this helper can be deleted. Otherwise it survives.
- `ComposerViewModel` state is not persisted — `ComposerOverlay.xaml.cs` constructs a fresh instance on each open. Safe to delete wholesale.
- `LiveStripCard` inside `HubView.xaml` is `Visibility="Collapsed"` dead code — confirmed by the Round-1 UI audit.
- `HubView.xaml:17-19 HeroStackRow` is `Height="0"` placeholder — never wired.

---

## 9. Five most important findings for Plan B execution

1. **60% token coverage exists.** Add 8 new tokens as aliases or constants. `HhapTheme.xaml` extends, does not rewrite.
2. **`CompanionViewModel` wraps, does not inherit, `OverlayViewModel`.** UI-only state belongs to the new VM; telemetry state stays on the existing HUD VM. Shared instance injected by `App.xaml.cs`.
3. **Event routing to `App.xaml.cs` is load-bearing.** `PresetChanged` / `OpacityChanged` / `PositionChanged` / `ShowModeChanged` / `ToggleOverlayRequested` / `ExitRequested` must be raised by `CompanionView` or `CompanionViewModel`. Breaking this breaks settings persistence.
4. **`TopBarControl.RebuildMetricViews` needs a vertical rendering path** for `LeftDock` / `RightDock`. The existing horizontal builders can be reused; only the container orientation changes.
5. **No `Assets/Fonts/` directory exists.** Custom font bundling is greenfield — no collision risk, but the `.csproj` + URI pattern must be correct on first try.
