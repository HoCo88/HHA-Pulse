# Telemetry Trace Map

**Last verified:** 2026-04-15 (Plan A backend expansion pass against current `HHAP-0.5` branch).
**Purpose:** every metric the HUD/widget can show, end-to-end. What we read, which API, what unit, how it's converted, what's shown to the user, and how the vendor's official documentation says it should be done.

Every claim cites a `file:line` from this repo or a vendor contract header vendored in `src/HHAPulse.Native/VendorContracts/`. Items that could not be proved against code or vendored docs are marked **UNVERIFIED**.

## Current Implementation Delta (2026-04-10)

The active-bug section below captures the audit findings that triggered the fix. The current implementation has addressed the user-visible truth issues this way. For the full hardware validation log on the MSI Core Ultra 7 258V / Arc 140V machine, see `memory-bank/2026/04/10/INDEX.md`.

- `input_latency` is reserved but no longer shown in the HUD, custom picker, metric status cards, or traces. `GpuBusyMilliseconds` is not labeled as latency.
- Refresh-rate sentinel values `0` and `1` render as `--` in HUD/widget/diagnostics.
- `AppFramesPerSecond`, `PresentFramesPerSecond`, and `DisplayFramesPerSecond` remain append-only MessagePack fields but are set to `0`/not-computed until a real source exists.
- `total_power` / Device Power is whole-device battery discharge watts only. On AC it returns `--` unless a real platform/whole-device power meter is found. RAPL `PKG + DRAM` is retained as diagnostics-only component evidence, not shown as Device Power.
- Battery capacity/runtime probing uses battery class IOCTLs only when absolute mWh units are reported; relative units are rejected for Wh conversion.
- AMD ADLX GPU power prefers `GPUTotalBoardPower` and falls back to `GPUPower`; D3DKMT `PowerRaw` remains rejected because it is not watts.
- Intel Lunar Lake / Arc 140V GPU power is validated via IGCL `gpuEnergyCounter/timeStamp` and the EMI `RAPL_Package0_PP1` iGPU rail. The observed IGCL aggregate support mask on this machine is `0x0028`, meaning only `gpuEnergyCounter` and `gpuCurrentClockFrequency` are supported there.
- Intel Lunar Lake / Arc 140V GPU temperature is still unavailable from the proved paths: D3DKMT reports `0.0C`; IGCL aggregate reports no temp field; IGCL dedicated temperature enumeration did not expose a usable sensor in the final native build. Do not fabricate it from CPU/ACPI/MSI temperatures.
- Intel UMA/iGPU VRAM is no longer hidden. Used memory comes from PDH GPU memory counters, preferring the target process when a valid capture target exists; DXGI supplies shared capacity/budget and records the 128 MB dedicated aperture as diagnostic-only evidence.
- CPU temperature is now sourced from the elevated capture service via WMI `MSAcpi_ThermalZoneTemperature`.
- Device/chassis fan is no longer labeled as GPU-only. On the MSI test machine it comes from the elevated capture service via read-only `root\WMI:MSI_ACPI.Get_Fan` subfeature `0x00`, decoded as big-endian tach values with `RPM = 480000 / raw`.
- MSI device/SoC temperature is now surfaced as a separate `device_temp` metric from the elevated capture service via `MSI_ACPI.Get_Temperature` subfeature `0x00`. It remains explicitly separate from `gpu_temp`.
- FPS target routing holds the last valid game target through overlay/Steam/Game Bar foreground transitions for the 10-second grace window; AVG FPS is now a 5-second rolling average.
- Capture diagnostics state the safety contract explicitly: ETW/PDH read-only, no hooks, no injection, no process memory reads, no kernel driver, and no VRR/display setting changes.
- Runtime provenance traces now carry collector/validator, API contract, raw unit, conversion rule, converted unit, validation state, and rejection/unavailable reason when collectors record them.
- `MetricStatusFactory` and `MeasurementTraceFactory` no longer invent hardcoded source/collector fallbacks. If runtime provenance is missing, diagnostics report `unknown collector`.
- "Boundary traces" has been renamed to "Last observed boundary state"; widget client count is last-observed, not a heartbeat.

Symbol legend:
- ✅ verified honest (matches code AND matches vendor contract)
- ⚠️ verified honest by accident (current code is right but the trace label is hardcoded with no runtime binding — see "Structural fragility" at the end)
- ❌ active bug — produces wrong or fabricated data the user can see
- 🔬 unverifiable from this repo alone (needs hardware sign-off)

---

## Table of Contents

