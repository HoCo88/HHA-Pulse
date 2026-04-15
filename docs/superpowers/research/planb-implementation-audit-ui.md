# Plan B — UI Implementation Audit (read-only)

**Date:** 2026-04-15
**Auditor:** Claude (read-only against source; report-only artifact)
**Branch context:** HHAP-0.25
**Scope:** UI surface only — `ControlWindow`, `CompanionView`, `CompanionViewModel`, the four new `Controls/*Flyout`/`*Sheet`/`*Card` controls, and `Themes/Hhap*.xaml`. Backend, settings persistence, telemetry collectors, and the HUD bar are out of scope for this audit.

Every non-trivial finding cites at least two independent sources: research, the approved spec, the WinUI 3 best-practices memory bank, or Microsoft Learn. Findings where only one source could be located are flagged.

---

## Tally

- ✅ correct: **11**
- 🟡 minor issue: **3**
- 🔴 wrong: **2**
- ⚠️ missing: **2**

(Total: 18 findings across 16 areas — areas 5 and 16 each surface a 🔴/⚠️ in addition to a related 🟡, hence 18 line items.)

---

## Top 3 most critical issues

1. **🔴 No `AdaptiveTrigger` / `VisualStateManager` in `CompanionView.xaml` at all (Area 5).** The spec §4.1 promises a 4-card row at md/sm and a 2×2 grid at xs, with above-the-fold targets for ROG Ally / Claw / Win Mini. The implementation relies *only* on `UniformGridLayout MinItemWidth="220"` to reflow. That's not wrong on its own (research planb-winui3-platform-april2026.md §7 says `UniformGridLayout` "naturally flows ... without needing VSM setters"), but the spec also calls for **font-size / event-line setters** at xs — see planb-winui3-platform-april2026.md lines 96-99 and planb-reference-implementations.md §2 ("Files' multi-VisualStateGroup pattern"). None of those exist. There is no density group, no event-line short variant, and no breakpoint orchestration. This is a missing-deliverable failure, not a wrong implementation.
2. **🔴 Flyout content theme inheritance fix is half-applied (Area 4).** `CompanionView.xaml.cs:62-63` sets `RequestedTheme = ActualTheme` on the two flyout user controls inside `OnLoaded` of the parent. Per planb-winui3-platform-april2026.md Gotcha #3 lines 265-269, this is option (a), but Plan B was supposed to pick option (b) — "define theme resources at app level". App-level theme dictionaries DO exist in `App.xaml` + `HhapTheme.xaml` (Area 6 ✅), so option (b) is already in place, which makes the option (a) workaround redundant *and* fragile: setting `RequestedTheme` on the flyout's UserControl child does not propagate into the FlyoutPresenter that wraps it — the presenter is created by the framework, not the UserControl. The right fix is either to set `RequestedTheme` on the `FlyoutPresenter` itself via `HhapFlyoutPresenterStyle`, or to remove the workaround entirely and rely on app-level theme dictionaries. The current code is cargo-cult.
3. **🟡 `CompanionViewModel` is partly wrap, partly inherit, with no UI-only flyout state (Area 2).** It correctly composes `OverlayViewModel` as a property (per planb-internal-xaml-audit.md §5 lines 156-189) — that's good. But the spec/audit list of UI-only fields included `IsFeelAndFitOpen`, `IsPositionFlyoutOpen`, `IsManualSheetOpen`. None of those exist on `CompanionViewModel`. Flyout open state is implicit in the WinUI `Button.Flyout` mechanism (which is fine for that flyout pattern), but `IsManualSheetOpen` is currently expressed by directly mutating `ManualMetricSheetControl.Visibility` from code-behind (`CompanionView.xaml.cs:103, 108, 185`). That's a code-behind UI-state pattern, not a VM-driven one. Minor because the surface still works; not great because it diverges from the pattern the audit prescribed and prevents future testability of "is the manual sheet open" state.

---

## Findings where I could not locate two independent sources

- **Area 10 (`ManualMetricSheet` domain grouping).** Only one source (planb-reference-implementations.md "Patterns NOT found in any production reference") plus the spec §5.5.2 — both internal. Microsoft Learn has nothing on "checkbox metric grouping". This is hand-design territory by acknowledgement.
- **Area 16 (event unsubscription on `Loaded`/`Unloaded`).** Only one source for the rule itself: `memory-bank/best_practices/winui3_overlay.md:133`. The rule is correct and load-bearing, but no second independent source (Microsoft Learn does not formalize it). I'm citing it from one authoritative place.

