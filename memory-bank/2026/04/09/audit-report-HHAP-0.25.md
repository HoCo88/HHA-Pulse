# HHAP-0.25 Full Audit Report

**Date:** 2026-04-09
**Audited by:** 10 independent verification agents + manual code review
**Scope:** All new code from GPU telemetry overhaul + all existing data pipelines + research MD fact-check

---

## EXECUTIVE SUMMARY

- **Existing code (pre-HHAP-0.25):** All pipelines verified correct — FPS, CPU, GPU usage, VRAM, RAM, Battery, Display
- **NVIDIA collector:** Verified correct — uses real NuGet package, all API calls confirmed
- **AMD ADLX native:** FABRICATED — COM vtables have wrong method count/order, would crash on real hardware
- **Intel IGCL native:** FABRICATED — struct is nested objects not flat bytes, all offsets read garbage
- **CPU Power EMI:** FABRICATED — wrong GUID, wrong IOCTLs, wrong structs, would never initialize
- **Research MD:** Mostly correct strategic direction, 5 factual errors found

---

## PART 1: WHAT WE HAVE (current state of each component)

### Working & Verified

| Component | File | Source API | Status |
|-----------|------|-----------|--------|
| FPS | EtwFrameCapture.cs → CaptureServiceCollector.cs | ETW DXGI/D3D9/DxgKrnl Present events | ✅ Real, accurate, no inflation |
| CPU Usage | CpuUsageCollector.cs | GetSystemTimes | ✅ Correct, clamped [0,100] |
| GPU Usage | GpuUsageCollector.cs | PDH `\GPU Engine(*engtype_3D*)\Utilization Percentage` | ✅ Correct, clamped [0,100] |
| GPU Temp/Fan (fallback) | GpuPerfDataCollector.cs | D3DKMT ADAPTERPERFDATA | ✅ Temp: deci-C/10. Fan: raw RPM. Power correctly hidden |
| VRAM | VramCollector.cs | DXGI QueryVideoMemoryInfo | ✅ bytes/(1024*1024) = MB |
| RAM | RamCollector.cs | GlobalMemoryStatusEx | ✅ bytes/(1024*1024) = MB |
| Battery | BatteryCollector.cs | CallNtPowerInformation | ✅ mW/1000=W, seconds/60=minutes |
| Display Hz | DisplayCollector.cs | EnumDisplaySettings | ✅ Current Hz (static on VRR) |
| NVIDIA GPU | NvApiGpuCollector.cs | NvAPIWrapper.Net + NVML P/Invoke | ✅ All API calls verified against NuGet XML |
| Vendor detection | App.xaml.cs DetectPrimaryGpuVendor | DXGI VendorId | ✅ Standard PCI-SIG IDs |
| Collector ordering | App.xaml.cs | D3DKMT[6] then vendor[9] | ✅ Last writer wins, safe |
| Formatters | MetricFormatterCompact.cs | N/A | ✅ No unit transformations, display-only |
| XamlControlsResources guard | App.xaml.cs | N/A | ✅ Moved to code-behind with try-catch |
| global.json | global.json | N/A | ✅ Pins .NET 8 SDK |
| MetricFlags | MetricFlags.cs | N/A | ✅ Clean, GpuClock added |
| MetricStatusFactory | MetricStatusFactory.cs | N/A | ✅ Dynamic vendor source strings |
| TelemetryContractGuard | TelemetryContractGuard.cs | N/A | ✅ Validates GpuMetrics properties |
| DependencyState.GpuTelemetrySource | TelemetrySnapshot.cs | N/A | ✅ MessagePack Key(13), default "D3DKMT" |

### Minor Issues (non-critical)

| Issue | File:Line | Detail | Fix |
|-------|-----------|--------|-----|
| Battery % unbounded | BatteryCollector.cs:39 | Could exceed 100% on degraded battery | Add `Math.Clamp(chargePercent, 0.0, 100.0)` |
| Display Hz static on VRR | DisplayCollector.cs | Shows nominal Hz, not dynamic | Document limitation; future: investigate DWM VRR APIs |
| Average FPS statistical skew | EtwFrameCapture.cs:127 | Averages FPS values not frame times | Minor; add comment documenting the choice |

---

## PART 2: WHAT'S WRONG (verified against real sources)

### CRITICAL: AMD ADLX Native (NativeTelemetry.cpp lines 9-416)

**Problem:** 6 hand-rolled COM vtable structs with fabricated method ordering.

| Vtable | Our Code | Real SDK | Impact |
|--------|----------|----------|--------|
| IADLXPerfMonServices | 9 methods | 17 methods | GetCurrentGPUMetrics at slot 7 → actually calls GetMaxPerformanceMetricsHistorySizeRange |
| IADLXSystem | Has Acquire/Release + 4 methods | 12 methods, NO Acquire/Release | GetPerformanceMonitoringServices at wrong slot |
| IADLXGPUMetrics | 12 methods | Unverifiable (header 404) | Possibly correct but unproven |
| IADLXGPUMetricsSupport | 11 methods | Unverifiable (header 404) | Possibly correct but unproven |
| IADLXGPUList | 9 methods | May not exist as separate interface | Possibly wrong |
| ADLX_FULL_VERSION | 0x0100000000000000 (v1.0) | SDK is at v1.4 | May be rejected or behave unexpectedly |

