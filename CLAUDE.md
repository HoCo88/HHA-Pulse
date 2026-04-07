# HHA Pulse

Windows handheld performance overlay. One install, one app, replaces RTSS + HWiNFO + Afterburner.

## Stack

C# / .NET 8, WinUI 3 (Overlay UI), UWP XAML (Game Bar Widget), Windows Service (Helper), Named Pipes IPC, PresentMon ETW, PawnIO driver, ADLX (AMD), IGCL (Intel).

## Architecture

```
Helper Service (SYSTEM) --> Named Pipe --> Overlay UI (user) + Game Bar Widget (UWP)
```

- Helper Service: PresentMon ETW, PawnIO, ADLX/IGCL, battery APIs, named pipe server
- Overlay UI: WinUI 3, top bar + side panel, hotkey handler, settings
- Game Bar Widget: UWP XAML, free companion, Game Bar Widget Store

## Commands

```bash
dotnet build                    # Build all projects
dotnet test                     # Run all tests
dotnet publish -c Release       # Release build
```

## Rules

- Read the actual code before any change. Never assume API behavior -- check docs and source.
- NEVER use WPF for overlay rendering. WPF has a known VRR bug (dotnet/wpf#2294).
- NEVER inject DLLs into game processes. NEVER hook DirectX/Vulkan. This is an anti-cheat safety rule.
- NEVER read game process memory. Use PresentMon ETW for FPS data (passive event tracing).
- All Named Pipe communication must use explicit DACLs. No default security.
- P/Invoke: always pin managed objects, use SafeHandle, free unmanaged memory.
- Async/await: always use ConfigureAwait(false) in library/service code. Never block on async (.Result, .Wait()).
- IDisposable: implement full pattern in any class holding unmanaged resources or subscriptions.
- PawnIO driver: only load from verified path with hash check. Never load arbitrary drivers.
- Helper Service: zero network access. Local Named Pipes only.
- No telemetry, no tracking, no analytics. All data stays local.

## Security

- Code-sign everything: EV cert for app, WHQL for driver, Store cert for widget.
- Named Pipes: explicit DACL (SYSTEM + local user + Package SID only).
- Service runs with restricted token -- drop unnecessary privileges.
- All NuGet packages pinned to specific verified versions.
- No WinRing0 dependency (flagged by Defender). Use PawnIO.

## Team Workflow

- Use teammates (TeamCreate) for parallel research or multi-file changes.
- HANDS OFF files that teammates are editing.
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