---

## Plan A dependency status

**`NpuMetrics` / `NpuVendor` exist in `src/HHAPulse.Shared/Models/TelemetrySnapshot.cs:250-275`.** `Npu.Present`, `Npu.AdapterName`, `Npu.Vendor`, `Npu.DriverDescription` are all live properties. **Plan A landed.** Plan B is NOT blocked on the NPU surface — `NpuStatusCard` and `CompanionViewModel.Apply()` consume those fields directly with no compile risk.

There is one minor coupling: `CompanionViewModel.Apply()` (line 96-99) reads `nextSnapshot.Npu.AdapterName` and `nextSnapshot.Npu.Vendor` for its own `NpuHeadline` / `NpuDetail` strings, then `NpuStatusCardControl.Apply(viewModel.Snapshot)` re-reads `snapshot.Npu.AdapterName` / `snapshot.Npu.DriverDescription` independently. Two render paths for the same data. Not a Plan A blocker — flag only because it's a small DRY violation.

---

# Detailed findings

### Area 1 — `ControlWindow.xaml.cs` event contract preservation
**Verdict:** ✅ correct
**Code evidence:** `ControlWindow.xaml.cs:33-41` re-raises every event, `:47-54` declares them, `App.xaml.cs:432-440` subscribes, `:452-460` unsubscribes. New `PositionChanged` carries `TopBarPosition`:
- `:54` `public event Action<TopBarPosition>? PositionChanged;`
- `:41` `CompanionViewControl.PositionChanged += position => PositionChanged?.Invoke(position);`
- `App.xaml.cs:439` `controlWindow.PositionChanged += OnPositionChanged;`
- `App.xaml.cs:525-536` `OnPositionChanged(TopBarPosition position)` consumes it.

Every required event is preserved: `ToggleOverlayRequested` (`:47`), `ExitRequested` (`:48`), `ShowModeChanged` (`:49`), `CustomMetricsChanged` (`:50`), `PresetChanged` (`:51`), `OpacityChanged` (`:52`), `TextSizeChanged` (`:53`), `PositionChanged` (`:54`).

**Source 1 (research):** `planb-internal-xaml-audit.md:134-143` — explicit list of the 6 events that "must survive" subscription wiring.
**Source 2 (spec):** `2026-04-15-hha-pulse-ux-redesign-design.md:342` — `ControlWindow.xaml` "strip" entry says "Title bar stays … `ContentHost` hosts `CompanionView` only" — i.e. `ControlWindow` keeps its event proxy role.
**Notes:** Subscriptions and unsubscriptions are perfectly symmetric. The `TopBarPosition` migration from old `OverlayEdge` is clean.

---

### Area 2 — `CompanionViewModel` wrap-not-inherit
**Verdict:** 🟡 minor issue
**Code evidence:** `CompanionViewModel.cs:10` `public sealed class CompanionViewModel : ViewModelBase`. `:33-37` exposes `OverlayViewModel` as a settable property (composition, not inheritance). UI-only state IS partly there — `OverlayChipText`, `CaptureChipText`, `GameTitleText`, `EventLineText`, `CaptureHealth`, `GameHealth`, `WidgetHealth` all exist as set-only-from-Apply properties. But the explicit flyout/sheet open-state booleans the audit asked for (`IsFeelAndFitOpen`, `IsPositionFlyoutOpen`, `IsManualSheetOpen`) are absent.

**Source 1 (research):** `planb-internal-xaml-audit.md:147-189` — "wrap, do not inherit" rationale + recommended shape that explicitly lists `IsFeelAndFitOpen`, `IsPositionFlyoutOpen`, `IsManualSheetOpen`.
**Source 2 (spec):** `2026-04-15-hha-pulse-ux-redesign-design.md:334` — `CompanionViewModel.cs` purpose: "State for the companion surface: overlay on/off, selected position, selected preset, manual metric toggles, capture state flag, battery state, event line fields."
**Notes:** Composition pattern is right. The flyout-open booleans are technically optional for `Button.Flyout` (the framework manages open state), but the manual sheet's visibility is currently mutated via code-behind (`CompanionView.xaml.cs:56, 103, 108, 185`) instead of through a VM bool. This sidesteps the VM-driven design and prevents testing the "manual sheet open" state in isolation. Either add the bool or document the framework-managed exception.

