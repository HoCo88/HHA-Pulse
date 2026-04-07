# HHA Pulse — System Architecture

## Component Overview

```
+--------------------------------------------------+
|                 HHA Pulse Desktop App             |
|                                                   |
|  +-------------------+  +---------------------+   |
|  | Helper Service    |  | Overlay UI          |   |
|  | C# .NET 8         |  | C# WinUI 3          |   |
|  | Runs as SYSTEM     |  | Runs as user         |   |
|  |                    |  |                      |   |
|  | Data Collection:   |  | Rendering:           |   |
|  | - PresentMon ETW   |  | - Top bar (1-2 lines)|   |
|  | - PawnIO driver    |  | - Side panel         |   |
|  | - ADLX (AMD GPU)   |  | - Custom layouts     |   |
|  | - IGCL (Intel GPU)  |  |                      |   |
|  | - Battery APIs     |  | Input:               |   |
|  | - PerfCounters     |  | - Hotkey handler     |   |
|  |                    |  | - Settings UI        |   |
|  | IPC:               |  | - Profile manager    |   |
|  | - Named Pipe server|  |                      |   |
|  +--------+-----------+  +-----+----------------+   |
|           |                     |                    |
|           +--- Named Pipe ------+                    |
+--------------------------------------------------+
                    |
                    | Named Pipe (same pipe, separate client)
                    |
+-------------------v------------------------------+
|  Game Bar Widget (FREE)                          |
|  C# UWP XAML                                     |
|  Game Bar Widget Store                           |
|                                                   |
|  Standalone: Battery, CPU%, GPU%, RAM             |
|  Enhanced (with desktop app): All metrics         |
|  Upsell: "Get FPS, temps — download HHA Pulse"   |
+--------------------------------------------------+
```

## Data Flow

```
Hardware Sensors
    |
    v
PawnIO Driver (ring-0) --> CPU temp, freq, power, fan
ADLX SDK (user-mode)   --> AMD GPU temp, usage, clock, power
IGCL SDK (user-mode)   --> Intel GPU temp, usage, clock, power
PresentMon ETW          --> FPS, frametime, GPU Busy, latency, FrameGen
Battery APIs            --> %, discharge W, health, capacity
PerformanceCounters     --> CPU %, GPU %, RAM
    |
    v
Helper Service (aggregates all data)
    |
    v
Named Pipe (push model, configurable interval)
    |
    +---> Overlay UI (WinUI 3) --> renders to screen
    +---> Game Bar Widget (UWP) --> renders in Game Bar
```

## IPC Protocol

- **Transport:** Named Pipe `\\.\pipe\HHAPulse`
- **Format:** MessagePack binary (low overhead, fast serialization)
- **Model:** Push — service sends metric updates at configured interval
- **Security:** Explicit DACL (SYSTEM, local user SID, Game Bar Package SID)
- **Reconnection:** Clients retry connection every 2s if pipe not available

## Key Design Decisions

1. **WinUI 3 for overlay, not WPF** — WPF has VRR bug (dotnet/wpf#2294)
2. **PawnIO, not WinRing0** — WinRing0 flagged by Defender
3. **PresentMon Service API for FPS** — MIT licensed, ETW-based, anti-cheat safe
4. **Named Pipes for IPC** — Microsoft recommended for Game Bar communication
5. **Traditional installer, not MSIX Store** — kernel driver + admin elevation required
6. **Vendor GPU APIs directly** — ADLX for AMD, IGCL for Intel (not through LibreHardwareMonitor)
