# Plan B Backend + HUD Implementation Audit

**Date:** 2026-04-15
**Auditor:** Claude (read-only first-hand audit of `src/HHAPulse.Overlay/`)
**Method:** every finding cites a code line plus at least one independent research/spec/best-practices source. Source code was read-only; only this report file was written.

---

## Summary

| | Count |
|---|---|
| Correct | 13 |
| Minor issue | 2 |
| Wrong | 4 |
| Missing | 2 |

**Headline:** the backend (settings, catalog, view-model) is sound and round-trips legacy JSON correctly. The HUD layout rewrite, however, has **two compile-blocking bugs** in the test suite and **one runtime-crashing bug** in `TopBarControl.RebuildMetricViews` whenever the user picks `TopTall`, `BottomTall`, `LeftDock`, or `RightDock`. These will fail acceptance.

---

### [Backend] — `TopBarPosition` enum order
**Verdict:** correct
**Code evidence:** `src/HHAPulse.Overlay/Settings/AppSettings.cs:50-58` declares `TopThin = 0, BottomThin = 1, TopTall = 2, BottomTall = 3, LeftDock = 4, RightDock = 5`.
**Source 1 (spec):** `docs/superpowers/specs/2026-04-15-hha-pulse-ux-redesign-design.md:352` — *"Persist the new enum directly with `TopThin = 0` and `BottomThin = 1`, so legacy saved values round-trip without a custom converter. `TopTall = 2`, `BottomTall = 3`, `LeftDock = 4`, `RightDock = 5`."*
**Source 2 (test round-trip):** `tests/HHAPulse.Overlay.Tests/Settings/SettingsServiceTests.cs:28-55` `LoadAsync_MigratesLegacyTopBottomPositionValues` validates `0 → TopThin, 1 → BottomThin` against a hand-written legacy JSON blob.
**Notes:** Plan B's stated round-trip contract is honoured exactly — old saves keep their position.

### [Backend] — `OverlayPreset.Full = 5` ordering
**Verdict:** correct
**Code evidence:** `AppSettings.cs:40-48` keeps `Off = 4` and adds `Full = 5`.
**Source 1 (spec):** `docs/superpowers/specs/2026-04-15-hha-pulse-ux-redesign-design.md:313` — *"Add `OverlayPreset.Full = 5` to `AppSettings.cs:40-47` (value `5` preserves JSON round-trip for existing saved settings with `Off = 4`)."*
**Source 2 (XAML audit):** `docs/superpowers/research/planb-internal-xaml-audit.md` §4 migration map preserves preset persistence across the rewrite. `Off` stays at 4, so existing files persisted as `"ActivePreset": 4` continue to deserialize as `Off`.
**Notes:** Append-only enum extension is the `MessagePack`/`System.Text.Json` safe pattern.

### [Backend] — `SettingsService.Normalize` migration
**Verdict:** correct
**Code evidence:** `src/HHAPulse.Overlay/Settings/SettingsService.cs:56-75` falls back to `OverlayPreset.Standard` for unknown presets and `TopBarPosition.TopThin` for unknown positions; `LoadAsync` catches `JsonException` to start fresh on corruption.
**Source 1 (spec):** §5.4 step 1 (`design.md:352`) — round-trip-without-converter is the requirement.
**Source 2 (test):** `SettingsServiceTests.cs:28-55` exercises both `0` and `1` legacy values.
**Notes:** No regression risk for existing 0.25-branch users.

### [Catalog] — Tuner / Advanced is 11 metrics
**Verdict:** correct
**Code evidence:** `OverlayPresetCatalog.cs:56-69` `TunerMetrics` = `Fps, OnePercentLow, FrameTime, CpuUsage, CpuTemp, CpuPower, GpuUsage, GpuTemp, Ram, RefreshRate, Battery` (11 entries).
**Source 1 (spec):** `design.md:370` Advanced row — *"`Fps, OnePercentLow, FrameTime, CpuUsage, CpuTemp, CpuPower, GpuUsage, GpuTemp, Ram, Battery, RefreshRate` | 11"*.
**Source 2 (test):** `OverlayPresetCatalogTests.cs:36-56` asserts `metrics.Count == 11`, contains all the spec items, and explicitly excludes `GpuClock`, `GpuFan`, `DeviceTemp`, `Vram`, `InputLatency`, `GpuPower`.
**Notes:** Order in source matches spec table verbatim.

