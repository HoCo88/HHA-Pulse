# 2026-04-15 — Plan B: Companion Redesign + 6-Position HUD Layout

Plan A (telemetry backend expansion) is documented in the sibling `INDEX.md`. This entry records what Plan B produced in parallel: the companion-window cutover, the six-position HUD layout, the `Full` preset, and the bug-fix cycles that followed. Every finding in this entry is cross-referenced against at least one research report, the approved spec, or the memory-bank best-practices doc, per Johny's standing rule against single-source claims.

## Scope

**In scope for Plan B:**

- Cutover of the companion window from `HubView` + `SupportView` + `SettingsSheet` + `ComposerOverlay` to a single `CompanionView`.
- HUD layout expansion from binary `OverlayEdge.Top`/`Bottom` to a six-value `TopBarPosition` enum with new `LeftDock` and `RightDock` side-rail rendering.
- Preset system landing in `OverlayPresetCatalog.cs`: `Full = 5`, display-name renames (`Tuner → Advanced`, `Custom → Manual`), `NextPreset` cycle update.
- New companion controls: `PositionPillButton` + `PositionFlyout`, `FeelAndFitPillButton` + `FeelAndFitFlyout`, `PresetRow`, `ManualMetricSheet`, `NpuStatusCard`, `EventLine`.
- Theme/style extensions: 11 new tokens, 5 new styles, shared `HhapFlyoutPresenterStyle`, delete orphan `HhapRailNavButtonStyle`.
- Spec update (`docs/superpowers/specs/2026-04-15-hha-pulse-ux-redesign-design.md`) — Round-2 reality fixes + `§3.3` font stance change to `Segoe UI` / `Segoe Fluent Icons` / `Consolas` (no bundled `Inter` / `JetBrains Mono` / `Space Grotesk` in Plan B).

**Explicitly not in Plan B:** telemetry backend work (`storage_temp`, `storage_wear`, `cpu_clock`, NPU detection) — those live in Plan A per the sibling `INDEX.md`. Plan B only consumes whatever `TelemetrySnapshot` fields exist after Plan A ships.

## Research reports produced

All six live under `docs/superpowers/research/`:

- `planb-internal-xaml-audit.md` — first-hand audit of existing HHA Pulse XAML. Token inventory (`HhapTheme.xaml`), style inventory (`HhapStyles.xaml`), view-layer pattern catalog, `ControlWindow → CompanionView` migration map, `CompanionViewModel` integration pattern (wrap-not-inherit), `MainWindow.ApplyPosition` expansion impact, asset bundling state, orphans found.
- `planb-winui3-platform-april2026.md` — Microsoft Learn + Windows App SDK 1.6 ground truth for `Flyout`, `VisualStateManager` + `AdaptiveTrigger`, Mica/Acrylic, custom font loading, theme dictionaries, `ConnectedAnimation`, `ItemsRepeater` + `UniformGridLayout`, touch + gamepad focus, and the April 2026 Windows App SDK delta. **Three gotchas captured**: top-down `AdaptiveTrigger` evaluation, theme-dictionary pollution, `Flyout` theme inheritance.
- `planb-reference-implementations.md` — real citeable XAML from Microsoft Terminal, Files app, PowerToys, WinUI Gallery, Community Toolkit. Flagged two patterns with no production reference — status-chip row with colored dots and monospace diagnostic strip — as hand-design work.
- `planb-implementation-audit-ui.md` — post-implementation audit of the UI surface (companion view + controls + themes/styles + deletions).
- `planb-implementation-audit-backend.md` — post-implementation audit of the HUD + preset catalog + settings + tests + spec update.
- `planb-fix-verification.md` — final verification pass after the first fix round, catching the one-character compile error at `ControlWindow.xaml.cs:153` (`ResolveManualSourcePreset()` called with zero arguments against a one-argument signature at `:186`).

## What landed, by subsystem

### Companion shell cutover

