# 2026-04-17 - Frame-gen vendor label + honest App/Present FPS (HHAP-0.60)

This entry records what landed on branch `HHAP-0.60` today: the frame-gen vendor label is now propagated end-to-end, the DXGI App/Present FPS fallback was deleted, and `MetricFlags.FrameGen` finally reflects real vendor evidence rather than being pinned to `0`. The pass also consumed two rounds of external GPT review and closed both.

## Scope completed

- Spec-vs-code audit against `CLAUDE.md` + `memory-bank/systems/ARCHITECTURE.md` across 5 areas (IPC, FPS/ETW, GPU telemetry, HUD/presets, security) — 4 GREEN + 1 YELLOW (App/Present FPS fallback violated the spec's "zeroed until real split exists" rule).
- Primary-source research on PresentMon 2.2 / 2.3 frame-type tracking and input-latency coverage.
- Plan: [framegen-handheld-plan.md](framegen-handheld-plan.md).
- Implementation: 17 files changed, 511 insertions, 30 deletions (commit `0cc0dd1`).
- Two rounds of external review-response — flagged overstatement, missing tests, doc rot, and partial-staging; all points closed.
- Commit + push to `HHAP-0.60`.

## What landed, by subsystem

### Frame-gen vendor propagation

- New enum `FrameGenVendor { None, IntelXeFG, AmdAFMF }` in `src/HHAPulse.Shared/Models/FrameGenVendor.cs`.
- Classifier surface: `IntelPresentMonFrameTypeEvidence` now returns a vendor label, not just a bool.
- Wire path: `EtwFrameCapture` → `CaptureFrameMetrics.FrameGenVendor` (Key 29) → `CaptureServiceCollector.ApplyFrameGenVendor` → `PerformanceMetrics.FrameGenVendor` (Key 10) → HUD.
- HUD FPS cell renders `"120/60fps AFMF"` / `"120/60fps XeFG"` when frame-gen is active; collapses to `"60fps"` when vendor is `None`.

### App/Present FPS honesty

- The prior DXGI fallback (`AppFramesPerSecond = fps; PresentFramesPerSecond = fps`) is removed from `EtwFrameCapture`.
- Both fields are now computed from real Intel-PresentMon frame-type counts inside the interval.
- When Intel-PresentMon did not fire in the window, both fields stay `0` rather than silently mirroring the DXGI fps.
- `DisplayFramesPerSecond` is still always `0` — no VBlank/scanout correlation exists yet.

### Seam extractions for testability

- `CaptureServiceCollector.ApplyFrameGenVendor` is now a dedicated internal static helper so the flag-wiring logic is directly unit-testable.
- `EtwFrameCapture.ComputeAppAndPresentFps` is now a dedicated internal static helper so the FPS-split math is unit-testable without driving real ETW.

### Diagnostic text

- `ControlShellTextBuilder` appends the vendor suffix (`XeFG` / `AFMF`) to diagnostics when frame-gen is active.

### Input latency

- Stays hidden in HUD / picker / diagnostics. Rationale and primary-source citations captured in [input-latency-handheld-gap.md](input-latency-handheld-gap.md). PresentMon 2.2 input-latency instrumentation does not cover XInput, and handheld primary input is controller. No vendor driver-level input→display API has been accepted into the product.

## Contract / surface changes

- `FrameGenVendor` enum added: `src/HHAPulse.Shared/Models/FrameGenVendor.cs`.
- `CaptureFrameMetrics.FrameGenVendor` added at MessagePack **Key 29**.
- `PerformanceMetrics.FrameGenVendor` added at MessagePack **Key 10**.
- `MetricFlags.FrameGen` is now actually set by the overlay collector (`CaptureServiceCollector.ApplyFrameGenVendor`). Previously it was reserved but never raised.
- HUD FPS cell format: `"120/60fps AFMF"` / `"120/60fps XeFG"` when frame-gen active, otherwise `"60fps"` (single rate).

## Tests added

All 7 added under `tests/` in the `0cc0dd1` commit:

- `tests/HHAPulse.CaptureService.Tests/Etw/EtwFrameCaptureTests.cs` — covers `ComputeAppAndPresentFps` across present-mode mixes and the zero-event regression guard (both fields stay `0` when no Intel-PresentMon evidence fired).
- `tests/HHAPulse.CaptureService.Tests/Etw/IntelPresentMonFrameTypeEvidenceTests.cs` — classifier vendor output for `Intel_XEFG`, `AMD_AFMF`, `Original`, `Repeated`, `Unspecified`.
- `tests/HHAPulse.Overlay.Tests/Collectors/Fps/CaptureServiceCollectorTests.cs` — `ApplyFrameGenVendor` flag wiring for each enum value (`None` does not raise the flag; `IntelXeFG` / `AmdAFMF` do).
- `tests/HHAPulse.Overlay.Tests/Helpers/MetricFormatterTests.cs` — HUD FPS cell format across active / inactive frame-gen states.

Honestly skipped: `EtwFrameCapture` integration test against a real ETW session. There is no public seam to drive the publish path with a synthetic provider; the extracted pure helpers (`ComputeAppAndPresentFps`, `ApplyFrameGenVendor`) are the testable surface for this pass.

## Research citations

Primary sources used today:

- PresentMon v2.3.0 — frame-gen tracking surface: https://github.com/GameTechDev/PresentMon/releases/tag/v2.3.0
- PresentMon `ConvertPMPFrameTypeToFrameType` source (authoritative mapping of vendor frame-type values): https://github.com/GameTechDev/PresentMon/blob/main/PresentData/PresentMonTraceConsumer.cpp
- PresentMon v2.2.0 — click-to-photon and all-input-to-photon latency: https://github.com/GameTechDev/PresentMon/releases/tag/v2.2.0
- PresentMon issue #366 — XInput coverage gap for all-input-to-photon: https://github.com/GameTechDev/PresentMon/issues/366

## External review loop

- **Round 1** flagged: overstatement of FPS honesty, missing tests for the seam, documentation rot in `memory-bank/systems/telemetry-trace-map.md` and `ARCHITECTURE.md`.
- **Round 2** flagged: overstatement about "17 files fully staged" when only a subset was staged at that moment.
- Both rounds closed in this commit: the seams plus the 7 tests landed, documentation corrections landed, and all 17 files are now staged and committed on `HHAP-0.60`.

## Still open / not claimed complete

- Windows `dotnet build` and `dotnet test` were **not** run from this pass — the host has no .NET SDK. Commands remain the two `dotnet build` and two `dotnet test` calls in [../../../../CLAUDE.md](../../../../CLAUDE.md).
- AMD AFMF hardware validation on Z1 / Z2 APUs is still pending — no handheld with AFMF was available in this pass.
- Intel XeFG hardware validation on Lunar Lake is still pending — local `IntelPresentMonFrameTypeEvidence` unit tests pass, but no end-to-end run on a Lunar Lake handheld has been signed off.

## Rules locked by this pass

- Never fabricate `AppFramesPerSecond` / `PresentFramesPerSecond` from DXGI fps when Intel-PresentMon didn't fire in the window. Both fields stay `0`.
- `MetricFlags.FrameGen` is gated strictly on `FrameGenVendor != None`, never on `HybridPresentDetected` alone.
- `DisplayFramesPerSecond` stays `0` until a real VBlank/scanout correlation source exists.
- Input latency stays hidden until PresentMon issue #366 closes (XInput coverage) OR a handheld OEM exposes a driver-level input→display API that we can verify against vendor documentation.
- Vendor DLL binaries (ADLX, IGCL, NvAPI/NVML) remain dynamic-loaded at runtime, never bundled into the repo. (Holdover rule, still true.)

## Branch + commit

- Branch: `HHAP-0.60`
- Commit: `0cc0dd1` — "HHAP-0.60: FG vendor label (XeFG/AFMF) + honest App/Present FPS"
- Stats: 17 files changed, 511 insertions, 30 deletions

## Sibling files for this day

- [framegen-handheld-plan.md](framegen-handheld-plan.md) — plan: surface vendor-labeled frame-gen on HUD, remove DXGI fallback, wire `MetricFlags.FrameGen`.
- [input-latency-handheld-gap.md](input-latency-handheld-gap.md) — decision: keep `input_latency` hidden; PresentMon 2.2+ input-latency does not cover XInput / controllers (the primary input on every target handheld).
- Supersession note appended to [../09/framegen-detect-only.md](../09/framegen-detect-only.md) — the April 9 "detect-only" scope is partially superseded by the frame-gen vendor surface shipped today.
