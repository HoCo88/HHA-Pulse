# 2026-04-13 — HHAP-0.5 overlay UI rewrite

Full replacement of the Codex-generated overlay UI. The earlier attempt on
this date produced a 1540-line code-behind shell (`ControlWindow.UI.cs`),
no design tokens, a broken scroll/resize path, and a left-rail that the
user explicitly rejected. This entry documents what shipped to replace
it — nothing from the earlier attempt remains in the repo.

## Visual direction

- Source-of-truth mockup is the user's `preview.html` (dark navy, cyan
  primary, amber for power/battery). Feel derived from handheldally.com:
  deep charcoal base, dual-accent energy, generous rounded cards.
- Target: Windows handhelds (Ally / Legion Go / Steam Deck on Windows).
  Minimum window 760×520, touch-first ≥44 px tap targets, explicit
  `OverlappedPresenter.IsResizable = true`.
- Layout rule: **no left sidebar.** Brand on the left of the top bar,
  Hub and Support buttons inline right of it, overlay-settings chip on
  the far right. Side rails waste space on handheld widths.

## Design system (new)

- `src/HHAPulse.Overlay/Themes/HhapTheme.xaml` — tokens from `preview.html`:
  20 SolidColorBrush entries, 5 LinearGradientBrush, 5 CornerRadius,
  spacing scale, typography scale, character-spacing ints.
- `src/HHAPulse.Overlay/Themes/HhapStyles.xaml` — card / stat-card /
  tile Border styles, 5 chip variants, full TextBlock typography scale,
  3 Button styles with 44 px minimum height, rail ToggleButton style
  (kept for future use).
- `App.xaml` merges both dictionaries via `ms-appx:///Themes/...` URIs.
- `App.xaml.cs:LoadControlsResources()` **must stay in code-behind**.
  It is the runtime catch for a WinAppSDK 1.7+ `XamlParseException` on
  `TabViewButtonBackground` — declaring `XamlControlsResources` in
  App.xaml re-triggers the crash before any try/catch can run.

## Shell

- `src/HHAPulse.Overlay/ControlWindow.xaml` — declarative shell, single
  scroll root. `ContentHost` is a `ContentControl` that swaps between
  `HubView` and `SupportView` UserControls (no Frame, no nested
  ScrollViewer).
- `src/HHAPulse.Overlay/ControlWindow.xaml.cs` — ~240 lines. Preserves
  the public event/method contract `App.xaml.cs:ShowControlWindow()`
  relies on (`ToggleOverlayRequested`, `ExitRequested`, `ShowModeChanged`,
  `EnableCaptureRequested`, `PresetChanged`, `CustomMetricsChanged`,
  `OpacityChanged`, `TextSizeChanged`, `PositionChanged`, `ViewModel`
  setter, `ApplySettings`).
- `ConfigureAppWindow()` on first Activated: explicit
  `presenter.IsResizable = true`, `PreferredMinimumWidth = 760`,
  `PreferredMinimumHeight = 520`. Title bar drag region is a single
  Grid with 160 px right inset so caption buttons and the resize grip
  stay hittable.
- `SettingsSheetHost` + `ComposerOverlayControl` live at the shell root
  with `Grid.Row="1"` / `Grid.RowSpan="2"` — they never live inside the
  content scroll viewer, which was the old scroll-bug trap.

## Views

- `Views/HubView.xaml` + `.xaml.cs` — bento dashboard. Hero card (54 px
  preset headline, chip row, primary Toggle + ghost Customize) + live
  HUD preview card (1.35/0.95 split at ≥900 px, stacked below 900).
  Preset row (Minimal / Standard / Performance / Build your own) with
  active preset highlighted via `HhapPrimaryButtonStyle`. Visibility +
  Position utility cards with `HhapStatCardStyle`. Optional live-metric
  strip at the bottom (FPS / CPU / GPU / Battery tiles, hidden when
  `snapshot.AvailableMetrics == 0`).