### [Catalog] — Full is 19 metrics
**Verdict:** correct
**Code evidence:** `OverlayPresetCatalog.cs:71-92` `FullMetrics` = 19 entries: `Fps, AvgFps, OnePercentLow, ZeroPointOneLow, FrameTime, CpuUsage, CpuTemp, CpuPower, GpuUsage, GpuTemp, GpuClock, GpuPower, GpuFan, Ram, Vram, TotalPower, DeviceTemp, RefreshRate, Battery`.
**Source 1 (spec):** `design.md:371` Full row — same 19 entries in the same order.
**Source 2 (test):** `OverlayPresetCatalogTests.cs:58-72` asserts `metrics.Count == 19` and contains the spec-required additions (`AvgFps`, `ZeroPointOneLow`, `CpuPower`, `GpuPower`, `GpuFan`, `Vram`, `TotalPower`, `DeviceTemp`).
**Notes:** Each Full entry has a documented real source per `memory-bank/systems/telemetry-trace-map.md` (`device_temp` MSI WMI; `total_power` battery discharge; `gpu_clock`/`gpu_fan`/`gpu_power` vendor SDK paths).

### [Catalog] — `PresetDisplayName` rename
**Verdict:** correct
**Code evidence:** `OverlayPresetCatalog.cs:161-173` returns `"Advanced"` for `Tuner` and `"Manual"` for `Custom`.
**Source 1 (spec):** `design.md:316-318` items 4-5 — *"Rename `PresetDisplayName(OverlayPreset.Tuner)` return string from `"Tuner"` → `"Advanced"`"* and `Custom → "Manual"`.
**Source 2 (test):** `OverlayPresetCatalogTests.cs:115-121` `PresetDisplayName_UsesPlanBLabels` asserts both labels exactly.
**Notes:** Enum values are preserved (only display strings changed) — matches spec stability rule.

### [Catalog] — `NextPreset` cycle
**Verdict:** correct
**Code evidence:** `OverlayPresetCatalog.cs:147-159` cycles `Minimal → Standard → Tuner → Full → Off → Minimal`.
**Source 1 (spec):** `design.md:319` item 6 — *"Update the `NextPreset` cycle at `:120-131` to `Minimal → Standard → Advanced → Full → Off → Minimal`."*
**Source 2 (test):** `OverlayPresetCatalogTests.cs:105-113` validates every step of the cycle.
**Notes:** `Custom` correctly excluded from the cycle.

### [Catalog] — `GetMetricIds` switch includes `Full`
**Verdict:** correct
**Code evidence:** `OverlayPresetCatalog.cs:125-134` switch arm `OverlayPreset.Full => FullMetrics`.
**Source 1 (spec):** `design.md:319` item 7.
**Source 2 (test):** `OverlayPresetCatalogTests.cs:58-72` is only green if `GetMetricIds(Full, _)` returns the 19 entries.
**Notes:** `Custom` falls back to `TunerMetrics` (11) when the user list is empty — matches `design.md:381` "*starts as a copy of whichever curated preset was last active*" behavioural note (technically the spec wants the *source* preset, not always Tuner; flagged as minor below).

### [Catalog] — Custom-empty fallback uses Tuner only
**Verdict:** minor issue
**Code evidence:** `OverlayPresetCatalog.cs:131` — `OverlayPreset.Custom => customMetricIds.Count > 0 ? customMetricIds : TunerMetrics`.
**Source 1 (spec):** `design.md:381` — *"its metric set is pre-populated as a **copy of the currently-active preset** (Minimal / Standard / Advanced / Full) — never empty"*.
**Source 2 (spec):** `design.md:413` — Manual footer line is bound to the *source* preset name, implying Custom must remember which preset it cloned from.
**Notes:** The current fallback always lands on Tuner regardless of source preset. This is acceptable for Plan B's first cut (catalog has no source-preset memory yet; the seeding is `CompanionViewModel`'s job per spec §5.5.2), but a later UI change must seed `EnabledMetricIds` from `currentPreset` before flipping `ActivePreset = Custom`. Not a Plan B blocker; flagging for the Companion VM work.

