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
- **GPU**: `GPU 85% 72°C 2400rpm 4.2/8G` — usage + temp + fan + VRAM all under "GPU" label. GPU watts stay hidden until real. No separate VRAM cell.
- **RAM**: `RAM 14.3/31.5G`.
- **System**: BAT + value, Hz icon + value.

### Row logic (FINAL — width-based, not count-based)

Single row is always tried first. The bar measures actual rendered width. Only splits to two rows (perf row + hardware row) if content exceeds screen width. No arbitrary component count thresholds. This was broken in earlier iterations that used element count (which included separators) or fixed component count rules.

### Presets (restored)

- Minimal (2), Standard (6), Tuner (11), Custom (all 18), Off.
- Custom mode has toggle switches in Control Window.
- Cycle: Minimal → Standard → Tuner → Off → Minimal.

## D3DKMT GPU Perf Data Collector

New `GpuPerfDataCollector` reads GPU temp and fan RPM via `D3DKMTQueryAdapterInfo(KMTQAITYPE_ADAPTERPERFDATA)` from gdi32.dll. Same API as Windows Task Manager. No vendor SDK, no elevation, WDDM 2.4+.

### Key findings (verified via web search)

- **Power field**: [MS docs](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/d3dkmthk/ns-d3dkmthk-_d3dkmt_adapter_perfdata) say "tenths of percentage of TDP" (not watts). Do NOT display this as GPU watts. Current code logs `PowerRaw` in diagnostics only and hides `gpu_power` until a validated true-watts source exists.
- **Struct alignment**: Must use `LayoutKind.Explicit` with `[FieldOffset]` because of `D3DKMT_ALIGN64` padding after the `uint PhysicalAdapterIndex` field.
- **Intel iGPU support**: UNKNOWN until the freshly built overlay is launched and the log is checked.

## Telemetry Stabilization (Evening)

Implemented after the stale DLL diagnosis:

- `Directory.Packages.props`: Windows App SDK updated from `1.6.250205002` to `1.8.260317003`; .NET remains on .NET 8.
- Startup runtime guard added in `TelemetryContractGuard`: logs loaded `HHAPulse.Shared.dll` path, version, and MVID, checks required `GpuMetrics` properties, and fails clearly if the runtime binary is stale.
- `TelemetrySnapshot` now carries append-only diagnostics fields: shared assembly identity, telemetry contract status, capture target identity, and per-metric health via `MetricStatus`.
- Control window now has a Diagnostics section: capture/GPU telemetry status, target process, loaded shared DLL identity, metric health summary, and Open log folder button.
- `CollectorOrchestrator` now logs collector init/collection failures once instead of spamming every tick.
- `GpuPerfDataCollector` now prefers a display-attached adapter (`NumOfSources > 0`) and logs selected LUID/source count.
- `VramCollector` logs selected DXGI adapter details. Intel iGPU VRAM hide logic still needs hardware verification.
- `gpu_fan` is now a real metric path: shared model, D3DKMT collector, compact formatter, grouped GPU HUD row, Custom picker, Tuner preset, and tests.
- `gpu_power` remains a known Custom metric for a future true-watts source, but is NOT in Tuner and is NOT populated from D3DKMT raw power.
- FPS model now has append-only `AppFramesPerSecond`, `PresentFramesPerSecond`, `DisplayFramesPerSecond`, and `HybridPresentDetected` fields. Current ETW service still fills them as scaffolding; deeper PresentMon-style parsing remains future work.
- Added `scripts/rebuild-clean.cmd` / `.ps1`: shuts down dotnet build servers, optionally stops running `HHAPulse*` processes, cleans selected output folders, and builds a selected target. Default target is Overlay, not full solution, because this machine lacks native/packaging workloads.
- Added `scripts/test-clean.cmd` / `.ps1`: runs focused Overlay/Shared/All test projects with `--disable-build-servers`, `UseSharedCompilation=false`, `MSBuildNodeReuse=false`, and a final `dotnet build-server shutdown`.

Important correction: GPU power/TDP must be real watts or hidden. Do not reintroduce `% TDP` in the HUD, and do not map D3DKMT `PowerRaw / 10` into `Gpu.PowerWatts`.

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

## What Does NOT Work Yet (Needs Hardware Validation)

| Metric | Status | Root Cause |
|--------|--------|------------|
| GPU temp | Shows `--` until runtime validation | Stale DLL issue is guarded now; needs fresh overlay launch and log check on Intel hardware. |
| GPU power | Shows `--` | Intentional until a validated true-watts GPU power source exists. D3DKMT `PowerRaw` is diagnostic only. |
| GPU fan RPM | Shows `--` until runtime validation | Stale DLL issue is guarded now; needs fresh overlay launch and log check on Intel hardware. |
| VRAM hidden on Intel | **NOT VERIFIED** | Code is correct (`VendorId == 0x8086` check) but still needs hardware validation. |

### The Stale DLL Problem

Root cause was confirmed: running overlay/build processes locked output DLLs and the overlay loaded an old `HHAPulse.Shared.dll` without `GpuMetrics.FanRpm`, causing `MissingMethodException: GpuMetrics.set_FanRpm`.

Now addressed by:

- Clean Release overlay build succeeded after fixing stale locks/ACL issues.
- Runtime contract guard logs loaded shared DLL identity and fails early if stale.
- `scripts/rebuild-clean.cmd` and `scripts/test-clean.cmd` shut down dotnet build servers and avoid MSBuild node reuse/shared compilation.
- Manual `dotnet test` can still leave worker processes after interruption; prefer `scripts\test-clean.cmd`.

Still needs hardware verification by launching the freshly built overlay and checking logs.

## Intel iGPU Detection (Verified via web search)

- `DXGI_ADAPTER_DESC1.VendorId`: `0x8086` = Intel, `0x1002` = AMD, `0x10DE` = NVIDIA ([MS docs](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/ns-dxgi-dxgi_adapter_desc1)).
- `DedicatedVideoMemory` alone is NOT reliable for iGPU detection — Intel iGPUs can report non-zero values ([wgpu #683](https://github.com/gfx-rs/wgpu/issues/683)).
- VendorId + DedicatedVideoMemory threshold (`<= 256MB`) is the current approach.
- More robust: `D3D12_FEATURE_DATA_ARCHITECTURE.UMA` flag (not implemented, requires D3D12 device).

## Bugs Found & Fixed

1. **`MissingMethodException: GpuMetrics.set_FanRpm`** — stale `HHAPulse.Shared.dll`. Fix: runtime contract guard + clean rebuild/test wrappers + kill running overlay/capture processes before rebuild when needed.
2. **Empty `settings.json` crash** — `File.Create()` truncates; `JsonException` on next launch. Fix: `SettingsService` checks `stream.Length == 0` and catches `JsonException`.
3. **Two-row triggered too aggressively** — element count included separators (`CPU + Sep + GPU + Sep + BAT = 5 > 2`). Then fixed to component count but threshold was wrong. **Final fix**: measure actual width, split only if exceeds screen.
4. **`MainWindow.TopBar` inaccessible** — XAML `x:Name` is private. Fix: `MainWindow.ApplyOpacity()` / `ApplyTextSize()` public methods.
5. **Battery icon unreadable** — Segoe Fluent Icons `\uE996` too small/ambiguous at 13px. Fix: replaced with "BAT" text.
6. **D3DKMT Power field semantics** — MS docs say "tenths of % TDP", not watts. Final fix: never show this as watts; log `PowerRaw` for diagnostics only and hide `gpu_power` until a true-watts source exists.
7. **`CollectorOrchestrator` didn't dispose collectors** — D3DKMT adapter handle leaked. Fix: implements `IDisposable`.
8. **Stale shared binary guard missing** — fix: startup `TelemetryContractGuard` logs `HHAPulse.Shared.dll` path/version/MVID and fails clearly on a bad contract.
9. **Manual dotnet cleanup during tests** — fix: added `scripts\test-clean.cmd` wrapper with build-server shutdown in `finally`.

## Build Status

- Release overlay build: 0 warnings, 0 errors.
- Overlay tests: 25/25 passed from user-run `dotnet test tests\HHAPulse.Overlay.Tests\HHAPulse.Overlay.Tests.csproj -c Debug -p:Platform=x64 --no-restore`.
- Shared tests: 4/4 passed via `scripts\test-clean.cmd -Target Shared -Configuration Debug -NoRestore`.
- Full solution test still fails on this machine because VS/native packaging targets are missing: `Microsoft.Cpp.Default.props`, `Microsoft.Windows.UI.Xaml.CSharp.targets`, and `Microsoft.DesktopBridge.targets`.

## Research Sources

- [DXGI_ADAPTER_DESC1 - MS Learn](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/ns-dxgi-dxgi_adapter_desc1)
- [D3DKMT_ADAPTER_PERFDATA - MS Learn](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/d3dkmthk/ns-d3dkmthk-_d3dkmt_adapter_perfdata)
- [D3DKMT_ADAPTER_PERFDATACAPS - MS Learn](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/d3dkmthk/ns-d3dkmthk-_d3dkmt_adapter_perfdatacaps)
- [How to filter GPUs by type in DXGI - MS Q&A](https://learn.microsoft.com/en-sg/answers/questions/2134992/how-do-i-filter-gpus-by-type-in-dxgi-(integrated-v)
- [Intel iGPU reported as discrete - wgpu #683](https://github.com/gfx-rs/wgpu/issues/683)
- [DXGI_ADAPTER_DESC2 - MS Learn](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_2/ns-dxgi1_2-dxgi_adapter_desc2)

## Next Steps

1. Launch fresh Release overlay and check log for `Loaded HHAPulse.Shared.dll:` with the expected current path/MVID.
2. Check log for `GPU Perf: D3DKMT initialized. Test read:` and confirm whether Intel reports temp and fan RPM.
3. Check Diagnostics panel for GPU telemetry status and metric health.
4. Check log for VRAM adapter selection / Intel iGPU hide path.
5. Add a real GPU-watts collector before showing `gpu_power` in Tuner. Do NOT use D3DKMT `PowerRaw` as watts.
6. Test with a game running. FPS/frametime/lows need the capture service connected with a target PID.
7. For local loops, prefer `scripts\test-clean.cmd -Target Overlay -Configuration Debug -NoRestore` and `scripts\rebuild-clean.cmd -Target Overlay -Configuration Release -StopRunning`.
