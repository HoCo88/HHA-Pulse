# HHA Pulse Telemetry Traceability Matrix

This matrix is the recovery baseline for HHA Pulse as of 2026-04-09. It preserves the current overlay + capture-service architecture, distinguishes validated versus disputed sources, and keeps the widget on the same read-only truth model.

## Runtime Boundaries

| Boundary | Owner | Transport | Runtime visibility |
| --- | --- | --- | --- |
| Foreground target -> capture control | `HHAPulse.Overlay` | `LOCAL\HHAPulse.Capture.Control` | Target PID/name and capture connection state in desktop shell |
| Capture output -> overlay collector | `HHAPulse.CaptureService` -> `CaptureServiceCollector` | `LOCAL\HHAPulse.Capture.Out` | Capture status, payload age, target PID/name |
| Collectors -> `TelemetrySnapshot` | `CollectorOrchestrator` | in-process | Per-metric validation rows in Metrics |
| Overlay -> widget companion | `HHAPulse.Overlay` | `LOCAL\HHAPulse` | Widget client count last observed during broadcast writes and widget pipe status |

## Metric Sources

| Metric | Owner | Source/API | Elevation | Freshness / fallback | Truth state |
| --- | --- | --- | --- | --- | --- |
| `fps` | Capture service | ETW present events | Elevated service | 2s freshness gate; unavailable when stale or disconnected | Verified |
| `avg_fps` | Capture service | ETW aggregate frame stats | Elevated service | Same as FPS | Verified |
| `one_percent_low` | Capture service | ETW aggregate frame stats | Elevated service | Same as FPS | Verified |
| `zero_point_one_low` | Capture service | ETW aggregate frame stats | Elevated service | Same as FPS | Verified |
| `frametime` | Capture service | ETW frame timing | Elevated service | 2s freshness gate | Verified |
| `framegen_fps` | Capture service | Intel-PresentMon ETW evidence | Elevated service | Diagnostic-only in this phase; numeric FG FPS remains disabled until a validated generated/display rate exists | Diagnostic-only; driver instrumentation required |
| `input_latency` | Reserved id only | No active source | N/A | Hidden from HUD, picker, status cards, and traces until a real input-to-display latency source exists | Unavailable / not displayed |
| `cpu` | Overlay | `GetSystemTimes` deltas | User | No fallback | Verified |
| `cpu_power` | Overlay | EMI energy deltas | User | No fallback | Disputed / pending hardware proof |
| `gpu` | Overlay | PDH GPU Engine counters | User | No fallback | Verified |
| `gpu_temp` | Overlay | D3DKMT fallback or vendor SDK | User | Fallback to D3DKMT when available | Mixed: D3DKMT verified, ADLX/IGCL disputed |
| `gpu_clock` | Overlay | Vendor SDK | User | No fallback | NvAPI verified in code path; ADLX/IGCL disputed |
| `gpu_power` | Overlay | Vendor SDK | User | No fallback; D3DKMT PowerRaw is rejected as not convertible to watts | NVML verified in code path; ADLX/IGCL hardware validation pending |
| `gpu_fan` | Overlay | D3DKMT fallback or vendor SDK | User | Fallback to D3DKMT when available | Mixed: D3DKMT verified, ADLX/IGCL disputed |
| `ram` | Overlay | `GlobalMemoryStatusEx` | User | No fallback | Verified |
| `vram` | Overlay | DXGI `QueryVideoMemoryInfo` | User | Hidden/unavailable when not meaningful | Verified |
| `total_power` | Overlay | Whole-device battery discharge watts | User | Unavailable on AC/charging; CPU+GPU sum is diagnostics-only and never labeled whole-device power | Verified when discharging |
| `refresh_rate` | Overlay | `EnumDisplaySettings` | User | Nominal display refresh only | Verified |
| `battery` | Overlay | `CallNtPowerInformation` | User | No fallback | Verified |

## Native Bridge Coverage

| Native path | Metrics | Current posture |
| --- | --- | --- |
| `HHAPulse.Native.dll` -> ADLX | GPU temp, clock, power, fan on AMD | Prefers `GPUTotalBoardPower`; falls back to `GPUPower`; hardware validation pending |
| `HHAPulse.Native.dll` -> IGCL | GPU temp, clock, power, fan on Intel | GPU watts from energy/time deltas; hardware validation pending |
| EMI collector | CPU package power | Quarantined until build + hardware validation |

## UX Rules Backed By This Matrix

- The HUD only renders validated metrics with correct units.
- `total_power` is whole-device battery discharge watts only. CPU+GPU component sum is diagnostics-only because it excludes display, memory, SSD, fans, radios, and platform losses.
- D3DKMT `PowerRaw` is never converted to watts; it is recorded as rejected provenance.
- VRR fields remain unused contract fields in this phase. No display-mode writes, VRR toggles, driver profile writes, hooks, or injection are part of this telemetry path.
- The desktop shell carries provenance and diagnostics; the widget stays companion-only.
- Shared contracts remain append-only. Pipe names, message types, and existing MessagePack keys stay compatibility-critical.
