# HHA Pulse — System Architecture (April 2026)

## One MSIX Package Architecture

```
One MSIX Package (Microsoft Store, free download + IAP):
┌────────────────────────────────────────────────────────┐
│                                                          │
│  HHA Pulse Overlay (FullTrustProcess, WinUI 3)           │
│  Runs as regular user, autostart via startup task        │
│                                                          │
│  Data Collection (no admin needed):                      │
│  ├── Battery: CallNtPowerInformation (%, W, health)      │
│  ├── CPU %: GetSystemTimes + delta                       │
│  ├── GPU: ADLX (AMD) / IGCL (Intel) via Native wrapper  │
│  ├── RAM: GlobalMemoryStatusEx                           │
│  ├── VRAM: Vortice.DXGI IDXGIAdapter3                   │
│  ├── Display: EnumDisplaySettings + DXGI VRR             │
│  ├── GPU % fallback: PDH API (GPU Engine counter)        │
│  ├── FPS/frametime: PresentMon Service API (if installed)│
│  └── CPU temp/fan: PawnIO IOCTLs (if installed)          │
│                                                          │
│  Overlay Rendering (WinUI 3):                            │
│  ├── Top bar: transparent topmost window (DWM interop)   │
│  ├── Side panel: scrollable vertical panel               │
│  ├── 4 presets: Minimal/Standard/Tuner/Diagnostic        │
│  ├── Custom mode: user picks layout + metrics            │
│  └── Metric Picker: toggle any metric ON/OFF             │
│                                                          │
│  Named Pipe Server: \\.\pipe\LOCAL\HHAPulse              │
│  └── Pushes TelemetrySnapshot to widget                  │
│                                                          │
├────────────────────────────────────────────────────────┤
│                                                          │
│  HHA Pulse Widget (UWP XAML, Game Bar SDK v7.3)          │
│                                                          │
│  Standalone (overlay not running):                       │
│  ├── Battery via Windows.System.Power                    │
│  └── Basic CPU/GPU/RAM (pending S6 spike validation)     │
│                                                          │
│  Enhanced (pipe connected):                              │
│  ├── All metrics from overlay via Named Pipe             │
│  └── "Connected to HHA Pulse" indicator                  │
│                                                          │
│  Upsell: IAP purchase flow for overlay features          │
│  "Powered by Handheld Ally"                              │
│                                                          │
└────────────────────────────────────────────────────────┘
```

## Data Flow

```
PresentMon Service (Intel, external)  →  FPS, frametime, GPU Busy, latency, FrameGen
PawnIO driver (external, optional)    →  CPU temp, fan speed, RAPL power
ADLX / IGCL (loaded by overlay)      →  GPU temp, usage, clock, power
Windows APIs (overlay process)        →  Battery, CPU %, RAM, VRAM, display
                    │
                    ▼
          CollectorOrchestrator
          (runs all collectors per tick, try/catch each)
                    │
                    ▼
          MetricAggregator
          (AVG, 1%/0.1% low, bottleneck, battery prediction)
                    │
                    ├──▶ Overlay UI (DispatcherQueue.TryEnqueue → ViewModels → Controls)
                    │
                    └──▶ Named Pipe Server (MessagePack binary push → Widget)
```

## IPC Protocol

- Pipe: `\\.\pipe\LOCAL\HHAPulse`
- Framing: 4-byte LE length prefix + MessagePack payload
- Envelope: version (1 byte) + message type (1 byte) + timestamp + payload
- Push model: overlay pushes at configurable interval (0.5s / 1s / 2s)
- Security: Package SID DACL (same MSIX package)
- Serialization: MessagePack 3.x with source generator (UWP .NET Native safe)

## Key Design Decisions (April 2026)

1. **No Windows Service** — Store policy 10.1.5 prohibits NT services
2. **One MSIX** — widget + overlay in same package (no cross-package IPC issues)
3. **WinUI 3** for overlay (NOT WPF — VRR bug dotnet/wpf#2294)
4. **PawnIO** for ring-0 access (NOT WinRing0 — flagged by Defender)
5. **PresentMon Service API** for FPS (MIT, ETW-based, anti-cheat safe)
6. **ADLX/IGCL** directly for GPU (not through LibreHardwareMonitor)
7. **PDH API** for GPU % fallback (not .NET PerformanceCounter — memory leak)
8. **Vortice.DXGI** for VRAM (MIT, clean COM wrapper)
9. **MessagePack 3.x** for IPC (supports netstandard2.0 + source generator)
10. **msbuild** for build (not dotnet build — UWP + wapproj requirement)

## Target Hardware

- AMD handhelds: ROG Ally, Legion Go (ADLX, FreeSync)
- Intel handhelds: MSI Claw (IGCL, Intel VRR)
- Runtime GPU detection: one universal binary
- x64 only for v1 (all current handhelds are x64)