- `Views/SupportView.xaml` + `.xaml.cs` — header + 3 health-chip status
  cards (Capture / Game detect / Companion) + 4-button action row +
  passive tip-jar card + Advanced Diagnostics Expander. **Expander is
  `IsExpanded="False"` by hard requirement** — the raw dump must stay
  hidden by default.
- `Views/SettingsSheet.xaml` + `.xaml.cs` — right-side overlay sheet
  with background opacity, text opacity, and HUD text-size sliders, a
  Top/Bottom segmented button, and a Close button.
- `Views/ComposerOverlay.xaml` + `.xaml.cs` — real 4-step Build-your-own
  drawer: Layout → Modules → Detail → Reorder. Responsive: 480 px at
  ≥1280, 420 px at 900–1279, full-screen < 900. Owns its own
  `ComposerViewModel`; host wires the outgoing events `CloseRequested`,
  `PresetApplied`, `LayoutChanged` and calls `ResetToStart()` on open.
- `Views/ControlShellTextBuilder.cs` — static domain text formatters
  migrated from the deleted `ControlWindow.UI.cs`: `BuildDiagnosticsDump`,
  `BuildGpuTelemetrySummary`, `BuildCaptureHelpText`, `BuildGameDetectSummary`,
  `BuildHubRuntimeSummary`, `FormatBatterySummary`, `FriendlyPresetName`,
  `ShowModeText`, `TruthBadgeText`, `IsDisputed`.

## Composer data (HHAPulse.Shared, netstandard2.0)

- `ComposerLayout.cs` — enum with `TopBar/BottomBar/DualLine/LeftDock/
  RightDock/SidePanel` + `DisplayName`/`Description` extensions.
- `ModuleDetailPreset.cs` — `ComposerDetail` (Off/Summary/Tuning/Deep),
  `ComposerModuleIds`, `ModuleDetailPreset`, `ComposerModuleCatalog`
  (9 modules: FPS/CPU/GPU/Memory/Power/Battery/Storage/Display/Advanced),
  `CustomPresetDraft` — immutable snapshot emitted on `PresetApplied`.

## In-game HUD (`TopBarControl` + `SparklineGraph`)

- **No inner tile borders.** Only the outer `RootBorder` shell with
  `#14FFFFFF` hairline remains. Flat metric strip — not boxes-in-boxes.
- **No pixel constants anywhere in the sizing path.** Everything
  derives from measurement:
  - `MainWindow.ResizeOverlayWindow` reads `TopBar.EstimatedHeight`,
    which comes from `MeasureContent()` summing each row's
    `DesiredSize.Height` plus the measured `RootBorder.Padding` and
    `BorderThickness`. The old `54 / 84 / 92` constants are gone.
  - `TopBarControl.PixelMinWidth(id)` = `ValueMinCharCount(id) ×
    digitAdvance`, where `digitAdvance` is the measured width of `"0"`
    at the current `currentValueFontSize` with `ExtraBold` and the
    theme's `HhapTrackHudValue` character spacing. Changes on every
    `ApplyTextSize(size)` call.
  - `ValueMinCharCount` returns integer character counts per metric
    (Battery = 10, FPS = 6, usage = 4, etc.) — a semantic constraint,
    not a pixel table.
- Role-based palette: 5 tokens replace 15 neon brush constants:
  `PerfBrush = HhapBlue2Brush` (FPS/usage), `ThermalBrush =
  HhapAmber2Brush` (temp/power/battery), `NeutralBrush = HhapTextBrush`
  (memory/clocks), `MutedBrush = HhapMutedBrush` (refresh rate),
  `ActiveBrush = HhapGreenBrush` (fan).
- **Opacity fix.** `ApplyOpacity(bg, text)` mutates a private mutable
  `hudBackgroundBrush` (`SolidColorBrush` with base color `#0A101F`)
  via `.Color.A`. Text opacity is a separate channel via
  `vb.Opacity = textOpacity` on value blocks. The old code cascaded
  `RootBorder.Opacity = 0`, which hid text and all. Never re-introduce
  that pattern.