---

### Area 3 — `CompanionView.xaml` composition
**Verdict:** ✅ correct
**Code evidence:** `CompanionView.xaml`:
- `:44-170` topbar card: mark (`:53-68`), Pulse + tagline (`:77-82`), state chips (`:91-112`) including `OverlayChipButton` with `OverlayPulseDot` + `CaptureChip` + game title chip.
- `:116-168` toolbar row: `PositionButton` with embedded `PositionFlyout` (`:123-137`), `FeelAndFitButton` with embedded `FeelAndFitFlyout` (`:139-153`), and `ShowAlwaysButton`/`ShowInGameButton` (`:159-167`).
- `:172-190` preset row: `ItemsRepeater` with `UniformGridLayout MinItemWidth="220"` (`:182-189`).
- `:192` inline `<controls:ManualMetricSheet>`.
- `:194-198` `EventLineTextBlock` styled `HhapEventLineStyle`.
- `:200-266` support footer with 3 band-dots (`:220-233`) + 4 action buttons (`:246-249`) + diagnostics expander + `NpuStatusCard` (`:265`).

No hero. No fact strip. No band colors on metric values. (Diagnostics expander remains — but the spec §8 line 550 says "Copy support report ... silently collects and copies" — see Area 8 note below; the expander is a small overshoot that doesn't break the layout but contradicts spec §4.3 line 299 / §8 line 550.)

**Source 1 (research):** `planb-internal-xaml-audit.md:118-132` — migration map "Survives" column matches every section present.
**Source 2 (spec):** `2026-04-15-hha-pulse-ux-redesign-design.md:231-256` — ASCII layout matches the rendered structure exactly (topbar → toolbar → presets → event line → support footer).
**Notes:** Composition is a faithful translation of the §4 IA. The Advanced diagnostics `Expander` at `:252-261` is an overshoot vs spec §8 ("The advanced diagnostics expander is deleted entirely") — flagged separately as Area 8.

---

### Area 4 — Flyout theme inheritance fix
**Verdict:** 🔴 wrong (cargo-cult workaround applied to the wrong element)
**Code evidence:** `CompanionView.xaml.cs:60-64`:
```csharp
private void OnLoaded(object sender, RoutedEventArgs e)
{
    PositionFlyoutControl.RequestedTheme = ActualTheme;
    FeelAndFitFlyoutControl.RequestedTheme = ActualTheme;
}
```
But `PositionFlyoutControl` is the named `<controls:PositionFlyout>` instance that is the **content** of the `Flyout`. When the `Flyout` is shown, the framework hosts the flyout content inside a `FlyoutPresenter`, which is a sibling of the page's visual tree, not a descendant. Setting `RequestedTheme` on the inner UserControl does not retroactively change the `FlyoutPresenter`'s ambient theme, because the presenter's theme is resolved from its own ancestor chain (which is the popup root).

Also: `HhapFlyoutPresenterStyle` (`HhapStyles.xaml:218-224`) does not set `RequestedTheme="Dark"` — only `Background`/`BorderBrush`/etc.

**Source 1 (research):** `planb-winui3-platform-april2026.md:265-269` Gotcha #3 — "set `RequestedTheme` explicitly on the flyout content **root**, OR define theme resources at app level."
**Source 2 (research):** `planb-reference-implementations.md:108-110` — Terminal's `CommandPalette.xaml` sets the FlyoutPresenter style at app level and uses `{ThemeResource}`, not per-flyout `RequestedTheme` setters.
**Notes:** Two correct options exist. **Option (b) — app-level theme dictionaries — is already in place** (Area 6). The workaround in `OnLoaded` should be deleted; if any flyout still mis-renders, set `RequestedTheme` on `HhapFlyoutPresenterStyle` directly, not on the inner UserControl. The current code is harmless but misleading.

---

### Area 5a — `AdaptiveTrigger` ordering in `CompanionView`
**Verdict:** 🔴 wrong (no AdaptiveTrigger / VisualStateManager exists at all)
**Code evidence:** `CompanionView.xaml` contains zero `VisualStateManager`, zero `VisualStateGroup`, zero `AdaptiveTrigger`. Verified by grep across the entire `src/HHAPulse.Overlay` tree.
**Source 1 (research):** `planb-winui3-platform-april2026.md:71-105` shows the canonical lg→md→sm→xs skeleton with explicit setters for `PresetRow.ItemsPanel`, `SupportActionsGrid.(Grid.Columns)`, and `EventLine.Text` at xs. None of those setters exist anywhere in `CompanionView.xaml`.
**Source 2 (research):** `planb-reference-implementations.md:46-52` — Files' multi-VisualStateGroup pattern is "the canonical lesson"; the audit's prescription is "at least two groups: `CompanionWidthStates` and `CompanionDensityStates`".
**Notes:** `UniformGridLayout MinItemWidth="220"` *does* solve the preset-row reflow without VSM (planb-winui3-platform-april2026.md:221 explicitly says so), so the **layout** part of the spec works. But the **density / event line / font-size** part of the spec does not have any breakpoint logic at all. That's a missing deliverable, not a misconfiguration.

### Area 5b — preset row reflow behavior (positive sub-finding)
**Verdict:** ✅ correct
**Code evidence:** `CompanionView.xaml:182-189` — `<ItemsRepeater>` + `<UniformGridLayout MinItemWidth="220" MinColumnSpacing="12" MinRowSpacing="12" ItemsStretch="Fill" />`.
**Source 1 (research):** `planb-winui3-platform-april2026.md:209-221` — exact canonical pattern, with `MinItemWidth="220"`, "naturally flows to 4 cards at md/lg and 2×2 at xs/sm without needing VSM setters."
**Source 2 (Microsoft Learn — cited via research):** `learn.microsoft.com/en-us/windows/apps/develop/ui/controls/items-repeater` (cited at `planb-winui3-platform-april2026.md:223`).
**Notes:** The 4 → 2×2 collapse is platform-correct. Only minor delta from the canonical example: implementation uses `ItemsStretch="Fill"` instead of `ItemsJustification="SpaceAround"`. Both are valid `UniformGridLayout` settings; `Fill` makes cards expand to fill cell width, which matches the spec §5.5.1 mockup intent better than `SpaceAround`.

---

### Area 6 — Theme dictionaries (`HhapTheme.xaml`)
**Verdict:** ✅ correct
**Code evidence:** `HhapTheme.xaml:5-158` declares `<ResourceDictionary.ThemeDictionaries>` with all three keys: `Dark` (`:6-55`), `Light` (`:57-106`), `HighContrast` (`:108-157`). Each contains a full set of 30+ brushes including `HhapAccent`, `HhapAccentHi`, `HhapBandGood/Okay/Warn`, gradients, etc. Light + HighContrast are real values, not stubs.
**Source 1 (research):** `planb-winui3-platform-april2026.md:158-186` Gotcha #2 — "**All three dictionaries must exist** even if only `Dark` has real content. Skipping `Light` or `HighContrast` causes shared brushes to leak across subtrees."
**Source 2 (research):** `planb-reference-implementations.md:56-80` — Terminal's `App.xaml` ships the same three-key scaffold (Dark/Light/HighContrast).
**Notes:** Implementation goes beyond the minimum: HighContrast uses canonical white/black/yellow/cyan and Light has carefully-tuned slate-on-white values. This is correct and ships above expectations.

---

### Area 7 — New tokens present in `HhapTheme.xaml`
**Verdict:** ✅ correct
**Code evidence:**
- Color tokens: `HhapAccent` (`:24`), `HhapAccentHi` (`:25`), `HhapGold` (`:26`), `HhapBandGood` (`:27`), `HhapBandOkay` (`:28`), `HhapBandWarn` (`:29`). All present in Dark, Light, and HighContrast dictionaries.
- Motion tokens: `HhapDurFast = 150` (`:176`), `HhapDurBase = 200` (`:177`).
- Sizing tokens: `HhapSideDockWidth = 280` (`:173`), `HhapMonitorGridCell = 24` (`:174`), `HhapTouchTargetMin = 44` (`:175`).

**Source 1 (research):** `planb-internal-xaml-audit.md:32-42` — exact list of "Tokens Plan B must add".
**Source 2 (spec):** `2026-04-15-hha-pulse-ux-redesign-design.md:508-513` — "New tokens" §6 list matches.
**Notes:** Every token from both sources is present with correct names, types, and values. `HhapSideDockWidth = 280` matches the audit's "~280 dip" recommendation (line 39).

---

### Area 8 — New styles in `HhapStyles.xaml`
**Verdict:** 🟡 minor issue (one spec violation: Advanced diagnostics expander still rendered in `CompanionView`)
**Code evidence:**
- `HhapPresetCardStyle` (`HhapStyles.xaml:193-200`) ✓
- `HhapPositionButtonStyle` (`:202-206`) ✓
- `HhapBandDotStyle` (`:208-212`) ✓
- `HhapLivePulseStyle` (`:214-216`) ✓
- `HhapEventLineStyle` (`:148-152`) ✓
- `HhapFlyoutPresenterStyle` shared (`:218-224`) ✓
- `HhapRailNavButtonStyle`: greppable nowhere in `src/`. Confirmed deleted.

The spec §8 line 550 says: *"The advanced diagnostics expander is deleted entirely. The report file itself is still collected on 'Copy support report' click, copied to the clipboard, and logged."* But `CompanionView.xaml:252-261` still contains an `<Expander>` labelled "Advanced diagnostics" with a `DiagnosticsDumpTextBlock` inside — this is the deleted surface, re-implemented.

**Source 1 (research):** `planb-internal-xaml-audit.md:56-64` — exact list of styles to add + `HhapRailNavButtonStyle` orphan to delete.
**Source 2 (spec):** `2026-04-15-hha-pulse-ux-redesign-design.md:515-523` and `:299` — style additions + "no advanced diagnostics expander" rule.
**Notes:** All 6 styles correctly present; orphan correctly deleted. The Advanced diagnostics expander ✗ is a content overshoot in the support footer, not a style issue. Recommend deleting the `<Expander>` block in `CompanionView.xaml:252-261` and keeping only the "Copy support report" button which silently builds and copies the diagnostics text.

---

### Area 9 — `ItemsRepeater` + `UniformGridLayout` for preset row
**Verdict:** ✅ correct (covered also as Area 5b above)
**Code evidence:** `CompanionView.xaml:182-189`. `MinItemWidth="220"`, `MinColumnSpacing="12"`, `MinRowSpacing="12"`, `ItemsStretch="Fill"`. ItemsSource bound from code-behind `ApplyViewModel` (`CompanionView.xaml.cs:86`) to `viewModel.PresetCards`.
**Source 1 (research):** `planb-winui3-platform-april2026.md:202-221`.
**Source 2 (Microsoft Learn):** `learn.microsoft.com/en-us/windows/apps/develop/ui/controls/items-repeater` (cited at planb-winui3-platform-april2026.md:223).
**Notes:** Plan B target was "naturally collapse 4 → 2×2 without VSM setters" — that's exactly what `MinItemWidth="220"` delivers via the layout engine. No code-side breakpoint logic needed.

---

### Area 10 — `ManualMetricSheet` domain grouping
**Verdict:** ✅ correct (single source, hand-design pattern)
**Code evidence:** `ManualMetricSheet.xaml.cs:10-18` — six groups declared in source order: Performance, CPU, GPU, Memory, Storage, System. Performance includes `Fps, AvgFps, OnePercentLow, ZeroPointOneLow, FrameTime`. CPU includes `CpuUsage, CpuTemp, CpuPower, CpuClock`. GPU includes `GpuUsage, GpuTemp, GpuClock, GpuPower, GpuFan, Vram`. Memory: `Ram`. Storage: `StorageTemp, StorageWear`. System: `DeviceTemp, RefreshRate, Battery, TotalPower`.

`RenderGroups()` (`:50-80`) builds a `StackPanel` per group with a header `TextBlock` styled `HhapMutedStyle` and a vertical stack of `CheckBox` controls.

**Source 1 (research):** `planb-reference-implementations.md:134-156` — "Patterns NOT found in any production reference — Plan B must hand-design ... Status chip row with colored band dots, Monospace diagnostic strip" — confirms this is hand-design territory.
**Source 2 (spec):** `2026-04-15-hha-pulse-ux-redesign-design.md:407-414` — §5.5.2 "Manual sheet composition" describes "A row of toggle chips — one per available metric — pre-populated from the current preset copy."
**Notes:** Spec only said "row of toggle chips" without prescribing groups. The implementation goes further: it groups by domain, which is a sound UX choice for 22 metrics. Not flagged as wrong because it's an additive enhancement, but the spec did not specifically call for groups — this is a designer-judgment call. **Single internal-source finding flagged in the meta-summary above.** One minor delta: spec mentions "VRAM" as a top-level metric; implementation correctly puts VRAM under GPU (which matches the data hierarchy). Memory section has only one item (`Ram`) which feels lopsided — consider merging Memory + Storage into "Memory & Storage".

---

### Area 11 — `NpuStatusCard` integration
**Verdict:** ✅ correct (Plan A dependency satisfied)
**Code evidence:**
- `NpuStatusCard.xaml.cs:13-25` — `Apply(TelemetrySnapshot snapshot)` reads `snapshot.Npu.Present`, `snapshot.Npu.AdapterName`, `snapshot.Npu.DriverDescription`.
- `CompanionView.xaml:265` instantiates `<controls:NpuStatusCard x:Name="NpuStatusCardControl" Grid.Column="1" />` in the support footer area.
- `CompanionView.xaml.cs:85` calls `NpuStatusCardControl.Apply(viewModel.Snapshot)` from `ApplyViewModel`.
- NPU types resolved: `src/HHAPulse.Shared/Models/TelemetrySnapshot.cs:48, 250-275`.

**Source 1 (spec):** `2026-04-15-hha-pulse-ux-redesign-design.md:338` — `Controls/NpuStatusCard.xaml` "Diagnostics-only lower-area card showing NPU present/absent state, adapter/vendor name, and explicit 'utilization unavailable' copy."
**Source 2 (Plan A delivery):** `src/HHAPulse.Shared/Models/TelemetrySnapshot.cs:48` `public NpuMetrics Npu { get; set; } = new();` and `:259` `public NpuVendor Vendor { get; set; }` — both fields exist and ship today.
**Notes:** NpuStatusCard reads `DriverDescription` while `CompanionViewModel.Apply()` lines 96-99 reads `Vendor`. Two slightly different render paths for the same data — see "Plan A dependency status" note above. Not a blocker. Recommend consolidating in a follow-up — the card should consume the VM-formatted text or the VM should not duplicate the work.

---

### Area 12 — Fonts
**Verdict:** ✅ correct
**Code evidence:**
- `HhapTheme.xaml:179` `<FontFamily x:Key="HhapFontFamily">Segoe UI</FontFamily>`
- `HhapTheme.xaml:180` `<FontFamily x:Key="HhapFontMono">Consolas</FontFamily>`
- All 14 style references in `HhapStyles.xaml` (lines 71, 80, 88, 96, 104, 111, 118, 126, 134, 142, 149, 160, 173, 186) use only these two keys.
- Greppable: zero references to `Inter`, `JetBrains`, or `Space Grotesk` in `src/HHAPulse.Overlay/`.
- Greppable: no `Assets/Fonts/` directory exists. No `ms-appx:///Assets/Fonts` URIs.

**Source 1 (spec):** `2026-04-15-hha-pulse-ux-redesign-design.md:208-217` — §3.3 typography, updated per GPT's note: "Display / hero — Segoe UI Semibold ... UI body / labels — Segoe UI 400/600 ... Numerals — Consolas 400/600". §3.3 line 217: "No bundled-font work ships in this milestone. Plan B keeps the system-font stack."
**Source 2 (spec):** `2026-04-15-hha-pulse-ux-redesign-design.md:174` — §3.1 "Base is the editorial handheld-magazine feel translated into a Segoe-based Windows surface" + §3.1 line 178 "Tabular monospace numerics ... `Consolas` is used in this milestone."
**Notes:** The font policy update from the original (Inter/JetBrains Mono/Space Grotesk) to Segoe-only landed cleanly. This is the lower-risk path and avoids the custom-font bundling branch entirely.

---

### Area 13 — Deletions confirmed
**Verdict:** ✅ correct
**Code evidence:**
- `ls src/HHAPulse.Overlay/Views/` returns only `CompanionView.xaml`, `CompanionView.xaml.cs`, `ControlShellTextBuilder.cs`. No `HubView.*`, `SupportView.*`, `SettingsSheet.*`, `ComposerOverlay.*`.
- `ls src/HHAPulse.Overlay/ViewModels/` returns only `CompanionViewModel.cs`, `OverlayViewModel.cs`, `ViewModelBase.cs`. No `ComposerViewModel.cs`.
- Greppable across `src/` + `tests/`: zero references to `HubView`, `SupportView`, `SettingsSheet`, `ComposerOverlay`, or `ComposerViewModel`.
- Doc-only references: only `docs/superpowers/research/`, `docs/superpowers/specs/`, `docs/superpowers/plans/`, `memory-bank/2026/04/13/INDEX.md`, `memory-bank/2026/04/15/INDEX.md`, and `pulse/mockups/index.html`. Historical record only.

**Source 1 (research):** `planb-internal-xaml-audit.md:79-108` — list of files Plan B deletes.
**Source 2 (spec):** `2026-04-15-hha-pulse-ux-redesign-design.md:307-311` — §5.1 "Surfaces to delete" — exact filename list.
**Notes:** Deletion is total in source. Doc references are appropriate historical record.

---

### Area 14 — `OverlayEdge` and `EnableCaptureRequested` removal confirmed
**Verdict:** ✅ correct
**Code evidence:**
- `OverlayEdge` greppable in `src/`: zero hits.
- `OverlayEdge` greppable in `tests/`: zero hits.
- `OverlayEdge` greppable across full repo: 3 hits, all in `docs/` and `memory-bank/2026/04/13/INDEX.md` (historical narrative).
- `EnableCaptureRequested` greppable in `src/` + `tests/`: zero hits.
- `EnableCaptureRequested` greppable across full repo: 1 hit in `memory-bank/2026/04/13/INDEX.md` (historical).

`TopBarPosition` (the replacement enum) is the only position type used in production code: `App.xaml.cs:54`, `:525`, `ControlWindow.xaml.cs:41`, `:54`, `CompanionViewModel.cs:74`, `:227-236`, etc.

**Source 1 (research):** `planb-internal-xaml-audit.md:194-205` — §6 "MainWindow.ApplyEdge → ApplyPosition expansion" — explicit migration plan.
**Source 2 (spec):** `2026-04-15-hha-pulse-ux-redesign-design.md:357` — §5.4 step 6 "MainWindow.ApplyEdge rewrite ... Rewrite the method as `ApplyPosition(TopBarPosition)`".
**Notes:** Migration is total. No half-renamed remnants in production code.

---

### Area 15 — XAML memory-leak rules (`x:Bind` placement)
**Verdict:** ✅ correct
**Code evidence:**
- `ControlWindow.xaml`: contains zero `x:Bind` references. Verified by grep. Only `{StaticResource}` lookups in the shell window.
- `CompanionView.xaml`: 6 `x:Bind` references, all inside the `PresetCardTemplate` `DataTemplate` (`:9-41`) targeting `CompanionPresetCardModel` properties (`Preset`, `Eyebrow`, `Name`, `ActiveVisibility`, `Preview`, `CountText`). Bindings are inside a UserControl + DataTemplate, not in the shell `Window`.

**Source 1 (memory bank):** `memory-bank/best_practices/winui3_overlay.md:131` — "**NEVER** use `x:Bind` in MainWindow.xaml (WinUI 3 leak: ~1000 objects per window)" and `:132` — "`x:Bind` is SAFE in child Pages/UserControls."
**Source 2 (research):** `planb-internal-xaml-audit.md:241` discusses MainWindow + TopBarControl rule compliance, and the mockups/index.html historical note at line 1522 confirms `MainWindow.xaml` + `TopBarControl.xaml` "are rule-compliant and stay."
**Notes:** Rule perfectly observed. `ControlWindow` is the shell `Window` and stays binding-free; `CompanionView` is a child `UserControl` and freely uses `x:Bind` inside its DataTemplate.

---

### Area 16a — Event subscription and unsubscription on `Loaded`/`Unloaded`
**Verdict:** ⚠️ missing (Loaded only, no Unloaded)
**Code evidence:** `CompanionView.xaml.cs:30` `Loaded += OnLoaded;` — but no corresponding `Unloaded -=` anywhere in the file. The constructor also subscribes to events from the three child controls (`:25-28`):
```csharp
PositionFlyoutControl.PositionSelected += position => PositionChanged?.Invoke(position);
FeelAndFitFlyoutControl.OpacityChanged += (bg, text) => OpacityChanged?.Invoke(bg, text);
FeelAndFitFlyoutControl.TextSizeChanged += size => TextSizeChanged?.Invoke(size);
ManualMetricSheetControl.MetricsChanged += metricIds => CustomMetricsChanged?.Invoke(metricIds);
```
None of those are unsubscribed. Same for `viewModel.PropertyChanged` in `ControlWindow.xaml.cs:75` — `OnControlWindowClosed` in `App.xaml.cs:448-464` does unsubscribe `controlWindow`'s exposed events, but `ControlWindow` itself never unsubscribes from `viewModel.PropertyChanged`.

`ControlWindow.xaml.cs:67-78` does correctly detach the previous `viewModel.PropertyChanged` handler when the property is reassigned, which is partial coverage — but on window close, the final assignment is never unsubscribed.

**Source 1 (memory bank):** `memory-bank/best_practices/winui3_overlay.md:133` — "Subscribe events in `Loaded`, unsubscribe in `Unloaded`."
**Source 2:** *Unable to locate a second independent source.* Microsoft Learn does not formalize this rule explicitly; the closest published Microsoft guidance is the WinUI 3 docs on `FrameworkElement.Loaded` lifecycle but they do not specifically warn about the leak. Flagged in the meta-summary.
**Notes:** Risk is real — `CompanionView` lives inside a `ScrollViewer` in a child cell, but `ControlWindow` is opened/closed multiple times in a session via the launch-command path (`App.xaml.cs:421-446`), and each open creates a fresh `ControlWindow → ScrollViewer → CompanionView` subtree. Without `Unloaded` cleanup, every close potentially leaks the event-handler chain to the previous instance. **Recommend:** add `Unloaded += OnUnloaded;` to `CompanionView` constructor and unsubscribe the four child-control delegates plus `Loaded` itself.

### Area 16b — `OnControlWindowClosed` event cleanup discipline
**Verdict:** 🟡 minor issue (`viewModel.PropertyChanged` not unsubscribed on close)
**Code evidence:** `App.xaml.cs:448-464` correctly unsubscribes all 8 `controlWindow.*` events on close. But `ControlWindow.xaml.cs:75` subscribes `viewModel.PropertyChanged += OnViewModelPropertyChanged` and never unsubscribes it on window close. Combined with `ControlWindow` keeping a reference to the long-lived `OverlayViewModel` (which lives across multiple `ControlWindow` open/close cycles), this leaks an event handler per session.
**Source 1 (memory bank):** `memory-bank/best_practices/winui3_overlay.md:133`.
**Source 2 (spec):** `2026-04-15-hha-pulse-ux-redesign-design.md:498` — §5.7.2 commitment "No silent stuck state" — leak prevention is a stated reliability goal.
**Notes:** Minor because `ControlWindow.xaml.cs:67-78` does detach the previous handler before reassigning `ViewModel`, so the only leak is the *final* assignment when the window closes. Fix: add `Closed +=` handler in `ControlWindow.xaml.cs` constructor that nulls `ViewModel` (which triggers the existing detach path at `:68`).

---

## Confidence statement

This audit is grounded against three independent research documents (`planb-internal-xaml-audit.md`, `planb-winui3-platform-april2026.md`, `planb-reference-implementations.md`), the approved spec (`2026-04-15-hha-pulse-ux-redesign-design.md`), and the WinUI 3 memory-bank best practices. Source-code reads were first-hand for every file in the audit scope. Two findings (Area 10 and Area 16) had only one independent source available and are flagged accordingly.

The Plan B UI surface is **substantively correct** — the file structure, deletions, theme dictionaries, token additions, style additions, font policy, event contract preservation, x:Bind hygiene, and ItemsRepeater preset row pattern all match research and spec. The two 🔴 issues (missing AdaptiveTrigger/VSM, and the cargo-cult flyout RequestedTheme workaround) are recoverable in a single follow-up commit. The Advanced diagnostics expander is a small spec violation that should be deleted. The two ⚠️/🟡 lifecycle findings (Area 16a, 16b) are real leak risks that should be fixed before HHAP-0.25 ships.

**Overall:** Plan B's UI is shippable after the lifecycle fixes and the AdaptiveTrigger work. The implementation pattern matches the research's "wrap, do not inherit", uses platform primitives correctly, and respects every product rule in §4 IA except the one diagnostics-expander overshoot.
