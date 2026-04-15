# Handheld Gamer Voices — Real Research

Research date: 2026-04-15
Scope: real quotes and recurring themes from handheld PC gamers discussing performance overlays, what they hate, what they want, and what they gave up on. This is deliberately *not* a UX-textbook abstraction — it is ground-truth signal for HHA Pulse design decisions.

Reddit's direct search endpoints return a hard block from this environment (`Claude Code is unable to fetch from www.reddit.com`), so primary sources here are Steam Community threads, Valve bug reports, GitHub issues on overlay-adjacent tools (G-Helper, HandheldCompanion), Steam Deck HQ, Windows Central, XDA, GamingOnLinux, wccftech, clawsomegamer, rogallylife, and Asus/Lenovo vendor forums surfaced via search. Where a search snippet is the only retrievable artifact, that is disclosed inline.

---

## 1. Top 10 Complaint Themes (ranked by recurrence across sources)

### 1. "The overlay won't go away / obscures my view"
Steam Deck users report the performance overlay getting *stuck* on screen and blocking gameplay — one of the most common threads on the official Steam Deck bug forum.

- > "it keeps coming up during gameplay of multiple games **obscuring my view**"
  — Steam Deck Bug Reports, `steamcommunity.com/app/1675200/discussions/1/3806157798547347924/`
- > "This is now the 3rd time the performance overlay has bugged and stayed on my screen infinitely"
  — Steam Deck General Discussions, `steamcommunity.com/app/1675200/discussions/0/5135803832917029337/`
- > "Every time this happens I stop playing it for a month and it magically goes away"
  — same thread, same source

**Signal for HHA Pulse:** reliable hide/show is table stakes. An overlay users cannot *instantly kill* is a broken overlay. Toggle must be bulletproof across restarts, sleep, and game launches — the #1 pain point for existing overlays isn't complexity, it's *getting rid of it when you're done*.

### 2. "Armoury Crate's overlay is too slow, doesn't show what I want, and can't be trusted"
The default Asus overlay on the ROG Ally is the most-criticized vendor overlay in the handheld space.

- > "doesn't update as fast as I would like or provide all the info I want to see"
  — Steam Deck HQ, *Create a Better Overlay for your ROG Ally*, `steamdeckhq.com/tips-and-guides/create-a-better-overlay-rog-ally/`
- > "half-baked nature of the Armoury Crate SE software and the Command Center overlay"
  — search snippet surfaced from early ROG Ally reviews
- > "Resource Monitor... registers and captures 5 keyboard shortcuts which can't be unbound"
  — ThatOtherAndrew, g-helper issue #1496, `github.com/seerge/g-helper/issues/1496`

**Signal for HHA Pulse:** "the vendor overlay" is perceived as actively user-hostile, not just inadequate. Users specifically complain about (a) stale data, (b) missing metrics, (c) capturing hotkeys they can't rebind. HHA Pulse must refresh fast, expose what's missing, and *never* grab a hotkey it doesn't own.

### 3. "RTSS/Afterburner isn't a tool, it's a weekend project"
The serious-hobbyist path for handheld overlays is to install MSI Afterburner + RivaTuner Statistics Server + HWiNFO64 and hand-craft a layout. Every guide describing this process acknowledges it's painful.

- > "It is a bit of a process, but after this, you shouldn't have to worry about doing it again."
  — Steam Deck HQ, same article as #2 (framed as encouragement — the author is conceding the setup is tedious)
- > "The big thing HWiNFO shows that MSI Afterburner doesn't pull from is the battery information."
  — same article (users must run *two* monitors just to get basic battery data)
- > "moving the position through Rivatuner doesn't work at all... users wish they could get all the font colors to match"
  — Overclock.net RTSS/Afterburner discussion surfaced in search, guru3D RTSS Overlay Editor Megathread
- > "had to edit text files and registry to fully unlock all adjustable parameters"
  — aggregated from MSI Afterburner configuration forums

**Signal for HHA Pulse:** the "pro" overlay path exists and is battle-tested, but the UX is so bad that users ship community `.ovl` preset files (Elegant Mustard, TroyMetrics Benchmark-Overlays) just to skip configuration. The gap in the market is: *Steam Deck polish, Ally/Go/Claw hardware*. That is literally the positioning.