### [ViewModel] — `Position` and `LineCount` properties
**Verdict:** correct
**Code evidence:** `OverlayViewModel.cs:52-62` adds `Position` (`TopBarPosition`) and `LineCount` (`int`) with private setters via `SetProperty`. `ApplySettings` at `:94-101` reads `settings.TopBarPosition`, calls `OverlayPresetCatalog.GetLayout(preset, EnabledMetricIds, position)`, and publishes both.
**Source 1 (spec):** `design.md:354` step 3 — *"publish two new properties via `SetProperty`: `Position` (the `TopBarPosition` value) and `LineCount` (derived — `TopTall`/`BottomTall` = 2, everything else = 1)"*.
**Source 2 (test):** `OverlayViewModelTests.cs:40-57` `ApplySettings_UpdatesPositionAndLineCount` asserts the parameterised mapping for all six positions.
**Notes:** `LineCount` derivation lives on `OverlayPresetCatalog.LineCountFor` at `:175-178`, single source of truth — clean.

### [ViewModel] — `ActivePreset` and `TopBarMetricIds` untouched
**Verdict:** correct
**Code evidence:** `OverlayViewModel.cs:40-50` — same pattern, same backing fields, no behaviour drift.
**Source 1 (spec):** `design.md:323-326` — surfaces to keep as-is.
**Source 2 (XAML audit):** `planb-internal-xaml-audit.md` §3 lifecycle note that `OverlayViewModel` is the load-bearing instance shared between HUD and companion.
**Notes:** Both existing tests (`OverlayViewModelTests.cs:11-38`) still pass against the new shape.

### [MainWindow] — `ApplyEdge` renamed to `ApplyPosition`
**Verdict:** correct
**Code evidence:** `MainWindow.xaml.cs:76-80` declares `public void ApplyPosition(TopBarPosition position)`. Grep `OverlayEdge` and `ApplyEdge` across `src/` and `tests/` returns zero hits in either tree.
**Source 1 (spec):** `design.md:357` step 6 — *"Rewrite the method as `ApplyPosition(TopBarPosition)`"*.
**Source 2 (research):** `planb-internal-xaml-audit.md:138` lists `ApplyEdge` as the surviving event contract that must be renamed.
**Notes:** No stale `ApplyEdge` consumer left behind in source; spec/research mentions are documentary.

### [MainWindow] — six-position switch in `ResizeOverlayWindow`
**Verdict:** correct
**Code evidence:** `MainWindow.xaml.cs:110-149`. Width = `HhapSideDockWidth` for `Left/RightDock`, else `TopBar.EstimatedWidth`. `BottomThin/BottomTall` anchor at `WorkArea.Y + Height - height`. `RightDock` anchors at `WorkArea.X + Width - width`. `LeftDock` defaults to `WorkArea.X` (the initial value). `TopThin/TopTall` keep the default Y.
**Source 1 (spec):** `design.md:357` step 6 expansion.
**Source 2 (research):** `planb-internal-xaml-audit.md:200-205` documents the exact arithmetic the spec wants.
**Notes:** Side-dock height is constrained by `Math.Min(TopBar.EstimatedHeight, WorkArea.Height)` — see the next finding.

