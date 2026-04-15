# Plan B — WinUI 3 Platform Patterns (April 2026)

**Date:** 2026-04-15
**Scope:** Microsoft Learn + Windows App SDK 1.6 (April 2026) ground truth for every primitive Plan B needs — `Flyout`, `VisualStateManager` + `AdaptiveTrigger`, Mica/Acrylic, custom font loading, theme dictionaries, `ConnectedAnimation`, `ItemsRepeater`, touch + gamepad focus. Supplements (does not repeat) `winui3-modern-patterns.md`.

---

## 1. `Flyout` — the pill-button + flyout canonical pattern

**Built-in Button.Flyout pattern:**

```xml
<Button Content="Position: Bottom · 1 line ▾">
  <Button.Flyout>
    <Flyout Placement="Bottom">
      <StackPanel Padding="12">
        <!-- PositionFlyout content -->
      </StackPanel>
    </Flyout>
  </Button.Flyout>
</Button>
```

**FlyoutBase.AttachedFlyout pattern** (for custom pill templates that don't natively expose `Flyout`):

```xml
<local:PositionPillButton FlyoutBase.AttachedFlyout="{StaticResource PositionFlyout}"
                          Tapped="OnPillTapped"/>
```

Then in code-behind:

```csharp
private void OnPillTapped(object sender, TappedRoutedEventArgs e)
{
    FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
}
```

### Key facts (April 2026)

- **`FlyoutPlacementMode` values:** `Top`, `Bottom`, `Left`, `Right`, `Full`, `TopEdgeAlignedLeft`, `TopEdgeAlignedRight`, `BottomEdgeAlignedLeft`, `BottomEdgeAlignedRight`, `LeftEdgeAlignedTop`, `LeftEdgeAlignedBottom`, `RightEdgeAlignedTop`, `RightEdgeAlignedBottom`, `Auto`.
- **Dismiss is automatic:** click outside, Esc key, back button, gamepad B button. No manual plumbing needed.
- **`XamlRoot` resolution is automatic in WinUI 3** Window-hosted scenarios. The flyout picks up the parent element's `XamlRoot` without explicit assignment.
- **Theme inheritance is NOT automatic** — the flyout content sits in its own XAML subtree. If the parent page has `RequestedTheme="Dark"`, the flyout may resolve `{ThemeResource}` against the app-level theme, not the page. **Fix:** set `RequestedTheme` on the flyout content explicitly, OR define theme resources at app level instead of page level. (This is Gotcha #3 below.)
- **Focus restoration** when the flyout closes: focus returns to the element that triggered the flyout, automatically.
- **`LightDismissOverlayMode`** can be `On` (Xbox default — dimmed scrim) or `Off` (desktop default). Plan B wants `Off` for the Position and Feel-&-fit flyouts — they should not scrim the companion window.
- **`ShouldConstrainToRootBounds`** defaults to `true` — flyout content stays inside the XAML root. Leave as default for Plan B.

**Custom `FlyoutPresenterStyle`:** to give the flyout a card-like appearance matching the spec's token system:

```xml
<Flyout.FlyoutPresenterStyle>
  <Style TargetType="FlyoutPresenter">
    <Setter Property="Background" Value="{ThemeResource HhapPanelBrush}"/>
    <Setter Property="BorderBrush" Value="{ThemeResource HhapLineStrongBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="{ThemeResource OverlayCornerRadius}"/>
    <Setter Property="Padding" Value="16"/>
  </Style>
</Flyout.FlyoutPresenterStyle>
```

**Doc source:** `learn.microsoft.com/en-us/windows/apps/develop/ui/controls/dialogs-and-flyouts/flyouts`, `learn.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.flyout`, `learn.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.primitives.flyoutbase`.

---

## 2. `VisualStateManager` + `AdaptiveTrigger` — exact skeleton for Plan B

```xml
<Grid x:Name="RootGrid">
  <VisualStateManager.VisualStateGroups>
    <VisualStateGroup x:Name="CompanionAdaptive">
      <VisualState x:Name="Lg">
        <VisualState.StateTriggers>
          <AdaptiveTrigger MinWindowWidth="1600"/>
        </VisualState.StateTriggers>
        <VisualState.Setters>
          <!-- lg setters -->
        </VisualState.Setters>
      </VisualState>
      <VisualState x:Name="Md">
        <VisualState.StateTriggers>
          <AdaptiveTrigger MinWindowWidth="1280"/>
        </VisualState.StateTriggers>
      </VisualState>
      <VisualState x:Name="Sm">
        <VisualState.StateTriggers>
          <AdaptiveTrigger MinWindowWidth="1024"/>
        </VisualState.StateTriggers>
      </VisualState>
      <VisualState x:Name="Xs">
        <VisualState.StateTriggers>
          <AdaptiveTrigger MinWindowWidth="0"/>
        </VisualState.StateTriggers>
        <VisualState.Setters>
          <Setter Target="PresetRow.ItemsPanel" Value="{StaticResource PresetGrid2x2}"/>
          <Setter Target="SupportActionsGrid.(Grid.Columns)" Value="2"/>
          <Setter Target="EventLine.Text" Value="{x:Bind ViewModel.EventLineShort, Mode=OneWay}"/>
        </VisualState.Setters>
      </VisualState>
    </VisualStateGroup>
  </VisualStateManager.VisualStateGroups>
  <!-- Content -->
</Grid>
```

### Key facts (April 2026)

- **Evaluation order: top-down (first-match-wins).** List states from **largest** `MinWindowWidth` to **smallest**. If the window is 1500 dip wide and both `MinWindowWidth="1280"` and `MinWindowWidth="1600"` are declared with `1280` listed first, the `1280` state wins because it's evaluated first. **Plan B's Round-1 VSM example had lg→md→sm→xs ordering — that is correct. Preserve it.**
- **States are mutually exclusive** within a `VisualStateGroup` — only one active at a time.
- **`Setter.Target` supports complex property paths:** `Target="PresetRow.ItemsPanel"`, `Target="HeroPickerGrid.(Grid.ColumnDefinitions)[1].Width"`, etc. This is documented and fully supported.
- **`Setter.Value` can reference `{StaticResource}`** at runtime: `Value="{StaticResource HhapSpace5}"`.
- **`MinWindowWidth` is in effective pixels** (not physical), which matches the handheld-device-matrix research.

**Doc source:** `learn.microsoft.com/en-us/uwp/api/windows.ui.xaml.adaptivetrigger`, `learn.microsoft.com/en-us/uwp/api/windows.ui.xaml.visualstate`.

---

## 3. Mica / Acrylic / backdrop — no April 2026 delta

**Still window-level only.** Per-element frosted glass remains impossible without `BackdropBrush` + composition interop. No changes since the earlier `winui3-modern-patterns.md` report.

**Recommended for Plan B:**

- Mica on the companion `Window.SystemBackdrop` for the whole companion window:
  ```csharp
  this.SystemBackdrop = new MicaBackdrop() { Kind = MicaKind.Base };
  ```
- **No stacked frosted glass cards.** Use layered opaque colors for cards, matching the Fluent approach: `LayerFillColorDefaultBrush`, `CardStrokeColorDefaultBrush`, `CornerRadius 8`.
- The Founder-tier "glass shell" mentioned in `brand-handheldally.md` is a Round-1 §C "future" mockup direction — not in Plan B scope.

**Doc source:** `learn.microsoft.com/en-us/windows/apps/develop/platform/mica`.

---

## 4. Custom font loading — exact URI syntax

```xml
<TextBlock FontFamily="ms-appx:///Assets/Fonts/Inter-SemiBold.ttf#Inter"/>
```

### Key facts (April 2026)

- **The `#` fragment is the font's internal family name**, read from the font file's `name` table, NOT the filename. A file named `Inter-SemiBold.ttf` is likely declared as `"Inter"` internally — but variable fonts and custom fonts may declare something different. Verify by opening the file in FontForge or running `fc-query` before trusting the URI.
- **MSIX-packaged apps resolve `ms-appx:///` URIs automatically** for files inside the package. No code-level registration needed.
- **Use static-weight files** (`Inter-Regular.ttf`, `Inter-Medium.ttf`, `Inter-SemiBold.ttf`, `Inter-Bold.ttf`) rather than a variable-font `Inter.ttf`. The variable-font path has intermittent fallback issues on older Windows builds. This guidance from the earlier `winui3-modern-patterns.md` remains current in April 2026.
- **Expose font families as XAML resources** in `HhapTheme.xaml` so styles reference by key:
  ```xml
  <FontFamily x:Key="HhapFontInter">ms-appx:///Assets/Fonts/Inter-Regular.ttf#Inter</FontFamily>
  <FontFamily x:Key="HhapFontInterSemiBold">ms-appx:///Assets/Fonts/Inter-SemiBold.ttf#Inter</FontFamily>
  ```

**Doc source:** `learn.microsoft.com/en-us/windows/apps/develop/ui/controls/text-block` and the FontFamily uri reference.

---

## 5. Theme dictionaries — ship all three from day one

```xml
<ResourceDictionary>
  <ResourceDictionary.ThemeDictionaries>
    <ResourceDictionary x:Key="Dark">
      <!-- Plan B ships only Dark content today -->
      <SolidColorBrush x:Key="HhapAccentBrush" Color="#FF22D3EE"/>
    </ResourceDictionary>
    <ResourceDictionary x:Key="Light">
      <!-- Stub — Plan B does NOT populate Light, but the key must exist -->
      <SolidColorBrush x:Key="HhapAccentBrush" Color="#FF22D3EE"/>
    </ResourceDictionary>
    <ResourceDictionary x:Key="HighContrast">
      <!-- HighContrast uses system colors -->
      <SolidColorBrush x:Key="HhapAccentBrush" Color="{ThemeResource SystemColorWindowTextColor}"/>
    </ResourceDictionary>
  </ResourceDictionary.ThemeDictionaries>
</ResourceDictionary>
```

### Key facts

- **All three dictionaries must exist** even if only `Dark` has real content. Skipping `Light` or `HighContrast` causes shared brushes to leak across subtrees with different `RequestedTheme`. **This is Gotcha #2 — fix it before it bites.**
- **Inside the dictionary:** use `{StaticResource}` for color → brush wiring.
- **At call sites** (pages, controls): use `{ThemeResource}` so the brush re-resolves on theme change.
- **No runtime theme-switching new features** in WinUI 3 1.6 April 2026. The pattern is stable.

**Doc source:** `learn.microsoft.com/en-us/windows/apps/develop/platform/xaml/xaml-theme-resources`.

---

## 6. `ConnectedAnimation` — still canonical

- Still the April 2026 recommended cross-surface motion primitive.
- Forward: `GravityConnectedAnimationConfiguration()` (default).
- Back: `DirectConnectedAnimationConfiguration()`.

**Plan B usage:** the preset card → HUD transition when the user picks a preset. Optional polish, not required for v1.

**Doc source:** `learn.microsoft.com/en-us/windows/apps/develop/motion/connected-animation`.

---

## 7. `ItemsRepeater` vs. `ListView` — canonical for preset row

**Use `ItemsRepeater` with `UniformGridLayout` for the four-card preset row.**

Microsoft's April 2026 guidance: *"ItemsRepeater does not provide a comprehensive end-user experience – it has no default UI and provides no policy around focus, selection, or user interaction. Instead, it's a building block. For custom collection experiences and adaptive layouts with full layout control, ItemsRepeater is the canonical choice."*

```xml
<muxc:ItemsRepeater x:Name="PresetRow"
                    ItemsSource="{x:Bind Presets, Mode=OneWay}"
                    ItemTemplate="{StaticResource PresetCardTemplate}">
  <muxc:ItemsRepeater.Layout>
    <muxc:UniformGridLayout MinItemWidth="220"
                            MinColumnSpacing="12"
                            MinRowSpacing="12"
                            ItemsJustification="SpaceAround"/>
  </muxc:ItemsRepeater.Layout>
</muxc:ItemsRepeater>
```

`UniformGridLayout` with `MinItemWidth="220"` naturally flows to 4 cards at `md`/`lg` and 2×2 at `xs`/`sm` without needing VSM setters — the layout engine handles it. Plan B can use VSM only for the font-size / event-line setters, not for the preset row grid.

**Doc source:** `learn.microsoft.com/en-us/windows/apps/develop/ui/controls/items-repeater`.

---

## 8. Touch target + gamepad focus — unchanged, 44 dip

- **Minimum touch target: 44 × 44 dip.** Fluent accessibility standard, unchanged in April 2026. Matches `HhapTouchTargetMin` token in the spec.
- **Gamepad navigation:** `XYFocusKeyboardNavigation="Enabled"` on `CompanionView` root. Tab order follows reading order: topbar chips → toolbar pills → preset cards → Manual link → event line → support footer.
- **Focus ring customization:** override `ControlFocusVisualPrimaryBrush` and `ControlFocusVisualSecondaryBrush` in theme dictionary to `HhapAccentBrush` + `HhapBgBrush` so the focused element has a cyan ring visible on dark-slate surfaces.

**Doc source:** `learn.microsoft.com/en-us/windows/apps/design/input/gamepad-and-remote-interactions`, `learn.microsoft.com/en-us/windows/apps/design/accessibility/accessible-text-requirements`.

---

## 9. Windows App SDK 1.6 April 2026 delta

**Latest stable:** 1.6.9 (current in April 2026).

**New features in 1.6 relevant to Plan B:** none material.

- TabView tear-out enhancement — not used
- PipsPager wrapping — not used
- Native AOT — infrastructure, no XAML impact
- Package Deployment API enhancements — not used
- Decoupled WebView2 — not used

**No new `Flyout`, `VisualState`, `AdaptiveTrigger`, or theme resource primitives.** The XAML layer is stable. This is good — Plan B can ship against a predictable platform.

**Doc source:** `learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-1-6`.

---

## The three biggest gotchas for Plan B

### Gotcha 1 — `AdaptiveTrigger` top-down, first-match-wins evaluation

List states from **largest** `MinWindowWidth` to **smallest**. If you list `sm` (1024) before `md` (1280), then at 1500 dip the `sm` state wins and `md` is ignored. The Round-1 spec's example was correct (lg → md → sm → xs); preserve the order when Plan B builds the real XAML.

### Gotcha 2 — Theme dictionary pollution

If Plan B ships only `Dark` and uses `{ThemeResource}` inside theme brushes (rather than just at call sites), later adding `Light` or `HighContrast` will corrupt shared brushes across subtrees with different `RequestedTheme`. **Define all three dictionaries from day one,** even if `Light` and `HighContrast` are stubs that point at the same color values as `Dark`. Cost: 6 extra lines of XAML. Benefit: prevents week-long debugging sessions later.

### Gotcha 3 — Flyout content theme inheritance

`Flyout` content sits in its own XAML subtree. If the parent page has `RequestedTheme="Dark"` set, the flyout content resolves `{ThemeResource}` against the **app-level** theme, not the page. This manifests as flyouts that look wrong (light theme colors inside a dark page).

**Fix:** either (a) set `RequestedTheme="Dark"` explicitly on the flyout's root `StackPanel`, or (b) define all theme resources at app level (`App.xaml`) instead of page level. Plan B should pick (b) for consistency — `HhapTheme.xaml` is already merged at app level, so this comes for free as long as Plan B doesn't scatter theme resources into individual page-level dictionaries.

---

## Plan B confidence statement

The WinUI 3 XAML platform Plan B depends on is stable and current in April 2026. Every primitive Plan B needs is documented, works in MSIX-packaged apps, and has an exemplar repository to copy from. No breaking changes between the earlier research and today. Ship the eight-file redesign with confidence — the only real risks are the three gotchas above, all of which are avoidable with awareness.
