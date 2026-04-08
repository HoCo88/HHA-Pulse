# Metric Collectors Reference (April 2026)

Base telemetry collectors run as the interactive overlay user. FPS/frametime is the exception: it comes from the elevated capture service because ETW session control requires elevated/service rights.

## Runtime Split

Overlay collectors:

- Battery: `CallNtPowerInformation`.
- CPU usage: `GetSystemTimes` delta.
- RAM: `GlobalMemoryStatusEx`.
- GPU usage: PDH GPU Engine counters.
- VRAM: Vortice.DXGI `IDXGIAdapter3.QueryVideoMemoryInfo`.
- Display refresh: Windows display APIs.

Capture service:

- FPS / frametime / lows: ETW session `HHAPulse_FrameCapture` for the overlay-provided target PID.

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

## VRAM - Vortice.DXGI

Use Vortice.DXGI instead of raw COM vtable calls:

```csharp
using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory4>();
using var adapter = factory.EnumAdapters1(0);
using var adapter3 = adapter.QueryInterface<IDXGIAdapter3>();
var info = adapter3.QueryVideoMemoryInfo(0, MemorySegmentGroup.Local);
```

For integrated GPUs, prefer budget/current usage over dummy dedicated-memory values.

## Display Refresh

Use `EnumDisplaySettings` for current refresh rate. Treat 0/1 Hz as hardware default/unknown.

## Logging

Collector failures should log once per failure class, not every tick.
