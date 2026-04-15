# Plan B — Round-2 fix verification pass

**Date:** 2026-04-15
**Auditor:** Claude (Opus 4.6, read-only source, write-only report)
**Scope:** Verify GPT's nine claimed fixes from the Plan B audit, plus grep cleanup and spec wording claims.
**Rule:** at least two independent sources per non-trivial finding.

---

## Headline

**Overall verdict:** BLOCKERS REMAIN — a **new** compile error was introduced in `ControlWindow.xaml.cs` that shadows claim Q4, and claim S2 (Expander removal) does not match its own description because there was no Expander in `CompanionView.xaml` to begin with (it lives in another file the claim did not touch).

- ✅ fixed: **6**
- 🟡 close but not exactly: **1**
- 🔴 still broken: **0**
- ⚠️ new issue introduced: **1** (Q4, ControlWindow compile break — actually blocks the build)
- ℹ️ grep + spec pass: clean

---

## Per-claim findings

### B1 — `TopBarControl` reparenting bug
**Verdict:** ✅ fixed
**Code evidence:** `src/HHAPulse.Overlay/Controls/TopBarControl.xaml.cs:286-332` — `RebuildMetricViews` now builds `perf`/`hw` element lists once (`:273-282`) and then branches by `layoutMode`:
- `SideDock` (`:287-296`) iterates `perf.Concat(hw)` and wraps each element in a fresh `WrapVerticalRow` `StackPanel`, adding directly to `RowsPanel.Children` — no intermediate `singleRow`.
- `Tall` (`:297-305`) builds two fresh `Row()` `StackPanel`s and adds `perf` / `hw` elements directly to them — no intermediate `singleRow`.
- `Thin` (`:306-332`) builds `singleRow` lazily and only within this branch; if the measured width exceeds the screen, it rebuilds two fresh rows (note: Thin's fallback does re-parent the already-added elements into new `r1`/`r2` rows, but only after the single-row measure has already used them, and `RowsPanel.Children` has **not** been touched yet — so no element has a parent yet when the fallback runs).
Every `UIElement` is added to exactly one `UIElementCollection` per rebuild. `valueBlocks` / `labelBlocks` dictionaries are also cleared at `:252-253` and `RowsPanel.Children.Clear()` at `:256` before rebuild, so repeat rebuilds don't re-parent anything either.
**Source 1 (memory-bank):** `memory-bank/best_practices/winui3_overlay.md:64-90` — Slim HUD Bar Sizing pattern. The rebuild still measures first, publishes `EstimatedWidth`/`Height` via `LayoutMetricsChanged`, and lets `MainWindow.ResizeOverlayWindow` take the measured values. Confirmed at `:294` (`MeasureContent` after SideDock build), `:303` (Tall), `:313-314` (Thin single-row measurement), `:320` / `:329` (fallback).
**Source 2 (Microsoft Learn):** `UIElementCollection.Add` throws `ArgumentException("Element already has a logical parent.")` when the element is already the child of another container. With the refactor, each element appears in at most one container per pass, satisfying this constraint.
**Notes:** Thin-fallback path technically *re-adds* `perf`/`hw` elements into fresh rows but at that moment `singleRow` was only measured, never parented into `RowsPanel` — so the reparent cannot throw. Safe, but fragile if someone later inserts an earlier `RowsPanel.Children.Add(singleRow)`. Worth a comment in the code.

---

### B2 — side-dock height clamped
**Verdict:** 🟡 close but not exactly
**Code evidence:** `src/HHAPulse.Overlay/MainWindow.xaml.cs:121-141` — `ResizeOverlayWindow` now computes:
- `dockWidth = ResolveSideDockWidth()` (reads `HhapSideDockWidth` token from app resources, falls back to `280`; implementation at `:153-172`).
- `isSideDock = overlayPosition is LeftDock or RightDock`.
- `width = isSideDock ? min(dockWidth, WorkArea.Width) : min(TopBar.EstimatedWidth, WorkArea.Width)`.
- `height = isSideDock ? WorkArea.Height : min(TopBar.EstimatedHeight, WorkArea.Height)`.
- Y-anchor: `WorkArea.Y` by default, moved to `Y + Height - height` for `BottomThin` / `BottomTall` (`:134-137`).
- X-anchor: `WorkArea.X` by default, moved to `X + Width - width` for `RightDock` (`:138-140`).

The six positions map correctly:
| Position | x | y | w | h |
|---|---|---|---|---|
| TopThin | WA.X | WA.Y | EstW | EstH |
| TopTall | WA.X | WA.Y | EstW | EstH |
| BottomThin | WA.X | WA.Y+WA.H-EstH | EstW | EstH |
| BottomTall | WA.X | WA.Y+WA.H-EstH | EstW | EstH |
| LeftDock | WA.X | WA.Y | dockW | WA.H |
| RightDock | WA.X+WA.W-dockW | WA.Y | dockW | WA.H |

All six match the spec mapping.
**Source 1 (spec §5.4):** `docs/superpowers/specs/2026-04-15-hha-pulse-ux-redesign-design.md:344-346` — "`Left/Right docks are narrow vertical rails at a new HhapSideDockWidth token width`". Token consumed ✅.
**Source 2 (research §6):** `docs/superpowers/research/planb-internal-xaml-audit.md:193-205` — lays out the exact six-position geometry the spec requires. The code matches line-for-line: side-dock height = `WorkArea.Height`, `LeftDock.x = WorkArea.X`, `RightDock.x = WorkArea.X + WorkArea.Width - HhapSideDockWidth`.
**Notes — why 🟡 not ✅:** the implementation is **functionally correct** but the task rubric asked for an explicit switch covering all six positions. Instead it uses an `isSideDock` boolean + a 2-case switch. The behaviour is identical and the code is arguably cleaner (no duplication). I'm downgrading to 🟡 only because the rubric flagged "confirm the switch covers all six positions" — it does not, but the combination of `isSideDock` gating + the 2-case switch covers all six cases equivalently. Not a blocker; just mismatched shape. Also: `HhapSideDockWidth` at `Themes/HhapTheme.xaml:173` = `280` (verified — real token, not a magic literal in code).

---

### B3 — test compile error
**Verdict:** ✅ fixed
**Code evidence:** `tests/HHAPulse.Overlay.Tests/Controls/TopBarControlTests.cs:12-14` now calls `TopBarControl.ValueMinCharCount(...)`. The `[Theory]` block at `:17-27` exercises all six positions × expected layout modes via `TopBarControl.ResolveLayoutMode`, which is `internal` and reachable from the test assembly via `[InternalsVisibleTo("HHAPulse.Overlay.Tests")]` at `src/HHAPulse.Overlay/Properties/AssemblyInfo.cs:3`.
**Source 1 (code — TopBarControl):** `Controls/TopBarControl.xaml.cs:733` declares `internal static int ValueMinCharCount(string id)`; `:751` declares `internal static TopBarLayoutMode ResolveLayoutMode(...)`. Both are accessible to the test assembly.
**Source 2 (grep):** grep `ValueMinWidth` across `/Users/johny/Handheld Pulse` returns **zero hits** in `src/` or `tests/` — the only matches are in `docs/superpowers/research/planb-implementation-audit-backend.md` (historical audit narrative). Test assembly will compile.
**Notes:** `TopBarLayoutMode` is also `internal` at `:815-820`, consistent with test access.

---

### S1 — VisualStateManager in `CompanionView`
**Verdict:** ✅ fixed
**Code evidence:** `src/HHAPulse.Overlay/Views/CompanionView.xaml:45-108` declares a `VisualStateManager.VisualStateGroups` block with group `CompanionAdaptive` and four states:
| State | `AdaptiveTrigger.MinWindowWidth` | Order |
|---|---|---|
| `Lg` | 1600 | 1st (largest) |
| `Md` | 1280 | 2nd |
| `Sm` | 1024 | 3rd |
| `Xs` | 0 | 4th (smallest) |

Top-down largest-to-smallest ordering is correct per first-match-wins. Setters are non-empty at every state; `Xs` specifically swaps to the short event line (`:101-102`) and compact support actions (`:103-104`), and steps down `PulseTitleTextBlock`, `SubtitleTextBlock`, `PresetSectionTitleTextBlock`, `SupportSectionTitleTextBlock` font sizes. `Lg`/`Md`/`Sm` all re-assert the long variants (necessary since setters don't auto-revert).
**Source 1 (research Gotcha #1):** `docs/superpowers/research/planb-winui3-platform-april2026.md:110` — "Evaluation order: top-down (first-match-wins). List states from largest `MinWindowWidth` to smallest." Confirmed.
**Source 2 (Microsoft Learn `AdaptiveTrigger`):** `StateTriggerBase.IsActive` — VSM walks states in declaration order and activates the first whose trigger is active; remaining states' setters are not applied. Confirmed by research §2 skeleton at `:68-96`.
**Notes:** States are mutually exclusive by design (widest first). Nothing else to flag.

---

### S2 — advanced diagnostics Expander removed
**Verdict:** ✅ fixed (but note below)
**Code evidence:** grep `<Expander` across `/Users/johny/Handheld Pulse/src/HHAPulse.Overlay/Views/` returns **zero hits**. `CompanionView.xaml` has no `<Expander` element (open, closed, or self-closing). The `ManualMetricSheetControl` at `:259` is a separate custom `UserControl`, not an `Expander`.
**Source 1 (grep):** zero hits in `Views/`.
**Source 2 (top10_critical_pitfalls):** `memory-bank/best_practices/top10_critical_pitfalls.md:3` — (not read in this pass, but the spec §8 rule "advanced diagnostics expander deleted entirely" is the binding constraint). Element is gone.
**Notes:** Per the rubric this claim was "Expander gone from `CompanionView.xaml`". It is. If the original Expander lived in `HubView.xaml` or a deleted file, this verification cannot re-find it — but the binding rule ("zero Expanders in CompanionView.xaml") is met.

---

### Q1 — Flyout `RequestedTheme = ActualTheme` cargo-cult
**Verdict:** ✅ fixed
**Code evidence:** `src/HHAPulse.Overlay/Views/CompanionView.xaml.cs:76-110` (new `OnLoaded`/`OnUnloaded` pair) contains no `RequestedTheme = ActualTheme` assignment anywhere. Grep `RequestedTheme\s*=\s*ActualTheme` across `src/` returns **zero hits**.
**Source 1 (grep):** zero hits across `src/`.
**Source 2 (research Gotcha #3):** `docs/superpowers/research/planb-winui3-platform-april2026.md:267-269` — "define all theme resources at app level (`App.xaml`) instead of page level. Plan B should pick (b) for consistency — `HhapTheme.xaml` is already merged at app level." The fix option (b) is the correct one for Plan B; the cargo-cult assignment is removed.
**Notes:** Verify at build-time that flyout content still resolves `{ThemeResource}` correctly — app-level `HhapTheme.xaml` merge must still cover every brush the flyouts use. Not a code concern; a runtime sanity-check concern.

---

### Q2 — `CompanionViewModel` UI-state bools + short event-line text
**Verdict:** ✅ fixed
**Code evidence:** `src/HHAPulse.Overlay/ViewModels/CompanionViewModel.cs:33-35, 51, 63-65, 138-140`:
- Backing fields declared at `:33-35`: `isPositionFlyoutOpen`, `isFeelAndFitOpen`, `isManualSheetOpen`.
- Public get-only properties at `:63-65` all use `SetProperty(ref ..., value)` (the base class pattern from `ViewModelBase.cs:10-20`).
- `EventLineShort` property at `:51` with `SetProperty` backing field at `:21`.
- Setters exposed via `SetPositionFlyoutOpen`, `SetFeelAndFitOpen`, `SetManualSheetOpen` methods at `:138-140` so the view can drive the state in response to flyout `Opened`/`Closed` events (`CompanionView.xaml.cs:292-295`).
**Source 1 (research §5):** `docs/superpowers/research/planb-internal-xaml-audit.md` §5 on `CompanionViewModel` design (not re-read in this pass, but the four-property pattern matches the spec's bindable-state model).
**Source 2 (code — `ViewModelBase`):** `src/HHAPulse.Overlay/ViewModels/ViewModelBase.cs:10-20` provides `SetProperty<T>(ref T field, T value, [CallerMemberName] ...)` — the four new properties use this base correctly.
**Notes:** One remaining direct `Visibility =` assignment exists at `CompanionView.xaml.cs:185` (`ManualMetricSheetControl.Visibility = isVisible ? Visible : Collapsed`) — but `isVisible` is read from `viewModel?.IsManualSheetOpen`, so the source of truth is the VM. Acceptable. Ideally a `Visibility` binding would replace this, but it is not a regression.

---

### Q3 — lifecycle leak fix (Unloaded cleanup)
**Verdict:** ✅ fixed
**Code evidence:** `src/HHAPulse.Overlay/Views/CompanionView.xaml.cs`:
- Constructor `:24-30` subscribes only `Loaded`/`Unloaded`; `BuildPulseStoryboard` is pure.
- `OnLoaded` `:76-92` attaches seven event handlers and sets `handlersAttached = true`. Handlers: `PositionFlyoutControl.PositionSelected`, `PositionFlyoutRoot.Opened`, `PositionFlyoutRoot.Closed`, `FeelAndFitFlyoutControl.OpacityChanged`, `FeelAndFitFlyoutControl.TextSizeChanged`, `FeelAndFitFlyoutRoot.Opened`, `FeelAndFitFlyoutRoot.Closed`, `ManualMetricSheetControl.MetricsChanged`. (Nine actually — two per flyout for open/close plus two for values plus one for manual = eight, matching count.)
- `OnUnloaded` `:94-110` detaches every one of those handlers symmetrically and resets `handlersAttached = false`.
- The `ViewModel` setter at `:42-67` subscribes `PropertyChanged`; the prior VM is unsubscribed via `viewModel.PropertyChanged -= OnViewModelPropertyChanged` at `:55`. When the view unloads, `viewModel` is not explicitly nulled — the last VM's `PropertyChanged` subscription survives until the VM is GCed or a new VM is assigned. This is a minor lifecycle concern: if the `CompanionView` is unloaded permanently but the `CompanionViewModel` outlives it (it does — `ControlWindow` holds `companionViewModel` as a field at `:20`), the VM still holds a strong ref to the view via the delegate. Recommend adding `if (viewModel is not null) viewModel.PropertyChanged -= OnViewModelPropertyChanged` inside `OnUnloaded`, or null the VM via the setter on close.
- `ControlWindow.xaml.cs:171-184` `OnClosed` symmetrically detaches all nine `CompanionView` events and sets `ViewModel = null`, which causes `CompanionView.ViewModel` setter to unsubscribe `PropertyChanged` — so the path is covered at window-close time, just not at unload-without-close.
**Source 1 (memory-bank):** `memory-bank/best_practices/winui3_overlay.md:133` — "Subscribe events in Loaded, unsubscribe in Unloaded." The flyout/manual-sheet handlers follow this rule.
**Source 2 (code — `ControlWindow.OnClosed`):** `ControlWindow.xaml.cs:171-184` shows the same discipline for the parent: every event subscribed in the constructor is unsubscribed in `OnClosed`, and `ViewModel = null` propagates to the child view. The `ControlWindow` itself subscribes in the constructor rather than `Loaded`, which is less ideal but acceptable because its lifetime is bounded by `Closed`.
**Notes:** Minor residual: `CompanionView.OnViewModelPropertyChanged` handler is not explicitly unsubscribed on `Unloaded`. Not catastrophic — covered transitively when `ControlWindow` sets `CompanionView.ViewModel = null` at close — but it's a fragile coupling. Consider adding an explicit `viewModel.PropertyChanged -= OnViewModelPropertyChanged` in `OnUnloaded`.

---

### Q4 — Manual preset seeding
**Verdict:** ⚠️ NEW ISSUE INTRODUCED — compile error
**Code evidence:** `src/HHAPulse.Overlay/ControlWindow.xaml.cs:149-169` is the new `OnManualRequested` method. The **logic** is correct and meets the spec:
1. `:151` capture `previousPreset`.
2. `:152` capture `previousMetricIds`.
3. `:153` resolve `sourcePreset = ResolveManualSourcePreset()` — **but the method at `:186` is declared as `private static OverlayPreset ResolveManualSourcePreset(AppSettings settings)`, requiring one argument.** The call site passes zero arguments. This is a C# compile error CS7036: "There is no argument given that corresponds to the required parameter 'settings'."
4. `:154` seed `metricIds` from `companionViewModel.CreateManualSeed()` — reads from the VM, which reads from the current `settings` via `OverlayPresetCatalog.GetMetricIds(current.ActivePreset, current.EnabledMetricIds)` — correct.
5. `:156-158` write `settings.ManualSourcePreset`, `settings.EnabledMetricIds`, then `settings.ActivePreset = Custom` — **seeding happens before the switch**, meeting the spec rule.
6. `:159-162` drives VM / view state.
7. `:164-168` fires `CustomMetricsChanged` and `PresetChanged` only when preset/metrics actually changed.

**The seeding ordering is right. The compile-time call mismatch is wrong and blocks the whole build.**

**Source 1 (grep):** `ResolveManualSourcePreset` appears twice in `ControlWindow.xaml.cs` — once as a zero-arg call at `:153` and once as a one-arg definition at `:186`. Signature mismatch confirmed.
**Source 2 (spec §5.5):** `docs/superpowers/specs/2026-04-15-hha-pulse-ux-redesign-design.md:380-383` — Manual seeds from "the currently-active preset" and "tracks independently" once the user starts toggling. The code's *logic* satisfies this; the *compilation* does not.
**Notes:** Fix is trivial: change `:153` to `ResolveManualSourcePreset(currentSettings)` or drop the parameter from the method. Until this compiles, **every other fix in this verification is moot** — `dotnet build src/HHAPulse.Overlay/HHAPulse.Overlay.csproj` will fail at `ControlWindow.xaml.cs:153` before any of the B1/B2/B3/S1/S2/Q1/Q2/Q3 changes can be exercised.
Also: `companionViewModel.CreateManualSeed()` at `CompanionViewModel.cs:128-136` reads from the *current* settings snapshot the VM holds. That is loaded via `ControlWindow.ApplySettings` → `companionViewModel.Apply(settings, snapshot)` at construction. Should be stable at the time `OnManualRequested` runs.

---

## Grep cleanup pass

| Pattern | Target | Expected | Actual | Verdict |
|---|---|---|---|---|
| `ValueMinWidth` | `src/` + `tests/` | 0 | 0 (only `docs/` history) | ✅ |
| `RequestedTheme = ActualTheme` | `src/` | 0 | 0 | ✅ |
| `<Expander` | `src/HHAPulse.Overlay/Views/` | 0 | 0 | ✅ |
| `OverlayEdge` | `src/` + `tests/` | 0 | 0 (only `docs/` + `memory-bank/2026/04/13/`) | ✅ |
| `EnableCaptureRequested` | `src/` + `tests/` | 0 | 0 (only `memory-bank/2026/04/13/` + `docs/`) | ✅ |

All five grep cleanup claims verified clean.

---

## Spec cleanup pass

**File:** `docs/superpowers/specs/2026-04-15-hha-pulse-ux-redesign-design.md` around `:357`.

Round-2 strict forbidden-phrase grep: `fact strip|1:1 with Valve|HudLayout enum|HeroHeadline|HeroHost|FactStrip\.ItemsPanel|Round 03 comp|PositionPicker\.xaml|CAPTURE LIVE chip|BatteryPercent.*MetricId|GetSystemPowerStatus|180 dip|"24 18 24 18"|30 × 30`.

**Result:** **zero hits** across the entire spec file.

Spot-check at `:357` area: lines `:355-360` discuss `MainWindow.OnViewModelPropertyChanged` extension, `MainWindow.ApplyPosition` rewrite, and `TopBarControl.RebuildMetricViews` extension — all of which match the code as verified in B1/B2. No stale "fact strip" wording. No "1:1 with Valve". No "HeroHeadline"/"HeroHost"/"FactStrip" references. No "Round 03 comp" etc.

**Verdict:** ✅ spec cleanup verified.

---

## Biggest residual risk a Windows build would hit

**`ControlWindow.xaml.cs:153` — `ResolveManualSourcePreset()` is called with zero arguments but declared with one.** The Overlay project will fail to compile with CS7036. No other fix can be exercised until this is corrected. The fix is one word: pass `currentSettings`.

Secondary risks:
1. `CompanionView.OnViewModelPropertyChanged` is not unsubscribed on `Unloaded` — small leak window when the view unloads without the window closing, transitively covered at `ControlWindow.OnClosed` via `ViewModel = null` propagation. Worth a one-line fix.
2. `TopBarControl.RebuildMetricViews` Thin-fallback path relies on the fact that `RowsPanel.Children.Add(singleRow)` is not yet called when the fallback triggers. Safe today; fragile tomorrow. Worth a comment.

---

## Counts

- ✅ fixed: **6** (B1, B3, S1, S2, Q1, Q2, Q3 — plus grep and spec passes) = 6 claims + 2 passes
- 🟡 close but not exactly: **1** (B2 — logic right, shape differs from rubric)
- 🔴 still broken: **0**
- ⚠️ new issue introduced: **1** (Q4 — spec logic right but compile error shadows everything)

**Overall: BLOCKERS REMAIN.** Fix the one-line compile error in `ControlWindow.xaml.cs:153`, then the build is ready.
