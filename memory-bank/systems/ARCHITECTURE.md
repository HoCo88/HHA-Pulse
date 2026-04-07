# HHA Pulse — System Architecture

## Component Overview

```
HHA Pulse paid Store app
  WinUI 3 FullTrustProcess, current user
  - direct user-mode metric collectors
  - optional external PresentMon/PawnIO integration
  - transparent overlay top bar and side panel
  - settings, profiles, hotkeys, metric picker, custom mode
  - named pipe server: \\.\pipe\LOCAL\HHAPulse

HHA Pulse Widget free Store app
  UWP XAML, Xbox Game Bar SDK
  - standalone limited metrics
  - enhanced pipe client when paid app is running
  - Store upsell and Handheld Ally branding
```

There is no HHA Pulse Windows Service in v1. The paid overlay app owns collection, aggregation, UI state, and IPC.

## Data Flow

```
Battery / CPU / RAM / VRAM / Display APIs
ADLX / IGCL native wrappers
PresentMon Service API (external, installed by user)
PawnIO driver IOCTLs (optional, installed by user)
        |
        v
Paid Overlay CollectorOrchestrator
        |
        +--> WinUI overlay view models and controls
        |
        +--> Named Pipe \\.\pipe\LOCAL\HHAPulse
                 |
                 v
             Free Game Bar Widget enhanced mode
```

If PresentMon is missing, FPS/frametime/latency/FrameGen are unavailable and the UI shows an install prompt. If PawnIO is missing, CPU MSR temp/fan/RAPL are unavailable and the UI continues without those metrics.

## IPC Protocol

- **Transport:** Named Pipe `\\.\pipe\LOCAL\HHAPulse`
- **Format:** 4-byte little-endian length + MessagePack envelope
- **Model:** Read-only telemetry push from paid overlay to widget clients
- **Security:** Explicit DACL from S2 spike result; prefer widget Package SID, fall back to Microsoft Game Bar guidance if needed
- **Reconnection:** Exponential backoff 100ms to 5s
- **Validation:** Size limits, protocol version checks, malformed frame rejection

## Key Design Decisions

1. **Two Store listings** — paid overlay app and free widget funnel.
2. **No Windows Service** — avoids Store policy risk for NT services and LocalSystem.
3. **WinUI 3, not WPF** — WPF is banned for overlay rendering due to VRR behavior.
4. **PresentMon external dependency** — advanced FPS metrics require installed PresentMon Service.
5. **PawnIO optional** — no driver bundling, downloading, or installing by HHA Pulse.
6. **Vendor GPU APIs directly** — ADLX for AMD and IGCL for Intel through thin C++ wrappers.
