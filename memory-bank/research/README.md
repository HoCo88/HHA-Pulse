# HHA Pulse - Research Archive

This folder is an archive. The active architecture is the operational rewrite from 2026-04-08:

- WinUI 3 interactive overlay.
- Bundled elevated ETW capture service for FPS and frametime.
- One slim HUD bar, component-grouped layout.
- Foreground PID is detected by the overlay and sent to the service.
- No DLL injection, no graphics hooks, no game memory reads.
- No DWM/D3DKMT FPS shortcut.

## Active Data Sources (no vendor SDK, no elevation except ETW service)

- FPS / frametime / lows / frame gen: ETW via capture service.
- CPU usage: `GetSystemTimes`.
- GPU usage: PDH GPU Engine counters.
- GPU temp / fan / raw power diagnostics: `D3DKMTQueryAdapterInfo(KMTQAITYPE_ADAPTERPERFDATA)` via gdi32.dll. Same as Task Manager.
- RAM: `GlobalMemoryStatusEx`.
- VRAM: Vortice.DXGI. Hidden on Intel iGPU (vendor `0x8086`).
- Battery: `CallNtPowerInformation`.
- Display Hz: `EnumDisplaySettings`.

## Verified Research (2026-04-08)

### GPU detection
- DXGI `VendorId`: `0x8086`=Intel, `0x1002`=AMD, `0x10DE`=NVIDIA. Source: [MS DXGI docs](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/ns-dxgi-dxgi_adapter_desc1).
- `DedicatedVideoMemory` alone is NOT reliable for iGPU detection. Intel iGPUs report non-zero values. Source: [wgpu #683](https://github.com/gfx-rs/wgpu/issues/683).
- More robust iGPU detection: `D3D12_FEATURE_DATA_ARCHITECTURE.UMA` flag. Source: [MS Q&A](https://learn.microsoft.com/en-sg/answers/questions/2134992/how-do-i-filter-gpus-by-type-in-dxgi-(integrated-v).

### D3DKMT perf data
- Power field: "tenths of percentage of TDP" per [MS docs](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/d3dkmthk/ns-d3dkmthk-_d3dkmt_adapter_perfdata). Do not surface this as GPU watts. Current product rule: user-facing power/TDP must be watts or hidden.
- Temperature: deci-Celsius.
- Struct needs `LayoutKind.Explicit` for `D3DKMT_ALIGN64` padding.
- Intel iGPU support: unconfirmed.

### Runtime stability
- Startup now logs loaded `HHAPulse.Shared.dll` path/version/MVID and validates the runtime telemetry contract before collectors start.
- Focused local build/test wrappers should shut down dotnet build servers before and after runs to reduce stale output/lock issues.

### Not yet researched
- CPU temperature via MSR / PawnIO — need to investigate if this works without kernel driver on modern Windows.
- PresentMon ETW for input latency and frame gen detection — capture service would need to parse additional ETW providers.

## Historical Research

Historical research remains useful for anti-cheat safety, VRR safety, Windows telemetry APIs, Game Bar concepts, ADLX/IGCL possibilities, and handheld UX preferences. When historical notes conflict with the current architecture, the current architecture wins.

Key historical sources:

- Microsoft ETW docs: https://learn.microsoft.com/en-us/windows/win32/api/evntrace/nf-evntrace-starttracew
- Microsoft DXGI video memory docs: https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_4/nf-dxgi1_4-idxgiadapter3-queryvideomemoryinfo
- Xbox Game Bar SDK: https://learn.microsoft.com/en-us/gaming/game-bar/
- ADLX SDK: https://gpuopen.com/adlx/
- IGCL SDK: https://intel.github.io/drivers.gpu.control-library
