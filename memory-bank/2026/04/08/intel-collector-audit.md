# Intel Handheld Collector Audit - 2026-04-08

## Current Status After Operational Rewrite

The audit remains useful for base telemetry, but the HUD now shows only real metrics:

- Battery percent / AC state: supported.
- CPU usage: supported.
- GPU usage: supported through PDH wildcard counter array handling.
- RAM: supported.
- VRAM: moved to Vortice.DXGI `IDXGIAdapter3.QueryVideoMemoryInfo`.
- Display refresh: supported.

Visible now when real data exists:

- GPU temperature via D3DKMT.
- GPU fan RPM via D3DKMT.

Hidden until real data exists:

- CPU temperature.
- CPU package power.
- GPU power.
- GPU clocks.

## Notes

Windows does not expose reliable CPU temperature/fan/power for all handhelds through a simple non-admin user-mode API. Do not show placeholders in the HUD. Add these only when a real privileged/vendor-backed collector exists.

D3DKMT raw GPU power must not be treated as watts. User-facing power/TDP must be watts or hidden.

## Remaining Useful Fixes

- Keep one-shot logging for collector failures; do not spam logs every tick.
- Keep using Windows API sources for base telemetry.
- Keep runtime shared-DLL identity logging and contract validation to catch stale binaries early.
- Validate battery charge/discharge watts separately before showing watts in the HUD.
- Validate GPU temp/fan and VRAM on Intel iGPU after the fresh build/runtime guard work.
- Add a real GPU-watts collector before enabling `gpu_power` in Tuner.
