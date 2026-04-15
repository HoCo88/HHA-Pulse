# HHA Pulse — Companion UX Redesign

**Date:** 2026-04-15
**Branch context:** HHAP-0.5 (current), landing on a new `HHAP-0.6` feature branch
**Owners:** jan@freelax.cz (product), Claude (implementation partner)
**Mockup reference:** `pulse/mockups/index.html`
**Research inputs:**
- `docs/superpowers/research/brand-handheldally.md`
- `docs/superpowers/research/ui-audit-hhap05.md`
- `docs/superpowers/research/winui3-modern-patterns.md`
- `docs/superpowers/research/handheld-gamer-voices.md`
- `docs/superpowers/research/handheld-device-matrix.md`
- `memory-bank/systems/telemetry-trace-map.md`
- `memory-bank/best_practices/code_signing.md`

---

## 1. Why this exists

The HHAP-0.5 companion window has lost the plot. A user seeing it for the first time meets a Hub tab with a hero card, a Live Preview card, four preset cards, Visibility and Position utility cards, a collapsed Live-now strip, and a "Build your own" wizard drawer — plus a separate Support tab and a right-side Overlay Settings flyout. It reads as a dashboard of dashboards instead of a one-glance tool. The in-game HUD itself (the thing the product actually *is*) is clean, but the window you use to configure it is noisy and off-brand.

The goal of this redesign is to make the companion window feel like it came from the same studio as handheldally.com — warm tinkerer voice, dark-slate surfaces, one confident cyan accent, gold reserved for a future Founder tier — and to reduce it to a single surface the user can finish in under ten seconds: *"overlay is on, it's here, this is the set of metrics I want, these are the knobs."*

## 1b. Source of truth

Every non-trivial claim in this spec is backed by one of the following. If a recommendation below cannot be traced to one of these, it is marked `(assumed)` and needs to be verified before the plan is executed.

- `CLAUDE.md` — product rules. Some rules are being revised in this spec (§9); the revisions are explicit, and no claim here contradicts CLAUDE.md silently.
- `memory-bank/best_practices/winui3_overlay.md` — proven WinUI 3 patterns for transparent windows, chromeless overlays, measured sizing, threading, XAML leak avoidance, and deployment.
- `memory-bank/best_practices/top10_critical_pitfalls.md` — the hard product rules about what never ships without real data.
- `memory-bank/systems/ARCHITECTURE.md` — how the overlay, capture service, and widget interoperate.
- `docs/superpowers/research/brand-handheldally.md` — handheldally.com voice, palette, component patterns, and the "warm tinkerer" positioning.
- `docs/superpowers/research/ui-audit-hhap05.md` — the current HHAP-0.5 XAML inventory: what to keep, what to delete, which rules the codebase currently violates.
- `docs/superpowers/research/winui3-modern-patterns.md` — WinUI 3 2026 capability survey: fonts, tokens, VisualStateManager, AdaptiveTrigger, and the three gotchas (window-level blur, popup-first shadows, theme-dictionary pollution).
- `docs/superpowers/research/handheld-device-matrix.md` — 14-device responsive-sizing matrix with vendor/Wikipedia citations.
- `pulse/mockups/index.html` — the clickable Round 05 comp (md/sm/xs sandboxes at different container widths).
- User instructions in the current conversation, verbatim: *"do not VIBE or ASSUME"*, *"no fluff and marketing around it"*, *"we do not use hardcoded px"*, *"4 presset and then the manual with all we can trace"*.

## 2. Scope

