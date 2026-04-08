# Intel Handheld Collector Audit - 2026-04-08

## Current Status After Operational Rewrite

The audit remains useful for base telemetry, but the HUD now shows only real metrics:

- Battery percent / AC state: supported.
- CPU usage: supported.
- GPU usage: supported through PDH wildcard counter array handling.
- RAM: supported.
- VRAM: moved to Vortice.DXGI `IDXGIAdapter3.QueryVideoMemoryInfo`.
- Display refresh: supported.

Hidden until real data exists:

- CPU temperature.
- CPU package power.
- Fan RPM.
- GPU temperature.
- GPU power.
- GPU clocks.

## Notes

Windows does not expose reliable CPU temperature/fan/power for all handhelds through a simple non-admin user-mode API. Do not show placeholders in the HUD. Add these only when a real privileged/vendor-backed collector exists.

## Remaining Useful Fixes

- Keep one-shot logging for collector failures; do not spam logs every tick.
- Keep using Windows API sources for base telemetry.
- Validate battery charge/discharge watts separately before showing watts in the HUD.
- Validate VRAM on Intel iGPU and AMD handhelds after the Vortice.DXGI migration.
