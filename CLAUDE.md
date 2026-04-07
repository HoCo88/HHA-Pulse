# HHA Pulse

Windows handheld performance overlay. Store-delivered paid app plus free Xbox Game Bar widget funnel for the Handheld Ally community.

## Stack

C# / .NET 8, WinUI 3 (paid overlay), UWP XAML (free Game Bar widget), Named Pipes IPC, PresentMon Service API, optional PawnIO, ADLX (AMD), IGCL (Intel), C++ native wrappers.

## Architecture

```
HHA Pulse paid WinUI 3 FullTrustProcess
  -> collects metrics as current user
  -> renders overlay
  -> publishes \\.\pipe\LOCAL\HHAPulse
  -> HHA Pulse Widget free UWP Game Bar client
```

- No HHA Pulse Windows Service in v1.
- No LocalSystem process in v1.
- PresentMon is an external user-installed dependency for FPS/frametime/latency/FrameGen.
- PawnIO is optional only for CPU MSR temp, fan, and RAPL power.
- The app never bundles, downloads, or installs PresentMon or PawnIO.

## Commands

```bash
msbuild HHAPulse.sln /p:Configuration=Release /p:Platform=x64
dotnet test tests/HHAPulse.Shared.Tests/HHAPulse.Shared.Tests.csproj
dotnet test tests/HHAPulse.Overlay.Tests/HHAPulse.Overlay.Tests.csproj
dotnet publish src/HHAPulse.Overlay/HHAPulse.Overlay.csproj -c Release -r win-x64 --self-contained
```

`dotnet publish` is for personal development/testing EXE output only. Store builds use MSIX packaging projects and full Visual Studio/MSBuild on Windows.

## Rules

- Read the actual code before any change. Never assume API behavior; check docs and source.
- NEVER use WPF for overlay rendering. WPF has a known VRR bug (dotnet/wpf#2294).
- NEVER inject DLLs into game processes. NEVER hook DirectX/Vulkan.
- NEVER read game process memory. Use PresentMon Service API or passive ETW-derived data.
- All Named Pipe communication must use explicit DACLs. No default security.
- Pipe name is `\\.\pipe\LOCAL\HHAPulse`.
- P/Invoke: always pin managed objects, use SafeHandle, free unmanaged memory.
- Async/await: always use ConfigureAwait(false) in library code. Never block on async (.Result, .Wait()).
- IDisposable: implement full pattern in any class holding unmanaged resources or subscriptions.
- PawnIO: detect only if already installed, hash-verify before use, never load arbitrary drivers.
- No telemetry, no tracking, no analytics. All data stays local.

## Security

- Store MSIX signing is handled by Microsoft Store.
- Named Pipes: explicit DACL for current user and widget Package SID; use S2 spike result for final ACL shape.
- All NuGet packages pinned to specific verified versions.
- No WinRing0 dependency. PawnIO is optional.
- External dependency use must be disclosed in Store certification notes.

## Team Workflow

- Use focused agents for independent workstreams.
- HANDS OFF files that another teammate is editing.
- For single-file work, handle directly.

## Git

- Conventional commits: `feat:`, `fix:`, `docs:`, `refactor:`, `test:`
- Small, focused commits
- NEVER include AI attribution or co-author tags
- Branch naming: `feature/xxx`, `fix/xxx`

## Memory Bank

Daily logs in `memory-bank/YYYY/MM/DD/INDEX.md`. Systems docs in `memory-bank/systems/`. Best practices in `memory-bank/best_practices/`. Research in `memory-bank/research/`.

## Reference

- Design spec: `docs/superpowers/specs/2026-04-07-hha-pulse-design.md`
- Best practices: `memory-bank/best_practices/`
- System architecture: `memory-bank/systems/`
- Research findings: `memory-bank/research/`

---
v1.0 | April 2026 | C# / .NET 8 / WinUI 3