**Entry points confirmed correct:** ADLXInitialize, ADLXTerminate ✅

### CRITICAL: Intel IGCL Native (NativeTelemetry.cpp lines 139-595)

**Problem:** ctl_power_telemetry_t is NOT a flat binary buffer. It's a container of nested `ctl_telemetry_field_t` objects.

| Item | Our Code | Reality | Impact |
|------|----------|---------|--------|
| Struct approach | Raw byte buffer with hard-coded offsets | Nested `ctl_telemetry_field_t` structs, each with bSupported + value union + units enum | Reads random memory |
| "supportedFields" bitmask at offset 8 | Used as uint64_t global bitmask | Does NOT exist — each field has its own bSupported boolean | Validity checks meaningless |
| Offset 80 (energy) | Assumed double | Wrong — nested struct at unknown offset | Garbage data |
| Offset 112 (temp) | Assumed double | Wrong | Garbage data |
| Offset 152 (clock) | Assumed double | Wrong | Garbage data |
| Offset 200 (fan) | Assumed double | Wrong | Garbage data |
| ctl_init_args_t | #pragma pack(1), plain uint32_t fields | May use default alignment, ctl_version_info_t may be a struct | ctlInit gets garbage args |
| DLL name | "ControlLib.dll" | May be "igcl64.dll" on some systems | Init fails on some hardware |

### CRITICAL: CPU Power EMI (CpuPowerCollector.cs)

**Problem:** Wrong GUID, wrong IOCTLs, wrong structs, missing protocol steps.

| Item | Our Code | Reality | Impact |
|------|----------|---------|--------|
| EMI GUID | {45BD8344-7ED6-49cf-A440-C276C933B220} | {45BD8344-7ED6-49cf-A440-C276C933B053} | Device enumeration fails completely |
| IOCTL GetMetadata | 0x00224000 | 0x00224404 | Wrong function called |
| IOCTL GetMeasurement | 0x00224004 | 0x00224408 | Wrong function called |
| IOCTL GetMetadataV2 | 0x00224008 | This is actually IOCTL_EMI_GET_VERSION | Wrong function called |
| EMI_MEASUREMENT_DATA_V2 | Single {energy, time} struct | Array of EMI_CHANNEL_MEASUREMENT_DATA (one per channel) | Wrong struct layout |
| Metadata V2 parsing | nameOffset=8 | Real struct has 256+ byte header (HardwareOEM[128] + HardwareModel[128] + ...) | Reads garbage as name |
| Protocol | Skip version/size queries | Must: GetVersion → GetMetadataSize → GetMetadata → GetMeasurement | Missing required steps |
| Variable naming | _prevEnergyMicrojoules | Actually stores picowatt-hours | Misleading (formula is correct for pWh) |

### Research MD Errors Found

| Section | Claim | Reality |
|---------|-------|---------|
| Section 2 | "D3DKMT ADAPTERPERFDATA type is 25" | Actually type 62 (KMTQAITYPE_ADAPTERPERFDATA) — code uses 62 correctly |
| Section 9 | "No C# wrapper exists anywhere for IGCL" | FanControl.IntelCtlLibrary IS a C# wrapper. Also: Rem0o/drivers.gpu.control-library.bindings exists |
| Section 9 | "ControlLib.dll in System32" | May be igcl64.dll depending on driver version |
| Section 11 | "EMI exposes RAPL data via WMI" | EMI uses device IOCTLs (SetupAPI + DeviceIoControl), NOT WMI |
| Section 9 | "gpuEnergyCounter in microjoules" | Confirmed correct per Intel docs, BUT the struct is nested — can't read at flat offset |

---

## PART 3: HOW TO FIX

### Fix 1: AMD ADLX — Use Rem0o/ADLXWrapper (RECOMMENDED)

**Don't hand-roll vtables.** Use the proven C# wrapper.