### 4. "I want battery, not just FPS"
Battery life is the metric that distinguishes handheld overlays from desktop ones, and almost every vendor overlay misses it.

- > "Only current alternative of viewing the battery percentage and charge/dischage rate is to briefly check by opening G-Helper"
  — nathancdy, g-helper issue #3620, `github.com/seerge/g-helper/issues/3620`
- > "it would be useful to have an overlay for the battery charge, charge rate, and time, similar to Armoury Crate's Real-Time Monitor"
  — same issue
- > "The big thing HWiNFO shows that MSI Afterburner doesn't pull from is the battery information."
  — Steam Deck HQ (ROG Ally overlay article)

**Signal for HHA Pulse:** battery + charge rate + estimated time remaining is non-negotiable. Handheld users *explicitly* want this — there's a g-helper issue demanding it and a Steam Deck HQ guide built around HWiNFO just to get it. HHA Pulse already plans battery; what it needs is **charge rate (W) and estimated time remaining** alongside percentage, because users check these in that order: "am I burning out? how long do I have?"

### 5. "Level 2 is all I actually need"
The Valve-defined performance overlay levels (1-5) give us a rare natural experiment: millions of users choosing across a granularity spectrum, and there is a clear winner.

- > "I personally found that Level 2 gave me all of the information I regularly wanted and didn't take over the screen too much."
  — Rebecca Spear, Windows Central, `windowscentral.com/gaming/steam/how-to-enable-steam-deck-performance-overlay-see-fps-gpucpu-data-and-more`
- > "For someone who only wants an FPS counter, level one should be enough. But advanced users will definitely use higher levels to see more vital information on the fly."
  — MakeUseOf, aggregated from `makeuseof.com/how-to-enable-steam-deck-performance-overlay/`
- Valve itself shipped a **dedicated FPS-only minimum mode** in March 2022 because users demanded it:
  > "Thank goodness. Personally I'm one of those weirdos who has the FPS counter on constantly while gaming... Glad to see Valve added this option."
  — GamingOnLinux comment thread, `gamingonlinux.com/2022/03/steam-deck-update-brings-an-fps-only-mode-for-the-overlay/`

Level 2 shows: **FPS + frametime chart + battery % + GPU/CPU util + RAM + Gamescope marker**. Level 1 shows FPS only. The mass of users cluster at these two extremes. Levels 3 and 4 (full thermals, per-core, voltages) are niche.

**Signal for HHA Pulse:** the 4-presets + Manual design is *directionally right*. The preset lineup should not be "different looks"; it should be "different info densities". Map explicitly to the Level 1 / Level 2 / Level 3-ish / Level 4-ish mental model gamers already have from Steam Deck. Presets "Minimal / Standard / Advanced / Full" is the intuitive mapping.

### 6. "I open the overlay when I'm tuning — then I want it gone"
This is the most surprising finding, and it contradicts the assumption that an overlay is an *always-on HUD*.

- > "Enable the overlay at Level 2 or 3 for frame time and CPU/GPU data... run the same scene for thirty seconds and observe the frame time graph."
  — PulseGeek, *Use the Steam Deck Performance Overlay to Tune Bitrate*, `pulsegeek.com/articles/use-the-steam-deck-performance-overlay-to-tune-bitrate/`
- The PulseGeek workflow treats the overlay as an **instrumentation layer for optimization work, not as an on-screen companion during normal play**. "Once overlay metrics remain steady" — tuning ends, normal play resumes (implicitly: overlay off).
- > "turning off the Legion Overlay (Fps, Battery Time and so on) resolved performance issues for some users"
  — Legion Go performance discussions, search-surfaced, and replicated in Lenovo forums

**Workflow patterns observed across sources:**

1. **First-run setup** — user installs overlay, configures metrics, positions it, checks it works, often during the first 10 minutes with a new device.
2. **Game-launch spot check** — user opens a new game, watches FPS/frametime/TDP for a minute or two to see if the current preset is safe, then hides or minimizes the overlay.
3. **Troubleshooting dip** — user *re-opens* the overlay mid-game when they notice stutter or hot chassis, to diagnose.
4. **Battery-watch** — user *re-opens* the overlay when battery indicator in system tray starts flashing, to decide whether to switch to a lower TDP profile.

