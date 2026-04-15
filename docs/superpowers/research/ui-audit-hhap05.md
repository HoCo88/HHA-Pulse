# HHAP-0.5 Overlay UI Audit

Branch: `HHAP-0.5`
Audit date: 2026-04-15
Scope: `src/HHAPulse.Overlay/` XAML surfaces + theme tokens + structural orphans.

## 1. File inventory

| Surface | File path | Purpose |
|---|---|---|
| App bootstrap | `src/HHAPulse.Overlay/App.xaml` | Merges `HhapTheme.xaml` + `HhapStyles.xaml` at app scope. |
| In-game HUD window | `src/HHAPulse.Overlay/MainWindow.xaml` | Transparent window hosting a single `TopBarControl`. This is the actual game overlay. |
| Companion shell | `src/HHAPulse.Overlay/ControlWindow.xaml` | Windowed "Handheld Ally Pulse" app: custom title bar + Hub/Support nav buttons + Overlay-settings button + ContentHost + `SettingsSheet` overlay + `ComposerOverlay`. |
| Hub page | `src/HHAPulse.Overlay/Views/HubView.xaml` | Hero card, Live preview card, **4 Preset buttons** (Minimal/Standard/Performance/Custom), Visibility + Position utility cards, collapsed "Live now" mini-strip. |
| Support page | `src/HHAPulse.Overlay/Views/SupportView.xaml` | Capture/GameDetect/Companion status cards, actions row (report/log/web/community), tip jar card, collapsed Advanced diagnostics expander, Close overlay button. |
| Build-your-own wizard | `src/HHAPulse.Overlay/Views/ComposerOverlay.xaml` | Right-side 480 px drawer: step dots (Layout / Modules / Detail / Reorder), 6 layout choices incl. **Side panel**, modules repeater, detail-per-metric list, reorder ListView, graph-suggestion card. |
| Overlay Settings flyout | `src/HHAPulse.Overlay/Views/SettingsSheet.xaml` | Right-edge 420 px sheet: bg/text opacity sliders, HUD text size, Top/Bottom chips, close. |
| HUD bar control | `src/HHAPulse.Overlay/Controls/TopBarControl.xaml` | The slim HUD bar — rounded `Border` hosting a vertical `StackPanel` whose children are programmatically built. |
| Sparkline | `src/HHAPulse.Overlay/Controls/SparklineGraph.xaml` | Canvas-hosted inline graph for HUD. |
| Tokens | `src/HHAPulse.Overlay/Themes/HhapTheme.xaml` | Colors, brushes, gradients, radii, spacing scale, font sizes/tracking. |
| Control styles | `src/HHAPulse.Overlay/Themes/HhapStyles.xaml` | Card/Chip/Tile, typography, button variants (Primary/Ghost/Chip), rail-nav toggle. |

## 2. Token system assessment

**Tokens exist and are centralized.** `HhapTheme.xaml` defines a full dark-only token set: `HhapBg/Panel/Panel2/Panel3`, `HhapLine/LineStrong`, `HhapText/Muted/Muted2`, accent brushes (`Blue/Blue2/Amber/Amber2/Green/Red/Purple`), `HhapScrim`, `HhapHudBg/HudTileBg/HudTileBorder`, 4 gradients, 5 corner radii (`HhapRadius=26`, `Shell=32`, `Sm=18`, `Tile=14`, `Pill=999`), a spacing scale (`HhapSpace1..7`), `HhapFontFamily=Segoe UI`, and 10 font sizes + 5 character-spacing tokens.

**Styles** in `HhapStyles.xaml` consume these tokens and expose `HhapCardStyle`, `HhapStatCardStyle`, `HhapTileStyle`, five chip variants, Hero/H1/H2/H3/Body/Muted/Eyebrow/Kicker/HudValue/HudLabel typography, and Primary/Ghost/Chip button styles. No custom Slider/ToggleSwitch templates yet — the file's header comment flags this as deferred.

**Gaps:** no light theme / `ThemeDictionaries` (dark is hardcoded). No elevation/shadow tokens. No motion tokens. Button/slider hover and pressed visual states rely on default WinUI templates, so token-branded states are not guaranteed. Hex alpha values are inlined in several XAML files (HubView ellipses, ComposerOverlay) rather than referencing brush tokens.

## 3. CLAUDE.md rule violations

> Rule: "One in-game HUD only: no side panel, no nested boxes, no diagnostic overlay, no custom metric picker, no preset label."
> Rule: "HUD metrics: FPS, 1% low, frametime, CPU, GPU, RAM, VRAM, battery, display Hz."
> Rule: "Temperature, fan, power, latency, and frame generation stay hidden until a real data source exists."

