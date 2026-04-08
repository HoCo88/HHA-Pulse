# Daily Log - 2026-04-07

## Superseded Historical Context

This was the first scaffold/research day. It is kept only as history.

The architecture recorded here was replaced on 2026-04-08 by the operational rewrite:

- Current runtime uses `HHAPulse.CaptureService` for elevated ETW FPS capture.
- Current overlay uses one slim HUD path only.
- Current overlay does not include side-panel, diagnostic, custom metric picker, frame-generation, bottleneck, or placeholder vendor telemetry UI.
- Current FPS path is the bundled capture service and must not use DWM or D3DKMT shortcuts.

Keep only these still-valid findings from the 2026-04-07 research:

- WinUI 3 remains the overlay renderer; do not switch to WPF.
- No DLL injection, no graphics API hooks, no game memory reads.
- Named pipes are the IPC mechanism.
- Battery, CPU usage, RAM, GPU usage, VRAM, and display refresh can be collected with Windows APIs.
- Vortice.DXGI is the preferred VRAM wrapper.
- PDH is the preferred GPU usage fallback.
- Game Bar/widget research is still useful as a later companion surface, not the current operational blocker.

For current architecture, read:

- `memory-bank/systems/ARCHITECTURE.md`
- `memory-bank/systems/DISTRIBUTION.md`
- `memory-bank/best_practices/windows_service.md`
- `memory-bank/best_practices/presentmon_etw.md`

