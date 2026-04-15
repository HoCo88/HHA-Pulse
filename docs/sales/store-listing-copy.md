# HHA Pulse — Microsoft Store listing copy

**Status:** draft · captured 2026-04-15
**Source:** brainstorm round 01 with jan@freelax.cz; voice extracted from handheldally.com (see `docs/superpowers/research/brand-handheldally.md`).
**Policy:** This copy lives *outside* the companion product surface. It belongs to the Store listing and the handheldally.com product page. It must **not** appear inside the companion window as editorial copy — that is reserved for status, controls, and support.

---

## Product metadata

- **Product name:** HHA Pulse
- **Short name / search:** Pulse, HHA Pulse
- **Category:** Gaming utilities
- **Tagline (≤ 40 chars):** *Stop glancing. Just play.*
- **Short description (≤ 100 chars):** *A slim overlay that whispers FPS, CPU, GPU, RAM, battery and display Hz.*

## Long description seeds

Each block below is standalone and can be reordered or trimmed to fit the Store's character limits.

### Opening paragraph

> Stop glancing. Just play. HHA Pulse is a slim in-game overlay that whispers FPS, CPU, GPU, RAM, battery and display refresh rate — the numbers you needed three seconds ago, without a wizard or a preset carousel. Built for handheld PC gamers by a fellow tinkerer.

### Feature block — "What you see"

> - **One slim bar.** Top, bottom, or a Steam-width dock on the left or right. Choose in one click.
> - **Four presets plus Manual.** Minimal for screenshots, Standard for most players, Performance for tuning, Manual to toggle every tracked metric.
> - **Real FPS.** Captured by a bundled elevated service using PresentMon ETW — the same method RTSS uses — so the numbers match the frame buffer, not a CPU guess.
> - **Band-tier colours.** Green / yellow / red dots next to live metrics tell you at a glance whether your frame rate is stable, busy, or struggling. The same three-band system used across handheldally.com.

### Feature block — "What it respects"

> - **No DLL injection.** No graphics API hooks. No game memory reads. Pulse watches system counters and ETW events, nothing more.
> - **No analytics.** Every byte stays on your device.
> - **Runs on your handheld.** Designed for Steam Deck, ROG Ally and Ally X, Legion Go family, MSI Claw, AYANEO Air / 2S / Kun, GPD Win 4 and Win Mini, and OneXPlayer X1. The companion window adapts to each device's effective viewport — from 960×540 to 1707×1067.

### Founder framing (for the /premium page when built)

> Built by a fellow tinkerer. This is our home. This is Handheld Ally. If Pulse saves you an afternoon of wrangling RTSS, MSI Afterburner, and HWInfo — consider tipping the jar. Founder licenses unlock a glass-and-gold skin for the companion window and get a mention on the About pane.

## Voice rules

From `docs/superpowers/research/brand-handheldally.md`:

- Warm, tinkerer-grade, community-first. Never clinical, never cyberpunk-hardcore, never esports.
- Use "ally" language: *whispers*, *glances*, *fellow tinkerer*, *our home*.
- Emoji are OK in the Store listing and handheldally.com headings (🎯 🏆 🎮 💬 💡 🔧) but **never** in the HUD itself or the companion window chrome.
- Tabular numerals everywhere a number appears.
- Missing data shows `--`, never fabricated.

## Banned in the product surface

These belong in the Store listing, not in the companion window:

- The tagline *"Stop glancing. Just play."*
- The body paragraph *"A slim bar that whispers…"*
- The Founder framing *"This is our home. This is Handheld Ally."*
- The tip jar CTA.
- Any `<TextBlock>` over 2 lines that isn't a label for a control.

The companion window shows status, knobs, and support actions. That is all.

## Open questions

- **Store asset sizes.** Hero 2400×1200, square 300×300, wide 1240×465. Need the handheld-ally mark in SVG (currently only a 40px favicon is published).
- **Pricing surface.** 49 CZK / €1.99 at launch per `project_pricing.md`. Confirm with Microsoft Store regional pricing tier selection before submission.
- **Trailer.** 10–30 sec loop showing: HUD fading in during a game → user opens companion → clicks a preset → HUD rebuilds → user resumes game. Record on a real handheld, not a desktop.