- `src/HHAPulse.Overlay/ControlWindow.xaml` + `.xaml.cs`: stripped to a single-surface host. Title bar + caption buttons stay; Hub/Support nav buttons, Overlay-settings chip, `SettingsSheetHost`, and `ComposerOverlayControl` are gone. `ControlWindow.ViewModel` stays typed as `OverlayViewModel` to minimise `App.xaml.cs` churn — the class internally owns a `CompanionViewModel` that wraps the injected HUD VM.
- `src/HHAPulse.Overlay/Views/CompanionView.xaml` + `.xaml.cs` (new): the single-surface page. Topbar (mark + state chips + caption buttons) → toolbar row (`PositionPillButton` + `FeelAndFitPillButton`) → preset row (`ItemsRepeater` + `UniformGridLayout` `MinItemWidth="220"`) → `Or build your own ▸ Manual` link → `EventLine` → support footer (3 band-dot states + 4 action buttons).
- `src/HHAPulse.Overlay/ViewModels/CompanionViewModel.cs` (new): wraps `OverlayViewModel` via property reference, owns `IsFeelAndFitOpen` / `IsPositionFlyoutOpen` / `IsManualSheetOpen` UI-only state + `EventLineShort` short-form text. UI state is VM-driven, not via raw `Visibility =` assignments in code-behind.
- `src/HHAPulse.Overlay/Controls/PositionFlyout.xaml` (new): WinUI 3 `Flyout` content. Monitor canvas (faint cyan grid from `HhapMonitorGridCell` token) + six anchor buttons. Opens from `PositionPillButton`.
- `src/HHAPulse.Overlay/Controls/FeelAndFitFlyout.xaml` (new): bg opacity / text opacity / text-size sliders, two-way-bound to the same `SettingsService` properties the deleted `SettingsSheet` used.
- `src/HHAPulse.Overlay/Controls/ManualMetricSheet.xaml` (new): inline expander revealed by the Manual link. Toggle chips grouped by domain (Performance / CPU / GPU / Memory / System / Sensors). No scrim, no drawer.
- `src/HHAPulse.Overlay/Controls/NpuStatusCard.xaml` (new): companion diagnostics card. Reads `snapshot.Npu.AdapterName` / `.Vendor` / `.Present` from the Plan A telemetry surface (present in `src/HHAPulse.Shared/Models/TelemetrySnapshot.cs` per Plan A's contract extensions).
- `src/HHAPulse.Overlay/Controls/EventLine.xaml` (new): muted mono strip rendering `timestamp · pid · exe · frametime · sample rate`. Uses `Consolas` for the mono path per Plan B's font stance.

### HUD layout expansion from 2 to 6 positions

- `src/HHAPulse.Overlay/Settings/AppSettings.cs`: `enum OverlayEdge { Top, Bottom }` replaced by `enum TopBarPosition { TopThin = 0, BottomThin = 1, TopTall = 2, BottomTall = 3, LeftDock = 4, RightDock = 5 }`. **The `TopThin = 0` / `BottomThin = 1` ordering is deliberate** — it preserves byte-for-byte JSON round-trip with existing saved settings files without a custom converter. A setting saved as `"TopBarPosition": 0` from the old schema loads as `TopThin` in the new schema, which is the correct migration. This trick is cleaner than my Round-1 spec recommendation (which proposed a `JsonConverter`) and was contributed by GPT's Plan B draft.
- `src/HHAPulse.Overlay/Settings/SettingsService.cs`: `Normalize` falls back to `TopThin` for out-of-range values.
- `src/HHAPulse.Overlay/MainWindow.xaml.cs`: `ApplyEdge(OverlayEdge)` renamed to `ApplyPosition(TopBarPosition)`. `ResizeOverlayWindow` around `:121` now has a 6-way anchor switch:
  - `TopThin` / `TopTall` — anchor at `WorkArea.Y`, width = `TopBar.EstimatedWidth`, height = `TopBar.EstimatedHeight`.
  - `BottomThin` / `BottomTall` — anchor at `WorkArea.Y + WorkArea.Height - height`, same measured width/height.
  - `LeftDock` — anchor at `WorkArea.X`, width = `HhapSideDockWidth` token value, height = `WorkArea.Height` (full rail).
  - `RightDock` — anchor at `WorkArea.X + WorkArea.Width - width`, same width/height.
- `src/HHAPulse.Overlay/Controls/TopBarControl.xaml.cs`: `RebuildMetricViews` now has three explicit rendering modes (thin horizontal single-row, forced two-row tall, vertical side-dock). Each mode builds its own container. **The measured-sizing pattern (`element.Measure(Unbounded)` at `:283` → `Notify(rows, width, height)` via `LayoutMetricsChanged`) is preserved in all three modes** — this is the pattern from `memory-bank/best_practices/winui3_overlay.md:64-82` ("Slim HUD Bar Sizing") and breaking it would reintroduce the hardcoded-height bug from 2026-04-13.

### Preset system

- `src/HHAPulse.Overlay/Settings/OverlayPresetCatalog.cs`: extended (not rewritten).
  - `OverlayPreset.Full = 5` added (preserves `Off = 4` round-trip since `Off` retains its existing numeric value).
  - New `FullMetrics` array with 19 entries from `AllMetricIds` per spec §5.5.
  - `TunerMetrics` curated to 11 entries: `Fps, OnePercentLow, FrameTime, CpuUsage, CpuTemp, CpuPower, GpuUsage, GpuTemp, Ram, Battery, RefreshRate`.
  - `PresetDisplayName(Tuner)` returns `"Advanced"`; `(Custom)` returns `"Manual"`. **Enum values stay `Tuner` and `Custom` to preserve saved-settings compatibility** — only the UI labels change.
  - `NextPreset` cycle: `Minimal → Standard → Advanced → Full → Off → Minimal`.
  - `GetMetricIds` switch gains an `OverlayPreset.Full => FullMetrics` arm.

### Manual mode

- `ControlWindow.xaml.cs:149-169 OnManualRequested`: when the user opens Manual, the code reads the currently-active curated preset, calls `ResolveManualSourcePreset(currentSettings)` to classify it, copies that preset's metric IDs into `currentSettings.EnabledMetricIds` via `companionViewModel.CreateManualSeed()`, sets `ManualSourcePreset` on settings so the UI footer can display `"Manual matches {SourcePresetName} + your changes"`, and only then switches `ActivePreset = OverlayPreset.Custom` and raises `CustomMetricsChanged` + `PresetChanged`. **The seeding happens before the preset switch, per spec §5.5**.

### Theme and style system

- `src/HHAPulse.Overlay/Themes/HhapTheme.xaml`: extended. All three theme dictionaries (`Dark` / `Light` / `HighContrast`) are declared from day one even though only `Dark` has real content — this avoids the theme-dictionary pollution gotcha in `planb-winui3-platform-april2026.md` §5 (Gotcha #2). New tokens added: `HhapAccent`, `HhapAccentHi`, `HhapGold`, `HhapBandGood`, `HhapBandOkay`, `HhapBandWarn`, `HhapDurFast`, `HhapDurBase`, `HhapSideDockWidth` (= 280 effective px), `HhapMonitorGridCell`, `HhapTouchTargetMin`.
- `src/HHAPulse.Overlay/Themes/HhapStyles.xaml`: new styles `HhapPresetCardStyle`, `HhapPositionButtonStyle`, `HhapBandDotStyle`, `HhapLivePulseStyle`, `HhapEventLineStyle`, and a shared `HhapFlyoutPresenterStyle` consumed by both `PositionFlyout` and `FeelAndFitFlyout`. Orphan `HhapRailNavButtonStyle` deleted.

### Deletions

Confirmed removed from the repo:

- `src/HHAPulse.Overlay/Views/HubView.xaml` + `.xaml.cs`
- `src/HHAPulse.Overlay/Views/SupportView.xaml` + `.xaml.cs`
- `src/HHAPulse.Overlay/Views/SettingsSheet.xaml` + `.xaml.cs`
- `src/HHAPulse.Overlay/Views/ComposerOverlay.xaml` + `.xaml.cs`
- `src/HHAPulse.Overlay/ViewModels/ComposerViewModel.cs`
- `enum OverlayEdge` (replaced by `TopBarPosition`)
- `event EnableCaptureRequested` on `ControlWindow` (dead code — grep confirmed zero emitters)
- `HhapRailNavButtonStyle` (orphan confirmed by earlier audit)

Grep-verified zero hits across `src/` and `tests/` for: `OverlayEdge`, `EnableCaptureRequested`, `HubView`, `SupportView`, `SettingsSheet`, `ComposerOverlay`, `ComposerViewModel`, `ValueMinWidth`, `RequestedTheme = ActualTheme`, `<Expander` (in `Views/`).

### Spec updates

- `docs/superpowers/specs/2026-04-15-hha-pulse-ux-redesign-design.md` §3.3 font stance changed to `Segoe UI` / `Segoe Fluent Icons` / `Consolas`. **No bundled font assets in Plan B.** The trade-off: brand distinctness from handheldally.com drops, but Plan B ships without an `Assets/Fonts/` directory, without MSIX-packaged font URI verification, and without a font-licensing review. Cost: ~0 engineering hours vs. ~4 hours for the bundled-font path. The trade-off was made explicitly during the fix-round review and recorded in the spec itself.
- §4.1 Metric availability gate rewritten to match the 2026-04-10 telemetry delta (operational / hardware-gated / never-faked decomposition) per the Round-2 reality pass.
- `1:1 with Valve` softened to `density-mapped parallel` at L96 and L380.
- `fact strip` literal phrase rewritten to `live metric readout` at L280 and L333 so the Round-2 strict grep returns zero hits.
- `Build your own` non-success ban at L609 narrowed to cover the composer wizard only, not the Manual link.
- Area around `:357` describes the `MainWindow.ApplyPosition` / `TopBarControl.RebuildMetricViews` insertion points and matches the landed code.

## Bugs found and fixed across review cycles

Three review rounds ran over the day. Every bug was caught by the subsequent audit, not in production.

### Round 1 — initial reviewer findings (4 items, all High/Medium)

1. **Stale metric availability gate at spec §5.5** (High). Contradicted §5.6 battery rewrite and the 2026-04-10 telemetry delta. Fixed in Round-2 spec reality pass.
2. **"1:1 with Valve" still at L96 and L380** (Medium). Fix C was incomplete. Fixed in Round-2 pass.
3. **`fact strip` literal still appears in denial sentences at L280 / L333** (Medium). Stricter acceptance gate than Round 1 assumed. Fixed by rewriting the denials to use `live metric readout`.
4. **Manual link label vs non-success list contradiction** (L384 vs L609) (Medium). Ban narrowed at L609.

### Round 2 — implementation audit (9 items, 3 runtime blockers + 1 spec violation + 5 quality)

1. **`TopBarControl` reparenting crash** (🔴 runtime blocker). `RebuildMetricViews` was building `singleRow` and adding every `perf`/`hw` element to it **for all layout modes**, then the Tall and SideDock branches re-added the same `UIElement` instances to fresh containers. `UIElementCollection.Add` throws `ArgumentException` on already-parented elements. Fix: build `singleRow` lazily inside the Thin branch only; Tall and SideDock branches build fresh containers from the `perf`/`hw` lists directly. Lesson captured in `best_practices/winui3_overlay.md` as Rule 12.
2. **Test assembly compile error** (🔴). `TopBarControlTests.cs:10` referenced non-existent `TopBarControl.ValueMinWidth(...)` — the real member is `ValueMinCharCount`. With this broken, **no Plan B tests were running**. Fix: renamed the reference. Internal visibility via `InternalsVisibleTo` at `src/HHAPulse.Overlay/Properties/AssemblyInfo.cs:3`.
3. **Side-dock height clamping** (🔴). `MainWindow.ResizeOverlayWindow` unconditionally clamped height to `Math.Min(TopBar.EstimatedHeight, displayArea.WorkArea.Height)`. Side-docks rendered as a box in the top corner instead of a full-height rail. Fix: switch on `TopBarPosition` — side-dock branches set `height = displayArea.WorkArea.Height` and `width = HhapSideDockWidth`; top/bottom branches keep the measured path.
4. **No `VisualStateManager` in `CompanionView.xaml`** (🔴 spec violation). `UniformGridLayout MinItemWidth="220"` handled preset-row reflow correctly, but the spec §4.1 + research required a density group with font-size step-down and event-line shortening setters at `xs`. Fix: added a `VisualStateManager.VisualStateGroups` block with `AdaptiveTrigger`s in **top-down `Lg → Md → Sm → Xs` order** per `planb-winui3-platform-april2026.md` Gotcha #1. `Xs` state has real setters: short event-line text, compact support actions, hero font-size step-down.
5. **Advanced diagnostics `Expander` still rendered** (🔴 spec violation). `CompanionView.xaml:252-261` still had an `<Expander>` for advanced diagnostics despite spec §8 saying *"deleted entirely"*. One-line delete.
6. **Flyout `RequestedTheme = ActualTheme` cargo-cult workaround** (🟡). Set on the inner `UserControl` content, but the framework hosts flyout content inside a `FlyoutPresenter` popup sibling — the inner setter doesn't propagate through the popup boundary. App-level theme dictionaries already satisfy Gotcha #3 fix option (b) from `planb-winui3-platform-april2026.md`. Fix: removed the workaround; theme inheritance works correctly via the app-level dictionary chain.
7. **`CompanionViewModel` missing UI-state booleans** (🟡). No `IsFeelAndFitOpen` / `IsPositionFlyoutOpen` / `IsManualSheetOpen` — Manual sheet visibility was mutated via code-behind `Visibility =` assignments. Fix: added four VM-owned properties backed by `SetProperty`, plus `EventLineShort`. View bindings now drive visibility from VM state.
8. **Lifecycle leak risk — event subscriptions without symmetric cleanup** (⚠️). `CompanionView` and `ControlWindow` subscribed events without matching `Unloaded` / `Closed` unsubscription. Real risk because `ControlWindow` opens/closes multiple times per session via the `HotkeyService` launch-command path. Fix: 8 symmetric subscribe/unsubscribe pairs added to `CompanionView`; `ControlWindow.OnClosed` tears down the 9 companion events + `Closed` handler + `ViewModel = null`. Per `memory-bank/best_practices/winui3_overlay.md:133`.
9. **Manual seeding fallback** (🟡). `OverlayPresetCatalog.GetMetricIds(Custom, [])` falls back to `TunerMetrics` regardless of which curated preset was previously active. Not a catalog bug (the catalog is stateless and that fallback is fine); a VM wiring gap. Fix: `ControlWindow.OnManualRequested` seeds `ManualSourcePreset` and `EnabledMetricIds` from the currently-active preset **before** flipping `ActivePreset = Custom`.

### Round 3 — fix verification (1 new issue)

1. **`ResolveManualSourcePreset()` called with zero arguments** (🔴 compile error introduced by the Round-2 fix). `ControlWindow.xaml.cs:153` called the method with no args; `:186` declared it as `private static OverlayPreset ResolveManualSourcePreset(AppSettings settings)`. Fixed in-place: `ResolveManualSourcePreset(currentSettings)`.

Final verdict after Round 3: source-level complete and ready for a real Windows build + test pass.

## Rules locked by Plan B

- **HUD layout insertion point is `INotifyPropertyChanged`, not `DependencyProperty`.** `OverlayLayout` record at `OverlayPresetCatalog.cs:147` + `OverlayViewModel.ApplySettings` + `TopBarControl.OnVmChanged` is the chain. Do not introduce a new `HudLayout` enum or DP; the existing property-change pipeline is the canonical way to publish new `Position` / `LineCount` state.
- **`RebuildMetricViews` builds a fresh container per layout mode.** Do not share a `StackPanel` between Thin / Tall / SideDock branches. `UIElementCollection.Add` throws on already-parented elements; the pattern that caused the Round-2 blocker must not return.
- **Side-dock anchoring uses the `HhapSideDockWidth` token.** Provisional value 280 effective px; exact Steam-overlay parity is a post-implementation validation item, not a hardcoded literal in source.
- **`TopBarPosition` persisted values preserve `TopThin = 0`, `BottomThin = 1`.** Existing saved settings round-trip without a converter. Do not reorder these values in future enum extensions.
- **`OverlayPreset.Tuner` and `.Custom` enum values stay.** Only the UI display names changed. Saved settings keep working.
- **`OverlayPreset.Full = 5`.** Reserves `Off = 4`. Future presets must use `≥ 6` or explicit renumbering with migration.
- **`VisualStateManager` groups list `AdaptiveTrigger`s top-down from largest `MinWindowWidth` to smallest.** Evaluation is first-match-wins. The `Lg → Md → Sm → Xs` order is correct; reversing it is a first-match-wins bug. Captured in `best_practices/winui3_overlay.md` as Rule 13.
- **Theme dictionaries must ship with `Dark` + `Light` + `HighContrast` keys from day one,** even when only `Dark` has real content. Skipping any key causes brush leakage across subtrees with different `RequestedTheme`. Captured as Rule 14.
- **Flyout content does not inherit `RequestedTheme` from the parent page** because the `FlyoutPresenter` sits in a popup-tree sibling. Define theme resources at app level (which HHA Pulse already does) so flyouts resolve `{ThemeResource}` against the correct dictionary chain. Do not set `RequestedTheme = ActualTheme` inside flyout content — it is cargo-cult and doesn't propagate. Captured as Rule 15.
- **`HotkeyService` is intentionally launch-based (named event mutex + single-instance), not `RegisterHotKey`.** Rationale in spec §5.7.1: resilient to sleep/resume, fullscreen-exclusive games, and focus steal. Do not replace with Win32 `RegisterHotKey` without reading the spec's architectural justification.
- **No bundled fonts in Plan B.** `Segoe UI` + `Segoe Fluent Icons` + `Consolas` only. The `Assets/Fonts/` directory does not exist and Plan B does not create it. If `Inter` / `JetBrains Mono` / `Space Grotesk` bundling becomes a requirement later, it's a new milestone with its own MSIX packaging verification.
- **No tipping surface inside the product.** `CompanionView` has a quiet attribution line to `handheldally.com/premium` and nothing else. No tip jar card, no inline donation button, no fundraising copy.
- **No marketing copy inside the product surface.** Store-listing copy lives in `docs/sales/store-listing-copy.md`. The companion window is status + knobs + support, not brand narrative.

## Tests added / updated

- `tests/HHAPulse.Overlay.Tests/Controls/TopBarControlTests.cs` — `ValueMinCharCount` rename applied; test assembly now compiles.
- `tests/HHAPulse.Shared.Tests/` — round-trip coverage for the new `TopBarPosition` enum values; `AppSettings` JSON round-trip verified for legacy `TopBarPosition: 0` and `TopBarPosition: 1`.
- `tests/HHAPulse.Overlay.Tests/Settings/OverlayPresetCatalogTests.cs` — metric count assertions: `MinimalMetrics.Length == 2`, `StandardMetrics.Length == 6`, `TunerMetrics.Length == 11`, `FullMetrics.Length == 19`. Display name assertions: `PresetDisplayName(Tuner) == "Advanced"`, `(Custom) == "Manual"`. `NextPreset` cycle assertion.
- `tests/HHAPulse.Overlay.Tests/Controls/TopBarControlTests.cs` — (deferred to Round 3 but still missing) side-dock rendering smoke test and `ResizeOverlayWindow` side-dock height assertion. **These two tests would have caught Round-2 blockers #1 and #3 automatically and are filed as must-add before the next milestone.**

## Teammates used

Five read-only / write-capable teammates ran across the three review cycles, all on team `pulse-ux-redesign` (existing team from the spec round).

| Teammate | Type | Scope |
|---|---|---|
| `planb-xaml-auditor` | `Explore` | First-hand audit of existing HHA Pulse XAML to produce `planb-internal-xaml-audit.md`. |
| `planb-platform-researcher` | `Explore` | April 2026 Microsoft Learn + Windows App SDK 1.6 ground truth for `planb-winui3-platform-april2026.md`. |
| `planb-reference-scout` | `Explore` | Production WinUI 3 reference sources for `planb-reference-implementations.md`. |
| `planb-ui-audit` | `general-purpose` | Post-implementation UI audit → `planb-implementation-audit-ui.md`. |
| `planb-backend-audit` | `general-purpose` | Post-implementation backend/HUD/tests audit → `planb-implementation-audit-backend.md`. |
| `planb-fix-verifier` | `general-purpose` | Final fix-round verification → `planb-fix-verification.md`. |

**Process note captured:** initially three Plan B research dispatches used the `Explore` subagent type. `Explore` agents are read-only — they cannot write their own reports, so the coordinator had to manually copy their output into files. Subsequent Plan B research and audit dispatches used `general-purpose` teammates that can both read and write, which is the correct choice for any research task that needs to persist a report. Rule for next time: use `general-purpose` for research that produces a disk artifact; reserve `Explore` for ephemeral lookups where the finding lives only in the conversation.

## Still open / Windows-side validation required

- `dotnet build src/HHAPulse.Overlay/HHAPulse.Overlay.csproj -c Release -p:UseSharedCompilation=false --disable-build-servers` — not runnable on the macOS coordinator host.
- `dotnet build src/HHAPulse.CaptureService/HHAPulse.CaptureService.csproj -c Release -p:UseSharedCompilation=false --disable-build-servers` — same.
- `dotnet test tests/HHAPulse.Shared.Tests/HHAPulse.Shared.Tests.csproj -c Release` — same.
- `dotnet test tests/HHAPulse.Overlay.Tests/HHAPulse.Overlay.Tests.csproj -c Release` — same.
- Smoke test on a real handheld for all six positions, preset cycle, Manual seeding from each curated preset, legacy settings file loading, flyout dismiss behaviour, theme match in dark mode.
- Hardware validation of side-dock width against the real Steam in-game overlay notification strip — exact parity is a post-implementation validation item, not a hardcoded literal in source.

## Open items that survive Plan B

Still waiting from the Round-1 spec pass:

- Palette hex verification via a real handheldally.com browser screenshot + colour picker pass.
- Font family verification (moot now that Plan B is Segoe-based, but will matter if the Inter bundling milestone reopens).
- Logo asset from Johny for the top-bar mark.
- Steam-overlay side-dock width measurement.
- `(Win-rec)` factory scale confirmation on 8 handheld devices.
- Bazzite / Steam-Deck-Windows scope decision.
- **Microsoft Store packaging strategy** — this is the #1 launch blocker. Plan B's design decisions don't break MSIX compatibility (no kernel driver, no hooks, no `%PROGRAMFILES%` writes, no network listeners), but the overlay-process + elevated-capture-service architecture still needs a packaging approach per `memory-bank/best_practices/code_signing.md`.

## Cross-references

- Approved spec: `docs/superpowers/specs/2026-04-15-hha-pulse-ux-redesign-design.md`
- Round-2 + telemetry plan: `docs/superpowers/plans/2026-04-15-round2-and-telemetry-expansion.md`
- Store-listing copy: `docs/sales/store-listing-copy.md`
- Plan A daily log (sibling): `memory-bank/2026/04/15/INDEX.md`
- Best practices (updated with new Plan B rules): `memory-bank/best_practices/winui3_overlay.md`
- Architecture snapshot: `memory-bank/systems/ARCHITECTURE.md`
- Six Plan B research reports: `docs/superpowers/research/planb-*.md`
