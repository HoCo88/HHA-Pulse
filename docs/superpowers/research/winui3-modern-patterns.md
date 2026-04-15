# WinUI 3 Modern Styling & Fluent Patterns — Research Report

**Scope:** What the HHA Pulse companion window can actually do in WinUI 3 (Windows App SDK, .NET 8) when matching the handheldally.com dark-mode editorial brand.
**Sources:** Microsoft Learn docs (XAML styles, theme resources, Mica, controls index, shadows, XAML animation, connected animation); WinUI 3 Gallery; WinUI repo (microsoft-ui-xaml).
**Date:** 2026-04-15

---

## 1. Capabilities Matrix

| Want | Possible in WinUI 3? | How |
|---|---|---|
| Dark-mode background with subtle depth | Yes | Mica or Mica Alt backdrop on the root `Window` (via `SystemBackdrop`). Opaque, GPU-cheap, samples wallpaper once. Use Mica Alt for tabbed title-bar hierarchy. |
| Custom dark palette (non-Fluent grays/accents) | Yes | Override Lightweight-Styling brush resources (`ButtonBackground`, `ButtonForegroundPointerOver`, `TextFillColorPrimary`, etc.) in `App.xaml` `ThemeDictionaries` with `Light`/`Dark`/`HighContrast` keys. |
| Share design tokens across XAML files | Yes | `ResourceDictionary.MergedDictionaries` from `App.xaml`. Define `Color` + `SolidColorBrush` tokens once in a `Tokens.xaml`, `Typography.xaml`, `Spacing.xaml` set. Reference with `{ThemeResource}` at call sites, `{StaticResource}` inside theme dictionaries. |
| Light/Dark/HighContrast swap at runtime | Yes | `FrameworkElement.RequestedTheme` flips, `{ThemeResource}` markup re-evaluates. Must define all three dictionaries — skipping Light/Dark causes the "polluted dictionary" bug documented by MS. |
| Custom web fonts (Inter, Geist, JetBrains Mono) | Yes | Ship `.ttf`/`.otf` in `Assets/Fonts/`, set `Build Action = Content`. Reference with `FontFamily="ms-appx:///Assets/Fonts/Inter.ttf#Inter"` (path + `#FamilyName`). Works for any `TextBlock`, `TextBox`, or style `FontFamily` setter. |
| Type ramp matching brand | Yes | Override the built-in `CaptionTextBlockStyle` → `DisplayTextBlockStyle` family, or define new `Display/Title/Body/Caption` styles that `BasedOn` the defaults. Built-in ramp already has 9 steps (12–68 px). |
| Full-bleed hero image with gradient overlay | Yes | `Grid` with an `Image` stretched Uniform/UniformToFill, then a sibling `Rectangle` with a `LinearGradientBrush` (transparent → dark) on top. No clipping issues in WinUI 3. |
| Rounded cards with subtle borders | Yes | Standard pattern from Mica layering: container `Background="{ThemeResource LayerFillColorDefaultBrush}"`, `BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"`, `CornerRadius="8"`. This is literally the Fluent "card pattern." |
| Drop shadow / glow on cards | Partial | `ThemeShadow` works natively but is **designed for popups** — for in-flow cards you must add elements to `ThemeShadow.Receivers` (ancestors excluded). `DropShadow` (composition) is fully customizable. On Win11 SDK 22000+, `ThemeShadow` automatically behaves like a drop shadow. For tight brand control, use `AttachedDropShadow` from Community Toolkit. |
| Smooth color transitions on hover | Yes | Two options: (a) Visual State storyboards in a re-templated control using `ColorAnimation`/`ObjectAnimationUsingKeyFrames`, (b) modify the built-in `PointerOver` Lightweight brushes (`ButtonBackgroundPointerOver`) — the default control templates already have transition timing wired up. **No DirectComposition plumbing required.** |
| Page transitions between nav destinations | Yes | `NavigationThemeTransition` on `Frame`; `EntranceThemeTransition` / `ContentThemeTransition` on containers. All XAML-only. |
| Element-to-element morphing animation | Yes | `ConnectedAnimationService` — `PrepareToAnimate` on source page, `TryStart` on destination. `GravityConnectedAnimationConfiguration` (forward) / `DirectConnectedAnimationConfiguration` (back) are built in. Must call `SuppressNavigationTransitionInfo` to avoid conflict. |
| Animated state-change icons | Yes | `AnimatedIcon` control with Lottie-JSON source (via LottieGen codegen). |
| Implicit layout animations (auto-animate size/position changes) | Yes | `ItemContainerTransitions` on `ListView`/`ItemsRepeater` (`RepositionThemeTransition`, `AddDeleteThemeTransition`). For arbitrary elements, composition `ImplicitAnimations` via `ElementCompositionPreview` (one-time setup, no ongoing plumbing). |
| Custom title bar (brand logo + controls) | Yes | `Window.ExtendsContentIntoTitleBar = true` + `SetTitleBar(element)`. Mica extends seamlessly into the title bar area. |
| Blur/glass panels on demand | Limited | Mica/Acrylic are **window-level** `SystemBackdrop`s, not per-element. You cannot put a blurred glass panel over other app content without `Popup` tricks or composition `BackdropBrush`. Plan once per window, not per card. |

---

## 2. Recommended Control Set (brand-matched companion window)

**Shell / navigation**
- `NavigationView` (Left mode) — primary shell. Collapsible pane, custom header, supports Mica/Mica Alt in title bar area. Use `PaneDisplayMode="LeftCompact"` for icon-only rail.
- `BreadcrumbBar` — in-content path for deep nav (Settings → Profiles → [game name]).
- `SelectorBar` — segmented control replacement for Pivot (tab-like inline switcher, more compact).

