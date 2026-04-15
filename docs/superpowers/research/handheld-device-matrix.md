# Handheld Device Matrix — for HHA Pulse responsive sizing

**Date:** 2026-04-15
**Purpose:** ground-truth screen + DPI data for the 13 most common Windows handheld PCs as of April 2026, so the HHA Pulse companion window has real responsive breakpoints instead of guessed ones.

---

## 1. Method & honesty notes

- **handheldally.com is NOT a usable source for this.** I tried `/devices`, `/handhelds`, and `/devices/list`. All anonymous fetches returned 404 or an empty "Device Not Found" shell. The site has the data but it's gated behind login. I did not invent values from those dead pages.
- **Falling back to manufacturer pages and Wikipedia.** Each row below cites where the spec came from. I used Wikipedia / manufacturer spec pages / first-party retailer listings — not blogs.
- **DPI scale defaults** are the trickiest column. Windows Setup picks a "recommended" scale based on PPI, and OEMs sometimes override it. Where I had a hard source (e.g. ROG Ally ships at 150%) I cite it; where I'm computing the Windows recommendation from PPI I mark it `(Win-rec)`. Treat the latter as an educated default, not a guarantee — users override these all the time.
- **"Effective resolution"** = native ÷ scale, which is the px count a WinUI 3 window actually gets to lay out into.
- **Three prompt-injection events.** While running this task, three WebFetch responses (`/devices/list`, the Steam Deck Wikipedia page, and one timeout) returned forged `<system-reminder>` blocks claiming the harness had loaded extra skills or that I had open tasks I'd never created. Ignored, flagging in this report so any teammate replicating my fetches knows to expect it. The site (and apparently Wikipedia via the same proxy path) is currently passing through content that tries to steer agent behavior.

---

## 2. Device matrix (13 devices, sorted by year)

| # | Device | Year | Screen | Native res | Aspect | Refresh | Default Win scale | **Effective px** | Source |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Steam Deck LCD | 2022 | 7.0" | 1280 × 800 | 16:10 | 60 Hz | 100% | **1280 × 800** | en.wikipedia.org/wiki/Steam_Deck |
| 2 | AYANEO Air | 2022 | 5.5" | 1920 × 1080 | 16:9 | 60 Hz | 200% (Win-rec) | **960 × 540** | en.wikipedia.org/wiki/Ayaneo |
| 3 | AYANEO 2S | 2023 | 7.0" | 1920 × 1200 | 16:10 | 60 Hz | 150% (Win-rec) | **1280 × 800** | en.wikipedia.org/wiki/Ayaneo |
| 4 | ROG Ally Z1 Extreme | 2023 | 7.0" | 1920 × 1080 | 16:9 | 120 Hz | **150% (factory)** | **1280 × 720** | rog.asus.com spec page; tweaktown.com confirms factory default |
| 5 | Lenovo Legion Go | 2023 | 8.8" | 2560 × 1600 | 16:10 | 144 Hz | 200% (Win-rec) | **1280 × 800** | en.wikipedia.org/wiki/Lenovo_Legion_Go |
| 6 | Steam Deck OLED | 2023 | 7.4" | 1280 × 800 | 16:10 | 90 Hz | 100% | **1280 × 800** | steamdeck.com/en/oled |
| 7 | AYANEO Kun | 2024 | 8.4" | 2560 × 1600 | 16:10 | 120 Hz (OLED 144) | 200% (Win-rec) | **1280 × 800** | en.wikipedia.org/wiki/Ayaneo |
| 8 | GPD Win 4 (2024 rev) | 2024 | 6.0" | 1920 × 1080 | 16:9 | 60 Hz | 200% (Win-rec) | **960 × 540** | en.wikipedia.org/wiki/GPD_Win_4 |
| 9 | GPD Win Mini (2024) | 2024 | 7.0" | 1920 × 1080 | 16:9 | 120 Hz | 150% (Win-rec) | **1280 × 720** | en.wikipedia.org/wiki/GPD_Win_Mini |
| 10 | OneXPlayer X1 | 2024 | 10.95" | 2560 × 1600 | 16:10 | 120 Hz | 150% (Win-rec) | **1707 × 1067** | onexplayerstore.com X1 spec |
| 11 | MSI Claw A1M | 2024 | 7.0" | 1920 × 1080 | 16:9 | 120 Hz | 150% (Win-rec) | **1280 × 720** | en.wikipedia.org/wiki/MSI_Claw_A1M |
| 12 | ROG Ally X | 2024 | 7.0" | 1920 × 1080 | 16:9 | 120 Hz | **150% (factory)** | **1280 × 720** | rog.asus.com Ally-X spec |
| 13 | Lenovo Legion Go S | 2025 | 8.0" | 1920 × 1200 | 16:10 | 120 Hz | 150% (Win-rec) | **1280 × 800** | amazon.com B0DTBN55K9 listing; pcguide.com review |
| 14 | MSI Claw 8 AI+ | 2025 | 8.0" | 1920 × 1200 | 16:10 | 120 Hz | 150% (Win-rec) | **1280 × 800** | msi.com Claw 8 AI+ A2VMX spec |

