# HHA Pulse — Design Specification

**Date:** 2026-04-07
**Product:** HHA Pulse (Handheld Ally Pulse)
**Author:** Johny (handheldally.com) + Claude
**Status:** Draft — awaiting review

---

## 1. Product Vision

HHA Pulse is a performance overlay app for Windows handhelds (ROG Ally, MSI Claw, Legion Go, and all Windows handheld PCs). It replaces the RTSS + HWiNFO + Afterburner triple-install nightmare with a single app that "just works" — like what Steam Deck has natively, but for Windows.

**Core promise:** One install. One app. Everything you need to see while gaming.

**Target users:**
- Casual gamers who want FPS + battery info without setup complexity
- Tuners who optimize TDP, compare settings, and need 1% lows and frametime data
- Reviewers (Phawx-level) who need detailed frametime analysis, bottleneck detection, and latency measurement

**Price:** ~2 EUR / ~49 CZK (desktop app). Game Bar widget is free.

**Connected to:** handheldally.com for cross-promotion and user acquisition.

---

## 2. Architecture

### 2.1 Two Products, One Ecosystem

```
+-----------------------------------------------+
|  Game Bar Widget (FREE)                       |
|  UWP XAML - Game Bar Widget Store             |
|  Basic metrics without admin rights           |
|  "Powered by Handheld Ally" + upsell link     |
|  Compact Mode (handheld-optimized)            |
|  Pinnable, transparent, click-through         |
+-------------------+---------------------------+
                    | Named Pipes IPC
                    | (enhanced data when desktop app running)
+-------------------v---------------------------+
|  HHA Pulse Desktop App (~2 EUR)               |
|  Win32 - Traditional installer                |
|  (handheldally.com / direct download)         |
|                                               |
|  +------------------+  +------------------+   |
|  | Helper Service   |  | Overlay UI       |   |
|  | (runs as SYSTEM) |  | (runs as user)   |   |
|  |                  |  |                   |   |
|  | - PawnIO driver  |  | - Top bar        |   |
|  | - CPU temp/power |  | - Side panel     |   |
|  | - Fan speed      |  | - Custom layouts |   |
|  | - PresentMon ETW |  | - Hotkey handler |   |
|  | - ADLX (AMD GPU) |  | - Settings UI    |   |
|  | - IGCL (Intel)   |  | - Profile mgr    |   |
|  | - Named Pipe srv |  |                   |   |
|  +--------+---------+  +------------------+   |
|           |                                    |
|           +-- Named Pipe / Shared Memory       |
+-----------------------------------------------+
```

### 2.2 Why This Architecture

- **Helper Service as SYSTEM** — required for PawnIO driver (CPU temp, RAPL power, fan speed), PresentMon ETW tracing (FPS/frametime), vendor GPU APIs
- **Overlay UI as regular user** — renders overlay, handles hotkeys, no admin needed
- **Game Bar widget** — free funnel via Game Bar Widget Store, anti-cheat 100% safe (DWM composited), reads basic metrics without admin + enhanced data from Helper Service when installed
- **Named Pipes IPC** — Microsoft's recommended method for Game Bar widget to desktop app communication. Security via Package SID.

### 2.3 Distribution

| Product | Channel | Price |
|---------|---------|-------|
| Game Bar Widget | Game Bar Widget Store (free) | Free |
| Desktop App | handheldally.com (MSI/EXE installer) | ~2 EUR |

**Why not Microsoft Store for desktop app:** Store prohibits kernel drivers, admin elevation, and Windows Services. The Helper Service + PawnIO driver require traditional installer.

**Sideloading:** Game Bar widget can also be sideloaded as APPX alongside the desktop installer (for users who prefer a single download from handheldally.com).

---

## 3. Anti-Cheat Safety

**The architecture is anti-cheat safe by design.** Zero risk of triggering EAC, BattlEye, Vanguard, or GameGuard.