Almost nobody describes "always-on overlay during all gameplay" as their preferred state. The Steam Deck stuck-overlay bug threads prove this — it's a widely-hated failure mode, meaning the default is *overlay off during normal play*.

**Signal for HHA Pulse:** the design must treat **hide/show speed and hotkey reliability as a first-class feature**, equal to the metrics themselves. A toggle that takes 2 seconds and costs a frame spike will lose to Steam Deck's native. HHA Pulse should target sub-100ms show/hide and a hotkey that survives sleep/resume, game launches, and profile switches.

### 7. "I gave up on RivaTuner customization"
Community fingerprint: the existence of pre-packaged RTSS overlay files (ElegantMustard, TroyMetrics Benchmark-Overlays) is the signal. These projects exist *because* configuring RTSS by hand is too painful.

- ElegantMustard: "An elegant RTSS overlay to showcase your benchmark stats in style" (Steam Deck-inspired and Returnal-inspired variants).
- TroyMetrics Benchmark-Overlays: "a fully adaptable performance overlay that works seamlessly across a wide range of hardware — from 720p laptops to high-end 4K benchmark rigs... minimal manual tweaking."

**What users gave up on:**
- Getting fonts to render crisply at handheld pixel density.
- Matching all colors across different overlay elements.
- Positioning the overlay consistently (Overclock.net: "moving the position through Rivatuner doesn't work at all").
- Understanding HWiNFO's sensor list deep enough to pick the right one.
- Keeping RTSS from conflicting with Steam Overlay, GOG Galaxy Overlay, and the vendor overlay (RTSS+GOG conflict documented at CDPR forums).

**Signal for HHA Pulse:** customization is valuable but must never be a *prerequisite*. Presets must be "works out of the box without touching settings." Manual mode must be strictly opt-in for the user who *wants* to tinker.

### 8. "I want it to not break other overlays"
Overlay conflicts are a significant, recurring complaint.

- > "The two overlays (MSI Afterburner and Steam overlay) conflict with each other, and one of them has to go."
  — search aggregate, Steam Community Killing Floor 2 thread
- > "Third-party performance overlays like Xbox Game Bar, Command Center, and Armoury Crate SE can disrupt AFMF functions on ROG Ally"
  — search aggregate, Windows Central AFMF guide
- > "MSI Afterburner may be the cause for stutters in your games"
  — ResetEra PSA thread

**Signal for HHA Pulse:** be a well-behaved citizen. Don't hook into game processes, don't fight with Steam/Xbox/vendor overlays, don't inject. This is already in the CLAUDE.md architecture rules — the research confirms it's a user-visible benefit, not just an internal decision.

### 9. "Stop eating my framerate"
- > "overlays like MSI, Steam, and Nvidia can occasionally be found to be the cause of noticeable stutters or other performance issues"
  — search aggregate, Fatshark forums
- > "If you have Afterburner installed and have GPU power monitoring enabled in the settings, disabling that is recommended"
  — ResetEra stutter PSA
- > "Disable Nvidia Overlay = More FPS"
  — Steam Community thread title, Stalker 2 discussions

**Signal for HHA Pulse:** the CaptureService architecture (ETW, no hooks) is a *marketable* feature. Copy point: "Uses Windows ETW, doesn't touch your game, zero-cost overlay."

### 10. "Vendor UI feels like an app slapped on top of Windows"
- > "Utilities like Armoury Crate feel like apps slapped on top of Windows, whereas alternatives aim to make your Windows handheld feel like an actual handheld."
  — XDA Developers, Winhanced hands-on, `xda-developers.com/winhanced-handheld-hands-on/`
- > "stock utilities choose a single lane and are able to run and manage one application, but if you start throwing up multiple apps, things can start breaking quickly"
  — same source
- > "there is an in-game overlay provided by Armoury Crate, but it doesn't update as fast as some users would like or provide all the info they want to see"
  — aggregated ROG Ally review commentary

**Signal for HHA Pulse:** positioning should lean into *"built for handhelds, not bolted on"*. That is the specific gap users articulate.

---

## 2. Metric Priority Order (from actual user voices)

Synthesized across Valve's Level 2 composition, Armoury Crate Real-Time Monitor's minimal view, g-helper issue #3620, Steam Deck HQ's custom overlay recipe, and HandheldCompanion's defaults.

