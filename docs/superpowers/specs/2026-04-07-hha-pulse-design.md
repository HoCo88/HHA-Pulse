# HHA Pulse — v1 Design Specification

**Date:** 2026-04-07
**Product:** HHA Pulse / HHA Pulse Widget
**Status:** v4 final architecture for implementation

## Product Vision

HHA Pulse is a Windows handheld performance overlay for ROG Ally, Legion Go, MSI Claw, and similar Windows handheld PCs. It replaces the RTSS + HWiNFO + Afterburner setup with a Store-delivered paid overlay app plus a free Xbox Game Bar widget funnel.

Public naming:
- Store paid app: **HHA Pulse**
- Store free widget: **HHA Pulse Widget**
- Brand line: **Powered by Handheld Ally**
- Website: **handheldally.com** for support, SEO, and Store deep links only

## Distribution

HHA Pulse ships as **one MSIX package** in the Microsoft Store:

| Component | Price | Purpose |
| --- | --- | --- |
| HHA Pulse (overlay + widget) | Free download, IAP $1.99 / 49 CZK / 1.99 EUR | Overlay + Game Bar widget in one package |

Free tier: Game Bar widget + Minimal overlay preset + battery monitoring.
Paid IAP: all 4 presets, custom mode, metric picker, FrameGen, bottleneck, side panel, profiles, hotkeys.

Use a **Company Partner Center account** for OSVC/commercial publishing. Do not distribute installers, drivers, or downloadable EXEs from handheldally.com. The personal EXE build is for local development/testing only.

## Architecture

There is **no HHA Pulse Windows Service** in v1. Store policy risk is too high for NT services and LocalSystem packaged services.

```
HHA Pulse paid MSIX
  WinUI 3 FullTrustProcess, medium integrity, regular user
  - transparent overlay top bar and side panel
  - settings, hotkeys, profiles, metric picker, custom mode
  - direct user-mode collectors
  - optional external integrations if already installed
  - named pipe server at \\.\pipe\LOCAL\HHAPulse

HHA Pulse Widget (same MSIX package)
  UWP XAML, Xbox Game Bar SDK
  - standalone limited metrics
  - upsell to IAP for full overlay features
  - enhanced mode via pipe when paid app is installed/running
```

### Data Collection

Core metrics run inside the paid overlay process and must not require admin:

| Metric area | Source |
| --- | --- |
| Battery | `GetSystemPowerStatus`, `CallNtPowerInformation`, battery IOCTLs where accessible |
| CPU usage | `GetSystemTimes` delta |
| Memory | `GlobalMemoryStatusEx` |
| VRAM | `IDXGIAdapter3::QueryVideoMemoryInfo` |
| Display | `EnumDisplaySettings`, DXGI feature checks |
| AMD GPU | ADLX via `HHAPulse.Native` C wrapper |
| Intel GPU | IGCL via `HHAPulse.Native` C wrapper |

Advanced integrations are external and optional from HHA Pulse's packaging perspective:

| Integration | Enables | Rule |
| --- | --- | --- |
| PresentMon Service | FPS, frametime, GPU Busy, latency, FrameGen | Required for advanced performance metrics; never ship `PresentMonAPI2.dll`; show install prompt when missing |
| PawnIO | CPU MSR temp, fan speed, RAPL power | Optional only; never bundle, download, or install; detect gracefully |

If PresentMon is missing, the app still runs and displays non-FPS metrics. If PawnIO is missing, CPU temp/fan/RAPL are unavailable but all other metrics continue.

## IPC

The paid overlay publishes read-only telemetry to the widget over:

```
\\.\pipe\LOCAL\HHAPulse
```

Protocol:
- 4-byte little-endian payload length
- MessagePack envelope with protocol version, message type, timestamp, and payload
- Read-only telemetry push from paid app to widget
- Message size limits and malformed frame rejection
- `FILE_FLAG_FIRST_PIPE_INSTANCE` to reduce pipe squatting risk

Security:
- Test Package SID-only DACL first.
- If the Game Bar/UWP AppContainer cannot connect, use the Microsoft Game Bar guidance pattern with `SECURITY_WORLD_SID_AUTHORITY` plus widget Package SID and keep the protocol read-only.
- Cross-package IPC must pass spike S2 and Store certification. If it fails, the widget remains standalone-only for v1.

## Overlay Features

Presets:
- **Minimal:** FPS + rolling graph + battery percent/time/watts
- **Standard:** Minimal + CPU/GPU usage and temperatures + frametime
- **Tuner:** Standard + AVG/1%/0.1% lows + FrameGen + bottleneck + power + max temps
- **Diagnostic:** Side panel with graphs, frametime table, latency, hardware, battery, display details

Customization:
- Metric picker for any preset
- Custom mode: top bar, side panel, or both; user-selected metric order
- Background and text transparency
- Small/medium/large font sizes
- Top/bottom bar and left/right panel positions
- Side panel width 200-350px
- Color thresholds and update interval
- Per-game profiles and auto-detection
- User-configurable hotkeys and guided handheld setup

Rendering rules:
- WinUI 3 only, never WPF
- No `x:Bind` in `MainWindow.xaml`
- UI updates from collectors/pipe callbacks must use `DispatcherQueue.TryEnqueue`
- Subscribe events in `Loaded` and unsubscribe in `Unloaded`
- Graphs use imperative drawing

## Widget Features

Standalone mode:
- Battery and charging status
- RAM/basic metrics that pass spike S6 in the UWP/Game Bar sandbox
- “Powered by Handheld Ally”
- Store upsell to paid HHA Pulse listing

Enhanced mode:
- Connects to `\\.\pipe\LOCAL\HHAPulse`
- Receives all telemetry the paid app can collect
- Shows connected state
- Compact and desktop layouts
- Controller navigation

## Spikes Required Before Production Completion

1. WinUI 3 transparent topmost click-through overlay over fullscreen game.
2. Separate free widget package connects to paid app pipe with correct DACL.
3. PresentMon Service API from C# reads FPS/frametime for a running game.
4. ADLX reads GPU temp/usage/clock on Ryzen Z1 Extreme iGPU.
5. Paid FullTrust MSIX and free widget MSIX sideload, launch, and appear in Game Bar.
6. Identify which standalone metrics work inside the Game Bar UWP sandbox.

## Testing Requirements

Automated:
- MessagePack round trips, protocol versioning, malformed frame rejection
- Pipe multi-client and DACL behavior
- Collector failure isolation and missing dependency states
- Battery prediction, FPS percentiles, FrameGen confidence, Bottleneck confidence
- Settings/profile persistence, hotkeys, metric picker, custom layout save/reload

Manual hardware:
- ROG Ally: presets, ADLX, VRR, hotkeys in game
- MSI Claw: IGCL and VRR
- Game Bar widget standalone/enhanced/upsell/controller nav
- PresentMon missing/installed and PawnIO missing flows
- Fortnite/EAC, Valorant/Vanguard, PUBG/BattlEye smoke tests
- 30-minute memory stability
- MSIX install/uninstall clean state

## Non-Goals for v1

- No HHA Pulse Windows Service
- No bundled or auto-installed drivers
- No public EXE/MSI distribution
- No DLL injection, DirectX/Vulkan hooking, or game process memory reads
- No telemetry, analytics, or network service in the helper path
