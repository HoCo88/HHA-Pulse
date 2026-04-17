# Frame-gen + input-lag fixes (handheld scope) — 2026-04-17

## Goal

Make frame generation (AMD AFMF / Intel XeFG) honestly visible on the HUD and fix the App/Present FPS spec violation. Explicitly keep input latency hidden on handhelds because PresentMon click-to-photon does not cover XInput/controllers (upstream issue [GameTechDev/PresentMon#366](https://github.com/GameTechDev/PresentMon/issues/366)).

## In scope

1. Preserve frame-gen vendor from classifier to HUD (XeFG / AFMF).
2. Set `MetricFlags.FrameGen` when a generated frame was observed in the publish window.
3. Remove the DXGI fallback that contaminates `AppFramesPerSecond` / `PresentFramesPerSecond` (`EtwFrameCapture.cs:161-165`). On no-FG games, both stay `0` — HUD falls back cleanly to the single `"60fps"` cell.
4. Render the vendor badge on the HUD (`FG XeFG` / `FG AFMF`).
5. Document the input-latency controller blocker so the decision is traceable.

## Out of scope

- NVIDIA DLSS-FG detection (no NVIDIA discrete GPU on target handhelds; requires NVTraceConsumer port — separate milestone).
- Any input-latency collector (controller input is not covered by PresentMon 2.x ETW).
- HUD redesign. Badge renders inside the existing FPS group on `TopBarControl`.

## File-level changes

### New file

**`src/HHAPulse.Shared/Models/FrameGenVendor.cs`** (new)

```csharp
namespace HHAPulse.Shared.Models;

public enum FrameGenVendor : byte
{
    None = 0,
    IntelXeFG = 1,   // Intel_XEFG
    AmdAFMF = 2,     // AMD_AFMF
}
```

Enum kept `byte` for MessagePack wire-compactness. Values locked — changing them breaks pipe compatibility.

### Shared contract

**`src/HHAPulse.Shared/Models/CaptureFrameMetrics.cs`**
- Add `[Key(29)] public FrameGenVendor FrameGenVendor { get; set; }` at end. Key 28 is the last used (`StorageReliabilityAvailable`); 29 is the next free slot. Placing at end keeps MessagePack layout backward-compatible for older `HHAPulse.Widget` clients if any still deserialize this shape.

**`src/HHAPulse.Shared/Models/TelemetrySnapshot.cs:51-83`** (`PerformanceMetrics`)
- Add `[Key(10)] public FrameGenVendor FrameGenVendor { get; set; }`. Key 9 is last used; 10 is next.

### Classifier (already has the data, just expose it)

**`src/HHAPulse.CaptureService/Etw/IntelPresentMonFrameTypeEvidence.cs`**
- Extend `TryClassifyFrameKind` (line 119-147) to optionally return the vendor. Cheapest change: add a second overload `TryGetFrameKindAndVendor(TraceEvent, out PresentFrameKind, out FrameGenVendor)` that maps `"Intel_XEFG" → IntelXeFG`, `"AMD_AFMF" → AmdAFMF`, everything else → `None`.
- Keep the existing `TryGetFrameKind` signature untouched so current test surface stays green.

### Capture loop

**`src/HHAPulse.CaptureService/Etw/EtwFrameCapture.cs`**
- Add private field `private FrameGenVendor frameGenVendorThisInterval;` alongside line 30-36 counters.
- `SetTarget` (line 52-72): reset `frameGenVendorThisInterval = FrameGenVendor.None` in the PID-change block.
- `HandleFrameTypeEvent` (line 196-224): when `PresentFrameKind.Generated`, also capture which vendor from the extended classifier output. If multiple vendors observed in the same window (shouldn't happen), last-writer-wins is fine.
- **Delete the fallback at lines 161-165.** Leave `appFps` / `presentFps` computed strictly from `originalFrameCountThisInterval / generatedFrameCountThisInterval`. When PresentMon didn't fire, both will be `0` — which is the spec-correct value.
- Populate `FrameGenVendor = frameGenVendorThisInterval` on the new `CaptureFrameMetrics` at line 167-182.
- Reset `frameGenVendorThisInterval = FrameGenVendor.None` after publish (line 183-187 block).

### Overlay propagation

**`src/HHAPulse.Overlay/Collectors/Fps/CaptureServiceCollector.cs`**
- After line 110 (`HybridPresentDetected = ...`), add:
  ```
  snapshot.Performance.FrameGenVendor = metrics.FrameGenVendor;
  if (metrics.FrameGenVendor != FrameGenVendor.None)
  {
      snapshot.AvailableMetrics |= MetricFlags.FrameGen;
  }
  ```
  This is the **only** place `MetricFlags.FrameGen` is ever set. Gate strictly on vendor evidence, never on `HybridPresentDetected` alone (which is a boolean derived from the same signal — we use the vendor enum as the single source of truth).

### Formatter (HUD rendering)

**`src/HHAPulse.Overlay/Helpers/MetricFormatterCompact.cs`**
- Update the comment block at lines 17-25 to drop the obsolete phrase *"AppFramesPerSecond equals FramesPerSecond (by the fallback in EtwFrameCapture)"* — the fallback is gone. Replace with: *"When frame gen is off, AppFramesPerSecond is 0 and the cell collapses to the single FramesPerSecond display."*
- `FormatTotalOverAppFps` (line 176-194) logic stays — `app > 0 && app < total` gate already handles the "no fallback" case correctly because 0 < total evaluates the `frameGenActive = false` branch.
- Add a dedicated formatter for the FG vendor badge (new metric id? or injected into FPS cell?). **Recommend**: inject into the FPS cell so there's no new preset entry to cycle. Final format: `"120/60fps AFMF"` when active, `"60fps"` otherwise.

Concrete diff for `FormatTotalOverAppFps`:

```csharp
return frameGenActive
    ? $"{total:0}/{app:0}fps {VendorBadge(snapshot.Performance.FrameGenVendor)}"
    : $"{total:0}fps";
```

And add:
```csharp
private static string VendorBadge(FrameGenVendor v) => v switch
{
    FrameGenVendor.IntelXeFG => "XeFG",
    FrameGenVendor.AmdAFMF   => "AFMF",
    _ => string.Empty,
};
```

### Status message (diagnostic)

**`src/HHAPulse.Overlay/Collectors/Fps/CaptureServiceCollector.cs:93-95`** — include vendor in the "Frame generation: detected" diagnostic string so `ControlShellTextBuilder` and traces show which vendor.

**`src/HHAPulse.Overlay/Views/ControlShellTextBuilder.cs:60`** — update "frame gen detected" text to include vendor when set.

## Tests

**`tests/HHAPulse.CaptureService.Tests/`** (existing project)
- New test: `IntelPresentMonFrameTypeEvidenceTests.TryGetFrameKindAndVendor_Intel_XEFG_ReturnsIntelXeFG`
- New test: `IntelPresentMonFrameTypeEvidenceTests.TryGetFrameKindAndVendor_AMD_AFMF_ReturnsAmdAFMF`
- New test: `IntelPresentMonFrameTypeEvidenceTests.TryGetFrameKindAndVendor_Original_ReturnsNone`
- New test on `EtwFrameCapture` (if it currently has unit coverage — if not, skip; this path is integration-heavy): confirm `CaptureFrameMetrics.AppFramesPerSecond` is `0` when `generatedFrameCountThisInterval == 0 && originalFrameCountThisInterval == 0`. Regression test for the deleted fallback.

**`tests/HHAPulse.Overlay.Tests/`**
- New test: `MetricFormatterCompactTests.FormatValue_Fps_WithAmdAfmf_IncludesAFMFBadge`
- New test: `MetricFormatterCompactTests.FormatValue_Fps_WithIntelXeFG_IncludesXeFGBadge`
- New test: `MetricFormatterCompactTests.FormatValue_Fps_NoFrameGen_NoBadgeAndNoSplit`
- Regression: existing formatter tests that rely on the old fallback behavior (App==FramesPerSecond when no PresentMon) must be updated to the new contract.

## Memory-bank

- **Update** `memory-bank/2026/04/09/framegen-detect-only.md` — add a dated note at the bottom: *"2026-04-17 supersession: vendor label now surfaced through FrameGenVendor enum; MetricFlags.FrameGen is set when vendor != None; AppFps/PresentFps fallback removed (spec violation fixed)."* Don't rewrite the April 9 note — preserve the history.
- **Create** `memory-bank/2026/04/17/input-latency-handheld-gap.md` — one-page note: PresentMon 2.2+ click-to-photon and all-input-to-photon exist and work via ETW without injection, but they cover mouse + keyboard only. Upstream issue #366 tracks the XInput gap. On handheld devices (Steam Deck / ROG Ally / Legion Go / Claw) primary input is controller — so honest input latency would render `--` for the vast majority of sessions. Decision: keep `input_latency` hidden as today (`MetricFlags.InputLatency` reserved, no collector) until a validated controller path exists. Revisit when either (a) PresentMon adds XInput coverage or (b) a specific handheld OEM exposes a driver-level input→display timing API.

## Verification

```powershell
dotnet build src\HHAPulse.CaptureService\HHAPulse.CaptureService.csproj -c Release -p:UseSharedCompilation=false --disable-build-servers
dotnet build src\HHAPulse.Overlay\HHAPulse.Overlay.csproj -c Release -p:UseSharedCompilation=false --disable-build-servers
dotnet test tests\HHAPulse.Shared.Tests\HHAPulse.Shared.Tests.csproj -c Release
dotnet test tests\HHAPulse.CaptureService.Tests\HHAPulse.CaptureService.Tests.csproj -c Release
dotnet test tests\HHAPulse.Overlay.Tests\HHAPulse.Overlay.Tests.csproj -c Release
```

Hardware validation (must happen before merge to main):
- **AMD APU** (Ryzen Z1 Extreme / Z2): launch an AFMF-enabled title, confirm HUD shows `120/60fps AFMF`. Disable AFMF — confirm HUD collapses to `60fps` with no badge.
- **Intel Lunar Lake / Arc 140V**: launch an XeFG-enabled title, confirm HUD shows `120/60fps XeFG`.
- Non-FG AAA title: confirm no badge, no `/` split, no App/Present FPS contamination in MessagePack payload (can inspect via widget client log).

## Acceptance criteria

- `MetricFlags.FrameGen` is `0` whenever `FrameGenVendor == None`; set whenever `FrameGenVendor != None`. Verified by unit test.
- `CaptureFrameMetrics.AppFramesPerSecond == 0` whenever the Intel-PresentMon provider did not fire during the publish window. No DXGI fallback anywhere in `EtwFrameCapture`.
- HUD FPS cell renders `"{total}/{app}fps XeFG"` or `"{total}/{app}fps AFMF"` when FG active, `"{total}fps"` otherwise.
- All three test projects green.
- Memory-bank `framegen-detect-only.md` updated with supersession note; new `input-latency-handheld-gap.md` present.

## Why this scope (citations)

- PresentMon FrameType enum values and access pattern: confirmed stable upstream for `Intel_XEFG` + `AMD_AFMF` — [github.com/GameTechDev/PresentMon/blob/main/PresentData/PresentMonTraceConsumer.cpp](https://github.com/GameTechDev/PresentMon/blob/main/PresentData/PresentMonTraceConsumer.cpp).
- Input latency scope limitation: upstream issue [GameTechDev/PresentMon#366](https://github.com/GameTechDev/PresentMon/issues/366) (XInput not covered).
- Spec rule being fixed: `memory-bank/systems/ARCHITECTURE.md:25` — *"AppFramesPerSecond, PresentFramesPerSecond, and DisplayFramesPerSecond stay in the MessagePack contract but are zeroed/not-computed until a real PresentMon-grade split exists."*
- Audit findings this plan closes: `etw-auditor` report (EtwFrameCapture.cs:161-165 spec violation); `CLAUDE.md` honesty rule *"never faked… numeric frame-generation FPS"*.