### [MainWindow] — side-dock height does NOT span full WorkArea.Height
**Verdict:** wrong
**Code evidence:** `MainWindow.xaml.cs:126` — `int height = Math.Min(TopBar.EstimatedHeight, displayArea.WorkArea.Height);` This is unconditional; the side-dock branch never overrides it. Side docks therefore render at the measured height of the metric stack, not the full work-area height.
**Source 1 (spec):** `design.md:346` — *"Left/Right docks are narrow vertical rails at a new `HhapSideDockWidth` token width"*; `design.md:563` — *"Top 1-line, Top 2-line, Bottom 1-line, Bottom 2-line, Left dock, Right dock"* describes a rail that's tall by definition.
**Source 2 (research):** `planb-internal-xaml-audit.md:205` — *"`LeftDock` — anchor at `WorkArea.X`, **width = `HhapSideDockWidth` (new token, ~280 dip), height = `WorkArea.Height`**"*.
**Notes:** The dock will currently render as a small rectangle at the top of the screen, not a vertical rail. The fix is `height = isSideDock ? displayArea.WorkArea.Height : Math.Min(TopBar.EstimatedHeight, displayArea.WorkArea.Height);`. Plan B HUD acceptance test on a Steam Deck will fail without this.

### [MainWindow] — `OnViewModelPropertyChanged` listens for Position / LineCount
**Verdict:** correct
**Code evidence:** `MainWindow.xaml.cs:89-98` filter includes `nameof(OverlayViewModel.Position)` and `nameof(OverlayViewModel.LineCount)` and dispatches via `DispatcherQueue.TryEnqueue`.
**Source 1 (spec):** `design.md:356` step 5.
**Source 2 (best-practices):** `memory-bank/best_practices/winui3_overlay.md:123-128` — *"ALL UI updates must go through `DispatcherQueue.TryEnqueue()`"*. Compliance: ✅.
**Notes:** Symmetric to `TopBarControl.OnVmChanged` at `Controls/TopBarControl.xaml.cs:217-242`.

