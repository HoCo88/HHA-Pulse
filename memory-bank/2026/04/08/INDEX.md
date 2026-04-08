# Daily Log - 2026-04-08

## Operational Rewrite

The old multi-preset prototype was replaced by one product path:

- One slim HUD bar rendered by `MainWindow` + `TopBarControl`.
- No side panel, diagnostic overlay, custom metric picker, preset label, frame-generation badge, or bottleneck UI in the runtime overlay.
- FPS is routed through `HHAPulse.CaptureService`, an elevated ETW capture service.
- The overlay detects the foreground game PID in the interactive session and sends it to the service over `LOCAL\HHAPulse.Capture.Control`.
- The service publishes fresh frame metrics over `LOCAL\HHAPulse.Capture.Out`.
- Stale or missing frame data shows `FPS --`, `1% --`, and `FT --`; no fake values.

## Runtime Metrics

Current HUD metric order:

```text
FPS | 1% | FT | CPU | GPU | RAM | VRAM | Hz | BAT
```

Real data sources:

- FPS / frametime / lows: capture service ETW session.
- CPU usage: `GetSystemTimes` delta.
- GPU usage: PDH GPU Engine counters.
- RAM: `GlobalMemoryStatusEx`.
- VRAM: Vortice.DXGI `IDXGIAdapter3.QueryVideoMemoryInfo`.
- Battery: `CallNtPowerInformation`.
- Display Hz: Windows display APIs.

Hidden until real data exists: temperature, fan, power, latency, frame generation.

## Deleted Dead Runtime Paths

Removed from overlay runtime:

- Fake frame collector / loader stubs.
- Vendor telemetry stubs.
- Optional driver placeholder collector.
- Side-panel controls.
- Large graph control.
- Metric picker/profile pages.
- Frame-generation and bottleneck UI.
- Advanced frame-analysis detector test/runtime path.

Shared telemetry models may still contain forward-compatible fields, but they are not shown in the HUD and are not populated by stub collectors.

## Build And Verification

Release builds passed:

```powershell
dotnet build src\HHAPulse.CaptureService\HHAPulse.CaptureService.csproj -c Release -p:UseSharedCompilation=false --disable-build-servers
dotnet build src\HHAPulse.Overlay\HHAPulse.Overlay.csproj -c Release -p:UseSharedCompilation=false --disable-build-servers
```

Tests passed:

```text
HHAPulse.Overlay.Tests: 14/14
HHAPulse.Shared.Tests: 4/4
```

Release overlay output contains both:

```text
HHAPulse.Overlay.exe
HHAPulse.CaptureService.exe
```

## Static Guardrails

Searches were clean in `src` and tests for old runtime strings and paths such as dead DWM FPS, installer prompts, old custom/diagnostic overlay presets, side-panel controls, and large graph controls.

## Manual Work Still Needed

- Install/start `HHAPulse.CaptureService` with admin approval on the handheld.
- Validate ETW event parsing against Battlefield and compare FPS/frametime/1% low against a trusted reference for a 60-second run.
- Confirm the one-bar HUD has no nested box, split, or stale label on the actual handheld screen.

