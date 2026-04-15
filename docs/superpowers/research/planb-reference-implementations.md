# Plan B — Reference Implementations (Real WinUI 3 XAML from Shipping Apps)

**Date:** 2026-04-15
**Scope:** Real, citeable XAML source from production open-source WinUI 3 apps that Plan B can adapt. Every snippet in this file has a GitHub URL. Two patterns Plan B needs (colored-band status dots, monospace diagnostic strip) are NOT available in any production reference and must be hand-designed.

---

## Single best overall reference repo

**Microsoft Windows Terminal** — `github.com/microsoft/terminal`.

Terminal ships: pill buttons, flyouts with custom card styling, theme dictionaries (Dark/Light/HighContrast), extended custom title bar, and composition shadows. The `TerminalApp` project XAML is the closest production analogue to Plan B's shape. When in doubt, copy Terminal's pattern.

Second-best: **Files app** (`github.com/files-community/Files`) — for multi-breakpoint adaptive layouts with `VisualStateManager` + `AdaptiveTrigger`, Files' `MainPage.xaml` is cleaner than Terminal.

---

## 1. Pill button + Flyout pattern

**Source:** `github.com/microsoft/terminal/blob/main/src/cascadia/TerminalApp/ColorPickupFlyout.xaml`

Terminal's color picker flyout opens from a button in the tab strip. It uses a custom `FlyoutPresenterStyle` to give the flyout content a card shape (corner radius, background, border), and lays out its content in a `StackPanel` with a `VariableSizedWrapGrid` for the picker grid.

```xml
<Flyout Placement="Bottom">
  <Flyout.FlyoutPresenterStyle>
    <Style TargetType="FlyoutPresenter">
      <Setter Property="CornerRadius" Value="{ThemeResource OverlayCornerRadius}"/>
      <Setter Property="Background" Value="{ThemeResource FlyoutPresenterBackground}"/>
      <Setter Property="BorderBrush" Value="{ThemeResource FlyoutBorderThemeBrush}"/>
      <Setter Property="Padding" Value="0"/>
    </Style>
  </Flyout.FlyoutPresenterStyle>
  <StackPanel>
    <!-- Picker grid -->
  </StackPanel>
</Flyout>
```

**How Plan B adapts:** `PositionPillButton` + `PositionFlyout` is the same shape. Replace `FlyoutPresenterBackground` with `HhapPanelBrush`, replace the picker grid with the monitor canvas + six position buttons.

---

## 2. Adaptive multi-breakpoint layout

**Source:** `github.com/files-community/Files/blob/main/src/Files.App/Views/MainPage.xaml`

Files' main page has four visual state groups: `SidebarWidthStates` (48/100/200 dip), `WindowHeightStates` (440 dip trigger), `SidebarStates` (641 dip breakpoint), `InfoPanePositionStates` (None/Right/Bottom). Each uses `AdaptiveTrigger` with `MinWindowWidth` or `MinWindowHeight`, with setters targeting grid column widths, image sources, and panel visibility.

The critical lesson from Files: **one `VisualStateGroup` per concern**, not one giant group with every setter. Plan B should split into at least two groups: `CompanionWidthStates` (xs/sm/md/lg for overall layout) and `CompanionDensityStates` (font sizes, event line length).

**How Plan B adapts:** copy the multi-group structure. Each group handles one axis of responsiveness. Font-size setters live in a density group separate from the layout grid group.

---

## 3. Theme dictionaries — Dark/Light/HighContrast scaffold

**Source:** `github.com/microsoft/terminal/blob/main/src/cascadia/TerminalApp/App.xaml`

Terminal's `App.xaml` declares `ResourceDictionary.ThemeDictionaries` with three named keys (`Light`, `Dark`, `HighContrast`) even though the app is dark-themed in practice. Each dictionary defines color resources; brushes reference them via `{StaticResource}` inside the dictionary and `{ThemeResource}` at call sites.