- `SparklineGraph` strokes with `HhapSparklineStrokeGradient` (blue
  gradient) for all series. The per-series `Color` parameter on
  `SetSeries` is preserved for API compatibility but ignored downstream.

## Dead code removed this session

- `ControlWindow.UI.cs` (1540 lines — entire programmatic UI)
- `Controls/BatteryMeter.xaml` + `.cs` (placeholder "Battery --", zero
  consumers)
- `Controls/MetricLabel.xaml` + `.cs` (unreferenced by `TopBarControl`)
- `ViewModels/TopBarViewModel.cs` (19-line ghost model, zero usages)
- `Helpers/ThresholdColorConverter.cs`, `Helpers/LayoutCalculator.cs`,
  `Aggregation/BatteryPredictor.cs`, `Aggregation/FpsPercentileCalculator.cs`
  — all confirmed zero-hit via grep before deletion
- Orphan test files under `tests/HHAPulse.Overlay.Tests/Aggregation/`
  for the two deleted aggregators
- Empty `src/HHAPulse.Overlay/Pages/` folder
- `RebuildMetricStatusCards` ghost method (cleared panel, never rebuilt)
- Repo-root `query` scratch file and `overlay-build-diag.log`
- Stale build dirs: `obj_codex*`, `bin_codex*`, `obj_alt*`, `obj_run*`,
  `obj-redesign`, `.codex-build`, `.testbin`, `obj_plan`, `obj_probe`
  under both `Overlay/` and `Shared/`

## Bugs fixed along the way

- **Silent launch crash.** `ComposerOverlay.SetStepDotActive` was
  reading `Resources["HhapChipBlueStyle"]` on the UserControl's own
  (empty) dictionary. WinUI 3's `ResourceDictionary` indexer calls
  `IMap.Lookup`, which throws `KeyNotFoundException` for missing keys
  — unlike .NET dictionaries. The `null ?? fallback` idiom never gets
  a chance. Fix: go straight to `Application.Current.Resources`, which
  is where the Hhap styles actually live via the merged dictionary.
- **Window can't scroll or resize.** Root cause was two collapsed
  `ScrollViewer`s sharing a `Grid` with no `RowDefinitions` and a
  title-bar drag region whose `Margin = 14,8,170,4` ate the resize
  grip. Replaced with a single `ScrollViewer` + `ContentControl` swap
  and an explicit `OverlappedPresenter.PreferredMinimumWidth/Height`
  call.
- **Hardcoded HUD heights.** Old `SingleRowHeight / TallRowHeight /
  DoubleRowHeight = 54 / 84 / 92` in `MainWindow.xaml.cs` never
  tracked the user's text-size slider. Replaced with measured content
  height. See "In-game HUD" above.
- **Background opacity hid everything.** `ApplyOpacity` modulated
  `RootBorder.Opacity` (cascading to text). Replaced with mutable
  background brush alpha — bg=0 now makes the background fully
  transparent while text stays visible at its own `textOpacity`.

## Build + test state

```
dotnet build src\HHAPulse.Overlay\HHAPulse.Overlay.csproj -c Release
  -p:UseSharedCompilation=false --disable-build-servers
  → Build succeeded. 0 Warning(s), 0 Error(s).

dotnet build src\HHAPulse.Overlay\HHAPulse.Overlay.csproj -c Debug
  -p:Platform=x64
  → Build succeeded. 0 Warning(s), 0 Error(s).

dotnet test tests\HHAPulse.Shared.Tests\HHAPulse.Shared.Tests.csproj -c Release
  → Passed 5 / Failed 0

dotnet test tests\HHAPulse.Overlay.Tests\HHAPulse.Overlay.Tests.csproj -c Release
  → Passed 69 / Failed 0
```

Test total grew from 39 to 74 as composer-era tests landed.

## Team used (hhap-ui-2026)

Five teammates ran in two waves. All stood down cleanly at session end.