1. [FPS / frame timing / frame generation](#1-fps--frame-timing--frame-generation)
2. [CPU](#2-cpu)
3. [GPU](#3-gpu)
4. [Memory (RAM + VRAM)](#4-memory-ram--vram)
5. [Battery](#5-battery)
6. [Display](#6-display)
7. [Power roll-up (CPU + GPU + total system + battery time)](#7-power-roll-up)
8. [Storage](#8-storage)
9. [CPU clock](#9-cpu-clock)
10. [NPU detection](#10-npu-detection)
11. [Structural fragility — why hardcoded labels are dangerous](#11-structural-fragility)
12. [Historical audit bugs (now addressed by the 2026-04-10 fix pass)](#12-historical-audit-bugs-now-addressed-by-the-2026-04-10-fix-pass)

---

## 1. FPS / frame timing / frame generation

### What we show
`FPS`, `Avg FPS`, `1% low FPS`, `0.1% low FPS`, `frametime (ms)`, `frame generation detected/not detected`.

### Pipeline
```
Game process → DXGI/D3D9 ETW Present-Start events
            → HHAPulse_FrameCapture session (Microsoft.Diagnostics.Tracing.TraceEvent)
            → EtwFrameCapture.OnEtwEvent (filters by target PID)
            → frame-time delta (ms) → FrameStatisticsCalculator
            → CaptureFrameMetrics
            → \\.\pipe\LOCAL\HHAPulse.Capture.Out (MessagePack)
            → CaptureServiceCollector (overlay-side)
            → snapshot.Performance.*
            → MetricFormatterCompact / HUD
```

### How

| Metric | Source API | File:Line | Conversion | Unit at HUD |
|---|---|---|---|---|
| Frame time delta | `TraceEvent.TimeStampRelativeMSec` (Microsoft.Diagnostics.Tracing) | `EtwFrameCapture.cs:103` | `nowMs - lastPresentMs` (clamped 0.1–1000 ms) | ms |
| FPS | `1000.0 / averageFrameTimeMs` | `FrameStatisticsCalculator.cs:13` | trivial reciprocal | fps |
| 1% low FPS | sort frame times asc, take index `ceil((N-1)*0.99)`, return `1000/ms` | `FrameStatisticsCalculator.cs:21-29` | percentile→fps | fps |
| 0.1% low FPS | same, with `0.999` | `FrameStatisticsCalculator.cs:21-29` | percentile→fps | fps |
| Frame generation | `IntelPresentMonFrameTypeEvidence.TryGetGeneratedFrameEvidence` reads `FrameType` payload from Intel-PresentMon ETW provider | `IntelPresentMonFrameTypeEvidence.cs:20-66` | maps `Intel_XEFG` / `AMD_AFMF` → detected; `Original` / `Repeated` / `Unspecified` → not detected | "detected" / "not detected" |

### ETW provider GUIDs (from `EtwFrameCapture.cs:11-13`)
| Provider | GUID | Event ID we filter |
|---|---|---|
| Microsoft-Windows-DXGI | `CA11C036-0102-4A2D-A6AD-F03CFED5D3C9` | `42` (Present Start) |
| Microsoft-Windows-D3D9 | `783ACA0A-790E-4D7F-8451-AA850511C6B9` | `1` (Present Start) |
| Microsoft-Windows-DxgKrnl | `802EC45A-1E99-4B83-9920-87C98277BA9D` | enabled but not filtered |
| Intel-PresentMon | `ECAA4712-4644-442F-B94C-A32F6CF8A499` | `FrameType` payload |

The DXGI and D3D9 event IDs were verified locally via `wevtutil` against the manifests on the dev machine, per the comments at `EtwPresentEventFilter.cs:7-13`. Intel-PresentMon's GUID is locked from PresentMon source per the prologue at `IntelPresentMonFrameTypeEvidence.cs:7-15`.

### How it should be done (vendor reference)

- **Frame timing**: PresentMon's pattern is to capture `Present-Start` events from DXGI and D3D9 providers per process and compute frame time as the delta between consecutive starts. Our code matches this pattern at `EtwFrameCapture.cs:88-160`.
- **1% / 0.1% low**: standard convention used by PresentMon and CapFrameX is `take the worst Nth-percentile frame time, convert to fps via 1000/ms`. Our code does exactly that for large windows. For small windows (`N < ~50`) the algorithm collapses to "absolute worst frame fps" — a conservative approximation.
- **Frame generation detection**: Intel publishes its frame-type evidence via the dedicated Intel-PresentMon ETW provider. We read it correctly. We deliberately do **not** invent a numeric "frame-gen FPS" — there is no such field that doesn't require display VBlank correlation, which we don't yet do.

### Status

- ✅ FPS calculation: math correct, ms→fps conversion correct.
- ✅ 1% / 0.1% low: percentile algorithm correct for realistic sample sizes.
- ✅ Frame time: read directly from ETW timestamp deltas, no fabrication.
- ✅ Frame-gen detection: parses real ETW payload by name, no fabrication.
- ✅ ETW provider GUIDs and event IDs: verified locally per code comments.
- ❌ **`AppFramesPerSecond`, `PresentFramesPerSecond`, `DisplayFramesPerSecond` are fabricated** (`EtwFrameCapture.cs:145-147`):
  ```csharp
  AppFramesPerSecond = fps,
  PresentFramesPerSecond = fps,
  DisplayFramesPerSecond = 0,
  ```
  These three distinct PresentMon concepts (game render rate, Present-call rate, scanout rate) are collapsed into one value or zeroed. The HUD doesn't currently display them, but the diagnostics dump and any future widget that binds to them will report nonsense. **Fix: delete the fields from `PerformanceMetrics` until they have real sources, or compute them from PresentMon evidence.**

---

## 2. CPU

### 2.1 CPU usage (%)

| Aspect | Detail |
|---|---|
| What we show | CPU usage 0–100 % |
| API | `kernel32!GetSystemTimes` |
| File:line | `CpuUsageCollector.cs:68-72` (P/Invoke), `:44-49` (formula) |
| Formula | `(kernelDelta + userDelta - idleDelta) / (kernelDelta + userDelta) * 100`, clamped to 0–100 |
| Unit chain | FILETIME ticks → percent (no further conversion) |
| Display | `MetricFormatterCompact.cs:46`: `{value:0}%` |
| Vendor reference | `learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemtimes` — confirms `lpKernelTime` already includes idle ticks, which is exactly why we subtract idle from the sum. |
| First-tick safety | `CpuUsageCollector.cs:38` — `_hasBaseline` guard prevents fabricated 0% on first sample. |
| Status | ⚠️ Verified honest. Label `"GetSystemTimes"` at `MetricStatusFactory.cs:22` is hardcoded, but matches reality. |

### 2.2 CPU power (watts)

| Aspect | Detail |
|---|---|
| What we show | CPU package watts (e.g. `12.5 W`) |
| API | Windows EMI driver via `setupapi!SetupDiGetClassDevs` + `kernel32!DeviceIoControl(IOCTL_EMI_GET_MEASUREMENT)` |
| EMI device GUID | `{45BD8344-7ED6-49CF-A440-C276C933B053}` (`EmiContract.cs:9`) |
| File:line | `CpuPowerCollector.cs:45` (open), `:176-183` (read), `EmiContract.cs:69-85` (math) |
| Raw fields | `EMI_CHANNEL_MEASUREMENT_DATA.AbsoluteEnergy` (picowatt-hours), `AbsoluteTime` (100-ns intervals) |
| Conversion | `EmiContract.cs:77-84`: `elapsedSeconds = (deltaTime100ns) / 10_000_000`; `watts = (deltaPwh × 3.6e-9) / elapsedSeconds` (1 pwh → 3.6e-9 joules) |
| Sanity clamp | `CpuPowerCollector.cs:192-197` — only sets `PowerWatts` if `> 0 and < 500`. Failures return early without writing the field. |
| Display | `MetricFormatterCompact.cs:49-52`: `{value:0.0}W`, `--` when `<= 0` |
| Vendor reference | `learn.microsoft.com/en-us/windows/win32/api/emi/ns-emi-emi_channel_measurement_data` — confirms picowatt-hours and 100-ns time units. The EMI driver is the OS-level abstraction over Intel RAPL MSR `0x611` (`MSR_PKG_ENERGY_STATUS`) and AMD RAPL MSR `0xC001029B` (`MSR_AMD_RAPL_PKG_ENERGY_STATUS`). On Intel platforms, Microsoft's IPM driver translates RAPL deltas into the picowatt-hour counter we read. On AMD, the OEM driver does the same thing. **We do not need vendor-specific code paths** — Windows EMI handles both. |
| Hardware truth | 🔬 Code path is contract-correct. Whether the watts on a real handheld match a power meter requires sign-off on actual hardware. Per `MetricStatusFactory.cs:23`, the status message warns "Hardware validation is still pending." |
| Status | ⚠️ Verified honest. Label `"EMI energy deltas"` matches reality. **Failure handling is correct** — every failure path returns early with no PowerWatts assignment, no fake values. |

---

## 3. GPU

GPU is the only metric category where we have to deal with vendor-specific code paths because each vendor publishes its own telemetry SDK with different units and contracts. We have **four** GPU collectors, plus a vendor-agnostic D3DKMT fallback.

### Vendor SDK matrix

| Vendor | Collector | Native? | DLL | Vendored contract |
|---|---|---|---|---|
| Intel | `IgclGpuCollector` (C#) → `HhaPulseIgclReadGpu` (C++) | yes | `ControlLib.dll` / `igcl64.dll` (loaded by native, see `NativeTelemetry.cpp:554-558`) | `src/HHAPulse.Native/VendorContracts/IgclTelemetryContract.h` (vendored from `github.com/intel/drivers.gpu.control-library/include/igcl_api.h`, retrieved 2026-04-09) |
| AMD | `AdlxGpuCollector` (C#) → `HhaPulseAdlxReadGpu` (C++) | yes | `amdadlx64.dll` (`NativeTelemetry.cpp:390`) | `src/HHAPulse.Native/VendorContracts/AdlxTelemetryContract.h` (vendored from `GPUOpen-LibrariesAndSDKs/ADLX` v1.4.0.110, retrieved 2026-04-09) |
| NVIDIA | `NvApiGpuCollector` (C#) — pure C#, NO native side | no | `nvapi64.dll` (via `NvAPIWrapper.Net` package) AND `nvml.dll` (P/Invoke) | none — uses official NvAPIWrapper.Net and NVML by P/Invoke |
| Any (fallback) | `GpuPerfDataCollector` (C#) | no | `gdi32.dll` (`D3DKMTQueryAdapterInfo`) | Microsoft-documented `KMTQAITYPE_ADAPTERPERFDATA = 62` |

> **Note on the suspected `MeasurementTraceFactory.cs:135` mislabel**: the table maps `"NVML" → "NvApiGpuCollector"`. This is **technically correct** because `NvApiGpuCollector.cs` is a single class that loads BOTH `nvapi64.dll` (for temp/clock/fan, lines 41-56) AND `nvml.dll` (for power, lines 189-258). The class is misleadingly named after only NvAPI even though it uses both APIs. Not a bug, but confusing — the class would be more honestly named `NvidiaGpuCollector`.

### 3.1 GPU usage (%)

| Aspect | Detail |
|---|---|
| API | `pdh!PdhAddEnglishCounter` + `PdhGetFormattedCounterArray` |
| Counter path | `\GPU Engine(*engtype_3D*)\Utilization Percentage` (`GpuUsageCollector.cs:30`) |
| File:line | `GpuUsageCollector.cs:28-46` (init), `:51-122` (collect) |
| Multi-instance handling | `:73-114` — enumerates ALL matching wildcard instances and SUMs them. Comment at lines 65-68 explains why: per-process `engtype_3D` instances are split, and `PdhGetFormattedCounterValue` returns 0 on Intel iGPUs. |
| Final clamp | `Math.Clamp(totalUtilization, 0.0, 100.0)` at line 114 |
| Display | `MetricFormatterCompact.cs:56`: `{value:0}%` |
| Vendor reference | The PDH `\GPU Engine(*)\Utilization Percentage` counter is provided by the WDDM driver and exposed by Windows. Task Manager's "GPU" column uses the same counter set. |
| Status | ⚠️ Verified honest. Label `"PDH GPU Engine"` matches the actual counter. The multi-instance summing is exemplary — works correctly on iGPUs, dGPUs, and hybrid. |

### 3.2 GPU temperature, clock, fan, power (vendor SDK paths)

The same shape applies to all four vendor collectors: read a struct, extract per-metric values, sanity-clamp, set the snapshot field. They differ only in which SDK they call and how power is computed.

#### Intel — IGCL

| Metric | Field on `ctl_power_telemetry_t` | Expected unit | C++ extraction |
|---|---|---|---|
| Temperature | `gpuCurrentTemperature` | `CTL_UNITS_TEMPERATURE_CELSIUS` | `NativeTelemetry.cpp:666-670` (clamps –50 to 200 °C) |
| Clock | `gpuCurrentClockFrequency` | `CTL_UNITS_FREQUENCY_MHZ` | `NativeTelemetry.cpp:672-676` (clamps 0–10000 MHz) |
| Fan | `fanSpeed[0..CTL_FAN_COUNT]` | `CTL_UNITS_ANGULAR_SPEED_RPM` | `NativeTelemetry.cpp:678-686` (loops fans, clamps 0–20000 RPM) |
| **Power (computed)** | `gpuEnergyCounter` + `timeStamp` | `CTL_UNITS_ENERGY_JOULES` + `CTL_UNITS_TIME_SECONDS` | `NativeTelemetry.cpp:688-711` |

**Power math (Intel)**:
```cpp
// NativeTelemetry.cpp:695-704
double deltaTime   = timeSeconds   - s_igclPreviousTimeSeconds;
double deltaEnergy = energyJoules  - s_igclPreviousEnergyJoules;
if (deltaTime > 0.0 && deltaEnergy >= 0.0) {
    double watts = deltaEnergy / deltaTime;          // joules / seconds = watts
    if (watts >= 0.0 && watts < 1000.0) {
        out->powerWatts = watts;
        out->validFlags |= HHAPULSE_GPU_VALID_POWER;
    }
}
```

**Vendor reference**: Intel's `igcl_api.h` (vendored at `IgclTelemetryContract.h:42-59`) defines `CTL_UNITS_ENERGY_JOULES = 6` and `CTL_UNITS_TIME_SECONDS = 7`. The `TryReadIgclTelemetryValue` helper at `NativeTelemetry.cpp:184-187` **verifies the unit code matches the expected enum** before extracting — defensive programming against an SDK update changing units. If Intel ever returns the energy field in microjoules or millijoules, the unit check fails and we don't read it (rather than silently producing wrong watts).

**Status**: ✅ Implementation matches the vendor contract precisely.

#### AMD — ADLX

| Metric | ADLX call | Return type | C++ extraction |
|---|---|---|---|
| Temperature | `IADLXGPUMetrics::GPUTemperature` | `adlx_double` (Celsius) | `NativeTelemetry.cpp:473-481` (clamps –50 to 200 °C) |
| Clock | `IADLXGPUMetrics::GPUClockSpeed` | `adlx_int` (MHz) | `NativeTelemetry.cpp:503-511` (clamps 0–10000 MHz) |
| Fan | `IADLXGPUMetrics::GPUFanSpeed` | `adlx_int` (RPM) | `NativeTelemetry.cpp:493-501` (clamps 0–20000 RPM) |
| **Power** | `IADLXGPUMetrics::GPUPower` | `adlx_double` (Watts) | `NativeTelemetry.cpp:483-491` (clamps 0–1000 W) |

ADLX is the easiest vendor — it gives watts directly, no integration math. Each call is gated by the corresponding `IADLXGPUMetricsSupport::IsSupportedGPUxxx` check (`NativeTelemetry.cpp:127-138`) so we don't read fields the GPU doesn't expose.

**Vendor reference**: `AdlxTelemetryContract.h:209-220` (vendored from AMD's official ADLX SDK 1.4.0.110) declares the `IADLXGPUMetricsVtbl` with `GPUPower(adlx_double*)` returning watts.

**Status**: ✅ Implementation matches the vendor contract.

#### NVIDIA — NvAPI + NVML

`NvApiGpuCollector.cs` is **pure C#** — no native shim needed. It loads two NVIDIA libraries:

| Metric | Library | API | File:line | Unit returned | Conversion |
|---|---|---|---|---|---|
| Temperature | `nvapi64.dll` (via NvAPIWrapper.Net) | `_gpu.ThermalInformation.ThermalSensors[0].CurrentTemperature` | `NvApiGpuCollector.cs:84-92` | °C (int) | direct |
| Clock | `nvapi64.dll` (via NvAPIWrapper.Net) | `_gpu.CurrentClockFrequencies.GraphicsClock.Frequency` | `:103-111` | **kHz** | `/ 1000.0` → MHz |
| Fan RPM | `nvapi64.dll` (via NvAPIWrapper.Net) | `_gpu.CoolerInformation.Coolers[i].CurrentFanSpeedInRPM` | `:122-130` | RPM | direct |
| **Power** | `nvml.dll` (P/Invoke) | `nvmlDeviceGetPowerUsage(device, &milliwatts)` | `:143-156`, P/Invoke at `:290-291` | **milliwatts** | `/ 1000.0` → watts |

**Why both libraries**: per the class comment at `NvApiGpuCollector.cs:9-15`, NvAPI provides true tachometer fan RPM (NVML only exposes intended fan %) but NVML is more reliable for wattage. The class loads both at startup and falls back gracefully if either is missing. NVML init is at `:189-258`; the binary is loaded from `C:\Program Files\NVIDIA Corporation\NVSMI\nvml.dll` if not on PATH.

**Vendor reference**: NVIDIA's NVML reference (`docs.nvidia.com/deploy/nvml-api`) defines `nvmlDeviceGetPowerUsage` as returning power in milliwatts (uint). The code's `milliwatts / 1000.0` is the textbook conversion. NvAPIWrapper.Net's `GraphicsClock.Frequency` returns kHz per the wrapper's docs — the `/ 1000.0` at line 108 converts to MHz.

**Status**: ✅ Both unit conversions correct. Confusion only in the class name (uses NVML too) and in the source labels: temperature/clock/fan get `"NvAPI"`, power gets `"NVML"` (different fields written by the same class). The trace factory's `CollectorForGpuSource()` mapping handles both correctly.

#### Microsoft fallback — D3DKMT

| Metric | API | File:line | Unit returned | Conversion |
|---|---|---|---|---|
| Temperature | `D3DKMTQueryAdapterInfo(KMTQAITYPE_ADAPTERPERFDATA=62)` → `D3DKMT_ADAPTER_PERFDATA.Temperature` | `GpuPerfDataCollector.cs:213-225` | **deci-Celsius** | `/ 10.0` → °C |
| Fan | `D3DKMT_ADAPTER_PERFDATA.FanRPM` | `:239-245` | RPM | direct |
| **Power** | `D3DKMT_ADAPTER_PERFDATA.Power` | `:227-236` | **tenths of % of TDP** (NOT watts) | **REFUSED — never set** |

This collector is **exemplary**. The comment at `GpuPerfDataCollector.cs:227-230` reads:
> "Power: D3DKMT reports 'tenths of % of TDP' — a unitless ratio, NOT watts. Never set PowerWatts or MetricFlags.GpuPower here. Only vendor SDK collectors (ADLX, IGCL, NVAPI) provide true watt readings and are authorised to set GpuPower."

The collector explicitly refuses to fabricate watts from a TDP ratio, and writes a status message saying so. This is the discipline that should apply everywhere.

**Vendor reference**: `learn.microsoft.com/en-us/windows-hardware/drivers/ddi/d3dkmthk/ns-d3dkmthk-_d3dkmt_adapter_perfdata` documents the fields (Temperature in deci-Celsius, Power in tenths-of-percent-of-TDP).

**Status**: ✅ Honest temperature, honest fan, honest **refusal** to expose watts.

---

## 4. Memory (RAM + VRAM)

### 4.1 System RAM

| Aspect | Detail |
|---|---|
| API | `kernel32!GlobalMemoryStatusEx` |
| File:line | `RamCollector.cs:34-36` (P/Invoke), `:20-29` (math) |
| Raw unit | bytes (`MEMORYSTATUSEX.ullTotalPhys`, `ullAvailPhys`) |
| Conversion | `RamCollector.cs:25-29`: `totalMb = ullTotalPhys / (1024.0 * 1024.0)`; `usedMb = totalMb - (ullAvailPhys / (1024.0 * 1024.0))` |
| Snapshot fields | `RamUsedMegabytes`, `RamTotalMegabytes` |
| Display | `MetricFormatterCompact.cs:119-126` — conditional MB/GB; `MeasurementTraceFactory.cs:54` — always GB; `CompactWidget.xaml.cs:87` — always GB |
| Vendor reference | `learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex` — `ullTotalPhys` is bytes. |
| Status | ⚠️ Math correct. Display has a minor inconsistency (HUD shows MB on tiny systems, trace/widget always show GB). All real handhelds have ≥1 GB so users never see the divergence in practice. |

### 4.2 VRAM

**Current truth after the 2026-04-10 final push:** this section's original table is historical audit context. The active Intel/UMA iGPU path no longer hides VRAM when `DedicatedVideoMemory <= 256MB`. On the MSI Arc 140V machine the HUD showed `4.5/18.0G` from PDH `GPU Process Memory Total Committed` for used memory, while DXGI provided shared capacity/budget evidence and retained the `128MB` dedicated aperture as diagnostic-only data. If there is no valid target process, the fallback is PDH adapter memory counters, not the tiny DXGI local usage number.

| Aspect | Detail |
|---|---|
| API | `IDXGIAdapter3::QueryVideoMemoryInfo(0, MemorySegmentGroup.Local)` (Vortice.DXGI binding) |
| File:line | `VramCollector.cs:69` (call), `:77-78` (assignment) |
| Raw unit | bytes (`Budget`, `CurrentUsage` are `UINT64`) |
| Conversion | `VramCollector.cs:77-78`: `totalBytes / (1024.0 * 1024.0)` → MB |
| Total selection logic | `VramCollector.cs:70-74`: prefer `desc.DedicatedVideoMemory` over `info.Budget` when dedicated VRAM > 256 MB. Reason: DXGI `Budget` is per-process, not adapter total. The `desc.DedicatedVideoMemory` from `IDXGIAdapter1::GetDesc1` is the actual adapter-installed VRAM. |
| Intel iGPU handling | `VramCollector.cs:62-67`: explicitly hides VRAM when `VendorId == 0x8086` AND `DedicatedVideoMemory <= 256 MB` — Intel iGPUs share system memory and have no meaningful "VRAM total". |
| Vendor reference | `learn.microsoft.com/en-us/windows/win32/api/dxgi1_4/nf-dxgi1_4-idxgiadapter3-queryvideomemoryinfo` — `Budget` and `CurrentUsage` are bytes. |
| Status | ✅ Bytes→MB conversion correct. Total-vs-budget distinction handled correctly. iGPU edge case handled correctly. **No nonsense-number risk** — the conversion IS applied. |

---

## 5. Battery

### What we show
- Charge percent (`87 %`)
- Charging vs discharging state
- Discharge watts when on battery (`12.3 W`)
- Charge watts when on AC (`+18.0 W` or `AC` if unknown)
- Estimated time remaining (when reported)

### How

| Aspect | Detail |
|---|---|
| API | `powrprof!CallNtPowerInformation(SystemBatteryStateLevel = 5)` |
| File:line | `BatteryCollector.cs:70-76` (P/Invoke), `:20-25` (call), `:27-29` (failure handling), `:40-47` (sign handling) |
| Struct | `SYSTEM_BATTERY_STATE` |
| Raw fields | `Charging` (bool), `BatteryPresent` (bool), `Capacity` (mWh), `FullChargedCapacity` (mWh), `Rate` (signed mW; **negative = discharging**, **positive = charging**), `EstimatedTime` (seconds, `0xFFFFFFFF` = unknown) |
| Sign handling | `BatteryCollector.cs:40-47`: |
|   |   `if (Rate < 0)  DischargeWatts = Rate / -1000.0;`  *(negate negative mW → positive watts)* |
|   |   `else if (Rate > 0)  ChargeWatts = Rate / 1000.0;`  *(positive mW → watts)* |
| Time conversion | `BatteryCollector.cs:49-51`: `EstimatedTime` (seconds) → `EstimatedMinutesRemaining = secs / 60`. Unknown sentinel `0xFFFFFFFF` is handled — we don't divide it. |
| Failure handling | `BatteryCollector.cs:27-29`: if call fails OR `!BatteryPresent`, return early **without** setting any battery fields. **No fake battery state on a desktop with no battery.** |
| Display (HUD) | `MetricFormatterCompact.cs:144-182` and `MeasurementTraceFactory.FormatBatteryValue:141-153` — formats as `"NN% +X.XW"` (charging), `"NN% AC"` (charging, unknown rate), `"NN% X.XW"` (discharging), or `"NN%"` (no rate info) |
| Vendor reference | `learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-system_battery_state` — confirms `Rate` is signed milliwatts (positive = charging) and `EstimatedTime` is seconds with `0xFFFFFFFF` sentinel for unknown. |
| Status | ⚠️ Math correct, sign handling correct, failure handling correct, no fabrication. Label `"CallNtPowerInformation"` matches reality. |

### Battery time-remaining (how we should compute it when Windows doesn't tell us)

Windows reports `EstimatedTime` only when its own heuristic is confident. On many handhelds, especially right after unplugging, the field returns `0xFFFFFFFF` (unknown) for several minutes. We currently surface that as "no estimate" rather than fake one.

A more aggressive (but still honest) approach we could add later:
```
remainingMinutes = (currentCapacityWattHours / dischargeWatts) * 60
```
Where `currentCapacityWattHours = Capacity_mWh / 1000`. This would give a real-time estimate based on the **current** discharge rate, refreshed every tick. The estimate would be jittery (any spike in CPU/GPU draw shifts it), but it would never be unknown when we have a non-zero discharge reading.

This is **not** implemented today and **must not** be implemented as an inline literal in the formatter — it should be a `BatteryEstimator` class that takes the snapshot, computes the value, and writes it to a clearly-named field like `Battery.EstimatedMinutesRemainingFromRate` so the user can tell which source it came from.

---

## 6. Display

| Aspect | Detail |
|---|---|
| What we show | Refresh rate in Hz (e.g. `120 Hz`) |
| API | `user32!EnumDisplaySettings(NULL, ENUM_CURRENT_SETTINGS, &dm)` |
| File:line | `DisplayCollector.cs:35-40` (P/Invoke), `:22` (call), `:28` (assignment) |
| Raw field | `DEVMODE.dmDisplayFrequency` (DWORD, Hz) |
| Conversion | none (direct integer Hz) |
| Display | `MetricFormatterCompact.cs:95-96`: prints `dmDisplayFrequency` raw |
| Vendor reference | `learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-devmodea` — `dmDisplayFrequency` is integer Hz, **but values of `0` or `1` mean "default refresh, hardware decides" and are NOT real Hz values**. |
| Status | ❌ **Active bug**: HUD and widget print `0` or `1` literally on default-refresh systems. Trace masks them via `> 0 ? format : "--"` (`MeasurementTraceFactory.cs:60-62`), so the same snapshot displays `"--"` in the diagnostic dump and `"1"` in the HUD on the same machine. **Fix**: change HUD path to `snapshot.Display.RefreshRateHertz > 1 ? $"{x:0}Hz" : "--"` and same in widget. Use `> 1` (not `> 0`) because `1` is also a sentinel per the docs. |

### What we do **not** show
- VRR supported / VRR active: fields exist on `DisplayMetrics` but no collector populates them. They are always `false`. Per `CLAUDE.md` rules they should not be surfaced until a real source exists. They are correctly hidden from the HUD.

---

## 7. Power roll-up

This is the section the user specifically asked for.

### CPU watts
- **Source**: Windows EMI driver (`IOCTL_EMI_GET_MEASUREMENT`) — see [§2.2](#22-cpu-power-watts).
- **Vendor mapping**: EMI is vendor-agnostic. Underneath, the OS reads Intel RAPL `MSR_PKG_ENERGY_STATUS = 0x611` or AMD RAPL `MSR_AMD_RAPL_PKG_ENERGY_STATUS = 0xC001029B`. We do not need to write per-vendor code.
- **Field**: `snapshot.Cpu.PowerWatts` (`double`, watts)
- **Display**: `{value:0.0}W` or `--` when unavailable
- **Truth status**: code path correct, hardware sign-off pending

### GPU watts
- **Source by vendor** (whichever native collector initializes; only one per machine):
  - Intel: `IGCL ctlPowerTelemetryGet` → energy/time delta integration in `NativeTelemetry.cpp:688-711`
  - AMD: `ADLX IADLXGPUMetrics::GPUPower` directly (`NativeTelemetry.cpp:483-491`)
  - NVIDIA: `NVML nvmlDeviceGetPowerUsage` → `mW / 1000` (`NvApiGpuCollector.cs:147`)
  - D3DKMT fallback: **explicitly refused** (it returns a TDP ratio, not watts)
- **Field**: `snapshot.Gpu.PowerWatts` (`double`, watts)
- **Display**: `{value:0.0}W` or `--` when unavailable
- **Truth status**: code paths correct against vendor contracts (vendored from official Intel and AMD SDK headers, retrieved 2026-04-09; NVML and NvAPI verified by P/Invoke signatures and the `NvAPIWrapper.Net` package). Hardware sign-off pending for Intel/AMD on real handhelds.

### Total system power

**Current truth after the 2026-04-10 final push:** Device Power / `total_power` is battery discharge watts only when unplugged. On AC it returns `--` unless Windows/OEM exposes a real whole-device/platform watt meter. RAPL `PKG`, `PP1`, and `DRAM` are kept as component rail diagnostics; `PKG + DRAM` is not shown as total/device power because it excludes display panel, SSD, radios, fans, and platform conversion losses. The old CPU+GPU/component-sum description below is retained as audit history, not the current product behavior.

- **File**: `src/HHAPulse.Overlay/Diagnostics/SystemPowerValidator.cs` (validator) + `MeasurementTraceFactory.SystemPowerTrace:103-114`
- **Two valid sources, in order of preference**:
  1. **Battery discharge watts** (when on battery and `Battery.DischargeWatts > 0`): the wall measurement of total system draw. Most accurate because the battery's BMS measures it directly.
  2. **CPU + GPU sum** (when validated component data exists): `Cpu.PowerWatts + Gpu.PowerWatts`. Correct lower bound, but does NOT include display, fans, USB peripherals, the SSD, etc., so it under-reports.
- **Display string** (`MeasurementTraceFactory.cs:107-111`):
  - Battery validated → `"{discharge:0.0}W (battery discharge)"`
  - Component-sum validated → `"{cpu+gpu:0.0}W (cpu+gpu sum)"`
  - Neither → `"--"`
- **What we do NOT do**: invent a "total" by adding TDP estimates, by scaling GPU usage %, or by any other heuristic. We only report what we actually measured.
- **Status**: validator logic is correct. The user-visible label `(battery discharge)` vs `(cpu+gpu sum)` is critical — it tells the user which source they're looking at.

### Battery time remaining
- **Primary source**: `SYSTEM_BATTERY_STATE.EstimatedTime` (seconds) → minutes via `/ 60`. This is Windows' own estimate, refined by the OS using a longer time window.
- **When unknown**: Windows returns `0xFFFFFFFF`, which we currently surface as "no estimate."
- **Future improvement (NOT implemented today)**: live `(currentCapacityWattHours / dischargeWatts) × 60` minutes, recomputed every tick. Jittery but always available. See [§5](#5-battery).

### How each vendor "officially" measures power

| Vendor | Where the watts come from | Spec reference |
|---|---|---|
| Intel CPU | RAPL MSR `0x611` (`MSR_PKG_ENERGY_STATUS`) → energy in 15.3 µJ units. The IPM driver exposes this as picowatt-hours via EMI. | Intel SDM Vol. 3B §15.10.3 |
| AMD CPU | RAPL MSR `0xC001029B` → similar energy counter. AMD's platform driver exposes via EMI. | AMD APM Vol. 2 §17.5 |
| Intel GPU | IGCL `gpuEnergyCounter` (joules) + `timeStamp` (seconds) → integration. | `IgclTelemetryContract.h:42-59` (vendored from Intel official `igcl_api.h`) |
| AMD GPU | ADLX `IADLXGPUMetrics::GPUPower` returns watts directly (the AMD driver does the integration internally). | `AdlxTelemetryContract.h:214` (vendored from AMD official ADLX SDK 1.4.0.110) |
| NVIDIA GPU | NVML `nvmlDeviceGetPowerUsage` returns milliwatts directly (the NVIDIA driver does the integration internally). | NVIDIA NVML API reference |

We honor each of these contracts. The IGCL path is the only one where we have to do the integration math ourselves; the AMD and NVIDIA SDKs do it for us.

---

## 8. Storage

### What we show
`storage_temp` (SSD temperature in °C) and `storage_wear` (percent-used wear from Windows storage reliability counters).

### Pipeline
```
Primary/system drive
  -> overlay StorageTempCollector
  -> IOCTL_STORAGE_GET_DEVICE_NUMBER (map system volume to physical drive)
  -> IOCTL_STORAGE_QUERY_PROPERTY(StorageDeviceTemperatureProperty)
  -> STORAGE_TEMPERATURE_DATA_DESCRIPTOR
  -> snapshot.Storage.TemperatureCelsius
  -> MetricFlags.StorageTemperature
  -> HUD/Manual via storage_temp

MSFT_StorageReliabilityCounter
  -> capture-service CaptureServiceSensorCollector
  -> CaptureFrameMetrics.StorageWear*
  -> overlay CaptureServiceCollector
  -> snapshot.Storage.WearPercentUsed / PowerOnHours / DeviceModel
  -> MetricFlags.StorageWear
  -> HUD/Manual via storage_wear
```

### How

| Metric | Source API | File:Line | Conversion | Unit at HUD |
|---|---|---|---|---|
| SSD temperature | `IOCTL_STORAGE_QUERY_PROPERTY` + `StorageDeviceTemperatureProperty` | `StorageTempCollector.cs:16-18`, `:31-76`, `:123-145` | signed `short` Celsius from `STORAGE_TEMPERATURE_INFO.Temperature` | `°C` |
| SSD wear | `MSFT_StorageReliabilityCounter.Wear` via elevated capture service | `CaptureServiceSensorCollector.cs:10-12`, `:209-245`; overlay ingest at `CaptureServiceCollector.cs:244-276` | direct percent-used value | `% used` |
| SSD model / power-on hours | `MSFT_PhysicalDisk.FriendlyName`, `PowerOnHours` | `CaptureServiceSensorCollector.cs:236-239` | none | diagnostics/detail only |

### Contract/storage-model wiring

- `TelemetrySnapshot` now carries `StorageMetrics` at `TelemetrySnapshot.cs:41-48` and defines `TemperatureCelsius`, `WearPercentUsed`, `PowerOnHours`, `DeviceModel`, and `Reliability` at `:198-218`.
- `MetricFlags.StorageTemperature` / `MetricFlags.StorageWear` were added at `MetricFlags.cs:27-30`.
- The Manual metric catalog exposes `storage_temp` / `storage_wear` at `OverlayPresetCatalog.cs:33-34` and `:95-100`.
- Compact HUD formatting is `47°C` / `6%` via `MetricFormatterCompact.cs:97-103` and `:151-160`.

### What we do **not** show

- SSD throughput, IOPS, queue depth, or SMART error-count walls. The current pass is thermal/reliability only.
- Fake wear estimates derived from age, TBW guesses, or vendor utilities.

### Status

- ✅ `storage_temp` is a real overlay-side user-mode path with no service dependency.
- ✅ `storage_wear` is a real capture-service path and preserves valid `0%` wear readings (`CaptureServiceSensorCollector.cs:233-239`, `CaptureServiceCollector.cs:244-276`).
- 🔬 Store/MSIX validation is still pending for the IOCTL path in a packaged build. We have chosen the capture service for `MSFT_StorageReliabilityCounter` because AppContainer compatibility is not yet trusted.

---

## 9. CPU clock

### What we show
One aggregate CPU clock value in the HUD/Manual picker, plus per-core nominal frequency detail in the snapshot for diagnostics.

### Pipeline
```
PDH \Processor Information(_Total)\Processor Frequency
  -> CpuClockCollector
  -> snapshot.Cpu.ClockMegahertz / snapshot.CpuDetail.AggregateEffectiveMhz
  -> MetricFlags.CpuClock
  -> MetricFormatterCompact "~3.21GHz"

CallNtPowerInformation(ProcessorInformation)
  -> PROCESSOR_POWER_INFORMATION[]
  -> snapshot.CpuDetail.PerCoreNominal
  -> ClockStatusMessage explains nominal-vs-live limitation
```

### How

| Metric | Source API | File:Line | Conversion | Unit at HUD |
|---|---|---|---|---|
| Aggregate CPU clock | PDH `\Processor Information(_Total)\Processor Frequency` | `CpuClockCollector.cs:10-13`, `:23-41`, `:99-140` | PDH reports MHz directly | `~GHz` / `~MHz` |
| Per-core nominal CPU clock | `CallNtPowerInformation(ProcessorInformation)` | `CpuClockCollector.cs:143-181`, parse helper at `:65-97` | direct `CurrentMhz` / `MaxMhz` / `MhzLimit` fields | diagnostics/detail only |

### Contract wiring

- `TelemetrySnapshot.CpuDetail` was added at `TelemetrySnapshot.cs:44-48`, with `AggregateEffectiveMhz`, `PerCoreNominal`, and `ClockStatusMessage` defined at `:220-247`.
- `MetricFlags.CpuClock` was added at `MetricFlags.cs:27-30`.
- Manual selection uses `cpu_clock` from `OverlayPresetCatalog.cs:17` and `:84-88`.
- The compact formatter deliberately prefixes the value with `~` at `MetricFormatterCompact.cs:138-148` to mark it as approximate/nominal rather than live turbo-accurate.

### Truth rule

If `PROCESSOR_POWER_INFORMATION.CurrentMhz == MaxMhz` for every core, we record the Windows limitation explicitly in `snapshot.CpuDetail.ClockStatusMessage` (`CpuClockCollector.cs:164-172`). We do **not** pretend this is live throttle-aware per-core telemetry.

### Status

- ✅ Aggregate CPU clock is real and HUD-selectable.
- ✅ Per-core values are captured as detail only, not exploded into HUD chips.
- 🔬 Packaged-build validation is still required for PDH and `CallNtPowerInformation`.

---

## 10. NPU detection

### What we show
NPU presence, vendor, and adapter/driver description in diagnostics/detail data only. No utilization number ships in this pass.

### Pipeline
```
DXCore
  -> DXCoreCreateAdapterFactory
  -> CreateAdapterList(NPU hardware-type attribute)
  -> GetProperty(DriverDescription)
  -> classify vendor from description
  -> snapshot.Npu.* and MetricFlags.NpuPresent
```

### How

| Metric | Source API | File:Line | Conversion | Unit at HUD |
|---|---|---|---|---|
| NPU present/vendor/adapter | DXCore adapter enumeration | `NpuDetectionCollector.cs:9-12`, `:31-67`, `:97-180` | string classification (`Intel AI Boost`, `AMD Ryzen AI`, `Qualcomm Hexagon NPU`, or other) | not shown in HUD |

### Contract wiring

- `TelemetrySnapshot.Npu` now exists at `TelemetrySnapshot.cs:47-48` and `:249-260`.
- `MetricFlags.NpuPresent` was added at `MetricFlags.cs:27-30`.
- The collector records an explicit `npu_present` provenance trace at `NpuDetectionCollector.cs:49-65`.

### What we do **not** show

- NPU utilization, temperature, power, or clock. No public proved source has been accepted into the product for those fields as of 2026-04-15.
- A HUD chip for NPU presence. This is diagnostics/detail data only in Plan A.

### Status

- ✅ Detection is real and proof-gated through DXCore.
- ✅ Vendor classification is explicit and conservative (`NpuDetectionCollector.cs:70-95`).
- 🔬 Store/MSIX packaged validation is still required for DXCore enumeration on the final Store-targeted build.

---

## 11. Structural fragility

> *"Anything hardcoded is a red flag — that's not real data, that's fabricated."* — user, 2026-04-10

The trace infrastructure has a fundamental weakness: **every `Source` and `Collector` label that ends up in the diagnostic dump is a hardcoded string literal that a developer typed.** There is no code anywhere in the repo that ties an `IMetricCollector` instance to the label that gets printed.

### Evidence

- `MetricStatusFactory.Create(TelemetrySnapshot snapshot)` at `MetricStatusFactory.cs:8` takes only the snapshot. **It does not have access to the collector list.** It cannot ask "who populated this metric?".
- Every `Source` argument at `MetricStatusFactory.cs:14-32` is a string literal: `"GetSystemTimes"`, `"PDH GPU Engine"`, `"EMI energy deltas"`, etc.
- Every `Collector` argument in `MeasurementTraceFactory.BuildTrace` (`MeasurementTraceFactory.cs:32-77`) is either a string literal (`"BatteryCollector"`, `"CpuUsageCollector"`, etc.) or reverse-derived from a source string via `CollectorForGpuSource` (`:128-139`).
- The semantics strings in `GpuPowerTrace` (`MeasurementTraceFactory.cs:90-95`):
  ```csharp
  "IGCL" => "IGCL path computes watts from gpuEnergyCounter/timeStamp delta.",
  "NVML" => "NVML path uses nvmlDeviceGetPowerUsage milliwatts / 1000.",
  "ADLX" => "ADLX path uses GPUPower from AMD metrics.",
  ```
  These are **decorative claims**. They happen to be accurate today (we verified each against the actual code), but if the native side switches to reading `gpuPower` directly instead of integrating energy, the trace text will silently lie until someone updates the literal.

### Why it matters

Today the trace is honest because every developer who has touched these factories has manually kept the labels in sync with the collector code. **That discipline is the only thing standing between the user and a fabricated trace.** Once it slips, the trace becomes a lie that no test catches.

### What an honest design looks like

Sketch (not code to apply blindly):

1. Add to `IMetricCollector`:
   ```csharp
   public interface IMetricCollector {
       string Name { get; }
       string SourceLabel { get; }     // e.g. "GetSystemTimes"
       string ApiContract { get; }     // e.g. "kernel32!GetSystemTimes (kernel/user/idle delta)"
       IReadOnlyList<string> MetricsITouched { get; }   // metric IDs this collector might populate
       // ... existing members
   }
   ```
2. Each collector keeps its labels next to its P/Invoke. Refactoring the P/Invoke is now coupled to updating the label — the build won't catch it but the file is one screen away.
3. `CollectorOrchestrator.CollectAsync` records, per metric ID, **which collector instance last successfully wrote to the snapshot**. This is a per-tick `Dictionary<string, IMetricCollector>`.
4. `MetricStatusFactory` and `MeasurementTraceFactory` consume that dictionary instead of hardcoded literals. If a metric has no recorded owner, the trace says `Source: <unknown>` instead of pretending.
5. Per-vendor source fields on `DependencyState` instead of single `GpuPowerSource`/`GpuTemperatureSource`/etc. — this also resolves the GPU last-write-wins race ([Bug 4](#9-active-bugs)) where multiple vendor SDKs would clobber each other's labels.

This is the only structural change that eliminates the fragility. Until it lands, every other label fix is a patch.

---

## 12. Historical audit bugs now addressed by the 2026-04-10 fix pass

In the original priority order. See `Section A` of the audit report for the full WHERE / WHAT / WHY / PROOF / HOW for each. These entries are retained as audit history; see the current implementation delta at the top of this file for the applied fixes.

| # | Bug | Where | Effort |
|---|---|---|---|
| 1 | **`InputLatency` shows `GpuBusyMilliseconds`** — metric label is a lie. Per CLAUDE.md, latency should be hidden until a real source exists. | `MetricFormatterCompact.cs:39-42`, `MeasurementTraceFactory.cs:38` | 30 min: delete from preset catalog and formatter switch |
| 2 | **Refresh rate `0`/`1` shown literally** in HUD/widget on default-refresh displays, while the trace correctly masks. | `MetricFormatterCompact.cs:95-96`, `CompactWidget.xaml.cs:94` | 15 min: add `> 1 ? format : "--"` mask |
| 3 | **`AppFps` / `PresentFps` / `DisplayFps` are fabricated** — set equal to `fps` or hardcoded `0`. | `EtwFrameCapture.cs:145-147`, `TelemetrySnapshot.cs:64-71` | 1 hr to delete the fields, days to compute them honestly from PresentMon evidence |
| 4 | **Hardcoded source/collector labels with no runtime binding** (structural). | `MetricStatusFactory.cs:14-32`, `MeasurementTraceFactory.cs:32-139` | 1-2 days to add `IMetricCollector.SourceLabel` + orchestrator provenance map |
| 5 | **"Boundary traces" in diagnostic dump are static snapshot reads, not real probes**. | `ControlWindow.xaml.cs:478-482` | 5 min: rename to "Last observed boundary state". Or: days to add real ping/pong probes. |
| 6 | **`IsDisputed` badge hides successful IGCL/ADLX reads** by tagging anything ADLX/IGCL as "Pending hardware proof" even when data flowed. | `ControlWindow.xaml.cs:441-462` | 15 min: split into "Hardware-validation-pending" vs "Unavailable" |
| 7 | **GPU vendor source fields are last-write-wins** in `CollectorOrchestrator` — multiple vendor collectors writing to the same field clobber each other. | `CollectorOrchestrator.cs:51-71` + the four GPU collectors | 1 day, naturally resolved by the bug 4 fix |

Bugs 1, 2, 3, 5, 6 are quick wins. Bug 4 is the structural fix that prevents this whole class of fabrication going forward. Bug 7 dissolves automatically once Bug 4 is in place.

---

## Historical pre-fix trust snapshot

The table below is retained from the audit that triggered the 2026-04-10 telemetry truth fix. It is not the current runtime state; use the current implementation delta at the top of this file for the active truth model.

| Metric | Trustworthy? | Notes |
|---|---|---|
| FPS, Avg, 1% low, 0.1% low, frametime | ✅ | math correct, ETW source verified |
| Frame generation detection | ✅ | parses real Intel-PresentMon ETW payload |
| CPU usage % | ⚠️ | math correct; label hardcoded |
| CPU power (watts) | ⚠️🔬 | EMI math correct; hardware sign-off pending |
| GPU usage % | ⚠️ | PDH counter correct; multi-instance summing correct |
| GPU temperature | ⚠️🔬 | per-vendor SDK conformant; hardware sign-off pending |
| GPU clock | ⚠️🔬 | per-vendor SDK conformant; NvAPI kHz→MHz conversion correct |
| GPU power (Intel IGCL) | ⚠️🔬 | joules/seconds integration matches Intel contract; hardware sign-off pending |
| GPU power (AMD ADLX) | ⚠️🔬 | direct watts via `GPUPower`; hardware sign-off pending |
| GPU power (NVIDIA NVML) | ⚠️ | `mW / 1000` conversion correct |
| GPU fan RPM | ⚠️🔬 | per-vendor SDK conformant; hardware sign-off pending |
| RAM | ⚠️ | bytes→MB correct; minor display unit inconsistency |
| VRAM | ✅ | bytes→MB correct; dedicated-vs-budget logic correct; iGPU edge case handled |
| Battery percent | ⚠️ | `CallNtPowerInformation` direct |
| Battery discharge watts | ⚠️ | sign handling correct (mW→W with negation) |
| Battery time remaining | ⚠️ | uses Windows estimate when available; correctly returns "unknown" when not |
| Display refresh rate | ❌ | shows literal `0`/`1` for hardware-default mode in HUD/widget |
| `InputLatency` | ❌ | label lie — shows GPU busy time |
| `AppFps` / `PresentFps` / `DisplayFps` | ❌ | fabricated — collapsed to one value or zero |
| "Boundary traces" diagnostic | ❌ | label oversells static reads as live probes |
| Trace `Source`/`Collector` labels | ⚠️ | hardcoded with zero runtime binding to collectors (structural) |

---

**Maintainer note**: this document is a snapshot of branch `HHAP-0.3` as of 2026-04-10. When any collector, formatter, factory, or display path changes, **this file must be updated in the same commit** — otherwise it will rot the same way the hardcoded labels do.