| Violation | File | Evidence | Recommendation |
|---|---|---|---|
| **Preset labels** ("Minimal", "Standard", "Performance", "Custom" + "RECOMMENDED"/"ADVANCED"/"CLEAN" eyebrows) | `Views/HubView.xaml:181-275` | `PresetMinimalButton`, `PresetStandardButton`, `PresetTunerButton`, `PresetCustomButton` | **Remove entire `PresetRow` ScrollViewer.** Also delete `Settings/OverlayPresetCatalog.cs` and preset plumbing in `OverlayViewModel` / `SettingsService`. |
| **Custom metric picker (wizard)** | `Views/ComposerOverlay.xaml` (whole file) + `ComposerOverlay.xaml.cs` + `ViewModels/ComposerViewModel.cs` | Step 2 Modules repeater, Step 3 Detail list, Step 4 Reorder ListView | **Delete ComposerOverlay entirely.** Also delete "Customize" button in `HubView.xaml:88-90` and its host slot `ComposerOverlayControl` in `ControlWindow.xaml:101-104`. |
| **Side panel layout option** | `Views/ComposerOverlay.xaml:157-166` (`LayoutSidePanelButton`) | Explicitly named "Side panel — Wide panel with room for graphs" | Dies with ComposerOverlay deletion. |
| **Dual-line / Left dock / Right dock** — not CLAUDE-forbidden but also not "one slim HUD bar" | `Views/ComposerOverlay.xaml:124-156` | `LayoutDualLineButton`, `LayoutLeftDockButton`, `LayoutRightDockButton` | Dies with ComposerOverlay deletion. HUD is top OR bottom bar only. |
| **Nested boxes / diagnostic overlay** | `Views/SupportView.xaml:104-118` | `<Expander>` "Advanced diagnostics" with nested ScrollViewer, dump TextBlock, traceability text, contract status — a diagnostic overlay embedded in the companion. | **Remove the Advanced diagnostics Expander.** Keep a "Copy support report" button (one action, no visible dump). |
| **Nested card-in-card** | `Views/HubView.xaml:96-163` | PreviewCard `Border` contains another `Border` with gradient fill, which contains two decorative `Ellipse`s plus `HudPreviewHost` ContentControl | Visual nesting contradicts "no nested boxes". Either flatten to a single card or move the preview into the HUD window itself. |
| **Custom metric picker entry point** | `Views/HubView.xaml:88-90` ("Customize" button) and `HubView.xaml:253-275` ("Build your own" preset card) | Both launch ComposerOverlay | Remove both. |
| **Widget chip** ("Widget optional") on hero | `Views/HubView.xaml:75-81` | Advertises a companion widget that is out of scope for the in-game HUD rule and bloats hero | Review — likely remove. |
| **"Live now" mini-strip duplicates HUD** | `Views/HubView.xaml:321-375` | `LiveStripCard` shows FPS/CPU/GPU/Battery tiles inside the companion window, duplicating what the HUD already shows | Currently `Visibility="Collapsed"` — delete dead markup. |

No temperature/fan/power/latency/framegen XAML was found in the surfaces above, so that rule is not currently violated in the UI layer. `Diagnostics/` folder contents are non-UI (telemetry contract + validators) and stay.

## 4. Dead / orphaned / duplicate

- `HubView.xaml:321-375` **`LiveStripCard`** — entire `LiveTileFps/Cpu/Gpu/Battery` block is `Visibility="Collapsed"` at the card level and each child tile is independently collapsed. No code path in HubView.xaml appears to flip it on at a glance — candidate for deletion regardless of the redesign.
- `HubView.xaml:17-19` **`HeroStackRow`** — second grid row with `Height="0"`, reserved for a responsive stack fallback. Either wire it up or drop it.
- `HubView.xaml:22-94` **decorative `Ellipse` + `RadialGradientBrush`** with inlined `#2958CBFF` colors — duplicates what a token could express; cosmetic debt, not a violation.
- `HhapStyles.xaml:206-218` **`HhapRailNavButtonStyle`** — targets `ToggleButton` for a side rail nav. `ControlWindow.xaml` uses inline `Button`s ("Hub"/"Support") with `HhapPrimaryButtonStyle`/`HhapGhostButtonStyle`, not this rail style. **Orphan** — delete or actually adopt it.
- `Settings/OverlayPresetCatalog.cs` — exists only to back the four preset buttons. Delete alongside the preset row.
- `ViewModels/ComposerViewModel.cs` + `Views/ComposerOverlay.xaml[.cs]` + `ControlShellTextBuilder.cs` (if it only serves the composer/preset shell text) — delete with composer.
- `Helpers/MetricFormatterCompact.cs` vs `Helpers/MetricFormatter.cs` — two formatters; verify only one is needed by the HUD after the redesign.

## 5. What survives / what to delete

**Keep (survives redesign):**
- `App.xaml`, `Themes/HhapTheme.xaml`, `Themes/HhapStyles.xaml` — token system is healthy, reuse as-is. Minor cleanup: remove orphan `HhapRailNavButtonStyle`, consider adding elevation/motion tokens.
- `MainWindow.xaml` + `Controls/TopBarControl.xaml` + `Controls/SparklineGraph.xaml` — this IS the in-game HUD the product rule protects. No violations.
- `ControlWindow.xaml` shell chrome (title bar, ContentHost scaffold) — but strip the Settings sheet host + Composer host rows.
- `Views/SettingsSheet.xaml` — opacity/size/position sliders are the one customization channel CLAUDE.md tolerates. Keep; consider inlining instead of flyout.
- `Views/SupportView.xaml` — status cards + actions row are within spec. Strip the Advanced diagnostics expander.

**Delete (redesign removes entirely):**
- `Views/ComposerOverlay.xaml` + `.cs` + `ViewModels/ComposerViewModel.cs`
- `Settings/OverlayPresetCatalog.cs`
- `PresetRow`, `UtilityRow`, `LiveStripCard`, Hero preset label, "Customize" button, "Build your own" card — essentially HubView shrinks to a minimal "overlay on/off + open settings + open support" surface, or HubView is replaced by a 2-card landing.
- `ControlWindow.xaml` composer host region (rows 100-104) and the settings-sheet scrim host if settings moves inline.

**Redesign implication:** the companion window becomes a *status + settings* surface, not a product-tour/composer surface. The "wizard" concept disappears. The only interactive customizations remaining are: Overlay on/off, Top vs Bottom position, opacity x3 sliders, support actions. Everything else is automatic.