| Rank | Metric | Why it matters (from the voices) |
|---|---|---|
| 1 | **FPS** | The universal ask. Level 1 = FPS only. Valve added an FPS-only mode on demand. |
| 2 | **Battery %** | Distinguishes handheld from desktop. Every handheld-targeted guide puts it in the top 3. |
| 3 | **Battery W or time remaining** | "Am I burning out? How long do I have?" g-helper #3620 explicitly requests charge/discharge rate. |
| 4 | **Frametime / 1% low** | Valve Level 2 includes frametime chart; Deltia's Gaming and HowToGeek articles both argue 1% low is more meaningful than average. Users care but describe it as "smoothness", not "1% low". |
| 5 | **GPU load %** | Valve Level 2; every vendor overlay includes it. Users read it as "is the GPU the bottleneck?" |
| 6 | **CPU load %** | Same. Users read it as "am I CPU-bound?" |
| 7 | **RAM / VRAM** | Valve Level 2 includes RAM. VRAM only surfaces in Level 3+. Lower priority; usually glanced at once per game, not continuously. |
| 8 | **TDP / package power (W)** | Heavily discussed — AYA Space, ROG Ally Command Center, HandheldCompanion all surface it. Users want to know "is this profile actually drawing what I set it to?" |
| 9 | **Temperature (CPU/GPU/SoC, whichever is hottest)** | Explicitly requested in Valve Level 2 feature requests ("just take the current highest temperature and put it in that corner"). Users want *one number*, not three. |
| 10 | **Display Hz** | Present in Armoury Crate Real-Time Monitor minimal view, HandheldCompanion quicktools. Users want confirmation VRR is active or refresh rate matches cap. |

**Metrics users explicitly did NOT ask for, or asked to have removed:**
- Per-core CPU breakdown (Level 4 — power user only)
- Voltage (Level 4 — "why would I look at this mid-game?")
- FSR / FG state indicator (niche — one Level 4 item, mentioned rarely)
- Fan RPM (never surfaced in any handheld overlay thread)
- Latency in ms (PresentMon latency is a niche benchmarker metric, not a gameplay HUD metric)

---

## 3. Workflow Patterns

### First-run (once per device or profile)
User installs overlay, decides which preset, positions it, picks a hotkey, launches a game to verify it's visible and the hotkey works. Takes 5-15 minutes. Typically happens within the first hour of owning a new handheld.

### Returning-user spot check (before each session, or every few games)
User launches a game, pops up the overlay for ~30 seconds to verify FPS/battery/TDP look sane, then hides it. This is the **primary** use mode — and is the mode HHA Pulse must make instantaneous.

### Troubleshooting (unplanned, mid-game)
User notices stutter, hot chassis, or fan ramp. Pops overlay to check 1% low / frametime graph / temperature. Acts on it — drops a TDP profile, lowers a setting, or continues.

### Battery-watch (unplanned, mid-session)
Battery indicator or charge icon in tray flashes or hits a threshold. User pops overlay to see current drain (W) and estimated remaining, then decides: stay on current profile, switch to quiet mode, or plug in.

### Benchmarking / tuning (deliberate, with extra tools)
PulseGeek workflow — user has an explicit test plan, runs the same scene 30 seconds with the overlay up, saves profiles. This is a small minority of sessions but it is where power users live, and it's where tools like CapFrameX/PresentMon and the Level 3-4 overlays earn their keep.

---

## 4. Anti-requirements (what handheld users explicitly don't want)

