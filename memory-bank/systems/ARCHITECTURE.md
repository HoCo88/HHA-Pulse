# HHA Pulse - System Architecture (April 2026)

## Operational Build

HHA Pulse now has two local executables:

- `HHAPulse.Overlay`: WinUI 3 full-trust overlay running as the interactive user.
- `HHAPulse.CaptureService`: elevated Windows service for ETW FPS capture.

The overlay owns foreground-window detection in the user session and sends the target PID to the capture service. The service owns the elevated ETW session and publishes frame metrics back over named pipes. This avoids DLL injection, game hooks, and game memory reads.

## Data Sources

- FPS / frametime / lows: `HHAPulse.CaptureService` ETW session, target PID supplied by overlay.
- CPU usage: `GetSystemTimes` delta.
- GPU usage: PDH GPU Engine counters.
- RAM: `GlobalMemoryStatusEx`.
- VRAM: Vortice.DXGI `IDXGIAdapter3.QueryVideoMemoryInfo`.
- Battery: `CallNtPowerInformation`.
- Display refresh: Windows display APIs.

CPU/GPU temperature, power, fan, latency, and frame generation are not shown until backed by real data.

## UI

The runtime overlay is one slim HUD bar. No side panel, metric picker, diagnostic overlay, preset label, nested cards, or user-visible dependency prompts are part of the in-game HUD.

## IPC

- Overlay/widget telemetry pipe: `LOCAL\HHAPulse`.
- Capture metrics pipe: `LOCAL\HHAPulse.Capture.Out`.
- Capture control pipe: `LOCAL\HHAPulse.Capture.Control`.