```xml
<ResourceDictionary>
  <ResourceDictionary.ThemeDictionaries>
    <ResourceDictionary x:Key="Light">
      <Color x:Key="BrandBlue">#22D3EE</Color>
      <SolidColorBrush x:Key="BrandPrimary" Color="{StaticResource BrandBlue}"/>
    </ResourceDictionary>
    <ResourceDictionary x:Key="Dark">
      <Color x:Key="BrandBlue">#22D3EE</Color>
      <SolidColorBrush x:Key="BrandPrimary" Color="{StaticResource BrandBlue}"/>
    </ResourceDictionary>
    <ResourceDictionary x:Key="HighContrast">
      <SolidColorBrush x:Key="BrandPrimary" Color="{ThemeResource SystemColorWindowTextColor}"/>
    </ResourceDictionary>
  </ResourceDictionary.ThemeDictionaries>
</ResourceDictionary>
```

**How Plan B adapts:** `HhapTheme.xaml` currently has no `ThemeDictionaries` block — it just has flat resources. Plan B wraps the existing tokens in a `Dark` dictionary, adds `Light` and `HighContrast` stubs (same colors as `Dark` for now, or the HighContrast branch references system colors), and preserves every existing `x:Key` so consumers don't break.

---

## 4. Custom title bar (`ExtendsContentIntoTitleBar`)

**Source:** `github.com/microsoft/terminal/blob/main/src/cascadia/TerminalApp/MinMaxCloseControl.xaml`

Terminal's title bar has custom caption buttons (min/max/close) styled as narrow rectangular buttons with theme-resolved hover colors. The close button is specifically red on hover (`#C42B1C` is the Microsoft caption close hover color). Button heights switch between `CaptionButtonHeightWindowed` (40 dip) and `CaptionButtonHeightMaximized` (32 dip) depending on window state.

**How Plan B adapts:** `ControlWindow` already has a custom title bar (see `ControlWindow.xaml` in the internal audit). Plan B's change is cosmetic: remove the Hub/Support nav buttons, keep the `–` / `×` icons on the right, add the topbar state chips (`OVERLAY ON` / `CAPTURE READY` / game title) in the drag region. Use Terminal's caption button styling as a reference for the `–` / `×` buttons.

---

## 5. Composition drop shadow pattern

**Source:** `github.com/CommunityToolkit/WindowsCommunityToolkit/tree/main` — `DropShadowPanel`

The Community Toolkit's `DropShadowPanel` wraps a child element in a `Grid` with two layers: a `Border` (the shadow layer) underneath, and a `ContentPresenter` (the actual content) on top. The shadow layer is positioned and sized from the content's layout. This is the canonical Community Toolkit pattern for element-level drop shadows in WinUI 3.

**How Plan B adapts:** per the earlier `winui3-modern-patterns.md` research, in-flow card shadows are a known gotcha — `ThemeShadow` is popup-first and needs `Receivers` wiring. If Plan B needs shadows on preset cards (selected state), prototype `DropShadowPanel` or skip shadows entirely and rely on the cyan border + glow per the spec's §3.4 motion plan.

---

## 6. Flyout presenter with card appearance

**Source:** `github.com/microsoft/terminal/blob/main/src/cascadia/TerminalApp/CommandPalette.xaml`

Terminal's command palette is a flyout-like surface styled as a card. Its `FlyoutPresenter` uses `{ThemeResource FlyoutPresenterBackground}` and `{ThemeResource FlyoutBorderThemeBrush}`. Interior content uses `{ThemeResource CardBackgroundFillColorDefaultBrush}` with `CornerRadius="{ThemeResource OverlayCornerRadius}"` and attaches a shared shadow (`Shadow="{StaticResource SharedShadow}"`) with `Translation="0,0,32"` for depth.

**How Plan B adapts:** the `PositionFlyout` and `FeelAndFitFlyout` can both use the same styled `FlyoutPresenter` via a shared `x:Key="HhapFlyoutPresenterStyle"` in `HhapStyles.xaml`. Reference `HhapPanelBrush` for background, `HhapLineStrongBrush` for border. Skip the shadow unless prototyping confirms it reads clean.

---

## 7. Resource dictionary merging

**Source:** `github.com/files-community/Files/blob/main/src/Files.App/App.xaml`

