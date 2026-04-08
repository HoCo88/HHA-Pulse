# HHA Pulse - Research Archive

This folder is an archive. The active architecture is the operational rewrite from 2026-04-08:

- WinUI 3 interactive overlay.
- Bundled elevated ETW capture service for FPS and frametime.
- One slim HUD path only.
- Foreground PID is detected by the overlay and sent to the service.
- No DLL injection, no graphics hooks, no game memory reads.
- No DWM/D3DKMT FPS shortcut.
- No in-game diagnostics, side panel, metric picker, or placeholder vendor telemetry.

Historical research remains useful for anti-cheat safety, VRR safety, Windows telemetry APIs, Game Bar concepts, ADLX/IGCL possibilities, and handheld UX preferences. When historical notes conflict with the current architecture, the current architecture wins.

Key sources:

- Microsoft ETW docs: https://learn.microsoft.com/en-us/windows/win32/api/evntrace/nf-evntrace-starttracew
- Microsoft DXGI video memory docs: https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_4/nf-dxgi1_4-idxgiadapter3-queryvideomemoryinfo
- Xbox Game Bar SDK: https://learn.microsoft.com/en-us/gaming/game-bar/
- ADLX SDK: https://gpuopen.com/adlx/
- IGCL SDK: https://intel.github.io/drivers.gpu.control-library