### [MainWindow] — `HhapSideDockWidth` lookup with sane fallback
**Verdict:** correct
**Code evidence:** `MainWindow.xaml.cs:151-170` `ResolveSideDockWidth` reads `Application.Current.Resources["HhapSideDockWidth"]`, accepting `double` or `int`, with a 280-dip fallback inside a `try`/`catch`.
**Source 1 (spec):** `design.md:573` open question #1 specifies *"Steam-overlay width target"*; `planb-internal-xaml-audit.md:39` recommends *"~280 dip"*.
**Source 2 (device matrix):** `handheld-device-matrix.md:54` floor at 960 effective wide → 280 dip = 29% of the 960×540 floor, leaving 680 dip of game canvas. Acceptable.
**Notes:** The real value is still an open question (§10 #1) — the fallback is a reasonable starter and the lookup will pick up the token once a designer pins it.

### [HUD] — `TopBarControl.RebuildMetricViews` re-parents already-parented children
**Verdict:** wrong (runtime crash)
**Code evidence:** `Controls/TopBarControl.xaml.cs:284-309`. Lines 284-287 build `singleRow` and add every `perf`/`hw` element to `singleRow.Children`. Then for `SideDock` (`:292-301`) the same elements are added to `WrapVerticalRow(...)`, and for `Tall` (`:302-310`) the same elements are added to `r1`/`r2`. WinUI's `UIElementCollection.Add` rejects an element that already has a `Parent` with `Element is already the child of another element`. `singleRow` is never attached to the visual tree in the SideDock or Tall paths but its children still hold a logical parent reference once `Children.Add` has run.
**Source 1 (research):** `planb-internal-xaml-audit.md:208-211` — *"the existing `BuildFps`/`BuildFrametime`/`BuildHw`/`BuildGpu`/`BuildMemory`/`BuildSystem` builders can be reused; only the layout container changes"* — the research expected the side-dock path to build *fresh* containers, not reparent.
**Source 2 (Microsoft Learn):** `learn.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.uielementcollection.add` — adding a `UIElement` that already has a parent throws `ArgumentException`. This is the same well-known WinUI/UWP rule that `winui3-modern-patterns.md` emphasises for tree manipulation.
**Notes:** **This is the most important bug in the audit.** Selecting any non-`Thin` position will crash the overlay process at runtime. Two viable fixes: (a) build `singleRow` only inside the `Thin` else-branch, or (b) call `singleRow.Children.Clear()` before reparenting. Option (a) is cleaner. Plan B HUD acceptance cannot be claimed until this is fixed and tested headfully.

### [HUD] — `OnVmChanged` listens for `Position` / `LineCount`
**Verdict:** correct
**Code evidence:** `Controls/TopBarControl.xaml.cs:217-242`. Property-name switch includes both new names plus the existing `TopBarMetricIds`/`ActivePreset`/`CurrentSnapshot`/sparkline histories. All updates go through `DispatcherQueue.TryEnqueue`.
**Source 1 (spec):** `design.md:355` step 4.
**Source 2 (best-practices):** `memory-bank/best_practices/winui3_overlay.md:123-128` threading rule. Compliance: ✅.
**Notes:** Same dispatcher rule honoured as MainWindow.

### [HUD] — measured-sizing pattern preserved
**Verdict:** correct
**Code evidence:** `Controls/TopBarControl.xaml.cs:657-672` `MeasureContent` calls `c.Measure(Unbounded)` on each row child, sums heights, and adds `RootBorder.Padding` + `BorderThickness`. `Notify` at `:674-683` publishes `EstimatedWidth`/`EstimatedHeight` via `LayoutMetricsChanged` — the same event `MainWindow.OnTopBarLayoutMetricsChanged` listens to (`MainWindow.xaml.cs:100-103`).
**Source 1 (best-practices):** `memory-bank/best_practices/winui3_overlay.md:64-82` — slim HUD bar sizing pattern: *"Measure cells with an unbounded size … Add root padding, publish the measured width, then resize the HWND."*
**Source 2 (research):** `planb-internal-xaml-audit.md:211` — *"the measured-sizing pattern (...) works unchanged for the new mode."*
**Notes:** This part of the rewrite respects the established pattern. Once the reparent bug above is fixed, sizing should be correct.

### [HUD] — `MainWindow.xaml` has not introduced `x:Bind`
**Verdict:** correct
**Code evidence:** `src/HHAPulse.Overlay/MainWindow.xaml` is 9 lines: a `Grid` hosting a single `TopBarControl`. No `{x:Bind}` markup.
**Source 1 (best-practices):** `memory-bank/best_practices/winui3_overlay.md:130-134` — *"NEVER use `x:Bind` in `MainWindow.xaml` (WinUI 3 leak: ~1000 objects per window)"*.
**Source 2 (spec):** `design.md:53` keeps `MainWindow.xaml` in scope for token-only swaps; the spec does not introduce data-bindings on the HUD window.
**Notes:** The 1k-object-per-window leak is explicitly avoided.

### [Spec] — §3.3 typography is Segoe-based
**Verdict:** correct
**Code evidence (spec, not source):** `design.md:208-217`. *Display* = Segoe UI Semibold; *Body* = Segoe UI 400/600; *Numerals* = Consolas; *Eyebrow* = Segoe UI 600 uppercase. Followed by *"Plan B keeps the system-font stack (`Segoe UI`, `Segoe Fluent Icons`, `Consolas`) and defers any branded-font pass."*
**Source 1 (Grep):** `Inter`, `JetBrains Mono`, `Space Grotesk` — zero matches across the spec file.
**Source 2 (theme):** `src/HHAPulse.Overlay/Themes/HhapTheme.xaml:180` — `<FontFamily x:Key="HhapFontMono">Consolas</FontFamily>`. The token plan in §6 is consistent with the §3.3 stance.
**Notes:** `HhapFontInter` etc. only appear in two *research* documents (`planb-reference-implementations.md`, `planb-winui3-platform-april2026.md`) where they are historical recommendations, not active spec.

### [Tests] — `TopBarControlTests.cs` references a method that does not exist
**Verdict:** wrong (compile failure)
**Code evidence:** `tests/HHAPulse.Overlay.Tests/Controls/TopBarControlTests.cs:12-14` calls `TopBarControl.ValueMinWidth(...)`. The actual member on `TopBarControl` is `internal static int ValueMinCharCount(string id)` at `Controls/TopBarControl.xaml.cs:733`. There is no `ValueMinWidth` member anywhere in `src/` (`Grep ValueMinWidth` returns zero hits).
**Source 1 (Grep):** zero hits for `ValueMinWidth` in `src/`.
**Source 2 (test):** the test asserts `>= 72`, `>= 144`, `>= 96` — pixel widths. The real `ValueMinCharCount` returns *digit counts* (`6`, `10`, `7`) which would not satisfy these assertions even after a rename. The test was written against an older API surface.
**Notes:** **The Overlay test project will not compile.** Either expose a `public static int ValueMinWidth(string id)` helper that multiplies char count by `digitAdvance`, or rewrite the test to assert on `ValueMinCharCount` directly. Until then no Overlay test runs — including the Plan B catalog and view-model tests — because xUnit won't load a broken assembly.

### [Tests] — `TopBarLayoutMode` parametrised test
**Verdict:** correct
**Code evidence:** `tests/HHAPulse.Overlay.Tests/Controls/TopBarControlTests.cs:17-27` exercises all six positions. `internal` visibility is provided by `src/HHAPulse.Overlay/Properties/AssemblyInfo.cs:3` (`InternalsVisibleTo("HHAPulse.Overlay.Tests")`).
**Source 1 (spec):** §5.4 step 7 (`design.md:358`).
**Source 2 (source):** `Controls/TopBarControl.xaml.cs:751-758` `ResolveLayoutMode` matches the parametrised expectations exactly.
**Notes:** Will run as soon as the previous compile failure is resolved.

### [Tests] — Round-trip + preset coverage present
**Verdict:** correct
**Code evidence:**
- Legacy round-trip: `SettingsServiceTests.cs:28-55`.
- `FullMetrics.Length == 19`: `OverlayPresetCatalogTests.cs:58-72`.
- `TunerMetrics.Length == 11`: `OverlayPresetCatalogTests.cs:36-56`, also `:91-95` for the Custom-empty fallback.
- `PresetDisplayName(Tuner) == "Advanced"` and `Custom == "Manual"`: `OverlayPresetCatalogTests.cs:115-121`.
- `NextPreset` cycle: `OverlayPresetCatalogTests.cs:105-113`.
- ViewModel position/line-count: `OverlayViewModelTests.cs:40-57`.
**Source 1 (spec):** all of §5.4 and §5.5.
**Source 2 (audit checklist):** matches the eight expected tests in the original Plan B audit scope.
**Notes:** Coverage is complete. The blocker is the `ValueMinWidth` compile failure above, which will hide every one of these green tests until fixed.

### [Tests] — Missing: `SideDock` runtime smoke test
**Verdict:** missing
**Code evidence:** `TopBarControlTests.cs` only validates `ResolveLayoutMode`; nothing actually calls `RebuildMetricViews` with a `SideDock` view-model and inspects the resulting tree.
**Source 1 (spec):** `design.md:358` step 7 explicitly enumerates the new vertical-stack rendering path as net-new code.
**Source 2 (research):** `planb-internal-xaml-audit.md:211` flags the side-dock path as the only structurally new container in the rewrite.
**Notes:** A unit test that injects an `OverlayViewModel` with `Position = LeftDock` and runs `RebuildMetricViews` would have caught the reparent crash documented above.

### [Tests] — Missing: `OverlayPresetCatalog.GetMetricIds(Custom)` non-Tuner-fallback intent
**Verdict:** missing
**Code evidence:** `OverlayPresetCatalogTests.cs:88-95` only tests the empty-list fallback. There is no test for the spec's intent that Manual seeds *from the source preset*. Acceptable now (catalog is stateless), but should be added to `CompanionViewModel`'s test suite once that VM lands.
**Notes:** Tracking-only, not blocking Plan B's backend slice.

### [Helpers] — `MetricFormatterCompact.FormatBatteryCompact` integrity
**Verdict:** correct
**Code evidence:** `src/HHAPulse.Overlay/Helpers/MetricFormatterCompact.cs:233-271`. Charging+rate → `92% +9.2W`; charging+no-rate → `92% AC`; discharging+time+rate → `92% 1h48m 9.2W`; discharging+rate → `92% 9.2W`; otherwise `92%`. Charge-percent < 0 → `--%`.
**Source 1 (spec):** `design.md:430-440` table — five-row truth table, matches line-for-line.
**Source 2 (telemetry trace map):** `memory-bank/systems/telemetry-trace-map.md` Battery section — composite render is the documented contract; collector is fail-closed, so `--` only appears when `MetricFlags.Battery` is unset.
**Notes:** No regression. `FormatTotalPower` at `:206-218` similarly defers to `SystemPowerValidator.HasValidatedBatteryPower` and returns `--` on AC, honouring the *no-fake-power* product rule.

### [Helpers] — `MetricFormatter.cs` orphan status
**Verdict:** minor issue
**Code evidence:** `Grep "MetricFormatter\."` across `src/` returns zero hits; the only consumers are `tests/HHAPulse.Overlay.Tests/Helpers/MetricFormatterTests.cs` and references in research/memory-bank docs.
**Source 1 (spec):** `design.md:321` — *"`Helpers/MetricFormatterCompact.cs` — if still unused after the redesign, delete; else keep."* (Names the wrong file but the intent is "keep what production uses, delete what nothing references.")
**Source 2 (XAML audit):** `planb-internal-xaml-audit.md:237` — *"`MetricFormatter.cs` — exists but NOT referenced in production paths."*
**Notes:** Production uses `MetricFormatterCompact`. `MetricFormatter` survives only because its own test exists. Recommend deleting both `MetricFormatter.cs` and its test as a Plan B cleanup commit, since no view consumes the verbose format. Not a blocker.

### [Dead code] — `ControlShellTextBuilder.cs` already removed
**Verdict:** correct
**Code evidence:** `Glob src/**/Helpers/*.cs` returns only `MetricFormatter.cs` and `MetricFormatterCompact.cs`; no `ControlShellTextBuilder.cs`.
**Source 1 (XAML audit):** `planb-internal-xaml-audit.md:238` — *"`Helpers/ControlShellTextBuilder.cs` — used by `HubView` and `SupportView` for status strings. If `CompanionView` inlines the strings instead, this helper can be deleted."* The helper is gone, consistent with `HubView`/`SupportView` deletion.
**Source 2 (spec):** `design.md:310-311` deletes `HubView.xaml` and `SupportView.xaml`.
**Notes:** Cleanup already done.

### [Plan A dependency] — `MetricFlags` covers every Plan B HUD metric
**Verdict:** correct
**Code evidence:** `src/HHAPulse.Shared/Models/MetricFlags.cs:8-30` defines `Fps, FrameTime, FrameGen, InputLatency, Battery, CpuUsage, CpuTemperature, CpuPower, GpuUsage, GpuTemperature, GpuPower, Memory, Vram, Display, Fan, SystemPower, GpuClock, DeviceTemperature, StorageTemperature, StorageWear, CpuClock, NpuPresent`. Every flag referenced by `MetricFormatterCompact.FormatValue` (`MetricFormatterCompact.cs:13-123`) and `OverlayViewModel.UpdateHistory` (`OverlayViewModel.cs:109-144`) is present.
**Source 1 (telemetry trace map):** `memory-bank/systems/telemetry-trace-map.md` `:8-28` documents every collector that raises these flags.
**Source 2 (catalog):** `OverlayPresetCatalog.cs` constants 1:1 with the flag set used by the formatter.
**Notes:** **No Plan A flag dependency is missing.** Plan B's HUD has no blocker waiting on a Plan A flag rename or addition.

### [Threading] — All UI updates dispatched
**Verdict:** correct
**Code evidence:**
- `Controls/TopBarControl.xaml.cs:219-242` — `OnVmChanged` wraps the entire body in `DispatcherQueue.TryEnqueue`.
- `MainWindow.xaml.cs:96` — `OnViewModelPropertyChanged` dispatches `ApplyLayoutState` via `DispatcherQueue.TryEnqueue`.
- `MainWindow.xaml.cs:102` — `OnTopBarLayoutMetricsChanged` does the same.
**Source 1 (best-practices):** `memory-bank/best_practices/winui3_overlay.md:123-128`.
**Source 2 (research):** `planb-winui3-platform-april2026.md` §1 — flyout dismiss/focus restoration is automatic but state mutations from worker threads must still cross the dispatcher.
**Notes:** Pattern compliance is total in the audited surface.

### [GPT handoff claims] — items not verifiable from current repo state
- GPT's claim that `ApplyPosition` covers *"all six positions"* in `MainWindow` is **partially true** — the position branch is correct, but the height branch is not (see "side-dock height" finding above). Verification: false on `LeftDock`/`RightDock` height.
- GPT's claim that the `TopBarControl` rebuild uses *"three rendering modes"* — code path exists, but two of the three modes crash at runtime due to the reparent bug. Verification: false on Tall and SideDock.
- GPT's claim that *"all Plan B tests are present"* — test files exist but the assembly does not compile because of `ValueMinWidth`. Verification: false until rename.
- GPT's claim about §3.3 spec update to Segoe — verified true (no Inter/JetBrains/Space Grotesk anywhere in the spec).
- GPT's claim about settings round-trip — verified true (legacy 0/1 deserialization tested).

---

## Three most critical issues

1. **`TopBarControl.RebuildMetricViews` reparents children that already have a parent.** Lines 284-310 build `singleRow` for every layout mode and only the `Thin` mode actually attaches `singleRow` to `RowsPanel`. The `Tall` and `SideDock` branches re-add the same `UIElement` instances to fresh containers, which throws `ArgumentException: Element is already the child of another element` at runtime. Selecting any of `TopTall`, `BottomTall`, `LeftDock`, or `RightDock` will crash the overlay. Fix: build `singleRow` lazily inside the `Thin` else-branch only.

2. **`TopBarControlTests.cs` references the non-existent `TopBarControl.ValueMinWidth(...)`.** The real internal method is `ValueMinCharCount`. The Overlay test assembly will not compile, hiding every other Plan B test (catalog, view-model, settings round-trip). Fix: either expose a `ValueMinWidth` pixel helper or rewrite the test to assert on `ValueMinCharCount`.

3. **`MainWindow.ResizeOverlayWindow` does not give side-docks the full work-area height.** Line 126 unconditionally clamps height to `TopBar.EstimatedHeight`, so `LeftDock`/`RightDock` render as a small box at `WorkArea.Y`, not a vertical rail. Spec §5.4 and `planb-internal-xaml-audit.md:205` both require `height = WorkArea.Height` for side docks. Fix: branch height on `isSideDock`.

## Tests that should exist but are missing

- A `RebuildMetricViews` smoke test that constructs an `OverlayViewModel`, sets `Position = LeftDock`, calls `RebuildMetricViews()` (or instantiates `TopBarControl` and assigns the VM), and asserts the resulting `RowsPanel.Children.Count > 0` without throwing. Would have caught critical issue #1.
- A `MainWindow.ResizeOverlayWindow` test (or a pure helper extracted for testability) that asserts the side-dock height equals the work area height. Would have caught critical issue #3.
- A `CompanionViewModel`-level test (when the VM lands in a future commit) that seeds Manual from the *currently-active* preset rather than from the catalog's `TunerMetrics` fallback (per spec §5.5 Manual mode opening state).

## Plan A dependency blockers

**None.** Every `MetricFlags` value referenced by `MetricFormatterCompact`, `OverlayViewModel`, and `TopBarControl` is already declared in `src/HHAPulse.Shared/Models/MetricFlags.cs`. `CpuClock`, `DeviceTemperature`, `StorageTemperature`, `StorageWear`, `SystemPower`, `Fan`, `GpuClock`, and `NpuPresent` are all present. Plan B is not waiting on a Plan A flag.
