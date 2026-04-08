# HHA Pulse

Windows handheld performance overlay for local operational builds. Current direction: one slim WinUI 3 HUD plus a bundled elevated ETW capture service for FPS.

## Stack

C# / .NET 8, WinUI 3 overlay, .NET 8 Windows capture service, MessagePack, named pipes IPC, Vortice.DXGI, PDH, Windows battery/display APIs, passive ETW frame capture.

## Current Architecture

```
HHAPulse.Overlay (interactive user, WinUI 3)
  -> detects foreground game PID in the user session
  -> renders one slim HUD bar
  -> collects CPU/GPU/RAM/VRAM/battery/display
  -> sends target PID to capture service
  -> reads FPS/frametime from capture service
  -> publishes \\.\pipe\LOCAL\HHAPulse for widget clients

HHAPulse.CaptureService (elevated Windows service)
  -> owns ETW session HHAPulse_FrameCapture
  -> captures frames only for overlay-provided PID
  -> publishes \\.\pipe\LOCAL\HHAPulse.Capture.Out
  -> receives target PID on \\.\pipe\LOCAL\HHAPulse.Capture.Control
```

## Product Rules

- One in-game HUD only: no side panel, no nested boxes, no diagnostic overlay, no custom metric picker, no preset label.
- HUD metrics: FPS, 1% low, frametime, CPU, GPU, RAM, VRAM, battery, display Hz.
- Missing FPS data shows `--`; never fake FPS from DWM, D3DKMT, or timers.
- Temperature, fan, power, latency, and frame generation stay hidden until a real data source exists.
- The overlay owns foreground detection; the service must not call `GetForegroundWindow` from Session 0.
- No DLL injection, no graphics API hooks, no game memory reads, no analytics.

## Commands

```powershell
dotnet build src\HHAPulse.CaptureService\HHAPulse.CaptureService.csproj -c Release -p:UseSharedCompilation=false --disable-build-servers
dotnet build src\HHAPulse.Overlay\HHAPulse.Overlay.csproj -c Release -p:UseSharedCompilation=false --disable-build-servers
dotnet test tests\HHAPulse.Shared.Tests\HHAPulse.Shared.Tests.csproj -c Release
dotnet test tests\HHAPulse.Overlay.Tests\HHAPulse.Overlay.Tests.csproj -c Release
```

If sandboxed builds hit `obj` access denied, use escalated execution or clean repo-local `bin`/`obj`. Do not test stale binaries.

## Security

- The capture service is the only elevated component.
- Named pipes must use explicit DACLs.
- Service IPC allows interactive user clients and LocalSystem/Admin control.
- All data stays local.
- NuGet packages are centrally pinned.

## Memory Bank

Daily logs live in `memory-bank/YYYY/MM/DD/INDEX.md`. Current architecture is in `memory-bank/systems/`. Best practices are in `memory-bank/best_practices/`.

---
Operational rewrite | April 2026 | .NET 8 / WinUI 3 / ETW service
