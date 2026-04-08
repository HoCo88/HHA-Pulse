# Daily Log - 2026-04-08

## Operational Rewrite (Morning)

The old multi-preset prototype was replaced by one product path:

- One slim HUD bar rendered by `MainWindow` + `TopBarControl`.
- FPS routed through `HHAPulse.CaptureService`, an elevated ETW capture service.
- Overlay detects foreground game PID and sends to service over `LOCAL\HHAPulse.Capture.Control`.
- Service publishes frame metrics over `LOCAL\HHAPulse.Capture.Out`.
- Missing data shows `--`; no fake values ever.

## Component-Grouped Layout Redesign (Afternoon)

Major UI redesign: metrics grouped by hardware component instead of individual cells.

### Layout

- **FPS group**: FPS + AVG + 1% + 0.1% + FrameGen (auto) + sparkline graph. 2 sub-rows at 3+ metrics, graph spans both rows.
- **Frametime**: separate component with own sparkline graph. Label "Frametime" not "FT".
- **CPU**: `CPU 45% 12W` — all CPU metrics inline under one label.
- **GPU**: `GPU 85% 72°C 15.2W 4.2/8G` — usage + temp + power + VRAM all under "GPU" label. No separate VRAM cell.
- **RAM**: `RAM 14.3/31.5G`.
- **System**: BAT + value, Hz icon + value.

### Row logic (FINAL — width-based, not count-based)

Single row is always tried first. The bar measures actual rendered width. Only splits to two rows (perf row + hardware row) if content exceeds screen width. No arbitrary component count thresholds. This was broken in earlier iterations that used element count (which included separators) or fixed component count rules.

### Presets (restored)

- Minimal (2), Standard (6), Tuner (11), Custom (all 17), Off.
- Custom mode has toggle switches in Control Window.
- Cycle: Minimal → Standard → Tuner → Off → Minimal.

## D3DKMT GPU Perf Data Collector

New `GpuPerfDataCollector` reads GPU temp, power, fan RPM via `D3DKMTQueryAdapterInfo(KMTQAITYPE_ADAPTERPERFDATA)` from gdi32.dll. Same API as Windows Task Manager. No vendor SDK, no elevation, WDDM 2.4+.

### Key findings (verified via web search)

- **Power field**: [MS docs](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/d3dkmthk/ns-d3dkmthk-_d3dkmt_adapter_perfdata) say "tenths of percentage of TDP" (not watts). Some drivers may report watts. Overlay uses heuristic: 0–100 → `%`, >100 → `W`. Init log dumps raw value for hardware validation.
- **Struct alignment**: Must use `LayoutKind.Explicit` with `[FieldOffset]` because of `D3DKMT_ALIGN64` padding after the `uint PhysicalAdapterIndex` field.
- **Intel iGPU support**: UNKNOWN — never tested with fresh binary (see stale DLL issue below).

## What Works (Verified on Intel hardware)