| What we do | Why it's safe |
|---|---|
| Game Bar widget overlay | DWM composited — invisible to all anti-cheat |
| PresentMon ETW for FPS | Passive event tracing — no process interaction |
| Named Pipes IPC | Standard Windows IPC between our own processes |
| LibreHardwareMonitor / PawnIO for sensors | Reads hardware registers, never touches game memory |
| Win32 transparent window (paid overlay) | Small topmost window — not injection, not hooking |

**What HHA Pulse NEVER does:**
- No DLL injection into any game process
- No DirectX/Vulkan hooking
- No ReadProcessMemory/WriteProcessMemory on games
- No OpenProcess with write access to games
- No kernel drivers that interact with games

**Exception: nProtect GameGuard** may block Game Bar entirely in some games (developer choice, not our fault). All other anti-cheat systems are safe.

---

## 4. VRR (Variable Refresh Rate) Safety

### 4.1 The Problem

Any window on top of a game can break VRR. This is a fundamental Windows DWM limitation — when an overlay is visible, DWM must composite two sources, which can kill Independent Flip mode and disable VRR.

### 4.2 Why Handhelds Are OK

**AMD FreeSync (ROG Ally, Legion Go) and Intel VRR (MSI Claw) are more tolerant of overlay windows than NVIDIA G-Sync.** NVIDIA breaks VRR with any topmost window. AMD/Intel continue VRR in many scenarios.

Since ALL Windows handhelds use AMD or Intel GPUs, this is favorable for our target market.

### 4.3 Mitigations

1. **Small overlay window** — top bar (~24px height) maximizes chance of MPO (hardware overlay plane) assignment
2. **DirectComposition rendering** — avoid WPF entirely (known VRR bug). Use WinUI 3 with native interop or DirectComposition
3. **Update interval setting** — 0.5s / 1s / 2s. Lower frequency = less composition impact
4. **Show/hide hotkey** — when hidden, zero VRR impact (no overlay window exists)
5. **Game Bar widget alternative** — for users who want guaranteed VRR safety, the Game Bar widget is DWM-composited by the system

### 4.4 Rendering Technology

