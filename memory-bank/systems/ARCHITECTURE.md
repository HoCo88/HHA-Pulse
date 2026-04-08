# HHA Pulse - System Architecture (April 2026)

## Operational Build

HHA Pulse has two local executables:

- `HHAPulse.Overlay`: WinUI 3 full-trust overlay running as the interactive user.
- `HHAPulse.CaptureService`: elevated Windows service for ETW FPS capture.

The overlay owns foreground-window detection in the user session and sends the target PID to the capture service. The service owns the elevated ETW session and publishes frame metrics back over named pipes. This avoids DLL injection, game hooks, and game memory reads.

## Data Sources

| Metric | Source | Collector | Elevation |
|--------|--------|-----------|-----------|
| FPS / frametime / 1% / 0.1% lows | ETW `HHAPulse_FrameCapture` | `CaptureServiceCollector` | Service (elevated) |
| Frame Gen FPS | ETW (auto-detected via `MetricFlags.FrameGen`) | `CaptureServiceCollector` | Service (elevated) |
| CPU usage | `GetSystemTimes` delta | `CpuUsageCollector` | User |
| GPU usage | PDH `\GPU Engine(*engtype_3D*)\Utilization Percentage` | `GpuUsageCollector` | User |
| GPU temp / power / fan | `D3DKMTQueryAdapterInfo(KMTQAITYPE_ADAPTERPERFDATA)` via gdi32.dll | `GpuPerfDataCollector` | User (no elevation) |
| RAM | `GlobalMemoryStatusEx` | `RamCollector` | User |
| VRAM | Vortice.DXGI `IDXGIAdapter3.QueryVideoMemoryInfo` | `VramCollector` | User |
| Battery / charge / discharge | `CallNtPowerInformation` | `BatteryCollector` | User |
| Display Hz | `EnumDisplaySettings` | `DisplayCollector` | User |

### Not yet implemented (show `--`)

- CPU temperature / power: needs MSR access (PawnIO) or WMI — no collector yet.
- Input latency: needs PresentMon ETW parsing — not in capture service yet.
- System total power: falls back to battery discharge watts; CPU+GPU sum only if both available.

## GPU Detection

GPU vendor and type are detected via `DXGI_ADAPTER_DESC1`:

| Field | Intel iGPU | Intel Arc | AMD APU | AMD/NVIDIA discrete | eGPU |
|-------|-----------|-----------|---------|---------------------|------|
| `VendorId` | `0x8086` | `0x8086` | `0x1002` | `0x1002`/`0x10DE` | any |
| `DedicatedVideoMemory` | ~128MB | 8GB+ | 512MB–8GB | 4–24GB | 4–24GB |

- **Vendor IDs**: `0x8086` = Intel, `0x1002` = AMD, `0x10DE` = NVIDIA (confirmed via [MS DXGI docs](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/ns-dxgi-dxgi_adapter_desc1)).
- **VRAM visibility**: Intel iGPU (`VendorId == 0x8086 && DedicatedVideoMemory <= 256MB`) → VRAM metric hidden (no real VRAM, shared system memory already shown by RAM). All other GPUs → VRAM shown.
- **Caveat**: Intel iGPUs can report non-zero `DedicatedVideoMemory` ([wgpu issue #683](https://github.com/gfx-rs/wgpu/issues/683)). The VendorId check prevents false positives. A more robust method would be `D3D12_FEATURE_DATA_ARCHITECTURE.UMA` flag but requires creating a D3D12 device.

## D3DKMT GPU Perf Data

`D3DKMTQueryAdapterInfo` with `KMTQAITYPE_ADAPTERPERFDATA` (type 62) reads GPU temperature, power draw, and fan RPM directly from the Windows kernel via gdi32.dll. This is the same API Windows Task Manager uses. No vendor SDK, no elevation.

- **Temperature**: stored in deci-Celsius (raw / 10 = °C).
- **Power**: [MS docs say "tenths of percentage of TDP"](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/d3dkmthk/ns-d3dkmthk-_d3dkmt_adapter_perfdata) (raw / 10 = % TDP). Some vendor drivers may report watts instead. The overlay uses a heuristic: values 0–100 display as `%`, values >100 display as `W`.
- **Fan RPM**: direct RPM value.
- **Struct layout**: Uses `LayoutKind.Explicit` with manual `[FieldOffset]` attributes to match `D3DKMT_ALIGN64` padding in the native header.
- **Intel iGPU support**: unknown — the init test-read gracefully handles failure (shows `--`).
- **WDDM 2.4+ required**: works on any modern AMD, Intel, NVIDIA driver.

## UI Layout

Component-grouped HUD bar. Metrics grouped by hardware component:

- **FPS group**: FPS + AVG + 1% + 0.1% + FrameGen (auto-detected) + sparkline graph. Stacks to 2 sub-rows when 3+ metrics active, graph spans both rows.
- **Frametime**: separate from FPS, own sparkline graph. Label "Frametime" not "FT".
- **CPU**: `CPU 45% 12W` — usage + power inline under one label.
- **GPU**: `GPU 85% 72°C 15.2W 4.2/8G` — usage + temp + power + VRAM all under "GPU" label.
- **RAM**: `RAM 14.3/31.5G`.
- **System**: battery icon + value, Hz icon + value (Segoe Fluent Icons).

**Row logic**: Always tries single row first. Measures actual rendered width. Splits to two rows (Row 1: performance, Row 2: hardware) only if content exceeds screen width. No arbitrary thresholds.

## Presets

- **Minimal**: FPS + Battery (2 metrics).
- **Standard**: FPS + 1% low + Frametime + CPU + GPU + Battery (6 metrics).
- **Tuner**: All metrics with real data sources (11 metrics).
- **Custom**: user toggles individual metrics from all 17 known IDs.
- **Off**: hidden.

Cycle: Minimal → Standard → Tuner → Off → Minimal (Custom is manual only).

## IPC

- Overlay/widget telemetry pipe: `LOCAL\HHAPulse`.
- Capture metrics pipe: `LOCAL\HHAPulse.Capture.Out`.
- Capture control pipe: `LOCAL\HHAPulse.Capture.Control`.

## Settings

Persisted to `%LOCALAPPDATA%\HHAPulse\settings.json`. Includes:
- Active preset, enabled metric IDs (for Custom).
- Background opacity, text opacity (user-adjustable via Control Window sliders).
- Show mode (Always / In-game only).
- Text size in pixels (user-adjustable via slider, 10-22px).
- `SettingsService` handles empty/corrupt JSON files gracefully (falls back to defaults).

## Known Issues (as of 2026-04-08 evening)

- **GPU temp/power/fan never tested on hardware** — every build since `FanRpm` was added failed due to file locks from running overlay process. D3DKMT init succeeds but `CollectAsync` crashes with `MissingMethodException` on the stale DLL. Must kill processes before building.
- **VRAM Intel hide never tested** — same stale DLL issue.
- **D3DKMT on Intel iGPU** — unknown whether Intel drivers implement `KMTQAITYPE_ADAPTERPERFDATA`. Init test-read will tell us once a fresh build runs.