| Teammate | Scope |
|---|---|
| `foundation` | Themes/HhapTheme.xaml + HhapStyles.xaml + App.xaml wiring |
| `hub-polish` | HubView bento layout + responsive breakpoints |
| `support-polish` | SupportView health chips + tip jar + diagnostics accordion |
| `composer-real` | 4-step ComposerOverlay + ComposerViewModel + Shared types |
| `hud-visual` | TopBarControl + SparklineGraph token refresh |

Team-lead (me) owned the shell rewrite, scroll/resize fix, dead-code
audit + deletion, composer event wiring, HUD measurement-based sizing,
ComposerOverlay `Resources[]` bug fix, and final build/test.

## Do not regress

- Do not re-introduce a left sidebar on the control window. Brand +
  inline tabs at the top is the committed direction.
- Do not wrap in-game HUD metric groups in nested tile borders.
- Do not modulate `RootBorder.Opacity` for the background opacity
  slider. Use brush alpha only.
- Do not hardcode pixel widths or heights anywhere in the HUD sizing
  path. Everything derives from measurement.
- Do not declare `XamlControlsResources` in App.xaml. `LoadControlsResources()`
  stays in code-behind.
- Do not use `this.Resources[key]` on a UserControl for theme lookups
  — it throws `KeyNotFoundException` for missing keys in WinUI 3. Go
  to `Application.Current.Resources` directly.
- Do not show the raw diagnostics dump by default. It lives behind
  the Advanced Diagnostics expander, collapsed on load.

## Open handoff items

- Support "Tip jar" button points at
  `https://handheldally.com/support-development` as a **placeholder URL**.
  Swap in the real donation target when available.
- `ComposerOverlay.LayoutChanged` forwards only `TopBar` and
  `BottomBar` through to `PositionChanged` today, because `OverlayEdge`
  only models those two. Dock / side-panel layouts are parked until
  `OverlayEdge` gains more cases (telemetry-layer scope, not UI).
- `HhapRailNavButtonStyle` exists but is currently unused after the
  sidebar was removed. Kept for future re-use if a toggle-style
  secondary nav is needed.

## Architecture snapshot (as of this commit)

```
HHAPulse.Overlay
├── App.xaml + .xaml.cs           (bootstrap + collector wiring)
├── ControlWindow.xaml + .xaml.cs (top-bar shell, scroll root)
├── MainWindow.xaml + .xaml.cs    (transparent overlay window)
├── Themes/
│   ├── HhapTheme.xaml            (design tokens)
│   └── HhapStyles.xaml           (control styles)
├── Views/
│   ├── HubView.xaml + .xaml.cs
│   ├── SupportView.xaml + .xaml.cs
│   ├── SettingsSheet.xaml + .xaml.cs
│   ├── ComposerOverlay.xaml + .xaml.cs
│   └── ControlShellTextBuilder.cs
├── ViewModels/
│   ├── OverlayViewModel.cs       (telemetry binding surface)
│   ├── ComposerViewModel.cs      (composer state)
│   └── ViewModelBase.cs
├── Controls/
│   ├── TopBarControl.xaml + .xaml.cs   (in-game HUD strip)
│   └── SparklineGraph.xaml + .xaml.cs
├── Aggregation/                  (RingBuffer only — rest deleted)
├── Collectors/                   (off-limits for UI work)
├── Diagnostics/                  (AppLogger, MetricStatusFactory, etc.)
├── Helpers/                      (MetricFormatter + MetricFormatterCompact)
├── Interop/                      (TransparentWindowHelper, NativeMethods)
├── Services/                     (AppHost)
├── Settings/                     (AppSettings, OverlayPresetCatalog, SettingsService)
└── Ipc/PipeServer.cs             (widget pipe host)

HHAPulse.Shared
├── ComposerLayout.cs             (new)
├── ModuleDetailPreset.cs         (new)
├── Models/                       (TelemetrySnapshot, CaptureTarget, ...)
├── Pipe/PipeClient.cs
└── Protocol/                     (IpcMessageEnvelope, PipeConstants, ...)
```