**NEVER use WPF.** WPF has a documented VRR bug (dotnet/wpf#2294) — it renders at low FPS and pulls display refresh rate down.

Options (in order of preference):
1. **WinUI 3 with SwapChainPanel** — modern, flip model capable, .NET friendly
2. **DirectComposition (DComp)** — best chance of MPO, but C++/WinRT required
3. **Composition Swapchain API** — most advanced (WDDM 3.0+), future option

Decision for v1: **WinUI 3** — best balance of .NET developer productivity and modern rendering.

---

## 5. Overlay Design

### 5.1 Core Principle: Metric Picker

The #1 community complaint is "all-or-nothing overlay levels." HHA Pulse solution:

**Every preset is a starting point, not a lock.** User can toggle ON/OFF every metric individually on any preset. The overlay dynamically resizes.

### 5.2 Layout Modes

User chooses their layout in settings:

| Layout | Description |
|--------|-------------|
| **Top Bar** | Horizontal bar at top/bottom edge, 1-2 lines |
| **Side Panel** | Vertical panel left/right side, ~200-350px wide |
| **Both** | Top bar always-on + side panel on hotkey |
| **Custom** | User picks layout + exact metrics |

### 5.3 Presets (Quick Start)

4 presets as starting points. User picks one, then fine-tunes by toggling metrics.

#### Preset 1 — Minimal

**Layout:** Top bar, 1 line
**Target:** Every gamer, daily use

```
73 FPS [||||||||] | 72% 1h48m 15W
```

**Metrics:**
- FPS (current) + mini rolling FPS sparkline graph
- Battery: %, estimated time remaining, discharge watts

**Why these:** Community data shows battery % is "more important than FPS" for handheld users. These two are universally wanted. The rolling FPS graph shows trends and spikes without taking more space than a few numbers would.

#### Preset 2 — Standard

**Layout:** Top bar, 1 line
**Target:** Daily driver for most gamers — MangoHud Level 2 equivalent ("the setting I use the most")

```
73 FPS [||||||||] 13.7ms | CPU 38% 57° | GPU 96% 74° | 72% 1h48m 15W
```

**Metrics:**
- FPS (current) + rolling FPS graph + frametime (ms)
- CPU: usage %, temperature
- GPU: usage %, temperature
- Battery: %, time remaining, discharge watts

**Color coding:** Green (OK) / Yellow (warning) / Red (hot/high). Configurable thresholds.

**Optional toggles (user adds from this preset):** MHz, RAM, VRAM, fan RPM, VRR Hz

#### Preset 3 — Tuner

**Layout:** Top bar, 2 lines
**Target:** Optimization, benchmarking, TDP tuning — Phawx-level analysis

```
Line 1: 73 FPS AVG:68 1%:52 0.1%:38 [||||||||] 13.7ms | FG:ON nat:37 | GPU-bound
Line 2: CPU 38% 57°/max:72° 3.8GHz 8W | GPU 96% 74°/max:81° 2.1GHz 12W | VRAM 8.1/15.9 | 72% 1h48m 15W
```

**Line 1 — Performance:**
- FPS: current / AVG / 1% low / 0.1% low
- Rolling FPS graph + frametime (ms)
- FrameGen: ON/OFF + native FPS (e.g., "FG:ON nat:37" = generating from 37 to 73)
- Bottleneck indicator: CPU-bound / GPU-bound (PresentMon GPU Busy metric)

**Line 2 — Hardware:**
- CPU: usage %, temp current/MAX, clock GHz, power watts
- GPU: usage %, temp current/MAX, clock GHz, power watts
- VRAM: used/total
- Battery: %, time, discharge watts

**Unique HHA Pulse features in this preset:**
- FrameGen detection for ALL games (not Steam-only like Steam's beta)
- Bottleneck indicator via PresentMon GPU Busy (nobody else shows this in overlay)
- Current/MAX temperatures (see peak during session)

#### Preset 4 — Diagnostic

**Layout:** Side panel (left or right, user choice)
**Target:** Deep analysis, debugging, reviewer workflow

**Panel contents (top to bottom):**

**Performance section:**
- FPS: current / AVG / 1% low / 0.1% low / min / max
- FrameGen: ON/OFF + native vs displayed FPS
- Bottleneck detail: GPU Busy time vs Frame Time (ms values + visual indicator)
- Frametime table: avg / min / max / last — for Total, CPU, GPU separately
- Input latency: click-to-photon (ms) via PresentMon 2.2+

**Graphs section:**
- FPS rolling graph (last 60 seconds)
- Frametime graph (last 300 frames) — spikes visible
- Battery discharge trend (last 30 minutes)

**Hardware section:**
- CPU: usage %, temp current/avg/max, clock GHz, power W, TDP stav, TjMax distance
- GPU: usage %, temp current/avg/max, clock GHz, power W
- VRAM: used/total
- RAM: used/total
- Fan: RPM
- Per-core CPU load (toggleable)

**Battery section:**
- Charge %
- Estimated time remaining (game-aware rolling average)
- Discharge rate (watts) — current + average
- Battery health %
- Cycle count
- Capacity: current / design (Wh)
- Charger status

**Display section:**
- Resolution + refresh rate
- VRR: Active/Inactive + current Hz
- Present Mode: Independent Flip / Composed Flip / Hardware Composed
- HDR status

**Side panel customization:**
- Position: Left / Right
- Width: 200px – 350px (slider)
- Background transparency: 0% – 100% (separate slider)
- Metrics transparency: 0% – 100% (separate slider, so text can be more visible than background)
- Scrollable if content exceeds panel height

#### Custom Mode

User builds their own overlay from scratch:
1. Choose layout: Top Bar / Side Panel / Both
2. Pick metrics: checkbox list of all available metrics
3. For top bar: choose which metrics on line 1, which on line 2 (drag to reorder)
4. For side panel: choose sections and order
5. Save as named profile
6. Assign to per-game profiles

### 5.4 Global Overlay Settings

| Setting | Options | Default |
|---------|---------|---------|
| Transparency (background) | 0% – 100% | 85% |
| Transparency (metrics text) | 0% – 100% | 100% |
| Font size | Small / Medium / Large | Medium |
| Position (top bar) | Top edge / Bottom edge | Top |
| Position (side panel) | Left / Right | Right |
| Side panel width | 200px – 350px | 260px |
| Color thresholds (temp) | Configurable °C for yellow/red | 70°C / 85°C |
| Color thresholds (GPU load) | Configurable % for green/yellow/red | 50% / 90% |
| Color thresholds (FPS) | Low FPS warning threshold | Based on target FPS |
| Update interval | 0.5s / 1s / 2s | 1s |
| Per-game profiles | ON/OFF | OFF |
| Hotkey: cycle presets | User-configurable | Ctrl+Shift+O |
| Hotkey: toggle overlay | User-configurable | Ctrl+Shift+H |
| Hotkey: toggle side panel | User-configurable | Ctrl+Shift+P |

### 5.5 Responsive Design (7" – 8.8" Screens)

- **7" / 1280x800 (Steam Deck, Ally):** Font size auto-adjusts to Small. Top bar shows fewer metrics per line. Side panel narrower (200px).
- **8" / 1920x1080 (Legion Go):** Medium font. Full metric set fits on one line.
- **8.8" / 1920x1200 (Legion Go S):** Large font option. More spacious layout.
- **External display / docked:** Full desktop layout. Side panel can be on second monitor (zero VRR impact on primary).

Auto-detection via `EnumDisplaySettings` for resolution + DPI. User can override.

---

## 6. Killer Features (Differentiators)

### 6.1 FrameGen Detection

**What:** Shows native FPS vs displayed (generated) FPS for ANY game — not just Steam games.

**How:** PresentMon ETW tracks both "Presented" frames (app-generated) and "Displayed" frames (including generated). The delta reveals frame generation.

**Display:** `FG:ON nat:37 -> 73` (37 native frames, 73 displayed with frame gen)

**Why it matters:** Steam added this in beta (July 2025) but only for Steam. No standalone overlay does this. With AFMF/DLSS/FSR frame gen becoming ubiquitous, users need to know their "real" FPS.

### 6.2 Bottleneck Indicator

**What:** Real-time CPU-bound vs GPU-bound detection.

**How:** PresentMon GPU Busy metric measures isolated GPU processing time per frame.
- If Frame Time >> GPU Busy time -> CPU bottleneck (GPU is idle waiting for CPU)
- If Frame Time ≈ GPU Busy time -> GPU bottleneck (GPU is the limiting factor)

**Display in top bar:** `GPU-bound` or `CPU-bound` badge with color coding
**Display in side panel:** `GPU Busy: 12.8ms / Frame: 13.7ms` + visual breakdown bar + percentage

**Why it matters on APU handhelds:** CPU and iGPU share TDP budget. If GPU-bound, user can increase TDP or lower resolution. If CPU-bound, lowering resolution won't help — need to lower game settings that stress CPU. Nobody else shows this simply.

### 6.3 Game-Aware Battery Prediction

**What:** Accurate battery time estimation based on actual gaming power draw, not Windows's wildly fluctuating estimate.

**How:** Rolling average of discharge rate over the last 5 minutes of gameplay. Formula: `remaining_Wh / avg_discharge_W * 60 = minutes`. Smooths out shader compilation spikes and loading screen dips.

**Display:** `~1h 48m` with higher confidence than Windows estimate. Optional discharge trend graph in side panel.

**Why it matters:** Community explicitly calls out Windows battery estimates as unreliable during gaming. "Battery readings fluctuate during shader compilation" — our rolling average fixes this.

### 6.4 Metric Picker (Customizable Presets)

**What:** User toggles individual metrics ON/OFF on any preset. Layout dynamically resizes.

**Why it matters:** #1 community complaint about MangoHud/Steam Deck overlay: "all-or-nothing levels." User Wolf on Steam: wants FPS + battery + CPU/GPU usage WITHOUT the frametime graph. Currently impossible without editing config files.

### 6.5 Zero Setup

**What:** One installer. One app. Everything works out of the box.

**Why it matters:** Current Windows handheld overlay requires RTSS + HWiNFO64 + MSI Afterburner = 3 separate programs, 15+ setup steps, shared memory configuration, controller hotkey mapping through OEM software. HHA Pulse: install, pick a preset, play.

### 6.6 Real-Time Input Latency

**What:** Click-to-photon latency measurement displayed in the overlay.

**How:** PresentMon 2.2+ measures input-to-display latency with ~30ms reporting delay. Shows actual responsiveness, not just FPS.

**Display in side panel:** `Latency: 18.2ms`

**Why it matters:** "Input latency is the all-too-frequently missing piece of framegen-enhanced gaming performance analysis" (Tom's Hardware). A game showing 73 FPS with frame gen may have input lag at the 37 FPS level. No consumer overlay shows this. Only PresentMon and FrameView do, and they're hard to set up.

---

## 7. Tech Stack

### 7.1 Language & Framework

| Component | Technology | Reason |
|-----------|-----------|--------|
| Helper Service | C# / .NET 8 | Windows Service, easy P/Invoke for native APIs |
| Overlay UI | C# / WinUI 3 | Modern rendering (no WPF VRR bug), flip model capable |
| Game Bar Widget | C# / UWP XAML | Required by Game Bar SDK |
| IPC | Named Pipes | Microsoft-recommended for Game Bar communication |

### 7.2 Data Collection (Helper Service)

| Metric | API/Library | Admin Required |
|--------|-------------|----------------|
| **FPS, frametime, GPU Busy, latency** | PresentMon Service API (`PresentMonAPI2.dll`) | Service runs elevated; client doesn't need admin |
| **FrameGen detection** | PresentMon (Presented vs Displayed FPS) | Same as above |
| **CPU temp, freq, power (RAPL)** | PawnIO driver + MSR access | Yes (kernel driver) |
| **GPU temp, usage, clock, power (AMD)** | ADLX SDK | No |
| **GPU temp, usage, clock, power (Intel)** | IGCL (Intel Graphics Control Library) | No |
| **Fan speed** | PawnIO + Super I/O chip / vendor WMI | Yes (kernel driver) |
| **Battery %, state, discharge rate** | `CallNtPowerInformation(SystemBatteryState)` | No |
| **Battery health, cycles, capacity** | `IOCTL_BATTERY_QUERY_INFORMATION` | No |
| **CPU usage %** | `GetSystemTimes()` | No |
| **GPU usage % (fallback)** | PerformanceCounter "GPU Engine" | No |
| **RAM usage** | `GlobalMemoryStatusEx()` | No |
| **VRAM usage** | `IDXGIAdapter3::QueryVideoMemoryInfo()` | No |
| **Display refresh rate** | `EnumDisplaySettings()` | No |
| **VRR detection** | `IDXGIFactory5::CheckFeatureSupport` | No |
| **Present mode** | PresentMon ETW | Same as FPS |

### 7.3 Key Libraries

| Library | Purpose | License |
|---------|---------|---------|
| **PresentMon Service API** | FPS, frametime, GPU Busy, latency, frame gen | MIT |
| **PawnIO** | Ring-0 driver for MSR/IO access (CPU temp, power, fan) | To verify |
| **ADLX SDK** | AMD GPU metrics | AMD GPUOpen (MIT-like) |
| **IGCL SDK** | Intel GPU metrics | Intel proprietary |
| **Xbox Game Bar SDK** | Game Bar widget (NuGet v7.3) | Microsoft |

**NOT using LibreHardwareMonitor directly** due to:
- WinRing0 driver flagged by Windows Defender
- MPL-2.0 license complications for commercial product
- Anti-cheat may flag WinRing0

Instead: PawnIO for ring-0 access + vendor GPU APIs (ADLX/IGCL) directly. More work but cleaner.

### 7.4 IPC Protocol (Named Pipes)

```
Helper Service creates: \\.\pipe\HHAPulse
- Security: Package SID for Game Bar widget access
- Protocol: Binary/MessagePack for low overhead
- Push model: Service pushes metric updates at configured interval
- Subscribers: Overlay UI + Game Bar widget (both connect as clients)
```

Message format:
```
{
  timestamp: uint64,
  fps: float,
  fps_avg: float,
  fps_1pct: float,
  fps_01pct: float,
  frametime_ms: float,
  gpu_busy_ms: float,
  framegen_active: bool,
  framegen_native_fps: float,
  cpu_usage: float,
  cpu_temp: float,
  cpu_temp_max: float,
  cpu_clock_mhz: float,
  cpu_power_w: float,
  gpu_usage: float,
  gpu_temp: float,
  gpu_temp_max: float,
  gpu_clock_mhz: float,
  gpu_power_w: float,
  gpu_vram_used_mb: float,
  gpu_vram_total_mb: float,
  ram_used_mb: float,
  ram_total_mb: float,
  battery_pct: float,
  battery_discharge_w: float,
  battery_time_remaining_min: float,
  battery_health_pct: float,
  fan_rpm: int,
  vrr_active: bool,
  present_mode: string,
  display_latency_ms: float,
  // ... extensible
}
```

### 7.5 Game Bar Widget Architecture

```
Game Bar Widget (UWP)
  |
  |-- Standalone mode (no desktop app):
  |     Reads basic metrics directly:
  |     - Battery: SystemInformation.PowerStatus
  |     - CPU %: PerformanceCounter
  |     - GPU %: PerformanceCounter "GPU Engine"
  |     - RAM: GlobalMemoryStatusEx
  |     (No temp, no FPS, no fan — those need admin)
  |
  |-- Enhanced mode (desktop app running):
  |     Connects to \\.\pipe\HHAPulse
  |     Receives ALL metrics including FPS, temp, power
  |     Shows "HHA Pulse Connected" indicator
  |
  |-- Upsell:
        "Get FPS, temps, and more — download HHA Pulse"
        Link to handheldally.com
```

Widget features:
- Pinnable (stays on screen during gameplay)
- Transparent + click-through
- Compact Mode for handhelds (min 464px width)
- Controller-navigable (LB/RB to switch widgets)

---

## 8. Target Hardware (v1)

### 8.1 AMD Handhelds
- ROG Ally / Ally X (Ryzen Z1 Extreme — RDNA 3 iGPU)
- Legion Go / Go S (Ryzen Z1 / Z1 Extreme / 8840U)
- Other AMD-based handhelds

**GPU API:** ADLX
**CPU sensors:** PawnIO + AMD MSR registers
**VRR:** FreeSync — tolerant of overlay windows

### 8.2 Intel Handhelds
- MSI Claw (Meteor Lake / Lunar Lake — Intel Arc iGPU)
- Other Intel-based handhelds

**GPU API:** IGCL (Intel Graphics Control Library) — 64-bit only
**CPU sensors:** PawnIO + Intel MSR registers (RAPL)
**VRR:** Intel Adaptive Sync — tolerant of overlay windows

### 8.3 Future Support
- NVIDIA eGPU (desktop users) — NVAPI/NVML. VRR warning for G-Sync users.
- Desktop PCs — naturally supported, wider audience.

---

## 9. Hotkey System

### 9.1 User-Configurable Hotkeys

All hotkeys are user-configurable via Settings UI. Users set their OEM back buttons (M1/M2 on Ally, Y1/Y2 on Legion Go) to keyboard shortcuts in OEM software, then map those shortcuts in HHA Pulse.

### 9.2 Default Hotkey Actions

| Action | Default | Description |
|--------|---------|-------------|
| Cycle preset | Ctrl+Shift+O | Cycles: Minimal -> Standard -> Tuner -> Diagnostic -> Off |
| Toggle overlay | Ctrl+Shift+H | Show/hide overlay entirely |
| Toggle side panel | Ctrl+Shift+P | Open/close side panel (independent of top bar) |
| Reset session stats | Ctrl+Shift+R | Reset AVG, MAX temps, session timer |

### 9.3 Guided Setup

First-run wizard detects handheld model (via WMI) and shows step-by-step:
- "You're on ROG Ally! Set M1 to Ctrl+Shift+O in Armoury Crate, then come back."
- Screenshot + instructions per device.

---

## 10. Per-Game Profiles

- User creates named profiles (e.g., "Cyberpunk — Tuner", "Elden Ring — Minimal")
- Each profile stores: preset selection + custom metric toggles + overlay settings
- Auto-switch: detect running game executable -> apply matching profile
- Default profile used when no game-specific profile exists

---

## 11. Game Bar Widget (Free Companion)

### 11.1 Purpose

Discovery and user acquisition funnel. Free in Game Bar Widget Store. When user presses Xbox button on handheld, they see HHA Pulse widget alongside Armoury Crate, MSI Center M, Legion Space widgets.

### 11.2 Standalone Features (no desktop app needed)

- Battery: %, charging status
- CPU usage %
- GPU usage %
- RAM usage
- "Powered by Handheld Ally" branding

### 11.3 Enhanced Features (desktop app running)

- All metrics from Helper Service
- FPS + frametime
- Temperatures
- Power draw
- Full preset support

### 11.4 Upsell Flow

Widget shows banner: "Unlock FPS monitoring, temps, and custom overlays — get HHA Pulse for 2 EUR" with link to handheldally.com.

---

## 12. Technical Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| PawnIO driver flagged by anti-virus | Users get malware warning | Code-sign driver, document in FAQ, whitelist with major AV vendors |
| PresentMon ETW blocked by specific games | No FPS data for those titles | Show "FPS unavailable" gracefully, document known incompatible titles |
| WinRing0 flagged by Defender | Can't use LibreHardwareMonitor directly | Use PawnIO instead (signed, sandboxed) |
| VRR broken on NVIDIA eGPU | External GPU users lose VRR | Show warning, offer "VRR-safe mode" (toggle overlay off during gameplay) |
| Xbox Mode (GDC 2026) changes overlay rules | Architecture may need update | Monitor Microsoft docs, Game Bar SDK updates |
| Game Bar SDK deprecated | Widget becomes unusable | Low risk — Microsoft investing heavily (v7.3, Compact Mode, all OEMs adopting) |
| ADLX/IGCL API breaking changes | GPU metrics break on driver update | Version-check at startup, fallback to PerformanceCounter for basic GPU % |

---

## 13. Phased Delivery

### Phase 1 — MVP (Target: v0.1)

- Desktop app with Helper Service
- Top bar overlay (Preset 1 + 2: Minimal and Standard)
- FPS via PresentMon + CPU/GPU basics + battery
- AMD support (ADLX) — ROG Ally primary test device
- Basic hotkey toggle
- Traditional installer

### Phase 2 — Tuner Features (Target: v0.2)

- Preset 3 (Tuner) — 1% low, 0.1% low, AVG, FrameGen detection, bottleneck indicator
- Intel support (IGCL) — MSI Claw
- Side panel (Preset 4: Diagnostic)
- Metric picker (toggle metrics)
- Per-game profiles
- Transparency + font size settings

### Phase 3 — Game Bar Widget + Polish (Target: v0.3)

- Game Bar widget (free, Game Bar Store)
- Named Pipes IPC between widget and desktop app
- Custom mode (build your own layout)
- Guided hotkey setup wizard
- Game-aware battery prediction
- Input latency display (PresentMon 2.2 click-to-photon)
- Color threshold configuration
- External display / docked mode

---

## 14. Success Metrics

- **Downloads:** 1000+ in first month from handheldally.com
- **Game Bar widget installs:** 5000+ in first 3 months (free funnel)
- **Conversion:** 10%+ from free widget to paid desktop app
- **Reviews:** 4+ stars on Game Bar Widget Store
- **Community:** Active feedback thread on handheldally.com

---

## 15. Open Questions

1. **PawnIO licensing** — need to verify commercial usage terms
2. **ADLX on Ryzen Z1 Extreme** — need to verify ADLX works for iGPU (not just discrete AMD GPUs)
3. **PresentMon FrameGen detection accuracy** — need to test with AFMF, DLSS FG, FSR FG
4. **Click-to-photon on handhelds** — PresentMon 2.2 latency measurement needs hardware testing
5. **Game Bar widget + Named Pipes Store policy** — may need Microsoft Store policy exception for cross-app IPC if not bundled in same MSIX