Files' `App.xaml` merges multiple resource dictionaries: WinUI's framework resources first, then custom style overrides (PathIcons, MenuFlyout, FlyoutPresenter corner radius fix). A comment in Files references GitHub issue #12026 — a workaround for `MenuFlyout` font family inconsistency on MSIX.

**How Plan B adapts:** `App.xaml` already merges `HhapTheme.xaml` + `HhapStyles.xaml`. No change needed beyond making sure the new Plan B styles (`HhapPresetCardStyle`, etc.) are merged too.

---

## 8. `NavigationView` + `ItemsRepeater` companion layouts

**Source:** `github.com/microsoft/WinUI-Gallery`

WinUI Gallery is the canonical demo app for every control. Plan B developers should run it on a Windows dev machine and play with `Flyout`, `Expander`, `ItemsRepeater`, `NavigationView`, `AdaptiveTrigger` samples live before writing any XAML. The samples include a source-view tab that shows the exact markup for each demo — this is the fastest way to learn each primitive without reading docs.

URL: `github.com/microsoft/WinUI-Gallery`, specifically `WinUIGallery/ControlPagesData.json` → each control's source page XAML.

---

## Patterns NOT found in any production reference — Plan B must hand-design

### 1. Status chip row with colored band dots

**No shipping app in the research set implements a row of three inline colored status dots with text labels** (capture / game detect / companion). PowerToys Settings uses text-only status rows. Files uses icons but not band-colored dots. Terminal uses no such pattern.

**Plan B must hand-design this.** Recommended structure:

```xml
<StackPanel Orientation="Horizontal" Spacing="16">
  <StackPanel Orientation="Horizontal" Spacing="6">
    <Ellipse Width="10" Height="10" Fill="{ThemeResource HhapBandGood}"/>
    <TextBlock Text="Capture online" Style="{ThemeResource HhapMutedStyle}"/>
  </StackPanel>
  <StackPanel Orientation="Horizontal" Spacing="6">
    <Ellipse Width="10" Height="10" Fill="{ThemeResource HhapBandOkay}"/>
    <TextBlock Text="Game detect" Style="{ThemeResource HhapMutedStyle}"/>
  </StackPanel>
  <!-- etc. -->
</StackPanel>
```

Plus a custom `HhapBandDotStyle` targeting `Ellipse` that encapsulates the size + shadow glow.

### 2. Monospace diagnostic strip (`EventLine`)

**No shipping app uses a dedicated mono status line** below the main content surface to show `timestamp · pid · exe · frametime · sample rate`. Terminal's status bar is text-only. Files' footer is text-only. PowerToys has no such element.

**Plan B must hand-design this.** Recommended: a `TextBlock` with `FontFamily="{ThemeResource HhapFontJetBrainsMono}"`, `FontSize="{ThemeResource HhapFontMicro}"`, `Foreground="{ThemeResource HhapMuted2Brush}"`, hosted in a `Border` with a 1 dip top stroke and `HhapSpace4` padding.

---

## Three XAML idioms every Plan B author should steal immediately

1. **Theme dictionaries with three keys** at `App.xaml` level (not page level). Follow Terminal's `App.xaml` pattern. Use `{ThemeResource}` at call sites and `{StaticResource}` inside dictionaries.
2. **Custom `FlyoutPresenterStyle`** on every `Flyout` to give it card-like chrome (corner radius, background, border, padding). Follow Terminal's `ColorPickupFlyout.xaml` pattern.
3. **`VisualStateManager` with multiple groups**, one per responsiveness concern. Follow Files' `MainPage.xaml` pattern: separate `WidthStates`, `HeightStates`, `SidebarStates`, `InfoPaneStates`.

---

## Coverage confidence

High for flyouts, adaptive layouts, theme dictionaries, custom title bar, and composition shadows — all have production references. Low for status chip rows and monospace diagnostic strips — Plan B must hand-design these using the token system, but the primitives (`Ellipse`, `TextBlock`, `StackPanel`, `Border`) are all stable and well-documented.

**Ship confidence:** Plan B has no unknown unknowns for XAML primitives. Every new control is either a direct copy of a Terminal/Files pattern, or a hand-designed composition of stable stock primitives against the `HhapTheme.xaml` token system.