**Content layout**
- `Grid` + `StackPanel` + `ItemsRepeater` for custom card grids (not `GridView`, which imposes its own template).
- `Expander` — collapsible sections in Settings or game profile detail.
- `ScrollViewer` with `ScrollView` (WinUI 3) for editorial long-form pages.

**Surfaces**
- Custom `Border`-based card (`LayerFillColorDefaultBrush` + `CardStrokeColorDefaultBrush`, `CornerRadius="8"`).
- `InfoBar` — status/error inline messages, already brand-adjacent.
- `TeachingTip` — onboarding/coach marks tied to a target element.
- `ContentDialog` — modal confirmations; re-template for brand.

**Input**
- `Button` (+ `ToggleSplitButton` for presets), `ToggleSwitch`, `Slider`, `NumberBox`, `ComboBox`, `AutoSuggestBox`.
- `ColorPicker` if we expose HUD color controls.

**Media**
- `Image` with `ImageBrush` for hero backgrounds; `PersonPicture` is irrelevant here.
- `AnimatedIcon` for the pulse/activity indicator in the taskbar corner widget.

**Do NOT reach for**
- `Pivot` (legacy, replaced by `SelectorBar` + `NavigationView` top mode).
- `TabView` unless we genuinely need document-style tabs (we don't — and the log notes a prior TabView crash).
- `GridView`/`ListView` default templates when we want bespoke cards — use `ItemsRepeater`.

---

## 3. Design Token Architecture (recommended)

```
src/HHAPulse.Overlay/Styles/
  Tokens.Colors.xaml      <- <Color> + <SolidColorBrush> pairs, themed
  Tokens.Typography.xaml  <- <FontFamily> + TextBlockStyle overrides
  Tokens.Spacing.xaml     <- <x:Double> for 4/8/12/16/24/32
  Tokens.Radius.xaml      <- <CornerRadius> 4/8/12
  Styles.Buttons.xaml     <- BasedOn DefaultButtonStyle, brand variants
  Styles.Cards.xaml       <- reusable card Style targeting Border
App.xaml:
  <ResourceDictionary.MergedDictionaries>
    <ResourceDictionary Source="Styles/Tokens.Colors.xaml"/>
    ...
```

Every `Tokens.Colors.xaml` must define `<ResourceDictionary.ThemeDictionaries>` with **Light + Dark + HighContrast** keys (skipping any of them causes cross-theme pollution — documented pitfall). Inside each theme dictionary use `{StaticResource}`; at call sites in pages use `{ThemeResource}`.

---

## 4. Three Gotchas the Redesign Must Plan Around

### Gotcha 1 — Theme dictionary pollution
Defining only `Default` + `HighContrast` (or only `Light` + `HighContrast`) while using `{ThemeResource}` inside the dictionary causes shared brush resources to "leak" across sub-trees with different `RequestedTheme`. **Rule:** always define all three (`Light`, `Dark`, `HighContrast`), and use `{StaticResource}` for color→brush wiring *inside* theme dictionaries. This is not a bug we can defer — it surfaces the moment we swap any subtree theme.

### Gotcha 2 — Blur/glass is window-level only
Mica and Acrylic are `SystemBackdrop`s applied to the whole window. There is **no cheap per-element glass panel** in WinUI 3 — attempts to fake one require composition `BackdropBrush` + element visual interop, which is exactly the DirectComposition plumbing we want to avoid. Design decision: **one Mica window, everything else is opaque layered colors.** Do not design comps with stacked frosted glass cards.

### Gotcha 3 — Shadows require receivers for in-flow elements
`ThemeShadow` was built for popups. To cast a shadow from an in-flow card onto the page behind it, we must explicitly populate `ThemeShadow.Receivers` (and the receivers cannot be ancestors in the visual tree). Easy to get wrong, easy to produce shadow-less cards. On Win11 SDK ≥22000 `ThemeShadow` auto-degrades to a drop shadow, which helps — but we should still prototype the card-shadow look early to confirm it lands before committing the whole design to it. Alternative: `DropShadow` (composition) or `AttachedDropShadow` from Community Toolkit, either of which gives fully custom blur radius/offset/color at the cost of manually picking values.

### Bonus gotcha — Font file + family name
Custom font `FontFamily="ms-appx:///Assets/Fonts/Inter.ttf#Inter"` requires the **internal family name** after `#` to match what the font file declares (not the filename). For variable fonts, each weight axis may need an explicit `FontWeight` setter — WinUI 3 handles variable fonts but some older versions of Windows 10 fall back to regular. Stick with static weight files (Inter-Regular.ttf, Inter-SemiBold.ttf, etc.) to avoid surprises.

---

## 5. Exemplar Reference Apps (to study visually, not to copy wholesale)

- **Files app** (files-community/Files on GitHub) — card-heavy modern WinUI 3 with Mica, custom title bar, NavigationView left rail, InfoBar, TeachingTip. Uses Community Toolkit extensively.
- **WinUI 3 Gallery** (microsoft/WinUI-Gallery) — the official reference for every control rendered in a realistic page layout. First stop for "what does this look like out of the box."
- **PowerToys Settings** — modern NavigationView + Settings cards pattern, all WinUI 3, open source.

---

## 6. Summary — What This Means for the Redesign

- We can hit a dark-mode editorial look with Mica + custom token dictionaries + Inter/Geist — no exotic tech required.
- The shell is `NavigationView` + custom title bar + `ItemsRepeater` card grids; everything else is stock Fluent.
- Hover color transitions, page transitions, connected-element animations, and implicit list animations are all XAML-only and need no composition plumbing.
- The *only* redesign constraint that changes the visual language: don't design frosted glass cards (window-level backdrops only), and plan card shadows against `ThemeShadow.Receivers` limits or budget for `DropShadow`/`AttachedDropShadow`.
