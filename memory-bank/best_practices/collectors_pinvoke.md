# Metric Collectors Reference (April 2026)

Base telemetry collectors run as the interactive overlay user. FPS/frametime is the exception: it comes from the elevated capture service because ETW session control requires elevated/service rights.

## Runtime Split

Overlay collectors:

- Battery: `CallNtPowerInformation`.
- CPU usage: `GetSystemTimes` delta.
- RAM: `GlobalMemoryStatusEx`.
- GPU usage: PDH GPU Engine counters.
- GPU temp / power / fan: `D3DKMTQueryAdapterInfo(KMTQAITYPE_ADAPTERPERFDATA)` via gdi32.dll.
- VRAM: Vortice.DXGI `IDXGIAdapter3.QueryVideoMemoryInfo`.
- Display refresh: Windows display APIs.

Capture service:

- FPS / frametime / lows / frame gen: ETW session `HHAPulse_FrameCapture` for the overlay-provided target PID.

Do not add placeholder collectors for metrics that are not populated by real APIs.

## Battery - CallNtPowerInformation

`SYSTEM_BATTERY_STATE.Rate` is signed: negative means discharging mW, positive means charging mW. Do not show watts in the HUD until charge/discharge behavior is validated on handheld hardware.

## CPU Usage - GetSystemTimes

Kernel time includes idle time. Subtract idle and use deltas between samples.

## RAM - GlobalMemoryStatusEx

Set `dwLength` before calling. Use the existing collector pattern.

## GPU Usage - PDH API

Use PDH directly, not .NET `PerformanceCounter`.

Counter path:

```text
\GPU Engine(*engtype_3D*)\Utilization Percentage
```

Use `PdhGetFormattedCounterArray` for wildcard counters so all 3D engine instances are enumerated. Do not use the single-value wildcard shortcut.

## GPU Temp / Power / Fan - D3DKMT

`D3DKMTQueryAdapterInfo` with `KMTQAITYPE_ADAPTERPERFDATA` (type 62) from gdi32.dll. Same API Windows Task Manager uses. No vendor SDK, no elevation, WDDM 2.4+.

### Key details

- **Temperature**: stored in deci-Celsius. Raw value / 10 = °C.
- **Power**: [MS docs](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/d3dkmthk/ns-d3dkmthk-_d3dkmt_adapter_perfdata) say "tenths of percentage of TDP" (raw / 10 = % of TDP). Do not expose this as GPU watts. Product rule: user-facing power/TDP must be watts or hidden. Keep D3DKMT `PowerRaw` diagnostic-only until a validated true-watts source exists.
- **Fan RPM**: direct RPM value.
- **Struct layout**: Must use `LayoutKind.Explicit` with `[FieldOffset]` to match `D3DKMT_ALIGN64` padding. After `PhysicalAdapterIndex` (uint at offset 0), there's 4 bytes padding before the first `ulong` field at offset 8.
- **Adapter enumeration**: `D3DKMTEnumAdapters2` two-call pattern (count then fill). Prefer a display-attached adapter (`NumOfSources > 0`) instead of blindly using adapter 0.
- **Disposal**: `D3DKMTCloseAdapter` must be called on shutdown. `CollectorOrchestrator` implements `IDisposable` and disposes collectors.
- **Init test read**: A test query at init confirms driver support. If it fails, the collector stays disabled and logs once.
- **Intel iGPU**: May not support ADAPTERPERFDATA — init handles failure gracefully. No documentation confirms Intel iGPU support.

### Caps query

`KMTQAITYPE_ADAPTERPERFDATACAPS` (type 63) returns static values: `TemperatureMax`, `TemperatureWarning`, `MaxFanRPM`, `MaxMemoryBandwidth`, `MaxPCIEBandwidth`. Queried once at init.

## VRAM - Vortice.DXGI

Use Vortice.DXGI instead of raw COM vtable calls:

```csharp
using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory4>();
using var adapter = factory.EnumAdapters1(0);
using var adapter3 = adapter.QueryInterface<IDXGIAdapter3>();
var info = adapter3.QueryVideoMemoryInfo(0, MemorySegmentGroup.Local);
```

### Intel iGPU VRAM handling

Intel iGPUs (`VendorId == 0x8086`, `DedicatedVideoMemory <= 256MB`) have no real VRAM — they use shared system memory already reported by RAM. The VRAM metric is hidden for these adapters. Log the detection: `VRAM: Intel iGPU detected (VendorId=0x8086, DedicatedVRAM=XXX MB). VRAM metric hidden.`

**Caveat**: `DedicatedVideoMemory` can be non-zero on Intel iGPUs ([wgpu issue #683](https://github.com/gfx-rs/wgpu/issues/683)). The VendorId check prevents false positives. Intel Arc (discrete, `DedicatedVideoMemory > 256MB`) correctly shows VRAM.

AMD APUs report usable VRAM through the local segment — always shown.

### GPU vendor detection

DXGI `DXGI_ADAPTER_DESC1.VendorId` PCI IDs ([MS docs](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/ns-dxgi-dxgi_adapter_desc1)):
- `0x8086` = Intel
- `0x1002` = AMD
- `0x10DE` = NVIDIA

For iGPU vs discrete: combine VendorId with `DedicatedVideoMemory` threshold. A more robust method is `D3D12_FEATURE_DATA_ARCHITECTURE.UMA` flag but requires creating a D3D12 device.

## Display Refresh

Use `EnumDisplaySettings` for current refresh rate. Treat 0/1 Hz as hardware default/unknown.

## Frame Generation Detection

Current frame-generation support is detect-only. The capture service may set `CaptureFrameMetrics.HybridPresentDetected` when explicit Intel-PresentMon ETW evidence is seen for the active target, but it does **not** set `MetricFlags.FrameGen` and it does **not** publish a numeric frame-gen FPS value.

Important constraints:
- Use source-backed ETW evidence only. Do not infer frame generation from FPS ratios, present timing, or heuristics.
- Keep the HUD metric disabled until a validated numeric display/generated rate exists.
- The current implementation prefers named payload access (`PayloadByName("FrameType")`). Hardware validation is still required to prove that GPU-driver instrumentation exposes that field through TraceEvent on real systems.

## Settings Resilience

`SettingsService.LoadAsync` checks `stream.Length == 0` and catches `JsonException` for corrupt settings files. Falls back to `AppSettings.CreateDefault()`. This prevents a crash when `SaveAsync` truncates the file via `File.Create()` and the app is killed before JSON is written.

## Logging

Collector failures should log once per failure class, not every tick. Use the `LogOnce` pattern.
