# 2026-04-09 — HHAP-0.25 Multi-Vendor GPU Telemetry Recovery

## What happened today

### Phase 1: Initial implementation (teams)
- Implemented multi-vendor GPU telemetry via TeamCreate with 7+ teammates
- Created: AdlxGpuCollector.cs, IgclGpuCollector.cs, NvApiGpuCollector.cs, CpuPowerCollector.cs
- Created: NativeTelemetry.cpp ADLX/IGCL C++ wrappers
- Fixed: global.json (.NET 8 SDK pin), XamlControlsResources code-behind guard
- Added: GpuClock as first-class metric, per-metric source attribution in DependencyState
- All managed tests passed (29/29 → later 35/35 → 39/39)

### Phase 2: Deep audit (10 verification agents)
**GPT and teammates fabricated critical low-level interop code.**

Found 15 fabricated items across 3 components:
1. **IGCL (Intel)**: `ctl_power_telemetry_t` is nested `ctl_oc_telemetry_item_t` objects, NOT a flat byte buffer. All 5 hardcoded offsets read random memory. The "supportedFields" bitmask doesn't exist — each field has its own `bSupported`.
2. **ADLX (AMD)**: COM vtables had wrong method counts (9 vs 17 for IADLXPerformanceMonitoringServices). IADLXSystem had Acquire/Release that don't exist. GetCurrentGPUMetrics was at the wrong slot.
3. **EMI (CPU Power)**: Wrong GUID (B220 vs B053), wrong IOCTLs, wrong structs, missing V2 multi-channel support, missing protocol steps.

**Verified correct**: NVIDIA (NvAPIWrapper.Net NuGet — all APIs confirmed), all existing collectors (FPS/ETW, CPU, GPU PDH, VRAM, RAM, Battery, Display), formatters, collector ordering.

Evidence: [audit-report-HHAP-0.25.md](audit-report-HHAP-0.25.md)

### Phase 3: Research MD fact-check
7 corrections found in [gpu-telemetry-investigation.md](gpu-telemetry-investigation.md):
- D3DKMT type 25 → actually 62
- "No C# IGCL wrapper exists" → FanControl.IntelCtlLibrary + bindings exist
- "ControlLib.dll" → may also be "igcl64.dll"
- "EMI via WMI" → actually device IOCTLs via SetupAPI
- EAC is game-dependent, not blanket safe
- Vanguard is risky, not safe
- PresentMon 2.5.0+ has FrameType metric

### Phase 4: Recovery plan + rebuild
Recovery plan v2 → final plan. Key decisions:
- **Keep HHAPulse.Native** — pure managed-only not achievable (ADLXWrapper needs SWIG/C++, IGCL bindings need SWIG)
- **Don't vendor third-party wrappers** — licenses unclear for Rem0o/ADLXWrapper, Rem0o/IGCL bindings, epinter/adlxwrapper
- **Rebuild from official vendor SDK headers only** — vendored minimal contract headers into repo
- **Fail closed** — if header incomplete or unit unknown, disable that metric, never guess

Rebuilt:
- **IGCL**: Real `ctl_power_telemetry_t` with nested `ctl_oc_telemetry_item_t`, per-field `bSupported` + `units` checks, energy→watts from device-reported time, LUID-based device matching, probes ControlLib.dll then igcl64.dll
- **ADLX**: Real vtable definitions from ADLX SDK v1.4.0.110, correct method slots, `HasDesktops` GPU selection, `StartPerformanceMetricsTracking` at 1Hz
- **EMI**: Real GUID {…B053}, proper protocol (Version→Size→Metadata→Measurement), V1/V2 support, channel ranking (package>cpu>rapl>soc), device-reported absolute time
- **Battery**: Clamped to [0, 100]

### Phase 5: Wrapper research
Verified via web search:
- **Rem0o/ADLXWrapper**: Exists but NOT pure C# (66% C#, 32% C++, SWIG). License unknown.
- **epinter/adlxwrapper**: Exists, pure C# single file. License unknown. v0.3.0 May 2024.
- **Rem0o/drivers.gpu.control-library.bindings**: Exists, SWIG-generated. License unknown.
- **FanControl.IntelCtlLibrary**: Exists, experimental. License unknown.
- **NvAPIWrapper.Net**: LGPL-3.0 confirmed. All API calls verified against NuGet XML docs.
- **ADLX SDK license**: Unclear, "ADLX SDK License Agreement" referenced but not publicly accessible.

### Current status
- All managed code verified ✅ (39/39 tests)
- Vendor contract headers vendored with provenance ✅
- Native C++ code reviewed line-by-line ✅
- **Blocker: C++ build not yet compiled** — VS 2026 Insiders installed but PlatformToolset mismatch being resolved
- After C++ build passes → dumpbin export check → hardware validation on Legion Go (AMD) + MSI Claw (Intel)

## Key files changed
- `src/HHAPulse.Native/VendorContracts/AdlxTelemetryContract.h` — AMD ADLX v1.4.0.110 official header subset
- `src/HHAPulse.Native/VendorContracts/IgclTelemetryContract.h` — Intel IGCL v1-r1 official header subset
- `src/HHAPulse.Native/NativeTelemetry.cpp` — rebuilt ADLX + IGCL from official contracts
- `src/HHAPulse.Overlay/Collectors/Cpu/EmiContract.cs` — real EMI GUID, IOCTLs, V1/V2 parsing
- `src/HHAPulse.Overlay/Collectors/Cpu/CpuPowerCollector.cs` — rewritten from emi.h
- `src/HHAPulse.Overlay/Collectors/Gpu/NvApiGpuCollector.cs` — per-metric source, NVML PCI hardening
- `src/HHAPulse.Overlay/Collectors/Battery/BatteryCollector.cs` — charge % clamped
- `src/HHAPulse.Shared/Models/TelemetrySnapshot.cs` — per-metric GPU source fields (Keys 14-21)
- `src/HHAPulse.Overlay/Diagnostics/MetricStatusFactory.cs` — per-field source labels
- `src/HHAPulse.Overlay/Interop/DxgiPrimaryAdapterSelector.cs` — unified display-attached adapter
- `src/HHAPulse.Overlay/Settings/OverlayPresetCatalog.cs` — GpuClock metric
- `tests/HHAPulse.Overlay.Tests/Collectors/EmiContractTests.cs` — EMI parser tests
- `tests/HHAPulse.Overlay.Tests/Collectors/BatteryCollectorTests.cs` — battery clamp test

## Lessons learned
1. **Teammates hallucinate low-level interop.** COM vtables, struct layouts, IOCTL codes, GUIDs — all fabricated when not derived from real headers. Must use official SDK headers, never hand-roll.
2. **"No assumptions" must be enforced.** Every struct offset, every vtable method, every IOCTL code must trace to an official source.
3. **Agent management needs hard deadlines.** Multiple agents stalled 30+ minutes without reporting. Poke once, then kill and move on.
4. **NuGet wrappers are safer than hand-rolled native interop.** NVIDIA worked because it used a real NuGet package. AMD/Intel failed because they hand-rolled C++ interop.
5. **Fail closed, never guess.** If a unit is unknown, a struct is incomplete, or a device match is ambiguous — hide the metric. Never show fake data.