| Metric | Status | Notes |
|--------|--------|-------|
| CPU usage | **WORKING** | Shows 30-39% in screenshots |
| GPU usage (PDH) | **WORKING** | Shows 0-1% (desktop, no game running) |
| RAM | **WORKING** | Shows 14.3-15.4/31.5G correctly |
| Battery | **WORKING** | Shows %, time remaining, discharge watts (e.g. 79% 4h42m 12.8W) |
| Display Hz | **WORKING** | Shows 50 (correct for user's display) |
| FPS / frametime / lows | Shows `--` | Expected — no game running, capture service needs a target PID |
| Frame Gen FPS | Shows `--` | Expected — no frame gen detected |
| Input Latency | Shows `--` | Expected — no PresentMon parsing in capture service yet |
| Background opacity slider | **WORKING** | 0% = transparent, 100% = opaque |
| Text opacity slider | **WORKING** | Controls metric text transparency |
| Text size slider | **NEW** | 10-22px range, saved to settings |
| Preset switching | **WORKING** | Minimal/Standard/Tuner/Custom/Off all work |
| Custom metric toggles | **WORKING** | Individual metric toggle switches |
| Component grouping | **WORKING** | CPU/GPU show inline values |
| FPS sparkline graph | **WORKING** | Multi-series (FPS green, 1% yellow, 0.1% orange) |
| Frametime sparkline | **WORKING** | Separate graph from FPS |

## What Does NOT Work Yet (Needs Fresh Build)

| Metric | Status | Root Cause |
|--------|--------|------------|
| GPU temp | Shows `--` | **STALE DLL** — `MissingMethodException: GpuMetrics.set_FanRpm` every tick. D3DKMT init succeeded but CollectAsync crashes on the missing property. |
| GPU power | Shows `--` | Same stale DLL issue. |
| GPU fan RPM | Shows `--` | Same stale DLL issue. |
| VRAM hidden on Intel | **NOT VERIFIED** | Code is correct (`VendorId == 0x8086` check) but never ran with fresh binary. |

### The Stale DLL Problem

Every build since the `FanRpm` field was added to `GpuMetrics` has FAILED because the running overlay process (`HHAPulse.Overlay.exe`) locks DLLs in the `bin\Release` output folder. MSBuild retries 10 times then errors with `MSB3027`. The user rebuilds and runs but gets the OLD binary. This means:

- D3DKMT collector crashes every tick with `MissingMethodException`.
- VRAM Intel check was never tested.
- GPU temp/power values were never displayed.
- All "fix" iterations for GPU data were untested.

**Resolution**: Kill `HHAPulse.Overlay.exe` AND `HHA Pulse FPS Capture.exe` BEFORE building. Both processes lock shared DLLs.

## Intel iGPU Detection (Verified via web search)

- `DXGI_ADAPTER_DESC1.VendorId`: `0x8086` = Intel, `0x1002` = AMD, `0x10DE` = NVIDIA ([MS docs](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/ns-dxgi-dxgi_adapter_desc1)).
- `DedicatedVideoMemory` alone is NOT reliable for iGPU detection — Intel iGPUs can report non-zero values ([wgpu #683](https://github.com/gfx-rs/wgpu/issues/683)).
- VendorId + DedicatedVideoMemory threshold (`<= 256MB`) is the current approach.
- More robust: `D3D12_FEATURE_DATA_ARCHITECTURE.UMA` flag (not implemented, requires D3D12 device).

## Bugs Found & Fixed

1. **`MissingMethodException: GpuMetrics.set_FanRpm`** — stale `HHAPulse.Shared.dll`. Fix: kill processes before building.
2. **Empty `settings.json` crash** — `File.Create()` truncates; `JsonException` on next launch. Fix: `SettingsService` checks `stream.Length == 0` and catches `JsonException`.
3. **Two-row triggered too aggressively** — element count included separators (`CPU + Sep + GPU + Sep + BAT = 5 > 2`). Then fixed to component count but threshold was wrong. **Final fix**: measure actual width, split only if exceeds screen.
4. **`MainWindow.TopBar` inaccessible** — XAML `x:Name` is private. Fix: `MainWindow.ApplyOpacity()` / `ApplyTextSize()` public methods.
5. **Battery icon unreadable** — Segoe Fluent Icons `\uE996` too small/ambiguous at 13px. Fix: replaced with "BAT" text.
6. **D3DKMT Power field semantics** — MS docs say "tenths of % TDP", not watts. Fix: heuristic display (0-100 = %, >100 = W). Init log dumps raw value.
7. **`CollectorOrchestrator` didn't dispose collectors** — D3DKMT adapter handle leaked. Fix: implements `IDisposable`.

## Build Status

- Build: 0 warnings, 0 errors.
- Tests: 21/21 overlay, 4/4 shared.
- **CRITICAL**: User has never run a fresh build since FanRpm was added. All GPU perf data is untested on hardware.

## Research Sources

- [DXGI_ADAPTER_DESC1 - MS Learn](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/ns-dxgi-dxgi_adapter_desc1)
- [D3DKMT_ADAPTER_PERFDATA - MS Learn](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/d3dkmthk/ns-d3dkmthk-_d3dkmt_adapter_perfdata)
- [D3DKMT_ADAPTER_PERFDATACAPS - MS Learn](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/d3dkmthk/ns-d3dkmthk-_d3dkmt_adapter_perfdatacaps)
- [How to filter GPUs by type in DXGI - MS Q&A](https://learn.microsoft.com/en-sg/answers/questions/2134992/how-do-i-filter-gpus-by-type-in-dxgi-(integrated-v)
- [Intel iGPU reported as discrete - wgpu #683](https://github.com/gfx-rs/wgpu/issues/683)
- [DXGI_ADAPTER_DESC2 - MS Learn](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_2/ns-dxgi1_2-dxgi_adapter_desc2)

## Next Steps

1. **Kill processes and rebuild** — Critical. No GPU perf data, no VRAM fix, no layout fixes have been tested on hardware.
2. **Check log after fresh build** — Look for `GPU Perf: D3DKMT initialized. Test read:` line to see if Intel iGPU reports temp/power.
3. **Check log for VRAM** — Should show `VRAM: Intel iGPU detected... VRAM metric hidden.`
4. **Validate D3DKMT Power raw value** — Log shows raw value. Determine if Intel reports % or W.
5. **Test with a game running** — FPS/frametime/lows need the capture service connected with a target PID.