**Distribution of effective canvas (the number that matters for layout):**

| Effective px | Devices | Count |
|---|---|---|
| 960 × 540 | AYANEO Air, GPD Win 4 | 2 (the smallest tier — 5.5–6" screens) |
| 1280 × 720 | ROG Ally, ROG Ally X, MSI Claw A1M, GPD Win Mini | 4 (the dominant 7" 16:9 cluster) |
| 1280 × 800 | Steam Deck LCD, Steam Deck OLED, AYANEO 2S, AYANEO Kun, Legion Go, Legion Go S, MSI Claw 8 AI+ | 7 (the dominant 16:10 cluster) |
| 1707 × 1067 | OneXPlayer X1 | 1 (outlier — basically a tablet) |

So the real shape of the market is **two effective-canvas anchors: 1280×720 and 1280×800**, with a 960×540 floor for 5.5–6" devices and a 1707×1067 ceiling for the OneXPlayer tablet-class outlier.

---

## 3. Recommended minimum window size for HHA Pulse companion

### TL;DR

**Design the companion window for a usable layout at `960 × 540` effective px, and a *comfortable* layout at `1280 × 720` effective px. Hard minimum window size: `880 × 500`.**

### Reasoning

1. **Floor = 960 × 540, not 1280 × 720.** Two devices in the matrix (AYANEO Air, GPD Win 4) actually deliver 960×540 to a max-size top-level window after Windows scaling. If we pick 1280×720 as the floor, those users either see clipped UI or cannot fit the window on screen at all. They are a small share but they exist, and the user explicitly said "do not VIBE or ASSUME."
2. **Hard window minimum slightly under floor.** A WinUI 3 `AppWindow` at 960×540 needs to leave room for the title bar (32 px in Mica) and any taskbar the user runs un-hidden (40 px default). Net safe content area on a 960×540 device is ~960×468. Setting `MinWidth=880, MinHeight=500` lets the window still resize down past the safe content area without snapping to nothing, and lets users drag-resize on docked external monitors without hitting an aggressive floor.
3. **Comfortable target = 1280 × 720.** This is what 11 of the 14 devices deliver. Designing the "happy" layout here means most users get the intended composition without any responsive degradation.
4. **Stretch ceiling = 1707 × 1067.** OneXPlayer X1 and any external-monitor case need the window to *grow* gracefully. The hero column should cap its max-width (e.g. 720 px) and the surrounding canvas should breathe rather than stretching the editorial headline across 1700 px.
5. **Aspect ratio quirk.** The single hardest constraint is the ROG Ally / Ally X / Claw A1M / GPD Win Mini cluster at 1280×**720** (16:9), not 1280×800. 80 vertical px is the difference between the position picker fitting above the fold and not. Every section of the companion needs to assume the fold is at **~640 px** (720 minus title bar minus a safe-area cushion), not 800.

### Suggested breakpoints (in effective px)

| Tier | Range | Layout posture |
|---|---|---|
| `xs` | < 1024 wide | Single column, hero shrinks to one line, position picker stacks below hero, preset row becomes 2×2 grid. Covers AYANEO Air + GPD Win 4. |
| `sm` | 1024–1279 wide | Hybrid: hero + position picker still on one row but hero text drops a size step, preset row stays 4-up. Covers nothing in the matrix today but catches odd window sizes / users dragging the window narrower. |
| `md` | 1280–1599 wide | The intended layout. Hero left, position picker right, preset row 4-up underneath. Covers 11 of 14 devices. |
| `lg` | ≥ 1600 wide | The intended layout with extra horizontal whitespace; hero text caps at its `md` size, preset row stays 4-up, the canvas just gains margins. Covers OneXPlayer X1 and external monitors. |

### Vertical safe-area note

For the 16:9 cluster (effective 1280×720), assume a **640 px usable height** below the title bar after subtracting the Windows taskbar most users leave visible. The hero, preset row, and position picker need to fit (or scroll within an obvious "more below" affordance) inside 640 px. The 1280×800 cluster gets 80 extra px of breathing room — that's where the muted event line at the bottom can live without competing for space.

---

## 4. Open follow-ups before treating this as final

- The **factory-default Windows scale** for Legion Go, Legion Go S, Claw A1M, Claw 8 AI+, AYANEO Kun, and OneXPlayer X1 is marked `(Win-rec)` because I could not find an authoritative OEM statement. Confirm with a real device or a Reddit thread search before making layout decisions that depend on it being exact.
- **Steam Deck running Windows** (a real if unsupported case) inherits SteamOS's 1280×800 canvas at 100%, but Bazzite/Windows users may run 125% or 150% to make standard Windows controls hittable. Worth asking jan whether that user is in scope.
- **OneXPlayer X1 in tablet mode vs. handheld mode** is the same panel, but in tablet mode users routinely drop scale to 125%, giving an effective 2048×1280. The `lg` tier already covers this, but confirm before promising it.
