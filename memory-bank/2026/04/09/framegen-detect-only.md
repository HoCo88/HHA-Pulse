# 2026-04-09 - Frame Generation Detection (Detect-Only)

## Scope locked by code

The current HHA Pulse implementation supports frame generation as a diagnostic signal only.

What the code does:
- `EtwFrameCapture` subscribes to the Intel-PresentMon provider.
- `IntelPresentMonFrameTypeEvidence` reads the named `FrameType` payload when available.
- Only verified names are accepted:
  - non-generated: `Unspecified`, `Original`, `Repeated`
  - generated: `Intel_XEFG`, `AMD_AFMF`
- `HybridPresentDetected` is set from explicit evidence in the current publish window.
- Detection resets on target PID change and after publishing.

What the code does not do:
- It does not change `FramesPerSecond`, `AppFramesPerSecond`, `PresentFramesPerSecond`, or `DisplayFramesPerSecond`.
- It does not set `MetricFlags.FrameGen`.
- It does not expose a HUD frame-gen FPS value.
- It does not use raw-byte payload offsets.

## Verified repo evidence

- Provider subscription: `src/HHAPulse.CaptureService/Etw/EtwFrameCapture.cs`
- Evidence parser: `src/HHAPulse.CaptureService/Etw/IntelPresentMonFrameTypeEvidence.cs`
- Overlay capture diagnostics: `src/HHAPulse.Overlay/Collectors/Fps/CaptureServiceCollector.cs`
- Settings-shell messaging: `src/HHAPulse.Overlay/ControlWindow.xaml.cs`
- Metric truth wording: `src/HHAPulse.Overlay/Diagnostics/MetricStatusFactory.cs`
- Traceability update: `docs/telemetry-traceability.md`

## Test evidence

- `tests/HHAPulse.Shared.Tests`: 4/4 passed
- `tests/HHAPulse.Overlay.Tests`: 43/43 passed
- `tests/HHAPulse.CaptureService.Tests`: 8/8 passed
- Total managed tests: 55/55 green

Relevant regression coverage:
- Frame-gen HUD remains unavailable without `MetricFlags.FrameGen`
- Overlay auto-detection does not light up from `HybridPresentDetected` alone
- Capture-service detection state clears on target switch

## External-evidence boundary

PresentMon primary sources confirm provider-backed frame-type support exists upstream. This repo phase does not claim numeric frame-gen FPS support; it only uses that upstream evidence to justify a detect-only implementation.

## Open question requiring hardware validation

The current implementation uses `PayloadByName("FrameType")` only.

That is intentionally conservative, but it means detection depends on TraceEvent exposing named payload fields for the provider on a real driver-instrumented machine. If that field does not resolve at runtime, the implementation will fail closed and report no detection instead of guessing.

If hardware validation proves named payload access is insufficient, a future patch may add a verified raw-layout fallback, but only after the layout is locked to upstream source evidence.

## 2026-04-17 supersession

The detect-only scope locked on 2026-04-09 has been partially superseded. The "what the code does not do" checklist above is no longer accurate in full — specifically, `MetricFlags.FrameGen` is now set, and a labeled HUD surface now exists.

Changes shipped:

- Vendor label is surfaced via the `FrameGenVendor` enum (`None` / `IntelXeFG` / `AmdAFMF`) on `CaptureFrameMetrics` (Key 29) and `PerformanceMetrics` (Key 10).
- `MetricFlags.FrameGen` is now set by `CaptureServiceCollector` when vendor != `None`.
- The DXGI fallback in `EtwFrameCapture.cs` that assigned `appFps = fps; presentFps = fps` when Intel-PresentMon didn't fire has been removed. Both fields now stay `0`, matching the original spec's prohibition on synthesizing app/present FPS.
- HUD FPS cell now renders `"{total}/{app}fps AFMF"` or `"{total}/{app}fps XeFG"` when frame-gen is active, and `"{total}fps"` otherwise.

Source plan: `memory-bank/2026/04/17/framegen-handheld-plan.md`.