**In scope**
- The companion window (`ControlWindow.xaml` and everything it hosts).
- Removing the Composer wizard and its entire viewmodel/code-behind stack.
- Removing the Overlay Settings flyout and inlining its controls onto the companion surface.
- Removing the Support tab as a destination; folding it into a footer section of the single surface.
- Adding a position picker with six layouts (Top 1L, Top 2L, Bottom 1L, Bottom 2L, Left dock, Right dock).
- Adding a preset system with four presets plus Manual mode.
- Extending `HhapTheme.xaml` with brand tokens pulled from handheldally.com (the current tokens are healthy but were picked without the brand in view).
- Updating `CLAUDE.md` to reflect the 2026-04-10 telemetry delta and the new product rules (see §9). `CLAUDE.md` edits ship in a companion change, not this spec file.
- `src/HHAPulse.Overlay/Collectors/Display/DisplayCollector.cs:23-30` refresh-rate bug fix (see §10 item #11 and Fix L in the execution plan). `dm.dmDisplayFrequency <= 1` must gate `MetricFlags.Display` and `RefreshRateHertz` so the HUD renders `--` per the missing-data rule.

**Out of scope / keep as-is**
- The in-game HUD bar itself. `MainWindow.xaml` + `Controls/TopBarControl.xaml` + `Controls/SparklineGraph.xaml` already render correctly and comply with the new rules. Styling updates are allowed only if they are token-level swaps (same XAML, different brush resolution).
- `Hotkeys/HotkeyService.cs` — launch-based toggle is intentional architecture (see §5.7). No Win32 `RegisterHotKey` work in this spec.
- `Services/ForegroundGameDetector.cs` + `Services/InGameVisibilityGate.cs` — complementary auto-show/hide already in place. Untouched.
- `Collectors/*` — the 2026-04-10 telemetry delta already did the collector work. Battery, CPU power, GPU telemetry and temps land where they can (see §2b.2 table and `memory-bank/systems/telemetry-trace-map.md`).
- `CaptureService/*` — separate elevated service, untouched. No IPC contract changes.
- Founder/Premium tier implementation. The glass+gold surface is designed on paper only; ship behind a feature flag if at all, in a later milestone.
- Any new telemetry MetricIds. The redesign only rearranges how existing metrics are displayed and picked; the battery `MetricId` stays composite (§5.6).

### 2.1 Microsoft Store eligibility

The final product must be Microsoft Store-submittable (MSIX). Every design decision in this spec is evaluated against the question *"would this block Store certification?"* — and no recommendation below proposes anything that would:

- No kernel driver, no DLL injection, no graphics API hooks, no game-memory reads.
- No writes under `%PROGRAMFILES%` at runtime (settings land in `%LOCALAPPDATA%\HHAPulse\`).
- No network listeners, no analytics, no telemetry leaving the device.
- No hardcoded `C:\` paths. Every path uses `Environment.GetFolderPath(…)` or `ms-appx:///`.
- The capture service is the only elevated component. Its distribution is currently outside the MSIX graph — packaging strategy is an open question (§10 item #10) and needs a resolution before launch.

Per `memory-bank/best_practices/code_signing.md:3`: *"Current operational build uses a normal desktop overlay plus an elevated local capture service. Microsoft Store-only guidance is historical and not the active blocker."* — the current architecture was not Store-designed, which means Store submission is a **new decision**, not a guarantee, and it is tracked as §10 item #10.

## 2b. What handheld gamers actually say — real voices, not UX laws

Earlier drafts of this section cited Fitts's Law, Hick's Law, Miller 7±2, and Jakob's Law as justification for design decisions. Those are training-data abstractions. The user explicitly pushed back: *"do not ASSUME or go by VIBES!!! trully think like a handheld gamer and learn about them before you think you know based on your dataset you been trained"*.

This section is rewritten from `docs/superpowers/research/handheld-gamer-voices.md` — 326 lines of direct quotes from Steam Community Bug Reports, GitHub issues on g-helper and HandheldCompanion, Steam Deck HQ, Windows Central, XDA, GamingOnLinux, PulseGeek, and community overlay projects ElegantMustard/TroyMetrics. Reddit direct-fetch was blocked by the Claude Code harness; every claim below cites an alternative primary source.

### 2b.1 Seven principles, each from a real voice

1. **The overlay must go away on demand.** This is the single most-recurring complaint across every surveyed source. Not complexity, not features — *getting rid of the overlay when you're done*.
   > *"This is now the 3rd time the performance overlay has bugged and stayed on my screen infinitely"* — Steam Deck General Discussions
   > *"it keeps coming up during gameplay of multiple games obscuring my view"* — Steam Deck Bug Reports
   > *"Every time this happens I stop playing it for a month and it magically goes away"* — same thread
   **Design implication:** hide/show reliability is a first-class feature, not an implementation detail. The toggle must be sub-100 ms, survive sleep/resume/game-launch, and have an always-working emergency kill path. See §5.7.

2. **Users don't leave the overlay on.** Across every source, the workflow is *pop it up for ~30 seconds to spot-check or tune, then hide it*. The earlier draft of this spec assumed an always-on HUD with peripheral-vision design; that is wrong for this audience.
   > *"Enable the overlay at Level 2 or 3 for frame time and CPU/GPU data… run the same scene for thirty seconds and observe the frame time graph"* — PulseGeek, *Use the Steam Deck Performance Overlay to Tune Bitrate*
   > *"turning off the Legion Overlay (Fps, Battery Time and so on) resolved performance issues for some users"* — Legion Go performance discussions
   **Design implication:** optimize for fast open → glance → close cycles, not for long sessions with the HUD visible. The companion window and the HUD bar are both "summoned" surfaces, not persistent ones.

3. **Valve's Level 2 preset is the mass-market sweet spot.** Valve shipped 5 granularity levels (L1 FPS-only, L2 standard, L3 tuning, L4 full, L5 dev) for the Steam Deck overlay, and users cluster hard at Level 1 and Level 2.
   > *"I personally found that Level 2 gave me all of the information I regularly wanted and didn't take over the screen too much"* — Rebecca Spear, Windows Central
   > *"For someone who only wants an FPS counter, level one should be enough. But advanced users will definitely use higher levels"* — MakeUseOf
   > *"Thank goodness… Glad to see Valve added this option"* — GamingOnLinux comment on Valve shipping an FPS-only mode in March 2022
   **Design implication:** the handheld community already has a mental model called "Levels". HHA Pulse's 4 curated presets should map onto a Level 1 → Level 4 density gradient so users instantly understand what they're picking. See §5.5.

4. **Battery is three numbers, not one.** Every vendor overlay gets this wrong, and users are explicit about wanting all three.
   > *"it would be useful to have an overlay for the battery charge, charge rate, and time, similar to Armoury Crate's Real-Time Monitor"* — nathancdy, g-helper issue #3620
   > *"Only current alternative of viewing the battery percentage and charge/dischage rate is to briefly check by opening G-Helper"* — same issue
   > *"The big thing HWiNFO shows that MSI Afterburner doesn't pull from is the battery information"* — Steam Deck HQ, ROG Ally overlay guide
   **Design implication:** battery = `percent` + `charge rate (W)` + `estimated time remaining`. A single `92%` is insufficient. See §5.3 and §5.5.

5. **"Weekend-project" overlays are a market gap, not a successful product.** Users install MSI Afterburner + RivaTuner + HWiNFO64 only because nothing better exists. The existence of community `.ovl` packages like ElegantMustard and TroyMetrics/Benchmark-Overlays proves the setup is so painful that users ship pre-cooked files to avoid it.
   > *"It is a bit of a process, but after this, you shouldn't have to worry about doing it again"* — Steam Deck HQ, framed as encouragement (conceding setup is tedious)
   > *"moving the position through Rivatuner doesn't work at all"* — Overclock.net RTSS discussion
   > *"had to edit text files and registry to fully unlock all adjustable parameters"* — MSI Afterburner configuration forums
   **Design implication:** presets must work out of the box without touching settings. Manual mode is strictly opt-in for users who *want* to tinker, and it starts from a copy of the currently-active preset — never empty. "Configure from scratch" is the exact UX failure that makes RTSS painful.

6. **Vendor overlays are "slapped on top of Windows", and users notice.** This is the specific gap in the market users articulate out loud.
   > *"Utilities like Armoury Crate feel like apps slapped on top of Windows, whereas alternatives aim to make your Windows handheld feel like an actual handheld"* — XDA Developers, Winhanced hands-on
   > *"doesn't update as fast as I would like or provide all the info I want to see"* — Steam Deck HQ on Armoury Crate
   > *"half-baked nature of the Armoury Crate SE software and the Command Center overlay"* — early ROG Ally review aggregate
   **Design implication:** HHA Pulse's positioning is *"built for handhelds, not bolted on"*. Marketing copy in `docs/sales/store-listing-copy.md` should lean into this directly.

7. **Overlays must not steal framerate, must not fight other overlays, and must not grab keys they don't own.**
   > *"MSI Afterburner may be the cause for stutters in your games"* — ResetEra PSA thread
   > *"Disable Nvidia Overlay = More FPS"* — Steam Community, Stalker 2 discussions
   > *"Resource Monitor… registers and captures 5 keyboard shortcuts which can't be unbound"* — ThatOtherAndrew, g-helper issue #1496
   > *"Third-party performance overlays like Xbox Game Bar, Command Center, and Armoury Crate SE can disrupt AFMF functions on ROG Ally"* — Windows Central AFMF guide
   **Design implication:** the CaptureService ETW approach already in the architecture is a *marketable* feature, not just an internal choice. Copy should say so. And HHA Pulse must not register any global hotkey the user did not explicitly opt into.

### 2b.2 Metric priority from the voices

Synthesized across Valve's Level 2 composition, g-helper issue #3620, Steam Deck HQ's custom overlay recipe, and HandheldCompanion defaults. Order reflects what users say they look at most, not what is easiest to measure.

| Rank | Metric | Evidence |
|---|---|---|
| 1 | FPS | Universal ask. Valve shipped an FPS-only mode on demand. |
| 2 | Battery % | Distinguishes handheld from desktop. Every handheld guide puts it top-3. |
| 3 | Battery charge rate (W) + time remaining | g-helper #3620 is an explicit, still-unresolved feature request. |
| 4 | Frametime / 1% low | Valve Level 2. Users describe this as "smoothness" or "stutter", not "1% low". |
| 5 | GPU load % | Valve Level 2. Read as "is the GPU the bottleneck?" |
| 6 | CPU load % | Same. Read as "am I CPU-bound?" |
| 7 | RAM | Valve Level 2. Glanced-at once per game, not continuously. |
| 8 | VRAM | Level 3+. Demote from top-tier "Standard" to Advanced-only. |
| 9 | TDP / package power (W) | Roadmap. Explicit demand across AYA Space, Command Center, HandheldCompanion. Currently blocked by CLAUDE.md "no power until real data". |
| 10 | Single hottest temperature | Roadmap. Users asked for *one* number ("just take the current highest temperature"), not three. Blocked by same rule. |
| 11 | Display Hz | Nice-to-have. Used mostly to confirm VRR is active or refresh rate matches a cap. |

**Metrics users explicitly did NOT ask for:** per-core CPU breakdown, voltages, fan RPM, PresentMon latency in ms, FSR/FG state indicator. All are power-user Level-4-only items. HHA Pulse should not ship them even in Manual mode.

### 2b.3 Anti-requirements, confirmed from real voices

- **No tooltips.** You're playing a game with a gamepad. There is no mouse, there is no hover.
- **No onboarding wizards.** Nobody asks for these. The complaint is "too much setup", never "not enough guidance".
- **No animations** except the frametime chart itself. ElegantMustard, TroyMetrics, Valve native — none use motion design.
- **No nested cards, no sidebar dashboards, no docked side panels.** The mental model is "HUD", not "dashboard". HandheldCompanion's "quicktools" side panel is a separate invoked entity, not the overlay itself.
- **No custom font loading, no gold trim, no RGB** on the default surface. Handheld users consistently picked the most sterile, legible overlays. Brand lives in type hierarchy and cyan accent, nothing more. (Gold stays reserved for a future Founder tier and never ships on default surfaces.)
- **No metric-picker UI that makes you start from empty.** Users who want granular control already use RTSS and complain it's awful; Manual mode must start from a preset copy.
- **No "smart insights"** ("Your CPU is running hot — try lowering TDP!"). Users want raw numbers and to make the call themselves.
- **No cloud sync, no accounts, no telemetry.** Confirmed as a feature by every popular handheld tool (HandheldCompanion, G-Helper, Steam Deck Tools — all local-only).

### 2b.4 Tone rule

The register observed across the sources is *direct, technical, slightly grumpy*. Lots of *"Just give me…"* constructions. Zero tolerance for marketing language. HHA Pulse copy (labels, error strings, empty states, support text) should match — short sentences, concrete numbers, no "experience" or "journey" or "unleash".

### 2b.5 What the current spec already gets right — cited from the research

- **One slim HUD, no side panel, no diagnostic overlay.** Matches universal preference for "thin strip at top" seen in Steam Deck L1-L2 and every community clone.
- **4 curated presets + Manual**, mapped to a Valve L1-L4 density gradient. HHA Pulse's Standard drops the Gamescope marker (Linux-only) and adds battery charge rate per g-helper #3620. Spiritual parallel, not literal clone. Should rename to expose the mapping (§5.5).
- **Missing FPS shows `--`, no faked values.** Users are allergic to overlays that lie to them — this is the core of the Armoury Crate complaint.
- **No DLL injection, no hooks, ETW-only capture.** This is a *marketable feature*, not just an implementation detail. Ship with copy that says so.
- **Local-only, no telemetry.** Confirmed as a handheld-community expectation.

The research conclusion, copied verbatim because it's the most useful framing for the whole project:

> *"HHA Pulse's current spec is the right shape. Its job is to ship the overlay Valve shipped for the Deck, on every other handheld, without the Armoury Crate bloat, without the RTSS weekend-project setup, and with a hide toggle that actually works. The biggest gap in the current spec is battery detail (rate + time remaining) and a rock-solid hide/show guarantee. Everything else is polish."*

## 3. Design direction

### 3.1 Aesthetic — Editorial Dark with Command-Deck details

Base is the editorial handheld-magazine feel translated into a Segoe-based Windows surface: Segoe UI for display/body, Consolas for mono readouts, generous whitespace, single cyan accent, hairline strokes instead of heavy shadows. This keeps the companion Store-safe and avoids a custom-font packaging branch in this milestone.

On top of that base, specific Command-Deck accents are folded in:
- **Pulsing status dot on the `OVERLAY ON` chip** in the top bar. Uses a small ellipse with a `DoubleAnimation` on `Opacity` (1.4s ease-in-out loop). The dot is the only motion in the topbar; state chips themselves are static.
- **Tabular monospace numerics** in the in-game HUD, the position flyout's current-state label, the preset mini-samples, and the event line. `Consolas` is used in this milestone; no bundled fonts ship in Plan B.
- **Faint cyan grid** (spacing referenced via `{StaticResource HhapMonitorGridCell}` token) on the monitor canvas inside the Position flyout only — nowhere else.
- **Event line** below the preset row: one muted mono line showing `timestamp · CAPTURE attached pid N · EXE.NAME · frametime Xms · sampling N Hz`. It replaces the entire right-side event log from the Command Deck comp.

No side menu. No three-pane layout. No grid wallpaper on anything except the position canvas. No module IDs like `M_01`. No nested cards.

### 3.2 Palette (brand tokens)

From `brand-handheldally.md`, with starting hex values. These replace the ad-hoc palette currently in `HhapTheme.xaml` by mapping onto the same brush names so consumers are not broken.

| Token | Value | Role |
|---|---|---|
| `HhapBg` | `#0F1115` | Window base |
| `HhapPanel` | `#171A21` | Cards, preset cards, Position flyout content |
| `HhapPanel2` | `#1F232B` | Hover, elevated popovers, inline expanders |
| `HhapPanel3` | `#252A33` | Pressed states |
| `HhapLine` | `#FFFFFF` @ 6% | Hairline dividers |
| `HhapLineStrong` | `#FFFFFF` @ 12% | Card strokes |
| `HhapText` | `#F4F6FA` | Headlines, HUD numbers |
| `HhapMuted` | `#9BA3AF` | Labels, unit suffixes |
| `HhapMuted2` | `#5B6372` | Placeholder `--`, dividers |
| `HhapAccent` | `#22D3EE` | Primary CTA, active preset, focused metric, band-1 fills |
| `HhapAccentHi` | `#67E8F9` | Hover/focus ring |
| `HhapGold` | `#F5B301` | **Founder tier only.** Never used on default surfaces. |
| `HhapBandGood` | `#4ADE80` | Band 1 — stable / excellent |
| `HhapBandOkay` | `#FACC15` | Band 2 — busy / good |
| `HhapBandWarn` | `#F87171` | Band 3 — struggling / poor |

**Band tiers** are a direct port of the handheldally.com device-rating pattern (green/yellow/red). The `HhapBandGood/Okay/Warn` tokens are reserved for the three support-footer health dots (capture / game detect / companion) only. No preset card, no metric value, and no topbar chip uses a band-tier colour — per the user's *"no stacking signals on one number"* rule in §4.1.

### 3.3 Typography

| Role | Family | Notes |
|---|---|---|
| Display / hero | Segoe UI Semibold | Used across the companion surface |
| UI body / labels | Segoe UI 400/600 | Default for everything else |
| Numerals (HUD, event line, preset minis, position-picker current label) | Consolas 400/600 | Monospace system font |
| Eyebrow / band labels | Segoe UI 600 uppercase, 0.12em tracking | Small caps feel |

No bundled-font work ships in this milestone. Plan B keeps the system-font stack (`Segoe UI`, `Segoe Fluent Icons`, `Consolas`) and defers any branded-font pass.

### 3.4 Motion

- Fade, not slide. 150–200ms ease-out on hover and selection. Match the site's discipline.
- Preset card selection: 200ms cyan border fade in + cyan dot in the top-right corner.
- Position button selection: 200ms background fade + the monitor-canvas highlight region transitions to the newly selected anchor in 220ms.
- `OVERLAY ON` status dot: 1.4s ease-in-out opacity loop on the small ellipse inside the chip. Independent of any data binding; stopped when the chip renders `OVERLAY OFF`.
- Page entrance: `EntranceThemeTransition` on the `ContentHost` once, on first load. Nothing on subsequent swaps.

## 4. Information architecture

**No hero. No tabs. No drawer. No marketing copy inside the product surface.** The companion window is a single vertical stack: topbar (identity + state chips) → toolbar row (Position + Feel-&-fit pill buttons, each opening its own `Flyout`) → preset row → event line → support footer. There is no live metric strip anywhere in the companion — see §4.1.

```
┌──────────────────────────────────────────────────────────────────┐
│ [P] Pulse      ● OVERLAY ON   CAPTURE READY   HADES II     – ×  │  ← topbar: identity + state chips
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ( Position: Bottom · 1 line ▾ )   ( Feel & fit: Default ▾ )     │  ← toolbar: pill dropdowns
│                                                                  │
│  ┌─────────┐ ┌─────────┐ ┌───────────┐ ┌──────────────────┐     │
│  │ CLEAN   │ │ RECOMM. │ │ TUNING    │ │ PRO              │     │
│  │ Minimal │ │ Standard│ │ Performance│ │ Manual          │     │
│  │         │ │   ● act │ │           │ │                  │     │
│  │ ┌─────┐ │ │ ┌─────┐ │ │ ┌───────┐ │ │ ┌──────────────┐ │     │
│  │ │ FPS │ │ │ │ FPS │ │ │ │ FPS 1%│ │ │ │ ◉FPS ◉CPU   │ │     │
│  │ │ 142 │ │ │ │ CPU │ │ │ │ FT CPU│ │ │ │ ◉GPU ◉BAT   │ │     │
│  │ │ BAT │ │ │ │ GPU │ │ │ │ GPU …│ │ │ │ ○1%  ○FT    │ │     │
│  │ │ 92% │ │ │ │ BAT │ │ │ └───────┘ │ │ └──────────────┘ │     │
│  │ └─────┘ │ │ └─────┘ │ │ 8 metrics │ │ pick any of 9    │     │
│  │ 2 metr. │ │ 4 metr. │ └───────────┘ └──────────────────┘     │
│  └─────────┘ └─────────┘                                         │
│                                                                  │
├──────────────────────────────────────────────────────────────────┤
│  17:27:08  pid 14228 · HADES_II.EXE · frametime 7.0 ms · 120 Hz  │  ← event line
├──────────────────────────────────────────────────────────────────┤
│  ● Capture online   ● Game detect   ● Companion standalone       │  ← support footer
│  [Copy support report] [Open log] [handheldally.com] [Community] │
└──────────────────────────────────────────────────────────────────┘
```

**The preset cards are the primary surface.** Position is a set-and-forget pill-button dropdown in the toolbar row; Feel & fit is its sibling. Each preset card shows three things: name, a **real mini HUD preview** of the metrics that preset renders on screen, and a muted metric count. A user scanning the presets sees exactly what they will get on their game screen before they click — no text label saying "shows CPU and GPU", an actual mini pill that looks like the HUD bar.

The Manual preset is different: its preview shows metric *toggle* chips (◉ for on, ○ for off), making it visually obvious that Manual is editable rather than curated.

**The companion window never shows live metric values.** FPS, CPU, GPU, RAM, battery, Hz — those numbers belong in the HUD bar the user has chosen to pin to their screen. The companion is a *configuration surface*, not a second display. A user who wants numbers all the time turns on the HUD; a user in the companion is picking a preset or a position and wants to see the knobs, not the same numbers they're already looking at in-game.

The topbar carries **state chips only**:
- `OVERLAY ON` / `OVERLAY OFF` — binary, with the pulsing dot on "ON"
- `CAPTURE READY` / `CAPTURE OFFLINE` / `CAPTURE LIMITED` — three-state, no number
- Current game title (when a game is foregrounded) or `NO GAME DETECTED` — identity, not metric

These are *configuration context*, not telemetry. They answer "is the thing I'm configuring actually working right now?" — nothing more.

### 4.1 What changed from round 03 → round 04

- **Position picker demoted to a pill-button dropdown.** Round 03 rendered the monitor illustration + six anchor buttons as a co-equal card in the work area, taking up roughly half the surface. Rejected: *"why is this so massive… it should have been just drop window like we do for when users choose opacity… it is just adition you set it and forget it"* (Johny 2026-04-15). Position is now a ≤ 2.5 rem pill button in the toolbar row showing `Position: Bottom · 1 line ▾`. Clicking it opens a WinUI 3 `Flyout` containing the monitor illustration and six anchor buttons. The flyout closes on select or outside click.
- **Feel & fit uses the same pill-dropdown pattern.** `Feel & fit: Default ▾` sits next to Position in the toolbar. Clicking opens a flyout with three sliders (bg opacity, text opacity, text size) and redundant top/bottom chips. Consistent interaction model for both set-and-forget knobs.
- **Preset cards are the primary surface.** With position demoted, the preset row occupies the whole work area below the toolbar — four cards in a horizontal grid at `md`, a 2 × 2 grid at `sm` / `xs` via `@container` queries.
- **Each preset card contains a real mini HUD preview.** Not a text label. Not a description. An actual pill-shaped mini-render of the metrics that preset shows — `FPS 142 · CPU 36 · GPU 72 · BAT 92` rendered in `Consolas`, tabular numerics, cyan FPS accent — because *"the presets have to show users what he would get there! like whats in minimal and so on not just plain text"* (Johny 2026-04-15). The mini preview is the thing that tells the user what they're picking; the name and eyebrow are just identity labels on top.
- **Manual preset preview uses toggle chips.** Visually distinct from the other three because Manual is editable, not curated. Chips show `◉FPS ◉CPU ◉GPU ◉BAT ○1% ○FT ○RAM ○VRAM ○Hz`. Clicking the Manual preset expands an inline metric sheet below the preset row (or in a bottom sheet, see §5.5) where the toggles become interactive. Clicking any other preset collapses the sheet.
- **Topbar carries configuration state only — not telemetry.** Three state chips — `OVERLAY ON/OFF`, `CAPTURE READY/OFFLINE/LIMITED`, current game title — nothing numeric, nothing that overlaps with what the HUD already displays in-game. *Rejected from round 03:* "we do not have to show in area where user should only click between menus the current data he would see them when he chose to see that all the time" (Johny 2026-04-15).
- **No duplicate live-metric readout anywhere in the companion.** There is no live metric display anywhere in the companion window. Users who want numbers always-visible turn on the HUD itself.
- **Band tiers are gone entirely.** `HhapBandGood/Okay/Warn` tokens stay in `HhapTheme.xaml` (the Support footer dots still use them for three-state capture/game/companion health) but no metric value or preset card gets a band indicator.
- **Editorial hero deleted.** Copy lives in `docs/sales/store-listing-copy.md` for the Store listing and `/premium` page only.
- **Above-the-fold promise.** At `md-720` (ROG Ally, Claw, Win Mini), topbar + toolbar + 4-card preset row + event line + support footer must all be visible without scrolling. Demoting the position picker from a co-equal card to a single pill button is the slack that makes this fit; the verification checklist in §13.5 confirms it on each device cluster.

### 4.2 The topbar in detail

Contents, left to right:
- **Mark + wordmark:** square cyan gradient tile + "Pulse" in Segoe UI Semibold. Minimum footprint.
- **State chip group:** three chips, no numbers, no telemetry. (1) `OVERLAY ON` with a pulsing cyan dot, or `OVERLAY OFF` static and muted. (2) `CAPTURE READY` / `CAPTURE OFFLINE` / `CAPTURE LIMITED` — three-state, driven by the capture service IPC health. (3) Current foregrounded game title (e.g. `HADES II`) or `NO GAME DETECTED`. The group wraps onto a second line if the window narrows to `xs`.
- **Icon row:** `–` (minimise) and `×` (close). Each is ≥ `2.75rem` square. Feel-&-fit is a pill button in the toolbar row (§4.1), not an icon in the topbar.

The topbar is the only place in the entire companion surface where the brand identity lives. Everything below the topbar is operator chrome.

### 4.3 Support footer in detail

- **States row:** three dots, each labeled — "Capture online", "Game detect", "Companion standalone". Uses band-tier colors (good = green, limited = yellow, down = red). These are the only three consumers of the `HhapBandGood/Okay/Warn` tokens in the whole product.
- **Actions row:** four outline buttons — Copy support report, Open log folder, handheldally.com, Community. Each ≥ `2.75rem` tall, grid-flows into `minmax(10rem, 1fr)` cells so they collapse gracefully on narrow containers.
- **Quiet attribution line.** Beneath the actions, one muted line: *"Built by a fellow tinkerer · handheldally.com"*. The whole line is a hyperlink to `handheldally.com/premium` and opens in the default browser. **There is no tipping surface inside the product** — no tip jar card, no inline "tip the jar" link, no fundraising copy. Any monetisation lives on the external page. This is consistent with §2b.3 and supersedes any earlier "tipping moves to a single line" note in this file.
- **No advanced diagnostics expander.** The "Copy support report" button silently collects and copies the dump file; no on-screen dump rendering.

## 5. Components — what changes

### 5.1 Surfaces to delete

From `ui-audit-hhap05.md`:

- `src/HHAPulse.Overlay/Views/ComposerOverlay.xaml` + `.cs` — the entire wizard. Gone.
- `src/HHAPulse.Overlay/ViewModels/ComposerViewModel.cs` — gone.
- `src/HHAPulse.Overlay/Views/SettingsSheet.xaml` + `.cs` — inlined into the single surface; file deleted.
- `src/HHAPulse.Overlay/Views/HubView.xaml` — replaced by a new `Views/CompanionView.xaml` (single-surface). `HubView.xaml` is deleted.
- `src/HHAPulse.Overlay/Views/SupportView.xaml` — footer content moves into `CompanionView.xaml`; the standalone file is deleted.
- `src/HHAPulse.Overlay/Settings/OverlayPresetCatalog.cs` — **extend, do not rewrite.** Keep the current structure (const string metric IDs, `MinimalMetrics`/`StandardMetrics`/`TunerMetrics` arrays, `GetMetricIds` switch, `NextPreset` cycle, `PresetDisplayName` map). Extensions required:
  1. Add `OverlayPreset.Full = 5` to `AppSettings.cs:40-47` (value `5` preserves JSON round-trip for existing saved settings with `Off = 4`).
  2. Add a new `FullMetrics` array in `OverlayPresetCatalog.cs` populated with all 19 entries from the existing `AllMetricIds` table (`:74-95`).
  3. Curate `TunerMetrics` down to 11 entries: add `CpuPower`, and remove `GpuClock`, `DeviceTemp`, `GpuFan`, and `Vram` (they move to Full only). The new Advanced (post-rename) set is: `Fps, OnePercentLow, FrameTime, CpuUsage, CpuTemp, CpuPower, GpuUsage, GpuTemp, Ram, Battery, RefreshRate` = 11 items.
  4. Rename `PresetDisplayName(OverlayPreset.Tuner)` return string from `"Tuner"` → `"Advanced"` (`:139`). The enum value stays `Tuner` to preserve saved-settings compatibility.
  5. Rename `PresetDisplayName(OverlayPreset.Custom)` return string from `"Custom"` → `"Manual"` (`:140`). Same enum-stability rule.
  6. Update the `NextPreset` cycle at `:120-131` to `Minimal → Standard → Advanced → Full → Off → Minimal`.
  7. Extend `GetMetricIds`'s switch expression at `:99-107` with an `OverlayPreset.Full => FullMetrics` arm.
- `HhapRailNavButtonStyle` in `HhapStyles.xaml` — orphan, delete.
- `Helpers/MetricFormatterCompact.cs` — if still unused after the redesign, delete; else keep.

### 5.2 Surfaces to keep as-is

- `App.xaml`
- `MainWindow.xaml` + `Controls/TopBarControl.xaml` + `Controls/SparklineGraph.xaml` (in-game HUD — untouched except for token swaps and the position/layout expansion described below)
- `Themes/HhapTheme.xaml` and `Themes/HhapStyles.xaml` (extend, do not rewrite)

### 5.3 Surfaces to add or rewrite

| File | State | Purpose |
|---|---|---|
| `Views/CompanionView.xaml` + `.cs` | **new** | The single-surface page: topbar + toolbar row (Position + Feel-&-fit pill buttons) + preset row (4 cards with mini HUD previews) + Manual link + inline Manual sheet + event line + support footer + `NpuStatusCard`. No hero, no duplicate live-metric readout. Replaces HubView + SettingsSheet + SupportView + ComposerOverlay. |
| `ViewModels/CompanionViewModel.cs` | **new** | State for the companion surface: overlay on/off, selected position, selected preset, manual metric toggles, capture state flag, battery state, event line fields. |
| `Controls/PositionFlyout.xaml` + `.cs` | **new** | WinUI 3 `Flyout` content (not a page, not a card): monitor canvas + six anchor buttons. Opens from `PositionPillButton`, closes on select or outside click. `DependencyProperty SelectedPosition` bound from `CompanionViewModel`. Raises `PositionChanged`. |
| `Controls/FeelAndFitFlyout.xaml` + `.cs` | **new** | Flyout content: background opacity / text opacity / text size sliders, two-way-bound to `SettingsService`. Replaces the old `FeelAndFitSheet.xaml` row from earlier drafts. |
| `Controls/ManualMetricSheet.xaml` + `.cs` | **new** | Inline expander content revealed when the Manual link is active. Toggles for every entry in `OverlayPresetCatalog.AllMetricIds`. |
| `Controls/NpuStatusCard.xaml` + `.cs` | **new** | Diagnostics-only lower-area card showing NPU present/absent state, adapter/vendor name, and explicit "utilization unavailable" copy. |
| `Settings/OverlayPresetCatalog.cs` | **extend** | Extend the existing catalog per §5.1 extension list (add `Full`, curate `Tuner`, rename display labels, extend `NextPreset` cycle, extend `GetMetricIds` switch). Do not rewrite. |
| `Themes/HhapTheme.xaml` | **extend** | Add `HhapAccentHi`, `HhapGold`, `HhapBandGood/Okay/Warn`, plus motion duration tokens (`HhapDurFast=150ms`, `HhapDurBase=200ms`). Existing brushes keep their names but their colour values shift to the §3.2 palette. |
| `Themes/HhapStyles.xaml` | **extend** | Add `HhapPresetCardStyle`, `HhapPositionButtonStyle`, `HhapEventLineStyle`, `HhapBandDotStyle`, `HhapLivePulseStyle`. Delete orphan `HhapRailNavButtonStyle`. |
| `ControlWindow.xaml` | **strip** | Title bar stays, nav buttons (Hub/Support) removed, `ContentHost` hosts `CompanionView` only, `SettingsSheet` host row deleted, `ComposerOverlay` host row deleted. |

### 5.4 Position picker — behaviour and insertion points

- Six positions: `TopThin`, `TopTall`, `LeftDock`, `BottomThin`, `BottomTall`, `RightDock`. `TopTall` / `BottomTall` render two rows of metrics (FPS + 1% low + frametime on row 1, CPU/GPU/RAM/battery on row 2). Left/Right docks are narrow vertical rails at a new `HhapSideDockWidth` token width (Steam-overlay width target — see §10 open questions for the concrete measurement).
- The monitor canvas inside the Position flyout shows a single highlight rectangle whose position corresponds to the selected anchor. Switching anchors animates the rectangle's geometry over 220ms.
- The Position flyout writes through to `SettingsService` via `CompanionViewModel` so existing persistence works unchanged.

**Insertion points in the real code (read first-hand today):**

1. **Position model expansion.** Persist the new enum directly with `TopThin = 0` and `BottomThin = 1`, so legacy saved values round-trip without a custom converter. `TopTall = 2`, `BottomTall = 3`, `LeftDock = 4`, `RightDock = 5`.
2. **`OverlayLayout` record extension.** Today `OverlayPresetCatalog.cs:147` declares `public sealed record OverlayLayout(IReadOnlyList<string> TopBarMetricIds)`. Extend to `record OverlayLayout(IReadOnlyList<string> TopBarMetricIds, TopBarPosition Position, int LineCount)`. `GetLayout(preset, customMetricIds)` at `:110-114` gains a `TopBarPosition position` parameter and `int` line-count derivation.
3. **`OverlayViewModel.ApplySettings()` extension.** Today at `ViewModels/OverlayViewModel.cs:80-85` it reads `settings.ActivePreset` and `settings.EnabledMetricIds` and publishes `ActivePreset` and `TopBarMetricIds`. Extend to also read `settings.TopBarPosition` and publish two new properties via `SetProperty`: `Position` (the `TopBarPosition` value) and `LineCount` (derived — `TopTall`/`BottomTall` = 2, everything else = 1). Backing fields follow the same pattern as `activePreset` at `:24` and `topBarMetricIds` at `:25`.
4. **`TopBarControl.OnVmChanged` extension.** Today at `Controls/TopBarControl.xaml.cs:212-235` it listens for `TopBarMetricIds` / `ActivePreset` / `CurrentSnapshot` / history properties. Extend the property-name switch at `:216-217` to also fire `RebuildMetricViews()` when `Position` or `LineCount` changes.
5. **`MainWindow.OnViewModelPropertyChanged` extension.** Today at `MainWindow.xaml.cs:96-103` it does the same listener pattern as `TopBarControl` — both the window and the control listen to the same ViewModel change-notifications. Preserve that dual-listener pattern: extend `:98-99`'s `PropertyName` check to include `Position` / `LineCount` so the window also rebuilds via `ApplyLayoutState()` → `ResizeOverlayWindow()`.
6. **`MainWindow.ApplyPosition` rewrite.** The legacy binary top-or-bottom placement path should be replaced by `ApplyPosition(TopBarPosition)` and a six-way switch inside `ResizeOverlayWindow()`: `TopThin`/`TopTall` anchor at the top of the work area, `BottomThin`/`BottomTall` anchor at the bottom, `LeftDock`/`RightDock` anchor at the left/right edge with width constrained to `HhapSideDockWidth`.
7. **`TopBarControl.RebuildMetricViews` extension.** Today at `Controls/TopBarControl.xaml.cs:239-310` the method builds a single row, measures with `Measure(Unbounded)` at `:283`, compares to `GetScreenWidth()` at `:285`, and either keeps the single row (`:287-292`) or auto-splits into two rows (`:293-303`). For `TopTall`/`BottomTall`, make the two-row path explicit rather than auto (skip the single-row measurement and always build row-1 perf + row-2 hw). For `LeftDock`/`RightDock`, add a new vertical-stack rendering path: orientation `Vertical`, width constrained to `HhapSideDockWidth` token, same measured-sizing pattern (`Measure(Unbounded)` → `Notify(rows, width, height)` via `LayoutMetricsChanged` at `:291,:302`). `MainWindow.OnTopBarLayoutMetricsChanged` at `:105` picks up the new measurement chain unchanged.

The existing `INotifyPropertyChanged` chain is the insertion point; nothing in the real code needs a new `DependencyProperty` to carry layout state. `FpsGroupIsTwoRow` at `TopBarControl.xaml.cs:328` (set inside `BuildFps`) continues to express the internal two-row FPS sub-grouping and is orthogonal to the new position/line-count model.

### 5.5 Preset system — behaviour

Four curated presets plus Manual, named to match Valve's Steam Deck Level 1-4 mental model so handheld gamers understand them instantly. This naming comes from §2b.1 research finding #3.

| Preset | Enum | UI label | Metric IDs (real `OverlayPresetCatalog.cs` keys) | Count |
|---|---|---|---|---|
| **Minimal** (L1) | `OverlayPreset.Minimal` (value 0) | `Minimal` | `Fps, Battery` | 2 |
| **Standard** (L2, default) | `OverlayPreset.Standard` (value 1) | `Standard` | `Fps, OnePercentLow, FrameTime, CpuUsage, GpuUsage, Battery` | 6 |
| **Advanced** (L3) | `OverlayPreset.Tuner` (value 2, enum unchanged) | `Advanced` (renamed from `Tuner`) | `Fps, OnePercentLow, FrameTime, CpuUsage, CpuTemp, CpuPower, GpuUsage, GpuTemp, Ram, Battery, RefreshRate` | 11 |
| **Full** (L4, new) | `OverlayPreset.Full` (new value 5) | `Full` | `Fps, AvgFps, OnePercentLow, ZeroPointOneLow, FrameTime, CpuUsage, CpuTemp, CpuPower, GpuUsage, GpuTemp, GpuClock, GpuPower, GpuFan, Ram, Vram, TotalPower, DeviceTemp, RefreshRate, Battery` | 19 |
| **Manual** | `OverlayPreset.Custom` (value 3, enum unchanged) | `Manual` (renamed from `Custom`) | `settings.EnabledMetricIds`, initialised as a copy of whichever curated preset was active when Manual was first opened | ≤ 19 |

**Mapping rationale:** this is a density-mapped parallel to the Valve Steam Deck performance overlay levels users already know. A user coming from Steam Deck will instantly understand the progression. A user new to handhelds will read the names left-to-right and get a clean density gradient.

**Layout in the companion:**
- The **four curated presets** are the preset row — four horizontal cards at `md`/`sm`, 2 × 2 at `xs`. Each card shows a mini HUD preview of its metric set (see §5.5.1 below).
- **Manual is a separate quiet link** below the preset row: *"Or build your own ▸ Manual"*. Clicking it expands an inline metric-toggle sheet in place. Manual is **not** a fifth card; it is a secondary entry point matching the user's original phrasing *"4 preset and then the manual"*.

**Manual mode's opening state:**
- When the user first opens Manual, its metric set is pre-populated as a **copy of the currently-active preset** (Minimal / Standard / Advanced / Full) — never empty. This comes from §2b.1 finding #5: *"configure from scratch"* is the exact UX failure that makes RTSS painful. Users want to *tweak*, not rebuild.
- Once the user starts toggling, Manual diverges from its source preset and tracks independently. Switching back to a curated preset does not destroy Manual's state; it is persisted separately via `SettingsService`.
- There is no "save Manual as preset" button. Manual is a live set, not a named configuration.

**Switching behaviour:**
- Curated sets are declared in `OverlayPresetCatalog.cs` as `IReadOnlyList<MetricId>`.
- Switching preset re-renders `TopBarControl` immediately. No "apply" button.
- When Manual is active, switching to a curated preset collapses the manual sheet and runs the preset's metric set.

**Metric availability gate:**
- Level 3 / Level 4 already map to real collector-backed telemetry where a proved source exists: battery remains one composite `battery` MetricId (percent + watts + time when available), CPU temp comes from the elevated capture service, CPU power from EMI, GPU clock/temp/power/fan from vendor SDKs where supported, `vram` from PDH + DXGI, `device_temp` from MSI WMI where supported, and `total_power` is battery-discharge-only. Unsupported hardware renders `--`; the product does not hide working telemetry just because it is hardware-gated.
- Still hidden / never faked: `input_latency`, numeric frame-generation FPS, and hardware gaps with no proved source (for example Intel Lunar Lake GPU temperature). This is the product rule from CLAUDE.md and §2b.3: show real data, show `--` for unavailable hardware-gated paths, never fabricate.

### 5.5.1 Preset card composition

Each of the four curated cards (`CompanionView.xaml` → `PresetRow.xaml` → `PresetCard.xaml`) contains:

- **Eyebrow** — one word of identity: `LEVEL 1` / `LEVEL 2` / `LEVEL 3` / `LEVEL 4`. Mono, muted.
- **Name** — the human label: `Minimal` / `Standard` / `Advanced` / `Full`. Display font, semibold.
- **Mini HUD preview** — a real render of the metric set the preset will display. Same monospace system font (`Consolas`), same tabular numerics, same cyan FPS accent as the actual in-game HUD. Not a text description — an actual pill that matches what the user will see on screen.
- **Metric count** — muted mono line: Minimal `2 metrics`, Standard `6 metrics`, Advanced `11 metrics`, Full `19 metrics`. These counts are reconciled against `OverlayPresetCatalog.cs:35-69` after the §5.1 extensions land.
- **Active indicator** — a single cyan dot in the top-right corner when the preset is selected. No other chrome.

The mini HUD preview uses sample values (not live telemetry) because live values have no place in a configuration surface (§4.1). Sample values are fixed constants in `PresetCardViewModel`: `FPS 142`, `CPU 36%`, `GPU 72%`, `BAT 92%`, etc. — chosen to look plausible on a mid-range handheld.

### 5.5.2 Manual sheet composition

When the user clicks the *"Or build your own ▸ Manual"* link, an inline expander reveals below the preset row (same surface, no scrim, no drawer). The sheet contains:

- A row of toggle chips — one per available metric — pre-populated from the current preset copy.
- Each chip shows `◉ FPS` (on) or `○ 1%` (off). Tapping toggles.
- No order control in this milestone. Metric order in the HUD bar follows the order declared in `MetricId` enum. Re-ordering is a deferred feature.
- A quiet footer line bound to `CompanionViewModel.ManualFooterText`: *"Manual matches {SourcePresetName} + your changes"* where `SourcePresetName` is `OverlayPresetCatalog.PresetDisplayName(sourcePreset)` at the time Manual was first opened. The `{SourcePresetName}` placeholder is not hardcoded — it tracks whatever curated preset was active when Manual was entered (`Minimal` / `Standard` / `Advanced` / `Full`).

Re-clicking the *"Or build your own"* link closes the sheet. Clicking any of the four curated presets also closes it and switches back to that preset.

### 5.6 Battery metric — composite render, single MetricId

Per §2b.2 and g-helper #3620, handheld gamers want percent + rate + time-remaining visible in one place. **That already works in HHA Pulse today.** This section documents the real state (read first-hand from the overlay tree on 2026-04-15); no MetricId splits, no collector changes, no formatter changes are required for this spec.

**Collector state** (`Collectors/Battery/BatteryCollector.cs` read in full):

- Uses `CallNtPowerInformation(SystemBatteryStateLevel = 5)` via `powrprof.dll` at `:20-28` — **not** `PowerManager.*` or the legacy Win32 battery-status path. The spec must not propose either.
- Decodes `SYSTEM_BATTERY_STATE.Rate` (signed mW) into `snapshot.Battery.ChargeWatts` / `snapshot.Battery.DischargeWatts` at `:43-50` with explicit sign handling.
- Converts `SYSTEM_BATTERY_STATE.EstimatedTime` (seconds, `0xFFFFFFFF` sentinel) → `snapshot.Battery.EstimatedMinutesRemaining` at `:52-55`.
- Opens the battery class device via `IOCTL_BATTERY_QUERY_INFORMATION` (`:87-215`) when the device reports absolute mWh capacity and populates `snapshot.Battery.DesignCapacityWattHours` (`:182-185`), `snapshot.Battery.CurrentCapacityWattHours` (`:194-197`), `snapshot.Battery.HealthPercent` (`:187-192`), `snapshot.Battery.CycleCount` (`:199`). Relative-capacity units are rejected per `telemetry-trace-map.md:16`.
- Fails closed at `:30-33`: when no battery is present or the call fails, no fields are set and `MetricFlags.Battery` is not raised.

**Formatter state** (`Helpers/MetricFormatterCompact.cs:196-234` read first-hand):

`FormatBatteryCompact(snapshot)` already composes `{pct} {time} {watts}` conditionally from `snapshot.Battery.*`:

| Condition | Render |
|---|---|
| Charging + known rate | `92% +9.2W` (`:206`) |
| Charging + unknown rate | `92% AC` (`:209`) |
| Discharging + known time + known rate | `92% 1h48m 9.2W` (time composed inline at `:213-218`, watts appended at `:222`) |
| Discharging + known rate only | `92% 9.2W` (`:230`) |
| Otherwise | `92%` (`:233`) |

`snapshot.Battery.ChargePercent < 0` renders as `--%` (`:198-200`).

**Preset catalog state** (`Settings/OverlayPresetCatalog.cs:29` read first-hand): one `MetricId` named `Battery` = `"battery"`. The Advanced preset and the new Full preset already include it (§5.5 table); the Minimal preset already includes it; Standard already includes it.

**What this spec must NOT propose:**

- **No split battery sub-metric IDs.** That would regress the composite single-line render `FormatBatteryCompact` already produces.
- **No legacy battery-status API dependency.** The real pipeline is `CallNtPowerInformation` + battery IOCTL; proposing the older Win32 battery-status shortcut would contradict `BatteryCollector.cs:23-28` and `telemetry-trace-map.md`.
- **No claim that the Battery collector needs extension.** It doesn't. The three fields users ask for are already flowing through the composite formatter.

**Deferred to a future "Battery detail" surface, tracked as §10 item #12:** visible display of `snapshot.Battery.HealthPercent` and `snapshot.Battery.CycleCount`. The collector already populates both when the hardware exposes them; no HUD metric card consumes them today, and adding one is a roadmap UI item, not a data-collection item.

### 5.7 Reliability — documented against the real launch-based architecture

From §2b.1 finding #1: the single most-recurring complaint about every existing handheld overlay is that *it won't go away*. Not complexity, not features, not design — reliability of the hide/show cycle. HHA Pulse treats this as a first-class feature. **The current implementation does not use Win32 `RegisterHotKey`; it uses a deliberately different, more resilient mechanism.** This section documents that mechanism and rates each commitment against the real code.

#### 5.7.1 Architecture — launch-based toggle (by design)

The user maps a handheld button (e.g. an AYANEO custom button, an ROG Ally back paddle, a Steam Deck mapping) to **launch `HHAPulse.Overlay.exe`** with one of five command arguments: `--toggle`, `--next-preset`, `--off`, `--menu` (or its alias `--settings`), or no argument (defaults to `--menu`). The launcher process always exits cleanly; the running overlay process picks up the signal.

The end-to-end signal path (every line cited from a first-hand read on 2026-04-15):

- `HotkeyService.SignalExistingInstance(string arguments)` at `Hotkeys/HotkeyService.cs:29-55` writes the args to `%LOCALAPPDATA%\HHAPulse\overlay-command.txt` (`:39`), opens the named event `Local\HHAPulse_Overlay_Toggle` (`:40`), sets it (`:41`), and returns. If the event does not yet exist (running instance not ready), it catches `WaitHandleCannotBeOpenedException` at `:45` and returns `false`.
- `HotkeyService.RegisterDefaults()` at `:57-63` creates the event, spawns the listener task, and logs `"Registered launch-to-toggle signal. Map a handheld button to launch HHAPulse.Overlay.exe."` at `:62`.
- `HotkeyService.ListenForToggleRequests` at `:96-120` blocks on `WaitHandle.WaitAny([toggleEvent, shutdown.Token.WaitHandle])` at `:102`; on signal, it dispatches `handleCommand(ReadPendingCommand())` on the WinUI dispatcher queue at `:108-118`.
- `HotkeyService.ReadPendingCommand()` at `:122-137` reads `overlay-command.txt` and returns the content.
- `App.HandleLaunchCommand(string)` at `App.xaml.cs:309-339` routes the five commands:
  - `--menu` / `--settings` (or empty) → `ShowControlWindow()` at `:316`.
  - `--toggle` → `ToggleOverlayVisibility()` at `:322`.
  - `--next-preset` → `ToggleHudMode()` at `:328`, which calls `SetPreset(OverlayPresetCatalog.NextPreset(current))` at `:385`.
  - `--off` → `SetPreset(OverlayPreset.Off)` at `:334`.
  - Unknown → logs `"Unknown launch command '{command}'. Use --toggle, --next-preset, --off, or --menu."` at `:338`.
- For `--toggle`: `App.ToggleOverlayVisibility()` at `:405-414` → `MainWindow.ToggleOverlayVisibility()` at `MainWindow.xaml.cs:46-49` → `SetOverlayVisible(!isOverlayVisible)` → `NativeMethods.ShowWindow(hwnd, SW_SHOWNOACTIVATE | SW_HIDE)` at `:59`, plus `SetWindowPos(hwnd, HWND_TOPMOST, …)` at `:64-68` when showing, plus `ResizeOverlayWindow()` at `:69` driven by `TopBar.EstimatedWidth` / `EstimatedHeight` per the measured-sizing pattern (`memory-bank/best_practices/winui3_overlay.md:64-82`).

**No `RegisterHotKey`, no `UnregisterHotKey`, no `WM_HOTKEY` handler.** Greppable: zero references in the overlay tree.

**Why launch-based is more resilient than Win32 hotkeys:**

- **Sleep / resume.** A new process launch is always fresh; there is nothing to re-register because the listener's named event lives only as long as the running process. After a lid-close round-trip the overlay process is still running, the named event still exists, and the next launch sets it normally.
- **Fullscreen-exclusive games.** A new process launch cannot be blocked by the foreground game's exclusive surface — the OS still spawns the process. A Win32 hotkey *can* be blocked.
- **Focus steal by another process.** There is no global hotkey for another process to steal. The named event is in the user session's `Local\` namespace and is owned by the running overlay.

#### 5.7.2 Current state by reliability commitment

| Commitment | Status | Evidence |
|---|---|---|
| Visible-state change < 100 ms from event signal to `SetOverlayVisible` return | 🟡 Achievable, unmeasured | Signal path at `HotkeyService.cs:96-120` + window flip at `MainWindow.xaml.cs:51-71` exists; no `Stopwatch` instrumentation today. Note: the **cold-start launch portion** (exe spawn → `SignalExistingInstance`) is OS-bound (~100-300 ms) and is not part of the in-process commitment. |
| Sleep / resume resilience | 🟢 By design | Launch-based toggle re-registers nothing; the named event persists across user sleep/resume as long as the overlay process is running. Documented above. |
| Fullscreen game launch resilience | 🟢 By design | New-process launch cannot be blocked by exclusive fullscreen. Plus `Services/ForegroundGameDetector.cs` + `Services/InGameVisibilityGate.cs` provide complementary auto-show/hide. |
| Emergency kill path | 🔴 Greenfield | Greppable: no `NotifyIcon` / `TrayIcon` / `Shell_NotifyIcon` / `System.Drawing.Icon` anywhere in the overlay tree. `ControlWindow.OnExitRequested()` at `:537` is a UI button on the companion window, not a tray path. Deferred to §10 item #14. |
| No silent stuck state | 🟢 Present | Visible signal logging via `AppLogger.Info` / `AppLogger.Error` at `HotkeyService.cs:42, 47, 52, 62, 116, 133` and `App.xaml.cs:338, 365, 401, 413, 620-634`. |

#### 5.7.3 Work items in this spec

**None of the reliability commitments are implementation work in this spec.** Per Phase 3 decision #3, the reliability surface is documented and frozen for now; concrete instrumentation, defense, and the tray-icon kill path are tracked as follow-ups in §10:

- §10 item #13 — `Stopwatch` instrumentation across the signal path.
- §10 item #14 — tray-icon emergency kill path (greenfield).
- §10 item #15 — `SystemEvents.PowerModeChanged` defense layer (precaution; the launch-based path does not strictly need it).
- §10 item #16 — capture the launch-based architecture as intentional in `CLAUDE.md`.

This section replaces the earlier draft's `RegisterHotKey` claim entirely. The earlier claim was wrong: the real code never used Win32 hotkeys.

## 6. Token & style plan

Extend, do not rewrite. The existing `HhapTheme.xaml` brush names stay stable; their values are remapped to the new palette. A migration note in the spec comment block at the top of the file records what changed.

New tokens:
- `HhapAccentHi` — hover / focus ring
- `HhapGold` — Founder tier only
- `HhapBandGood/Okay/Warn` — band tier dots
- `HhapDurFast = 0:0:0.15`, `HhapDurBase = 0:0:0.20` — motion
- `HhapMonitorGrid` — `rgba(34,211,238,0.04)` used in `PositionPicker` only

New styles in `HhapStyles.xaml`:
- `HhapPresetCardStyle` (default + active visual states)
- `HhapPositionButtonStyle` (default + on state + glyph sub-element)
- `HhapBandDotStyle` (three colour variants)
- `HhapLivePulseStyle` (for the capture-live chip)
- `HhapEventLineStyle` (mono text block)

Deleted styles:
- `HhapRailNavButtonStyle` (orphan — nothing consumes it)

Lightweight Styling fallbacks (per `winui3-modern-patterns.md`):
- Override `ButtonBackgroundPointerOver`, `ButtonBorderBrushPointerOver` on `HhapPrimaryButtonStyle` to the new accent so hover states are branded, not default Fluent blue.
- Override `SliderTrackFillPointerOver` on `FeelAndFitSheet` sliders to `HhapAccent`.

Theme dictionary discipline: continue to ship only the `Dark` dictionary (this is a dark-mode product), but structure `Themes/HhapTheme.xaml` so a future `Light` / `HighContrast` pair drops in without restructure. Use `{StaticResource}` inside the dictionary, `{ThemeResource}` at call sites.

## 7. The HUD bar (in-game)

**Structurally close to today, with insertion points enumerated in §5.4.** The existing `Controls/TopBarControl.xaml.cs` is a rounded `Border` hosting `RowsPanel` populated programmatically by `RebuildMetricViews()` at `:239-310`. The rebuild measures content with `Measure(Unbounded)` at `:283`, compares against `GetScreenWidth()` at `:285`, and either uses a single row (`:287-292`) or auto-splits into two (`:293-303`) — the measured-sizing pattern from `memory-bank/best_practices/winui3_overlay.md:64-82`. Both `TopBarControl` and `MainWindow` listen to the same `OverlayViewModel` `INotifyPropertyChanged` events (`MainWindow.xaml.cs:96-103` and `Controls/TopBarControl.xaml.cs:212-235`).

It keeps:

- Background `HhapHudBg` (remapped to `#0F1115` @ 78% with hairline `HhapHudTileBorder`).
- `Consolas` for values and event-line text, `Segoe UI` for labels. No bundled fonts in this milestone.
- The `FpsGroupIsTwoRow` internal sub-grouping at `Controls/TopBarControl.xaml.cs:328` driven by FPS metric count ≥ 3.

The position-model expansion uses the `INotifyPropertyChanged` chain documented in §5.4 — there is no `HudLayout` enum to add, and no new `DependencyProperty` to create. The viewmodel publishes `Position` and `LineCount`, both `TopBarControl.OnVmChanged` and `MainWindow.OnViewModelPropertyChanged` listen for those names, and `RebuildMetricViews` plus `MainWindow.ApplyEdge` (rewritten as `ApplyPosition`) consume them as described in §5.4 steps 1-7.

Net new code paths in `RebuildMetricViews`:

- Explicit two-row build for `TopTall` / `BottomTall` (skip the auto-split single-row measurement; always populate row-1 perf + row-2 hw).
- Vertical-stack rendering for `LeftDock` / `RightDock` — orientation `Vertical`, width constrained to the new `HhapSideDockWidth` token, same `Measure(Unbounded)` → `Notify(rows, width, height)` chain via `LayoutMetricsChanged`.

## 8. Support footer

Three band-dot status chips (capture / game detect / companion) + four outline buttons (Copy support report / Open log folder / handheldally.com / Community). Beneath the actions, one quiet attribution line — *"Built by a fellow tinkerer · handheldally.com"* — that links to `handheldally.com/premium` in the default browser. **There is no in-product tipping surface**: no card, no inline "tip the jar" link, no fundraising copy. Any monetisation lives on the external page (consistent with §4.3 and §2b.3). The advanced diagnostics expander is deleted entirely. The report file itself is still collected on "Copy support report" click, copied to the clipboard, and logged.

## 9. CLAUDE.md — rule revision

The current CLAUDE.md rule block says:

> - One in-game HUD only: no side panel, no nested boxes, no diagnostic overlay, no custom metric picker, no preset label.

This rule was written when the wizard was the enemy. It over-shoots. The user's decision to ship four presets plus a Manual mode means two of those clauses need revision. The replacement block:

> - **One in-game HUD only.** The HUD is a single strip — top bar, bottom bar, or a slim left/right dock at Steam-overlay width. No dashboards, no nested boxes inside the HUD, no diagnostic overlay, no floating sub-panels.
> - **Companion window is one surface, no tabs, no side menu, no drawer.** Position and Feel & fit are pill-button dropdowns in a toolbar row; presets are the primary surface; support lives in a flat footer.
> - **Four presets plus Manual, density-mapped to Valve Level 1-4.** Minimal (L1), Standard (L2), Advanced (L3), Full (L4), plus Manual. Manual starts as a copy of whichever curated preset was last active — never empty. Spiritual parallel to the Valve Steam Deck overlay mental model, not a literal clone.
> - **Six positions.** Top 1-line, Top 2-line, Bottom 1-line, Bottom 2-line, Left dock, Right dock. Selected via a flyout opened from the Position pill button, not a persistent card.
> - **HUD metrics stay restricted.** FPS (+ avg, 1% low, 0.1% low), frametime, CPU (% / temp / power), GPU (% / temp / clock / power / fan), RAM, VRAM, battery (composite percent + rate + time render via `FormatBatteryCompact`, single MetricId — see §5.6), refresh rate, total package power, device temp. Latency stays hidden until a real source exists. Frame generation count stays hidden; Intel-PresentMon FG *detection* is allowed as a boolean. Vendor-gated metrics (GPU temp/clock/power/fan, device temp, total power) render `--` when the device does not expose them.
> - **Missing data shows `--`.** Never fake FPS from DWM, D3DKMT, or timers. Never fake battery rate or time remaining. Never assign `RefreshRateHertz` when `dm.dmDisplayFrequency <= 1`.
> - **Hide/show reliability is launch-based by design.** A handheld button mapped to launch `HHAPulse.Overlay.exe --toggle` (or `--next-preset`, `--off`, `--menu`) writes a command file and signals a named event that the running instance is listening on (`HotkeyService.cs:29-55`). This is intentional architecture, not a stopgap, and is more resilient than Win32 `RegisterHotKey` against sleep/resume, fullscreen-exclusive games, and focus-steal. Latency `Stopwatch` instrumentation, `PowerModeChanged` defense, and a tray-icon emergency kill path are explicit follow-ups (§10 #13-#15) — they are not in this milestone.
> - **No marketing copy inside the product.** Editorial copy lives in the Store listing and `/premium` page only. The companion window surface is status + knobs + status.

This `CLAUDE.md` revision lands in a separate change, not in this spec file. The §10 follow-ups stay open until they are explicitly scheduled.

## 10. Open questions

1. **Steam-overlay dock width.** "Steam level size" for the left/right docks needs a concrete value bound to the new `HhapSideDockWidth` token. Measure against the Steam in-game overlay's notification strip and propose a token value. *Owner:* Claude during implementation; verify with a screenshot comparison in the Plan execution phase.
2. **Inferred hex values.** `brand-scout` flagged that the handheldally.com palette was inferred from rendered descriptions, not picked from real pixels. Before the tokens land in `HhapTheme.xaml`, pull a real browser screenshot of the homepage and a `/premium` page and pick colour values with a picker. *Owner:* Claude, pre-implementation.
3. **Brand-font follow-up.** Plan B ships with `Segoe UI` + `Consolas`. If a later brand-font pass is approved, verify the handheldally.com type stack and packaging/licensing requirements before introducing bundled fonts. *Owner:* follow-up, not Plan B.*
4. **Logo asset.** Need a real SVG/PNG of the handheld-ally mark for the top bar — the 40px favicon is too small. *Owner:* jan@freelax.cz to provide, or brief a designer.
5. **HHA Pulse product page on handheldally.com.** `/pulse`, `/hha-pulse`, `/apps`, `/ecosystem`, `/devices` all 404. Is the page built behind login, or not built yet? If not built, the Store listing copy captured here (see §12) should be ported to a page on handheldally.com too, for consistency. *Owner:* jan@freelax.cz.
6. **Shadows.** `winui3-modern-patterns.md` flags that in-flow card shadows require `ThemeShadow.Receivers` wiring or composition plumbing. The design uses no card shadows by default (hairline strokes instead), so this should not block implementation — but prototype `HhapPresetCardStyle`'s selected state early to confirm the cyan border + glow reads without needing a shadow.
7. **Factory default Windows scale unconfirmed** for Legion Go, Legion Go S, Claw A1M, Claw 8 AI+, AYANEO Kun, and OneXPlayer X1 (marked `Win-rec` in the matrix). Confirm on real hardware or from an authoritative OEM statement before a PR that depends on these being exact can merge. *Owner:* Claude, during implementation verification phase.
8. **Steam Deck running Windows (Bazzite / dual-boot).** A real-but-unsupported-by-Valve case. The same 1280×800 canvas at 100% works, but some users bump to 125% or 150% for tappable Windows controls, which drops effective to 1024×640 or 853×533 — the latter is below our `880 × 500` minimum. *Are Bazzite/Steam-Deck-Windows users in scope for this milestone?* If yes, the minimum needs to drop further and `xs` needs another sub-tier. *Owner:* jan@freelax.cz.
9. **Prompt injection in WebFetch results.** Both `brand-scout` research tasks surfaced forged `<system-reminder>` blocks inside WebFetch responses — five events total, across `handheldally.com`, Wikipedia, and timeout pages. The injection appears to be coming from the WebFetch proxy path rather than the target sites. Ignored in every case; flagged here so future research tasks know to expect it. If this continues, escalate — it is an actual supply-chain signal for the agent harness, not a one-off glitch.
10. **Microsoft Store packaging strategy.** Per §2.1 the final product must be Store-submittable, but `memory-bank/best_practices/code_signing.md:3` explicitly notes the current architecture was not Store-designed. Choose between MSIX with bundled service, sparse package + side-by-side installer for the elevated capture service, or a non-Store distribution channel. Blocking for launch. *Owner:* jan@freelax.cz with Claude support.
11. **Active refresh rate bug — `DisplayCollector.cs:23-30`.** Today the collector unconditionally assigns `snapshot.Display.RefreshRateHertz = dm.dmDisplayFrequency` and raises `MetricFlags.Display`, even when `dm.dmDisplayFrequency <= 1` (Windows hardware-default sentinel). The HUD therefore renders `1 Hz` instead of `--`. Fix: gate the assignment and the flag behind `if (dm.dmDisplayFrequency > 1)`. Tracked in the same plan that produced this spec; landing in a separate teammate's PR.
12. **Battery health / cycle count display deferred.** `BatteryCollector.cs:182-199` already populates `HealthPercent`, `CycleCount`, `DesignCapacityWattHours`, `CurrentCapacityWattHours` when the hardware exposes absolute mWh capacity. No HUD card consumes them today. Deferred to a future "Battery detail" surface (roadmap).
13. **`HotkeyService` latency `Stopwatch` instrumentation deferred.** Per §5.7.3, the in-process signal path at `HotkeyService.cs:96-120` → `MainWindow.xaml.cs:51-71` is unmeasured. Add `Stopwatch` end-to-end instrumentation in a follow-up to validate the < 100 ms commitment.
14. **Tray-icon emergency kill path deferred.** Greenfield: zero references to `NotifyIcon` / `TrayIcon` / `Shell_NotifyIcon` / `System.Drawing.Icon` in the overlay tree. The companion window's "Overlay off" chip is the current fallback. Adding a tray icon is a follow-up.
15. **`SystemEvents.PowerModeChanged` defense layer deferred.** The launch-based architecture does not strictly need it (per §5.7.1), but adding a `PowerModeChanged` listener as a precaution against hung listener tasks would close a theoretical edge case. Follow-up.
16. **Document intentional launch-based architecture in `CLAUDE.md`.** The current `CLAUDE.md` does not explain why `HotkeyService` is launch-based instead of `RegisterHotKey`. Add a paragraph explaining the choice (per §5.7.1) so future contributors do not "fix" it back to Win32 hotkeys. Tracked as a `CLAUDE.md` edit, not part of this spec file.

## 11. Success criteria

A reviewer opening the companion window for the first time can:

1. See, within two seconds, whether the overlay is on, where it will appear, and which preset is active.
2. Switch the HUD position in one click and see the on-screen highlight update immediately.
3. Switch preset in one click and see the HUD rebuild live.
4. Enter Manual mode and toggle any tracked metric without leaving the current surface.
5. Find the "Copy support report" button without opening any menu.
6. Finish their first configuration session in under ten seconds.

Non-success:
- A reviewer needing to open a tab, drawer, or flyout to reach any of the above.
- A reviewer being dropped into a wizard, composer, or build-from-scratch flow to reach Manual.
- Any surface with nested card-in-card.
- FPS ever showing a fabricated value.

## 12. Store listing copy (captured for later)

Saved here so the Microsoft Store listing milestone can pull it without re-inventing the line.

**Product name:** HHA Pulse
**Tagline:** *Stop glancing. Just play.*
**Short description:** *A slim bar that whispers FPS, CPU, GPU, RAM, battery and display Hz. No wizard, no preset carousel — the ally that already knows what matters. Built by a fellow tinkerer.*
**Long description seeds:**
- "One in-game HUD. Four presets plus Manual. Top, bottom, or Steam-width side dock."
- "Capture is handled by a bundled elevated service. No DLL injection, no game memory reads, no analytics. Everything stays local."
- "Built by a fellow tinkerer. The community-powered platform for handheld PC gaming optimization — now with an overlay that doesn't nag."
- Footer line: *"Thanks for tipping the jar. This is our home. This is Handheld Ally."*

## 13. Responsive design — handheld constraint

**This is a handheld-first product.** The companion window must be usable on the same device it configures. Desktop reviewers are the secondary audience. "Responsive" here does not mean mobile-web responsive; it means the WinUI 3 window must render and be fully operable on every handheld PC HHA Pulse targets, at the DPI scales those devices ship with.

### 13.0 No hardcoded dimensions — ever

The entire layout must express size and spacing through the WinUI 3 intrinsic-sizing system, not through inline width/height numbers. This is non-negotiable for this project and is captured in memory as `feedback_fluid_design_no_fluff.md`.

**XAML rules:**

- `Grid.ColumnDefinitions` and `Grid.RowDefinitions` use `*`, `Auto`, or `MinWidth`/`MaxWidth` with tokens — never a fixed dip value like `Width="400"`.
- Any `FontSize` must reference a theme-ramp token via `{ThemeResource}`. Example: `FontSize="{ThemeResource HhapFontSizeLead}"`. Literal numbers are forbidden except inside `HhapTheme.xaml` itself, where the tokens are defined.
- Any `Padding`, `Margin`, or `Spacing` must reference the `HhapSpace1..6` token scale via `{StaticResource}`. No inline `Padding="12,8,12,8"` unless it references tokens.
- Any `Brush` must reference a named brush token via `{ThemeResource}`. No inline `#RRGGBB` or `Color` values.
- `UserControl` / `Page` / `Grid` roots do **not** set `Width` or `Height`. They inherit from their host.
- `VisualStateManager` + `AdaptiveTrigger` is the responsive mechanism. No code-behind calculating ActualWidth and swapping layouts manually.
- `MinWidth` / `MaxWidth` caps are permitted on content containers where editorial line-length matters (e.g. the about-pane caps text at `MaxWidth="640"` dip) but must reference a token.
- **The only place a hardcoded dip value is permitted is `AppWindow.MinSize = new SizeInt32(880, 500)`** — the window-edge backstop. Everywhere else, tokens and star-sizing.

**Mockup (CSS) rules for the clickable comp at `pulse/mockups/index.html`:**

- Type scale via `clamp(min, preferred, max)` in `rem`.
- Layout via `grid-template-columns: minmax(0, 1fr) minmax(0, 1fr)`.
- Gaps and padding via `var(--s-*)` tokens backed by `clamp()`.
- Colors via `color-mix(in srgb, …)` where alpha mixes are needed, referencing the `--pulse-*` tokens.
- Responsive via `container-type: inline-size` + `@container pulse (max-width: 56rem)` queries — not `@media`.
- No pixel values outside the root token declarations.

Any PR that introduces a new hardcoded dimension must be rejected in review unless it references a token added in the same PR.

### 13.1 Target device matrix

Sourced from `docs/superpowers/research/handheld-device-matrix.md`. Each row below cites a vendor/Wikipedia source. `handheldally.com/devices` is login-gated and not a usable anonymous source; the research report falls back to first-party spec pages and Wikipedia. Cells marked `(Win-rec)` are computed Windows-recommended scales for devices where the OEM did not publish a factory default — verify on hardware before making any decision that depends on an exact scale.

| Device | Year | Screen | Native | Default scale | Effective |
|---|---|---|---|---|---|
| Steam Deck LCD | 2022 | 7.0" 16:10 | 1280 × 800 | 100% | **1280 × 800** |
| AYANEO Air | 2022 | 5.5" 16:9 | 1920 × 1080 | 200% (Win-rec) | **960 × 540** |
| AYANEO 2S | 2023 | 7.0" 16:10 | 1920 × 1200 | 150% (Win-rec) | **1280 × 800** |
| ROG Ally Z1 Extreme | 2023 | 7.0" 16:9 | 1920 × 1080 | **150% (factory)** | **1280 × 720** |
| Lenovo Legion Go | 2023 | 8.8" 16:10 | 2560 × 1600 | 200% (Win-rec) | **1280 × 800** |
| Steam Deck OLED | 2023 | 7.4" 16:10 | 1280 × 800 | 100% | **1280 × 800** |
| AYANEO Kun | 2024 | 8.4" 16:10 | 2560 × 1600 | 200% (Win-rec) | **1280 × 800** |
| GPD Win 4 (2024) | 2024 | 6.0" 16:9 | 1920 × 1080 | 200% (Win-rec) | **960 × 540** |
| GPD Win Mini (2024) | 2024 | 7.0" 16:9 | 1920 × 1080 | 150% (Win-rec) | **1280 × 720** |
| OneXPlayer X1 | 2024 | 10.95" 16:10 | 2560 × 1600 | 150% (Win-rec) | **1707 × 1067** |
| MSI Claw A1M | 2024 | 7.0" 16:9 | 1920 × 1080 | 150% (Win-rec) | **1280 × 720** |
| ROG Ally X | 2024 | 7.0" 16:9 | 1920 × 1080 | **150% (factory)** | **1280 × 720** |
| Lenovo Legion Go S | 2025 | 8.0" 16:10 | 1920 × 1200 | 150% (Win-rec) | **1280 × 800** |
| MSI Claw 8 AI+ | 2025 | 8.0" 16:10 | 1920 × 1200 | 150% (Win-rec) | **1280 × 800** |

**Market clusters into four effective-canvas anchors:**

- **960 × 540** — 2 devices (AYANEO Air, GPD Win 4) — the 5.5–6" floor
- **1280 × 720** — 4 devices (ROG Ally, Ally X, Claw A1M, GPD Win Mini) — the 7" 16:9 cluster
- **1280 × 800** — 7 devices (Steam Deck LCD/OLED, AYANEO 2S/Kun, Legion Go/Go S, Claw 8 AI+) — the 16:10 plurality
- **1707 × 1067** — 1 device (OneXPlayer X1) — the 10.95" tablet-class outlier

**The binding constraint is vertical, not horizontal.** Nine of fourteen devices deliver either 720 or 540 effective pixels of height; after subtracting the Mica title bar and the default Windows taskbar (combined chrome ≈ 80 effective px on the 16:9 cluster), **usable content height on the 16:9 cluster is ~640 px**, not 720. Every section above the fold must fit inside 640 px, or scroll within an obvious "more below" affordance.

### 13.2 Effective-pixel breakpoints

The WinUI 3 layout system works in **effective pixels**, not physical. Each device's native resolution is divided by its OS DPI scale to get the effective viewport the XAML sees. The companion window's layout rules are expressed against effective pixels, matching the tier names in the research report.

| Tier | Width range | Layout posture | Covers |
|---|---|---|---|
| **`xs`** | < 1024 effective wide | Single column. Toolbar row wraps so the Position pill button sits above the Feel-&-fit pill. Preset row becomes 2 × 2. Event line elides to `timestamp · pid · exe`. | AYANEO Air, GPD Win 4 |
| **`sm`** | 1024–1279 effective wide | Toolbar row stays single-line (Position + Feel-&-fit pill buttons side-by-side). Preset row stays 4-up. Catches users who drag the window narrower than any shipping handheld, plus tight docked panes. | No shipping handheld today |
| **`md`** | 1280–1599 effective wide | **Intended layout.** Topbar row + toolbar row + 4-up preset row + event line + footer all visible above the fold. | 11 of 14 handhelds (both 1280×720 and 1280×800 clusters) |
| **`lg`** | ≥ 1600 effective wide | Intended layout with extra horizontal whitespace. The canvas gains side margins; the preset row stays 4-up; the window does not balloon. | OneXPlayer X1, desktop/laptop, external monitors |

**Hard window minimum:** `MinWidth = 880 dip, MinHeight = 500 dip`. Enforced at the `AppWindow` level. Below the minimum the window clips — the layout does not re-flow further. Rationale: `xs` covers 960 × 540 comfortably, but WinUI 3 windows need a little slack below the comfortable floor so drag-resize doesn't snap awkwardly. 880 × 500 sits just below 960 × 540 and gives the user room to breathe without breaking the layout below `xs`.

### 13.3 Adaptive layout mechanism

Use a `VisualStateManager` on the root `Grid` of `CompanionView.xaml` with four named states and `AdaptiveTrigger`s:

```xml
<VisualStateManager.VisualStateGroups>
  <VisualStateGroup x:Name="CompanionAdaptive">
    <VisualState x:Name="Lg">
      <VisualState.StateTriggers>
        <AdaptiveTrigger MinWindowWidth="1600" />
      </VisualState.StateTriggers>
    </VisualState>
    <VisualState x:Name="Md">
      <VisualState.StateTriggers>
        <AdaptiveTrigger MinWindowWidth="1280" />
      </VisualState.StateTriggers>
    </VisualState>
    <VisualState x:Name="Sm">
      <VisualState.StateTriggers>
        <AdaptiveTrigger MinWindowWidth="1024" />
      </VisualState.StateTriggers>
      <VisualState.Setters>
        <Setter Target="EventLine.FontSize" Value="{ThemeResource HhapFontSizeMicro}" />
      </VisualState.Setters>
    </VisualState>
    <VisualState x:Name="Xs">
      <VisualState.StateTriggers>
        <AdaptiveTrigger MinWindowWidth="0" />
      </VisualState.StateTriggers>
      <VisualState.Setters>
        <Setter Target="ToolbarRow.(StackPanel.Orientation)" Value="Vertical" />
        <Setter Target="PresetRow.ItemsPanel" Value="{StaticResource PresetGrid2x2}" />
        <Setter Target="SupportActionsGrid.(Grid.ColumnDefinitions)" Value="{StaticResource SupportActions2x2}" />
        <Setter Target="EventLine.Text" Value="{x:Bind ViewModel.EventLineShort, Mode=OneWay}" />
      </VisualState.Setters>
    </VisualState>
  </VisualStateGroup>
</VisualStateManager.VisualStateGroups>
```

WinUI 3's `AdaptiveTrigger` evaluates top-down on `MinWindowWidth` — list from largest to smallest. This is the native adaptive pattern; no custom measure/arrange needed. `EventLineShort` is a second view-model property that renders only `timestamp · pid · exe` for `xs`, without frametime and sample rate. Every `FontSize` setter in this state group references a `{ThemeResource}` font-size token (`HhapFontSizeLead`, `HhapFontSizeBody`, `HhapFontSizeMicro`, …) — never a literal number — per §13.0.

### 13.4 Touch, input and accessibility on handhelds

Handheld PCs are used in at least three input modes: touchscreen, D-pad/joystick with on-screen focus, and docked keyboard/mouse. The companion must support all three.

- **Touch targets.** Every interactive element has a minimum hit region referencing the `{StaticResource HhapTouchTargetMin}` token per Microsoft's Fluent accessibility guidance. Preset cards and position pill buttons already exceed it; slider thumbs and the topbar icon buttons (minimise / close) reference the same token rather than a literal dip value.
- **Gamepad navigation.** `CompanionView` sets `XYFocusKeyboardNavigation="Enabled"` and `IsTabStop="True"` on every preset card, pill button, slider, and footer action. Tab order follows reading order: topbar chips → toolbar pill buttons (Position → Feel & fit) → preset cards → Manual link / sheet (if open) → support footer.
- **Focus visuals.** Override `ControlFocusVisualPrimaryBrush` to `HhapAccent` and `ControlFocusVisualSecondaryBrush` to `HhapBg` so the focused element has a cyan ring that is visible on dark-slate surfaces.
- **Font sizes on handheld.** All `FontSize` references resolve through `{ThemeResource}` tokens defined in `HhapTheme.xaml` (`HhapFontSizeLead`, `HhapFontSizeBody`, `HhapFontSizeMono`, `HhapFontSizeMicro`, …). Tier-specific overrides happen via `VisualState` setters that swap one token for another, never via literal numbers.
- **Safe-area inset.** Handheld screens have curved/rounded corners on some devices (Legion Go edge, ROG Ally corner). The `CompanionView` root padding references `{StaticResource HhapSpace5}` so nothing important touches the corner zone — no inline dip values.

### 13.5 Verification checklist before merge

Before the redesign PR can merge, physically test or emulate the companion window at **each** of these effective viewports, chosen to hit every tier and every real-device cluster in the matrix:

- `1920 × 1080` effective — desktop baseline (`lg`)
- `1707 × 1067` effective — OneXPlayer X1 at 150% (`lg`)
- `1366 × 768` effective — laptop baseline (`md`)
- `1280 × 800` effective — Steam Deck, Legion Go family, Claw 8 AI+, AYANEO 2S/Kun (`md`, the 16:10 plurality)
- `1280 × 720` effective — ROG Ally / Ally X / Claw A1M / GPD Win Mini (`md`, the 16:9 cluster — **binding constraint**)
- `1024 × 600` effective — tight docked pane (`sm`)
- `960 × 540` effective — AYANEO Air / GPD Win 4 (`xs`, the floor)
- `880 × 500` effective — `MinWidth/MinHeight` floor (must not clip controls)

At each viewport, confirm:

1. **No horizontal scroll.** Anywhere. Ever.
2. **Vertical fold.** On the `md-720` sub-cluster (ROG Ally et al.), the topbar, toolbar pill buttons, preset row, event line, and support footer must all be visible above the 640 px fold, or the "more below" affordance must be visible before the fold.
3. **Touch targets** meet `{StaticResource HhapTouchTargetMin}` for every preset card, pill button, slider thumb, chip, and icon button.
4. **Gamepad focus.** Navigating with a connected Xbox controller reaches every interactive element in reading order.
5. **No clipping.** Preset card names, pill-button labels, chip text, and the event line are either fully visible or cleanly elided (never cut off mid-word).
6. **No double-scrollbar.** The page scrolls; the preset row does not scroll independently.

Use Windows' `Display → Scale` dropdown on physical hardware where available. For emulation, `Window.ExtendsContentIntoTitleBar = true` plus a debug toolbar that can force a specific `AppWindow.Size` is sufficient to exercise each viewport.

## 14. Non-goals

- Redesigning the capture service UI. The service has no UI.
- Building the Founder/Premium tier. Gold is reserved in tokens; no surface consumes it in this milestone.
- Supporting light mode.
- Supporting any language other than English.
- Adding new telemetry metrics.
- Re-doing the Game Bar widget (separate product).
