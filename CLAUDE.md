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

- One in-game HUD only: no side panel, no nested boxes, no second diagnostic overlay. Manual metric selection stays in the companion, not on top of the game.
- Operational universal metrics: FPS, 1% low, frametime, CPU usage, GPU usage, RAM, battery, display Hz.
- Operational hardware-gated metrics: CPU temp, CPU power, GPU temp, GPU clock, GPU power, GPU fan, VRAM, total power, device temp, storage temp, storage wear. Show them when a proved source exists; otherwise render `--`.
- Nominal-but-honestly-labeled metrics: aggregate `cpu_clock` only. Render with the `~` prefix and never describe it as live throttle-accurate per-core frequency.
- Detect-only metrics: NPU presence/vendor may be reported in diagnostics, but no utilization number ships without a proved public source.
- Missing FPS data shows `--`; never fake FPS from DWM, D3DKMT, or timers.
- Never faked: input latency, numeric frame-generation FPS, unsupported vendor temperatures, NPU utilization, or any power/thermal field without a proved source.
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