1. Reference [Rem0o/ADLXWrapper](https://github.com/Rem0o/ADLXWrapper) — production-proven, used by FanControl.ADLX
2. Either:
   - (a) Add ADLXWrapper as a NuGet/submodule and call from AdlxGpuCollector.cs directly (eliminates NativeTelemetry.cpp ADLX code entirely)
   - (b) Copy the verified vtable definitions from ADLXWrapper's source to NativeTelemetry.cpp (keep C++ approach but with CORRECT vtable ordering)
3. Option (a) is safer — removes all hand-rolled COM interop
4. Verify ADLX_FULL_VERSION against current ADLXWrapper (they handle version negotiation)

### Fix 2: Intel IGCL — Use Rem0o/drivers.gpu.control-library.bindings (RECOMMENDED)

**Don't hand-roll struct offsets.** Use the existing C# bindings.

1. Reference [Rem0o/drivers.gpu.control-library.bindings](https://github.com/Rem0o/drivers.gpu.control-library.bindings) — has real struct definitions for IGCL
2. Also reference [FanControl.IntelCtlLibrary](https://github.com/Rem0o/FanControl.IntelCtlLibrary) — working implementation that reads IGCL telemetry on Intel Arc
3. Either:
   - (a) Use the bindings package directly from C# (eliminates NativeTelemetry.cpp IGCL code entirely)
   - (b) Extract the verified struct layouts from the bindings and port to C++
4. Option (a) is strongly preferred — the nested `ctl_telemetry_field_t` objects are complex and error-prone in hand-rolled C++
5. Verify DLL name: check whether FanControl uses "ControlLib.dll" or "igcl64.dll"

### Fix 3: CPU Power EMI — Rewrite from Windows SDK headers

**Every constant and struct is wrong.** Near-total rewrite needed.

1. Get correct values from `emi.h` (Windows SDK/WDK):
   - Fix GUID: `{45BD8344-7ED6-49cf-A440-C276C933B053}`
   - Fix IOCTLs: GetMetadata=0x00224404, GetMeasurement=0x00224408
   - Fix EMI_MEASUREMENT_DATA_V2: array of channel measurements
   - Fix metadata parsing: real struct has 256+ byte header
2. Implement proper protocol: GetVersion → GetMetadataSize → GetMetadata → GetMeasurement
3. Handle V1 vs V2 devices based on version query
4. Reference [open-source EMI implementations](https://github.com/search?q=IOCTL_EMI_GET_MEASUREMENT) for working examples
5. Rename `_prevEnergyMicrojoules` → `_prevEnergyPicowattHours`

### Fix 4: Minor Issues

| Fix | File | Change |
|-----|------|--------|
| Battery % clamp | BatteryCollector.cs:39 | Add `Math.Clamp(chargePercent, 0.0, 100.0)` |
| Document VRR Hz limitation | DisplayCollector.cs | Add comment: "Shows nominal Hz on VRR displays" |
| Rename EMI variable | CpuPowerCollector.cs:26 | `_prevEnergyMicrojoules` → `_prevEnergyPicowattHours` |

### What NOT to change

- NVIDIA collector (NvApiGpuCollector.cs) — verified correct, leave as-is
- All existing collectors — verified correct
- Collector ordering in App.xaml.cs — design is sound
- MetricFormatterCompact.cs — no unit errors
- global.json, MetricFlags, MetricStatusFactory — all clean

---

## PART 4: PRIORITY ORDER

1. **IGCL (Intel)** — This is the #1 fix (MSI Claw Intel Arc 140V is the test device). Use Rem0o bindings.
2. **ADLX (AMD)** — Primary handheld market (ROG Ally, Legion Go). Use Rem0o ADLXWrapper.
3. **EMI (CPU Power)** — Nice-to-have. Rewrite from SDK headers. Lower priority than GPU.
4. **Minor fixes** — Battery clamp, variable rename. Trivial.

---

## PART 5: 2026 WEB VERIFICATION (latest data)

| Claim | Status | Detail |
|-------|--------|--------|
| NVIDIA G-Sync + topmost HWND | ⚠️ STILL BROKEN | No 2026 driver fix found. Composed Flip demotion persists. |
| EAC allows external overlays | ⚠️ GAME-DEPENDENT | Not blanket safe. Per-title whitelisting. Research MD oversimplified. |
| BattlEye allows overlays | ✅ CONFIRMED | Explicitly allows non-cheat overlays. Injection-based ones risky. |
| Riot Vanguard safe | ⚠️ RISKY | Kernel-level monitoring. User reports of innocent overlays flagged. No guarantee. |
| WinRing0 Defender blocked | ✅ CONFIRMED | Still flagged CVE-2020-14979. No fix. |
| EMI GUID | ✅ CONFIRMED | `{...B053}` is correct. Our code has `{...B220}` = WRONG. |
| PresentMon ETW frame gen | ✅ CONFIRMED | PresentMon 2.5.0+ has Frame Type Differentiation. FrameType metric available. |
| LHM IGCL v0.9.6 | ✅ CONFIRMED | Added IGCL telemetry for Intel iGPU. |

---

## PART 6: RESEARCH MD CORRECTIONS NEEDED

Update `memory-bank/2026/04/09/gpu-telemetry-investigation.md`:

1. Section 2: Change "type 25" to "type 62" (KMTQAITYPE_ADAPTERPERFDATA)
2. Section 9: Remove "No C# wrapper exists anywhere" — FanControl.IntelCtlLibrary and drivers.gpu.control-library.bindings both exist
3. Section 9: Note DLL may be "igcl64.dll" not just "ControlLib.dll"
4. Section 11: Change "WMI query" to "Device IOCTLs via SetupAPI + DeviceIoControl"
5. Section 18 (Implementation Plan): Add requirement to use verified open-source wrappers (Rem0o) instead of hand-rolling interop
6. Section 15 (Anti-cheat): EAC is game-dependent, not blanket safe. Vanguard is risky, not safe.
7. Section 16 (Frame Generation): PresentMon 2.5.0+ now has Frame Type Differentiation with FrameType metric — update from generic "PresentMon ETW" to specific version
