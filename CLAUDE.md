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

## Code Review & Fact-Checking

- **Never review or assess code based on assumptions or "vibes."** Every claim must be backed by evidence.
- When reviewing code: read the actual source, trace the logic, and verify behavior against API docs or official documentation. If unsure, search online for authoritative sources (MSDN, vendor SDKs, GitHub repos, RFCs).
- When stating something is wrong: cite the exact line, explain *what* is wrong, *why* it is wrong, and link to the documentation or specification that proves it.
- When stating something is correct: show *where* the data comes from, what API/function produces it, and confirm it matches the official contract.
- If a claim cannot be verified from code or documentation, say so explicitly — do not fill the gap with guesses.
- Deep research is mandatory. Use web search, read vendor docs, check SDK headers, read source code. Go as deep as needed to give a factual answer.
- Every review finding must include: the file and line, what the code does, what it should do, and the authoritative source that confirms the gap.

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