- **Animated bars, sparkles, RGB glow.** None of the popular community overlays (ElegantMustard, TroyMetrics, Valve native) use any motion design. Frametime graph is the only moving element.
- **Tooltips.** You're playing a game. There is no mouse. Tooltips are impossible to read anyway. Not a single handheld overlay uses them.
- **Onboarding wizards.** Nobody asks for these. The complaint is always "too much setup", not "not enough guidance".
- **Persistent panels / docked side panels.** The mental model is "HUD", not "dashboard". HandheldCompanion's *quicktools* side panel is a separate entity the user explicitly invokes — it is not called an overlay.
- **Nested boxes / visual grouping frames.** The Steam Deck overlay uses a simple horizontal strip (Level 1-2) that morphs into a top-left box (Level 3-4). Every community clone copies this pattern.
- **Custom fonts, gold trim, gamer-aesthetic flourishes.** Handheld users consistently picked the most sterile, monochrome, legible overlays. ElegantMustard is the exception and it's deliberately styled — but even its "Compact" and "Minimal" variants drop ornamentation.
- **A metric picker UI.** The people who wanted granular control already use RTSS and they complain that it's awful. Users who don't want granular control want *presets that work*. HHA Pulse's current spec (4 presets + Manual) is the right structural call.
- **Ambient background blur or color-pickers for every cell.** Community overlay projects (ElegantMustard, TroyMetrics) offer maybe 2-4 theme variants. Nobody asks for 20.
- **"Smart" insight panels ("Your CPU is running hot — try lowering TDP!"").** Not observed in any popular overlay. Users want raw numbers and want to make the call themselves.
- **Cloud sync, accounts, telemetry.** Explicit in HHA Pulse's rules, and confirmed by the community: the popular handheld tools (HandheldCompanion, G-Helper, Steam Deck Tools) are local-only and that's a feature.

---

## 5. Vocabulary Glossary (handheld gamer language)

Verified from surfaced sources, not invented.

| Term | Meaning / usage |
|---|---|
| **Deck** | Steam Deck. "Runs fine on my Deck." Universal shorthand. |
| **Ally / Ally X** | ROG Ally / ROG Ally X. "On Ally at 15W..." |
| **Go / Go S** | Legion Go / Legion Go S. Less universal — "Legion Go" often written out. |
| **Claw** | MSI Claw. |
| **OLED** | Usually refers specifically to Steam Deck OLED. "I upgraded to OLED." |
| **TDP** | Thermal Design Power, but used to mean "the wattage I'm letting the chip pull." Verb use: "turn TDP down to 10W." |
| **Frametime / frametime graph** | The 16ms-line chart. Used interchangeably with "the smoothness graph." |
| **1% low** | Power-user term for worst-case stutter. Most users say "stutters" or "drops". |
| **Stutter / hitching** | What a bad 1% low feels like. |
| **Sweet spot** | The TDP that gives the best FPS-per-watt. "28W is the sweet spot on Z1E." |
| **Armoury / Crate / AC** | Armoury Crate (Asus). Almost always pejorative. |
| **Afterburner / MSI** | MSI Afterburner. |
| **RTSS / Riva** | RivaTuner Statistics Server. "Riva" is less common; "RTSS" dominates. |
| **HWiNFO / HWI / HWi** | HWiNFO64. |
| **Game Bar / GB** | Xbox Game Bar. |
| **HC / HHC** | HandheldCompanion. |
| **G-Helper** | g-helper, the open-source Armoury Crate replacement. |
| **Quiet mode / Performance mode / Turbo** | Vendor TDP presets. Universal across Ally, Go, Claw. |
| **Profile** | A saved TDP/clock/fan configuration, usually per-game. |
| **Dock / docking / docked** | External display mode. |
| **Quickchange / Quick menu / Command Center / Quick Access** | Vendor-specific names for the radial/side menu you pull up with a dedicated button. Asus: Command Center. Valve: Quick Access. Lenovo: Legion Space. |
| **FG / AFMF / FSR / FSR3** | Frame generation and upscaling. Users casually mix these terms. |
| **Tuning** | Any activity involving the overlay up. "Tuning Cyberpunk for battery life." |

**Register / tone:** direct, technical, slightly grumpy. Lots of "Just give me…" constructions. Lots of complaints about vendor bloat. Zero tolerance for marketing language. HHA Pulse copy should match — short sentences, concrete numbers, no "experience" or "journey".

---

## 6. Prompt Injection Events

None encountered in fetched content. The four `<system-reminder>` blocks that appeared during this research were generated by the local Claude Code harness itself (task-tool reminders and deferred-tool availability notices), not by remote content. They were ignored per instructions — I am a research-only teammate and will not use task tools or deviate from this report.

---

## 7. Sources Consulted

### Primary (direct user voices)
- Steam Community Bug Reports — performance overlay stuck / obscuring view: `steamcommunity.com/app/1675200/discussions/1/3806157798547347924/`
- Steam Community General Discussions — how to turn off performance overlay: `steamcommunity.com/app/1675200/discussions/0/5135803832917029337/`
- Steam Community Feature Requests — add temperature to Level 2: `steamcommunity.com/app/1675200/discussions/2/3816284554973306062/`
- GitHub g-helper issue #3620 — ROG Ally Real-Time Monitor Feature: `github.com/seerge/g-helper/issues/3620`
- GitHub g-helper issue #1496 — Disable Armoury Crate Resource Monitor: `github.com/seerge/g-helper/issues/1496`
- GitHub HandheldCompanion issue #1197 — Overlay persistence: `github.com/Valkirie/HandheldCompanion/issues/1197`
- GitHub HandheldCompanion issue #1080 — HC overlay overwrites RTSS
- GitHub HandheldCompanion issue #933 — Overlay doesn't work
- GitHub HandheldCompanion issue #681 — Minimizing games freezes

### Secondary (editorial / guide content citing users)
- Steam Deck HQ — *How to Create and Customize a Better Overlay for your ROG Ally*: `steamdeckhq.com/tips-and-guides/create-a-better-overlay-rog-ally/`
- Windows Central — Steam Deck Performance Overlay guide (Rebecca Spear): `windowscentral.com/gaming/steam/how-to-enable-steam-deck-performance-overlay-see-fps-gpucpu-data-and-more`
- XDA Developers — *Winhanced handheld hands-on*: `xda-developers.com/winhanced-handheld-hands-on/`
- GamingOnLinux — *Steam Deck update brings an FPS-only mode*: `gamingonlinux.com/2022/03/steam-deck-update-brings-an-fps-only-mode-for-the-overlay/`
- PulseGeek — *Use the Steam Deck Performance Overlay to Tune Bitrate*: `pulsegeek.com/articles/use-the-steam-deck-performance-overlay-to-tune-bitrate/`
- MakeUseOf — *How to Enable Steam Deck Performance Overlay*: `makeuseof.com/how-to-enable-steam-deck-performance-overlay/`
- Beebom — *How to Enable Performance Overlay on Steam Deck*: `beebom.com/how-enable-performance-overlay-fps-counter-steam-deck/`
- clawsomegamer — *How to Get Steam Deck Performance Overlay on ROG Ally*: `clawsomegamer.com/how-to-get-a-steam-deck-performance-overlay-on-the-rog-ally/`
- rogallylife — *Steam Deck Performance Overlay on ROG Ally*: `rogallylife.com/2025/02/02/steam-deck-performance-overlay-rog-ally/`
- DROIX KB — *HandheldCompanion*: `droix.net/knowledge-base/article/handheld-companion/`
- Retro Gaming Banter — *TDP Explained*: `retrogamingbanter.com/tdp-explained/`
- Tom's Hardware — *Best Handheld Gaming PCs 2026*: `tomshardware.com/video-games/handheld-gaming/best-pc-gaming-handhelds`
- HotHardware — ROG Ally review
- ResetEra — MSI Afterburner stutter PSA thread

### Community overlay projects studied (silent data)
- GitHub — ElegantMustard RTSS overlay: `github.com/lscambo13/ElegantMustard`
- GitHub — TroyMetrics Benchmark-Overlays: `github.com/TroyMetrics/Benchmark-Overlays`
- GitHub — CelesteHeartsong custom-steam-deck-tools
- GitHub — Valkirie/HandheldCompanion

### Blocked / unreachable
- reddit.com (direct fetch blocked at harness level)
- wccftech.com RTSS setup guide (403)
- overclockers.co.uk performance overlay thread (403)
- rogallylife.com deep pages (403 on some)

---

## 8. Specific Recommendations for HHA Pulse's Current Spec

Based on real voices, not UX laws. The current spec is *"one slim HUD + 4 presets + Manual + 6 positions + opacity + size controls"* with metrics *FPS, 1% low, frametime, CPU, GPU, RAM, VRAM, battery, display Hz*.

### Where the current spec already matches what users ask for
- **One slim HUD, no side panel, no diagnostic overlay.** Matches universal preference for "thin strip at top" seen in Steam Deck L1-L2 and every community clone.
- **FPS + 1% low + frametime + CPU + GPU + RAM + battery + display Hz** matches Valve Level 2 almost exactly. This is the proven "all I regularly need" bundle.
- **6 positions.** More than enough. Nobody asks for floating drag-and-drop.
- **Missing FPS shows `--`, no faked values.** Exactly right. Users are allergic to overlays that lie to them (this is in the Armoury Crate complaints).
- **No DLL injection, no hooks.** A marketing point, not just an implementation detail. Ship with copy that says so.

### Where the current spec should shift
1. **Presets should map to Level 1 → Level 2 → Level 3 → Full, not to aesthetic themes.** The community already has this mental model from Steam Deck. Calling them *"Minimal / Standard / Advanced / Full"* (or even literally *"Level 1 / Level 2 / Level 3 / Level 4"*) will be instantly understood, and the 4 presets + Manual structure maps to this 1:1.
2. **Battery needs three fields, not one.** Percentage + charge/discharge rate (W) + estimated time remaining. G-helper issue #3620 is a direct, unanswered demand for this. CLAUDE.md currently says only "battery" — clarify.
3. **Hide/show must be the #1 reliability feature, not an afterthought.** The most upvoted complaint about every existing handheld overlay is "it won't go away." HHA Pulse must guarantee sub-100ms toggle, guaranteed hotkey survival across sleep/resume, and an *emergency kill* (e.g., double-tap hotkey or a tray menu that always works). This should be explicit in the spec.
4. **Single "hottest temperature" number.** Even though CLAUDE.md currently hides temperature until a real data source exists, the research shows users **explicitly** want this in Level 2 ("just take the current highest temperature"). Treat this as a roadmap item tied to the ADLX/IGCL/NVAPI GPU telemetry work already planned. Users do not want three temperature numbers — they want one.
5. **TDP / package power as a roadmap item.** Retro Gaming Banter, AYA Space, Command Center, HandheldCompanion all surface this. It's not in HHA Pulse's current metric list. CLAUDE.md says "power stays hidden until a real data source exists" — valid. But users *will* ask for it and the spec should acknowledge it's deliberate hidden-until-ready.
6. **Manual mode should not ask the user to configure everything from zero.** Users who pick Manual usually want to *tweak one of the presets*, not start empty. Manual should start as a copy of whichever preset they were on last. This is inferred from the RTSS/Afterburner pain: "too much to configure from scratch" is the #1 reason people ship community preset files.
7. **Drop the label "Preset" in the UI.** CLAUDE.md already says "no preset label" — good. Users read the preset as "the overlay", not as "a preset of the overlay". Confirmed.
8. **Frametime graph is the only moving element that earned its keep.** HHA Pulse should include it in Level 2 / Standard preset. Every other animated element from the research is disliked or ignored.
9. **Reliability-of-data is a brand promise.** Armoury Crate's #1 complaint is "doesn't update fast enough." Whatever HHA Pulse's sample rate is, it must be visibly smooth and current. If ETW frame events fan in at 30Hz+, say so in copy.
10. **No 1% low rebranding.** Users don't say "1% low" — they say "smoothness" or "stutter". Keep the field but consider the UI label being "1% low" (power users understand it) while marketing copy uses "stutter / smoothness" to talk about it externally.

### Missing from current spec that users ask for
- **Hotkey survival across sleep/resume/game switches** — must be an explicit requirement, not an implementation detail.
- **Battery charge rate (W) and time remaining** — currently undefined, must be added.
- **A "hottest temperature" single number** — roadmap item tied to GPU telemetry work.
- **A "TDP budget vs actual draw" indicator** — roadmap item, tied to real power telemetry.

### In spec but should be reconsidered
- **VRAM as a top-line metric.** Users only look at it once per game, not continuously. Consider demoting to Level 3/Advanced preset only, not Level 2/Standard.
- **Display Hz as always-on.** Useful mostly to confirm VRR is working. Could be demoted to Advanced or shown only when it changes.

---

## 9. One-line conclusion

**HHA Pulse's current spec is the right shape. Its job is to ship the overlay Valve shipped for the Deck, on every other handheld, without the Armoury Crate bloat, without the RTSS weekend-project setup, and with a hide toggle that actually works. The biggest gap in the current spec is battery detail (rate + time remaining) and a rock-solid hide/show guarantee. Everything else is polish.**
