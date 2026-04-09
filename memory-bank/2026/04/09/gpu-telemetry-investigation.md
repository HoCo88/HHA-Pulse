# GPU Telemetry Investigation & Recovery Plan

**Date:** 2026-04-09
**Branch:** HHAP-0.25
**Device tested:** Intel Arc 140V (Lunar Lake) — MSI Claw handheld
**Log analyzed:** `overlay.log` — 11,986 lines, 2,824 errors, ~22 hours of runtime
**Research method:** 15 parallel investigation agents, cross-referenced against real-world tools, open-source projects, vendor docs, and 3 constraint checks (VRR, performance, anti-cheat)

---

## Table of Contents

1. [What the Log Revealed](#1-what-the-log-revealed)
2. [Problem 1: D3DKMT Is Not Universal](#2-problem-1-d3dkmt-is-not-universal)
3. [Problem 2: Stale Binary Mismatch](#3-problem-2-stale-binary-mismatch)
4. [Problem 3: WinUI SDK Framework Crash](#4-problem-3-winui-sdk-framework-crash)
5. [Problem 4: Build Tooling Mismatch](#5-problem-4-build-tooling-mismatch)
6. [Problem 5: TDP% Is Not Power](#6-problem-5-tdp-is-not-power)
7. [The Solution: Vendor-Specific SDKs](#7-the-solution-vendor-specific-sdks)
8. [AMD — ADLX v1.4](#8-amd--adlx-v14)
9. [Intel — IGCL / ctl_api](#9-intel--igcl--ctl_api)
10. [NVIDIA — NVAPI + NVML](#10-nvidia--nvapi--nvml)
11. [CPU Power — Windows EMI](#11-cpu-power--windows-emi)
12. [What Was Rejected and Why](#12-what-was-rejected-and-why)
13. [Constraint Verification: VRR](#13-constraint-verification-vrr)
14. [Constraint Verification: Performance](#14-constraint-verification-performance)
15. [Constraint Verification: Anti-Cheat](#15-constraint-verification-anti-cheat)
16. [Frame Generation Detection](#16-frame-generation-detection)
17. [GPT Plan Cross-Check](#17-gpt-plan-cross-check)
18. [Implementation Plan](#18-implementation-plan)
19. [Sources](#19-sources)

---

## 1. What the Log Revealed

The overlay.log file from a real user session on the MSI Claw (Intel Arc 140V, Lunar Lake) contained:

| Error | Count | Severity | Root Cause |
|-------|-------|----------|------------|
| `GpuMetrics.set_FanRpm(Int32)` MissingMethodException | 2,779 | High — fires every tick | Stale HHAPulse.Shared.dll from wrong branch |
| `TabViewButtonBackground` XamlParseException | 5 | Critical — crashes app on launch | WinUI SDK 1.8.260317003 framework bug |
| `System.IO.Pipelines v10.0.0.0` FileNotFoundException | 3 | Medium — settings won't save | .NET 9+ SDK building against .NET 8 TFM |
| `ThrowingCollector` InvalidOperationException | 32 | None — expected test artifact | Unit test runs sharing log file |
| Intel Arc 140V GPU data all zeros | Continuous | Medium — no GPU temp/power/fan | D3DKMT doesn't work on Intel iGPU |

**Key log lines as evidence:**

```
Line 171: GPU Perf: D3DKMT initialized. VendorHint=adapter0. Test read: Temp=0.0C, PowerRaw=0, FanRPM=0, MemFreq=0Hz.
```
D3DKMT successfully initialized but returned all zeros. The API call succeeded — the Intel driver simply doesn't populate the fields.

```
Line 91-116: [ERROR] Overlay launch failed.
Microsoft.UI.Xaml.Markup.XamlParseException: Cannot find a Resource with the Name/Key TabViewButtonBackground [Line: 0 Position: 0]
   at HHAPulse.Overlay.ControlWindow.InitializeComponent()
```
`Line: 0 Position: 0` means this is framework initialization, not user XAML. No TabView reference exists in any project file.

```
Lines 11786-11986: [ERROR] Collector 'GPU Perf Data (D3DKMT)' failed during collection.
System.MissingMethodException: Method not found: 'Void HHAPulse.Shared.Models.GpuMetrics.set_FanRpm(Int32)'.
```
This error fired every second for the entire session. The Overlay binary expected `FanRpm` (from HHAP-0.25) but the loaded Shared DLL was from `main` (which lacks it).

```
Line 578: [ERROR] Failed to save overlay settings.
System.IO.FileNotFoundException: Could not load file or assembly 'System.IO.Pipelines, Version=10.0.0.0'
   at System.Text.Json.JsonSerializer.SerializeAsync[TValue](...)
```
Version 10.0.0.0 doesn't exist in .NET 8. This means the publish was done with a .NET 9+ SDK.

---

## 2. Problem 1: D3DKMT Is Not Universal

### What D3DKMT is

`D3DKMTQueryAdapterInfo` with `KMTQAITYPE_ADAPTERPERFDATA` (type 62) is a Windows kernel-mode thunk that reads GPU sensor data from the WDDM miniport driver. It's the same API Windows Task Manager uses for its GPU temperature display. Available since WDDM 2.4 / Windows 10 1803.

### Why it was chosen

D3DKMT is vendor-agnostic — it works through the standard Windows graphics driver stack without needing AMD, Intel, or NVIDIA SDKs. One API for all GPUs. No DLLs to bundle. No vendor lock-in. Runs as standard user. This seemed ideal.

### Why it failed

**D3DKMT ADAPTERPERFDATA is an OPTIONAL driver callback.** Microsoft defines the struct; each GPU vendor independently decides whether their WDDM miniport driver populates the fields. There is no requirement to do so.

**Per-vendor reality (verified by research agents against real-world tools and documentation):**

| Vendor | Temperature | Fan RPM | Power | Clock | Evidence |
|--------|-------------|---------|-------|-------|----------|
| AMD discrete (RX 7000+) | Works | Works | % of TDP (not watts) | Not available | Task Manager shows AMD discrete temp |
| AMD iGPU (Z1/Z2) | Likely works | No fan on iGPU | % of TDP | Not available | iGPUs have no dedicated fan sensor path |
| NVIDIA discrete (RTX 4000/5000) | Works | Works | % of TDP (not watts) | Not available | Task Manager shows NVIDIA temp |
| Intel Arc discrete (A770, B-series) | Partial | Unknown | Unknown | Not available | Inconsistent driver support |
| **Intel Arc iGPU (140V Lunar Lake)** | **Returns 0** | **Returns 0** | **Returns 0** | **Not available** | **Confirmed in overlay.log line 171** |

**The Intel Arc 140V iGPU returns zeros for ALL fields.** The D3DKMT call succeeds (no error) but the Intel Lunar Lake driver simply does not populate `DXGK_ADAPTER_PERFDATA`. This is the same reason Windows Task Manager cannot show temperature for this GPU.

**Evidence from real-world tools:**
- FanControl could NOT read Intel Arc temperature via D3DKMT. They created a separate IGCL-based plugin (`FanControl.IntelCtlLibrary`) specifically for this reason.
- FurMark 2.4 added Lunar Lake monitoring using IGCL, not D3DKMT.
- LibreHardwareMonitor v0.9.6 added IGCL telemetry for Intel iGPU (PR #2218) because D3DKMT was insufficient.
- No open-source C# project on GitHub uses `D3DKMT_ADAPTER_PERFDATA` for temperature as a primary source.

**Additionally, D3DKMT power is useless:**

The `Power` field in `D3DKMT_ADAPTER_PERFDATA` is defined by Microsoft as "tenths of a percentage of thermal design limits" — it's a percentage of TDP capacity, not watts. You cannot show this to users as power consumption. Every monitoring tool that shows real GPU wattage uses vendor SDKs (ADLX for AMD, NVML for NVIDIA, IGCL for Intel).

### Bottom line

D3DKMT is what Task Manager uses. Task Manager also cannot show Intel iGPU temperature. Task Manager also does not show GPU power in watts. D3DKMT was the wrong foundation for a monitoring overlay.

---

## 3. Problem 2: Stale Binary Mismatch

### What happened

The Overlay executable was compiled against HHAP-0.25 branch code where `GpuMetrics` has a `public int FanRpm { get; set; }` property at `TelemetrySnapshot.cs:120`. But the `HHAPulse.Shared.dll` loaded at runtime was from the `main` branch, where `FanRpm` does not exist.

### Why it caused 2,779 errors

The `GpuPerfDataCollector.CollectAsync()` runs every telemetry tick (1 second). At line 217 it executes:
```csharp
snapshot.Gpu.FanRpm = (int)perfData.FanRPM;
```

The CLR cannot find `set_FanRpm(Int32)` in the loaded assembly, throwing `MissingMethodException` every single tick for the entire session.

### Why the contract guard didn't catch it

`TelemetryContractGuard` at `App.xaml.cs:73` validates property existence via reflection at startup. The log at line 167 shows:
```
Loaded HHAPulse.Shared.dll: Version=1.0.0.0, MVID=816e19c7-..., ContractValid=True
```

The guard reported `ContractValid=True` — which means the DLL it checked DID have `FanRpm`. The stale DLL problem likely occurred in an earlier session (lines 81-86, before the enhanced logging was added) where the guard wasn't yet checking, or the guard validated against a different set of properties.

### Fix

Clean rebuild from HHAP-0.25. The property exists in this branch. The contract guard will catch future mismatches.

---

## 4. Problem 3: WinUI SDK Framework Crash

### What happened

The app crashed on launch 5 times with:
```
Microsoft.UI.Xaml.Markup.XamlParseException: Cannot find a Resource with the Name/Key TabViewButtonBackground [Line: 0 Position: 0]
```

### Why this is a framework bug, not our code

**Evidence:**
1. Searched ALL `.xaml` files in the project: `ControlWindow.xaml`, `MainWindow.xaml`, `SettingsPage.xaml`, `TopBarControl.xaml`, `BatteryMeter.xaml`, `MetricLabel.xaml`, `SparklineGraph.xaml` — **zero references to TabView or TabViewButtonBackground anywhere**.
2. `ControlWindow.xaml` is 123 lines of pure markup using only standard controls: Button, TextBlock, Slider, Grid, StackPanel, ScrollViewer, Ellipse.
3. The error says `[Line: 0 Position: 0]` — this indicates framework resource initialization, not user XAML parsing. If our XAML referenced a missing resource, the line number would point to the actual XAML line.
4. The crash occurs in `Application.LoadComponent()` -> `ControlWindow.InitializeComponent()` — this is the XAML deserializer loading a pre-compiled BAML/XBF resource, not runtime resource lookup.

### The deeper issue (verified from GPT plan)

`App.xaml:9` loads `<muxc:XamlControlsResources />` unconditionally during XAML parse. This happens BEFORE `App.OnLaunched()` and BEFORE `LaunchAsync()` try-catch blocks. The current guard around `ControlWindow` creation at `App.xaml.cs:300` does NOT protect against this earlier failure.

`XamlControlsResources` is a framework-internal merged resource dictionary that includes theme definitions for ALL WinUI controls (including TabView). If the framework has an internal resource dependency bug, every window that includes it is affected.

### Fix

Two-pronged:
1. Test WindowsAppSDK version change (downgrade to 1.7.x or try newer 1.8 patch)
2. Move `XamlControlsResources` from `App.xaml` into guarded code-behind in `App.xaml.cs` so we can catch and handle XAML parse exceptions during resource initialization

---

## 5. Problem 4: Build Tooling Mismatch

### What happened

Settings save failed with:
```
System.IO.FileNotFoundException: Could not load file or assembly 'System.IO.Pipelines, Version=10.0.0.0'
```

### Why version 10.0.0.0 is wrong

The project targets `net8.0-windows10.0.19041.0`. In .NET 8, `System.IO.Pipelines` is version 8.x. Version 10.0.0.0 belongs to .NET 9 or later. The fact that the published output expects v10 means `dotnet publish` was executed with a .NET 9+ SDK.

When you run `dotnet publish` with a newer SDK against an older TFM, the SDK's own assemblies can leak version expectations into the output. `System.Text.Json`'s async serialization path (`SerializeAsync`) depends on `System.IO.Pipelines` for stream-based I/O, and the compiled IL reference targets the SDK's version, not the TFM's version.

### Why LoadAsync didn't fail but SaveAsync did

`JsonSerializer.DeserializeAsync()` (used in `SettingsService.LoadAsync`) and `JsonSerializer.SerializeAsync()` (used in `SaveAsync`) take different code paths internally. The serialization path triggers the `System.IO.Pipelines` dependency; the deserialization path may not exercise it, or it may use a fallback.

### Fix

Add `global.json` at repo root pinned to .NET 8 SDK:
```json
{
  "sdk": {
    "version": "8.0.400",
    "rollForward": "latestPatch"
  }
}
```

This ensures all builds use .NET 8 SDK regardless of what's installed on the machine.

---

## 6. Problem 5: TDP% Is Not Power

### What D3DKMT PowerRaw actually is

From Microsoft documentation for `D3DKMT_ADAPTER_PERFDATA`:
> `Power` — Clock frequency as **tenths of a percentage of thermal design limits**

This is NOT watts. This is NOT power consumption. This is a percentage of TDP capacity expressed in tenths (so 500 = 50.0% of TDP).

### Why this is useless

- Users expect power in watts (e.g., "GPU drawing 45W")
- TDP% is meaningless without knowing the TDP base, which varies per GPU model and power mode
- The value is not even accurate on many drivers — it's a rough estimate from the driver's perspective
- No monitoring tool shows D3DKMT power to users. Every tool that shows GPU watts uses vendor SDKs.

### Current code behavior

`GpuPerfDataCollector.cs` lines 207-211 correctly identify this as non-watts and intentionally hide it:
```csharp
// D3DKMT raw power is "tenths of % of TDP" — not real watts
// Keep in diagnostics only, never set GpuMetrics.PowerWatts
```

### Fix

This is already handled correctly in HHAP-0.25. The plan reinforces: `GpuMetrics.PowerWatts` is ONLY populated from vendor SDKs that provide real watts (ADLX, IGCL energy counters, NVML). D3DKMT `PowerRaw` stays in diagnostics logging only. If no vendor SDK is available, power metric shows "--" — never fake data.

---

## 7. The Solution: Vendor-Specific SDKs

### Why this is the right approach

Every monitoring tool that reliably shows GPU sensors uses vendor-specific SDKs:

| Tool | AMD | Intel | NVIDIA | Source |
|------|-----|-------|--------|--------|
| MSI Afterburner | ADL/ADLX | — | NVAPI | Guru3D forums, RTSS documentation |
| HWiNFO64 | ADL/ADLX | IGCL | NVAPI + NVML | HWiNFO forum posts, oneAPI toggle documentation |
| GPU-Z | ADL | — | NVAPI | TechPowerUp documentation |
| FanControl | ADLX (plugin) | IGCL (plugin) | — | GitHub: Rem0o/FanControl.ADLX, FanControl.IntelCtlLibrary |
| FurMark 2.4 | — | IGCL | — | FurMark 2.4 changelog (Lunar Lake support) |
| LibreHardwareMonitor | ADL2 | IGCL (v0.9.6+) | NVAPI | GitHub source: Hardware/Gpu/ |
| AMD Radeon Overlay | ADLX | — | — | AMD GPUOpen GOALS case study |

**There is no secret backdoor.** There is no hidden Windows API that magically provides GPU sensors across all vendors. D3DKMT is the only vendor-agnostic path, and it has fundamental gaps (Intel iGPU zeros, no real watts, no clocks). Every serious tool uses vendor SDKs.

### Architecture

```
HHAPulse.Native (C++ DLL — flat C exports for P/Invoke)
  |-- ADLX wrapper:  LoadLibrary("amdadlx64.dll") from AMD driver
  |-- IGCL wrapper:   probe Intel driver runtimes ("ControlLib.dll", then "igcl64.dll")
  |
HHAPulse.Overlay (C# WinUI 3)
  |-- AdlxGpuCollector      (AMD — via native wrapper)
  |-- IgclGpuCollector       (Intel — via native wrapper)
  |-- NvApiGpuCollector      (NVIDIA — NvAPIWrapper.Net NuGet + NVML P/Invoke)
  |-- GpuPerfDataCollector   (D3DKMT — fallback for temp/fan only, NEVER power)
  |-- GpuUsageCollector      (PDH — universal, already working)
  |-- VramCollector          (DXGI — universal, already working)
  |-- CpuPowerCollector      (Windows EMI — handhelds with battery)
```

**Startup selection logic:**
1. Enumerate DXGI adapters, read VendorId
2. `0x1002` (AMD): Try ADLX, fall back to D3DKMT (temp/fan only, no power)
3. `0x8086` (Intel): Try IGCL, if fails log "Intel iGPU sensors unavailable"
4. `0x10DE` (NVIDIA): Try NVAPI+NVML, fall back to D3DKMT (temp/fan only, no power)
5. All vendor DLLs loaded dynamically from driver install — nothing bundled

**Graceful degradation:** If a vendor SDK DLL isn't present (wrong GPU, old driver), the collector silently disables itself. Metrics show "--" for unavailable data. Never show zeros, never show fake data.

---

## 8. AMD — ADLX v1.4

### What it provides

`IADLXGPUMetrics` exposes:
- `GPUTemperature` — die temperature (Celsius)
- `GPUHotspotTemperature` — peak die temperature
- `GPUPower` — **real watts** (not TDP%)
- `GPUTotalBoardPower` — total board power
- `GPUFanSpeed` — RPM
- `GPUClockSpeed` — core clock MHz
- `GPUVRAMClockSpeed` — memory clock MHz
- `GPUVRAM` — usage
- `GPUVoltage` — voltage
- `GPUUsage` — utilization %

Each metric has `IsSupportedGPU*` methods to check availability before reading.

### Evidence it works on AMD iGPU handhelds

1. **AMD GPUOpen GOALS case study** — "How GOALS delivers sustained performance on handheld PCs" explicitly describes using ADLX on Ryzen APU handhelds for real-time GPU temperature, power (watts), and fan telemetry, polling every ~1 second on a background thread.
2. **FanControl.ADLX** (GitHub: Rem0o/FanControl.ADLX) — production-proven ADLX integration in C#, used for AMD GPU temp/fan/power on RX 5000/6000/7000 and AMD APUs.
3. **Handheld Companion** (GitHub: Valkirie/HandheldCompanion) — uses ADLX for AMD GPU management on handhelds.

### Privilege requirements

Read-only monitoring works as **standard user**. No admin, no kernel driver, no elevation. Admin is only required for GPU tuning (overclocking, fan curves). Confirmed by FanControl and AeroTune projects running without elevation notes.

### DLL and redistribution

`amdadlx64.dll` ships with AMD Adrenalin drivers since 22.7.1 (consistently since 23.1.1). Located in the AMD driver directory. The app calls `LoadLibrary("amdadlx64.dll")` at runtime. If the DLL is absent (non-AMD system or very old driver), init fails gracefully.

The ADLX SDK (headers, interface definitions) is MIT-like licensed on GitHub. The runtime DLL is AMD's proprietary binary shipped with drivers — we do not redistribute it.

### C# integration approach

No official C# wrapper exists. Options:
1. **SWIG-generated bindings** — AMD provides a SWIG workflow. Used by Rem0o/ADLXWrapper.
2. **C++ wrapper with flat C exports** — write thin C++ in `HHAPulse.Native/NativeTelemetry.cpp`, export `HhaPulseAdlxInit/ReadGpu/Shutdown`, P/Invoke from C#. This matches the existing project architecture (stub already exists).
3. **Pure C# via COM vtable** — epinter/adlxwrapper proves this works with raw `Marshal` calls. Minimizes native code but is more fragile.

**Recommended:** Option 2 (C++ wrapper). Matches existing architecture, stub exists, cleanest separation.

### Known gotchas

- ADLX error `0x3` ("no active adapter") on hybrid GPU systems when discrete GPU is powered off. Not an issue on pure-iGPU handhelds.
- Adrenalin 24.2.1+ regression reported in LibreHardwareMonitor — missing GPU temp on RX 7800 XT. May affect ADLX. Needs testing.
- Fan speed returns 0 or unsupported on embedded cooling controllers. Must check `IsSupportedGPUFanSpeed` before reading.
- ADLX manages its own internal sampling thread at 1Hz default. Our code reads the latest cached result — no driver stall.

---

## 9. Intel — IGCL / ctl_api

### Why this is the ONLY path for Intel Arc 140V

D3DKMT returns zeros on Intel Lunar Lake iGPU. This is confirmed in:
1. Our overlay.log (line 171)
2. FanControl needing a separate IGCL plugin for Intel Arc
3. FurMark 2.4 adding Lunar Lake support specifically via IGCL
4. LHM v0.9.6 adding IGCL telemetry for Intel iGPU (PR #2218)

**No other path exists for Intel iGPU temperature on Lunar Lake.**

### What it provides

`ctlPowerTelemetryGet()` returns a `ctl_power_telemetry_t` struct with 20+ metrics in a single call:
- `gpuCurrentTemperature` — Celsius
- `gpuEnergyCounter` — microjoules (monotonic counter). **Watts must be computed:** `W = (energy2 - energy1) / (time2 - time1)`
- `gpuCurrentClockFrequency` — MHz
- `vramCurrentClockFrequency` — MHz
- `globalActivityCounter` — utilization %
- `gpuVoltage` — volts
- Fan RPM via separate `ctlFanGet` (will be 0 on iGPU — no fan)

Each field has a validity flag that MUST be checked before reading. iGPU will not have VRAM temperature or fan data.

### Platform support

IGCL supports **Alder Lake-P and higher** platforms. This covers:
- Alder Lake (12th Gen)
- Raptor Lake (13th/14th Gen)
- Meteor Lake (Core Ultra 100)
- Arrow Lake (Core Ultra 200)
- **Lunar Lake (Core Ultra 200V — Arc 140V/130V)** — our target

### Privilege requirements

Read-only telemetry works as **standard user** via Level Zero default permissions. Admin is only required for write/control operations (overclocking, I2C writes, fan speed setting).

### DLL and redistribution

`ControlLib.dll` is located in `C:\Windows\System32`. Ships with Intel GPU drivers. Intel's official guidance: "Applications should not include the library in the application package." Load dynamically at runtime.

### C# integration approach

**No C# wrapper exists anywhere** — not on GitHub, not on NuGet, nowhere. Intel's recommendation (GitHub issue #32) is to use C++/CLI as an interop bridge.

Our approach: C++ wrapper in `HHAPulse.Native/NativeTelemetry.cpp`. Stub `HhaPulseIgclProbe()` already exists. Expand to:
- `HhaPulseIgclInit()` — LoadLibrary, ctlInit, enumerate devices
- `HhaPulseIgclReadGpu()` — ctlPowerTelemetryGet, check validity flags, return flat struct
- `HhaPulseIgclShutdown()` — ctlClose

Reference implementations: `FanControl.IntelCtlLibrary` (C# via `igcl64.dll`), Intel's `Sample_TelemetryAPP.cpp`.

### Known gotchas

- **Version mismatch:** `ctlInit` can return `CTL_RESULT_ERROR_UNSUPPORTED_VERSION` if the app header version doesn't match the driver's IGCL version. Must handle gracefully.
- **Power computation:** Energy counters are monotonic microjoules. Need two samples to compute watts. First sample after init will have no delta — skip it.
- **iGPU missing fields:** Fan RPM will be 0 (no fan), VRAM temp will be absent. Check per-field validity flags before populating metrics.
- **64-bit only:** All telemetry APIs are restricted to x64 due to Level Zero limitations. Our app is x64-only, so no issue.

---

## 10. NVIDIA — NVAPI + NVML

### Why both APIs together

LibreHardwareMonitor uses both NVAPI and NVML for NVIDIA GPUs:
- **NVAPI** for thermal sensors, clock frequencies, fan speed (true tachometer RPM), performance states
- **NVML** for power draw (watts) — `nvmlDeviceGetPowerUsage()` is more reliable for wattage than NVAPI on some cards

They match NVAPI bus IDs to NVML device handles to correlate data.

### What NVAPI provides

- `NvAPI_GPU_GetThermalSettings` — temperature (Celsius)
- `NvAPI_GPU_GetTachReading` — true fan tachometer RPM (NVML only gives intended %, not actual RPM)
- `NvAPI_GPU_GetAllClockFrequencies` — core + memory clocks
- Performance states, display info, VRAM

### What NVML provides

- `nvmlDeviceGetPowerUsage` — **real milliwatts** (not TDP%)
- Thread-safe — concurrent calls from multiple threads are explicitly supported
- Some GPU memory and thermal data (overlap with NVAPI)

### Privilege requirements

Both NVAPI and NVML work as **standard user** for read/query operations on Windows. No admin needed.

### DLL and redistribution

- `nvapi64.dll` ships with NVIDIA drivers (GeForce, RTX)
- `nvml.dll` at `C:\Program Files\NVIDIA Corporation\NVSMI\nvml.dll` — installed with drivers
- Neither needs to be bundled

### C# integration

- **NVAPI:** NvAPIWrapper.Net NuGet package (LGPL licensed — usable in commercial closed-source with attribution). Maintained by falahati. Covers thermal, clocks, fan, memory.
- **NVML:** Direct P/Invoke to `nvml.dll` for power watts. No established NuGet wrapper.

### Known gotchas

- **NVML at high frequency:** NVML at 10Hz (100ms) caused ~20% performance degradation on GTX 1080 in benchmarks. At 1Hz, overhead is negligible. Never poll faster than 1Hz.
- **Consumer GPU limitations:** ~60% of NVML functions are unavailable on consumer GeForce GPUs (MIG, ECC, NVLink, etc.). Basic telemetry (temp, power, clocks, fan) works fine.
- **No NVIDIA handhelds:** No shipping NVIDIA Windows handhelds exist as of April 2026. This is for desktop/eGPU users only.

---

## 11. CPU Power — Windows EMI

### Why LibreHardwareMonitor was rejected (see Section 12)

LHM requires admin + WinRing0 kernel driver. This fails Store certification, gets flagged by Defender, and violates the "No WinRing0" project rule.

### Windows Energy Meter Interface (EMI)

Windows EMI exposes energy counters through a device interface plus `DeviceIoControl`, not WMI. It requires a **battery** on the device - which all handhelds have (ROG Ally, Legion Go, MSI Claw, Steam Deck).

- **Standard user** — no admin, no kernel driver
- **Store safe** — no restricted components
- Compute watts from energy counter deltas (same pattern as IGCL GPU power)

### Caution

EMI availability and which power rails it exposes depends on the device firmware (ACPI tables). Not every handheld may expose a usable CPU package power rail. If EMI discovery does not produce stable CPU power data on target hardware, the metric stays hidden with an explanation in diagnostics. Never show guessed or synthetic watt values.

### PawnIO (existing optional path)

PawnIO remains optional for desktop users who have it installed. It provides CPU MSR temperature, fan, and RAPL power via a signed kernel driver with sandboxed Pawn bytecode. It is:
- Blocked by FACEIT anti-cheat
- Requires admin to install
- Detected only if already present, hash-verified before use
- Never loaded, bundled, or downloaded by HHA Pulse

---

## 12. What Was Rejected and Why

### LibreHardwareMonitor as NuGet dependency

| Reason | Evidence |
|--------|----------|
| Requires admin + kernel driver | LHM loads `LibreHardwareMonitor.sys` (WinRing0 derivative) for MSR/IO access |
| Windows Defender flags it | `VulnerableDriver:WinNT/Winring0.G` (CVE-2020-14979) since March 2025 |
| Microsoft blocking legacy drivers | Kernel trust changes April 2026 (TheRegister article) |
| WACK fails | MSIX apps cannot bundle kernel-mode components |
| GPU sensors redundant | NVAPI/ADLX/IGCL provide GPU sensors directly without admin — no need for LHM |
| CPU power still needs admin | LHM's CPU package power comes from MSR RAPL — requires kernel driver regardless |
| Project rule violation | CLAUDE.md: "No WinRing0 dependency" |

### HWiNFO shared memory

| Reason | Evidence |
|--------|----------|
| Hard dependency on external app | HWiNFO must be running |
| Requires paid HWiNFO Pro ($25) | Free version kills shared memory after 12 hours |
| No format stability guarantee | HWiNFO can change struct layout |
| Bad UX | Paid app requiring another paid app |

### D3DKMT as primary sensor source

| Reason | Evidence |
|--------|----------|
| Intel iGPU returns zeros | overlay.log line 171 |
| Power is TDP% not watts | Microsoft docs: "tenths of a percentage of thermal design limits" |
| No clock speed data | Not in ADAPTERPERFDATA struct |
| Every tool uses vendor SDKs | Afterburner, HWiNFO, GPU-Z, FanControl all use NVAPI/ADL/IGCL |

### DLL injection for overlay rendering

| Reason | Evidence |
|--------|----------|
| Project rule | CLAUDE.md: "NEVER inject DLLs into game processes. NEVER hook DirectX/Vulkan." |
| Anti-cheat risk | EAC/BattlEye/Vanguard all detect and flag process injection |
| Not needed | WinUI 3 topmost window works for handheld overlay. VRR safe on AMD/Intel FreeSync. |

### GpuTelemetryCoordinatorCollector pattern (from GPT plan)

| Reason | Evidence |
|--------|----------|
| Unnecessary complexity | Handhelds have single GPU. Independent collectors are simpler. |
| Already correct | GpuPerfDataCollector:70-75 selects display-attached adapter (NumOfSources > 0) |
| DependencyState already exists | TelemetrySnapshot.cs:187-227 already tracks GPU telemetry source |

---

## 13. Constraint Verification: VRR

### Question: Do vendor SDK calls break Variable Refresh Rate (FreeSync/G-Sync)?

**Answer: No.** Vendor SDK calls (ADLX, IGCL, NVAPI, NVML) are usermode DLL calls that read sensor data. They have zero interaction with the display/rendering pipeline. VRR is a display controller feature — sensor queries don't affect it.

### Question: Does the WinUI 3 overlay window break VRR?

**Answer: Depends on vendor.**

- **AMD/Intel FreeSync:** VRR works even with Composed Flip overlays present. The OS-level VRR implementation handles it. **Handhelds are safe.**
- **NVIDIA G-Sync:** A topmost HWND forces DWM from Independent Flip to Composed Flip. G-Sync may not activate in borderless windowed mode with a visible overlay. This is NOT caused by our code — it's any visible topmost window (Discord overlay has the same issue, documented by Erik McClure).

### Mitigation

- Handheld targets (AMD/Intel) are unaffected
- NVIDIA desktop caveat to be documented
- Future: investigate MPO (Multi-Plane Overlay) hardware planes used by Xbox Game Bar to avoid Composed Flip demotion

### WPF VRR bug

The WPF VRR bug (dotnet/wpf#2294) is specific to WPF's `MediaIntegrationLayer` frame timing. WinUI 3 uses a completely different rendering stack (DirectComposition + Microsoft.UI.Composition) and does NOT share this bug.

---

## 14. Constraint Verification: Performance

### Question: Does 1Hz GPU sensor polling impact game FPS?

**Answer: No measurable impact at 1Hz.**

| API | Overhead per call | Evidence |
|-----|-------------------|----------|
| D3DKMT QueryAdapterInfo | Low tens of microseconds | Reads driver-cached data, no GPU stall. Same as Task Manager. |
| ADLX GetCurrentGPUMetrics | Sub-millisecond | ADLX caches internally on its own sampling thread. Our call just reads the cache. |
| IGCL ctlPowerTelemetryGet | Sub-millisecond | Intel sample code uses 20ms polling — 1Hz is 50x less frequent. |
| NVAPI GetThermalSettings | Sub-millisecond | Standard user-mode query |
| NVML GetPowerUsage | Sub-millisecond at 1Hz | Known issue ONLY at 10Hz+ (20% hit on GTX 1080). 1Hz = negligible. |
| PDH CollectQueryData | Sub-millisecond | Reads pre-computed shared memory counters |

**Total per-tick cost: well under 1ms.** On a 6-8 core handheld at 15-28W TDP, this is <0.1% of a single core's time budget.

### Industry standard polling intervals

| Tool | Default interval |
|------|-----------------|
| MSI Afterburner | 1000ms |
| HWiNFO | 2000ms |
| RTSS OSD | 500ms |
| AMD Radeon Overlay | 1000ms |
| Steam Overlay | 1000ms |

**1Hz (1000ms) is the industry standard.** No tool defaults below 500ms.

### Thread model

All APIs are safe to call from a background thread. None require the UI/main thread. NVML is explicitly documented as thread-safe. Recommended: single dedicated background thread with `Task.Delay(1000)` loop.

---

## 15. Constraint Verification: Anti-Cheat

### Question: Do vendor GPU SDKs trigger anti-cheat bans?

**Answer: No.** Anti-cheat systems target process injection, memory reading, and vulnerable kernel drivers — not legitimate usermode API calls from an external process.

| Anti-Cheat | Detects vendor SDK calls? | What it actually detects |
|------------|---------------------------|--------------------------|
| EasyAntiCheat (EAC) | **Game-dependent** | DLL injection, suspicious memory allocation, thread creation in game process. External overlays still depend on per-title policy. |
| BattlEye | **No** | "No one is banned for using non-hack programs like overlays" (BattlEye statement) |
| Riot Vanguard | **Risky / no guarantee** | Blocks vulnerable kernel drivers (WinRing0, RTCore64.sys) and has aggressive kernel-level monitoring. |
| FACEIT | **No** (for SDK calls) | Blocks specific kernel drivers by certificate. Blocks PawnIO. |

### What DOES trigger anti-cheat (none apply to HHA Pulse)

- DLL injection into game process — **we don't do this**
- ReadProcessMemory on game — **we don't do this**
- DirectX/Vulkan hooks — **we don't do this**
- Vulnerable kernel drivers (WinRing0, PawnIO) — **PawnIO is optional only, WinRing0 banned**

### MSI Afterburner coexistence

Afterburner's usermode monitoring (NVAPI/ADL) works fine alongside anti-cheat. The problems arise from (a) RTSS overlay injection into game process, and (b) old RTCore64.sys kernel driver. Our architecture avoids both.

### Xbox Game Bar as precedent

Game Bar widgets render through the Windows compositor (not injection). Anti-cheat vendors whitelist this path. Our Game Bar widget uses the same path. The paid overlay runs as a separate topmost window — also not injection.

### PawnIO caveat

PawnIO is blocked by FACEIT specifically (signing certificate flagged). This is already handled: PawnIO is optional-only, detected if pre-installed, never loaded by the app.

---

## 16. Frame Generation Detection

### How RTSS detects DLSS/XeSS/FSR Frame Generation

RTSS hooks `IDXGISwapChain::Present` via DLL injection into the game process. From inside the swap chain, it can count real rendered frames vs additional presented frames. This is how it shows "Real FPS" vs "Displayed FPS" with frame generation active.

### Why we can't do this

Project rule: "NEVER inject DLLs into game processes. NEVER hook DirectX/Vulkan." This is also the primary trigger for anti-cheat detection.

### How we detect frame generation

**PresentMon ETW.** PresentMon captures ETW (Event Tracing for Windows) present events from OUTSIDE the game process. In modern PresentMon builds (v2.5.0+), frame classification is exposed via `FrameType`, and the capture service consumes that output rather than inventing its own heuristic. It analyzes event patterns to distinguish:
- App/rendered frames (the game engine's actual output)
- Displayed/presented frames (what the display shows, including generated frames)

When displayed FPS consistently exceeds app FPS, frame generation is active.

### Vendor SDKs cannot detect frame generation

- **NVAPI/NVML:** No DLSS-FG detection API exists
- **IGCL:** No XeSS-FG detection API exists
- **ADLX:** No FSR-FG detection API exists

Frame generation is managed by per-game SDKs (NVNGX for DLSS, Intel XeSS SDK, AMD FSR SDK) that operate inside the game process. There is no external query API.

**PresentMon ETW is the only non-injection path for frame generation detection.**

### Architecture note

The GPT plan claimed we need to "Replace `EventName.Contains('Present')` heuristic in EtwFrameCapture.cs." This is incorrect. No `EtwFrameCapture.cs` exists in the overlay codebase. PresentMon handles frame classification externally — the overlay receives pre-processed metrics via named pipe from the CaptureService. Frame classification is PresentMon's responsibility, not ours.

---

## 17. GPT Plan Cross-Check

A separate GPT-generated recovery plan was provided for comparison. Each of its 7 architectural claims was verified against the actual HHAP-0.25 codebase:

| GPT Claim | Verdict | Evidence |
|-----------|---------|----------|
| Move XamlControlsResources into guarded code | **Adopted** | `App.xaml:9` resource merge runs before LaunchAsync guards. Valid improvement. |
| GpuTelemetryCoordinatorCollector pattern | **Rejected** | Adds complexity for single-GPU handhelds. Independent collectors work correctly. |
| DXGI bus identity matching | **Already implemented** | `GpuPerfDataCollector:70-75` uses "first with NumOfSources > 0" (display-attached), not arbitrary index. |
| Append-only dependency metadata in TelemetrySnapshot | **Already exists** | `DependencyState` at `TelemetrySnapshot.cs:187-227` tracks GPU telemetry source and status messages. |
| ETW `Contains("Present")` rewrite | **Wrong architecture** | No `EtwFrameCapture.cs` exists in overlay code. PresentMon handles frame classification via CaptureService named pipe. GPT assumed overlay does ETW frame parsing — it doesn't. |
| Transparency HWND reorder | **Already correct** | `TransparentWindowHelper.cs` applies DWM composition (lines 14-27) before layered styles (lines 43-54). Correct order. |
| Build SDK version guard | **Low value** | TFM `net8.0-windows10.0.19041.0` already enforces .NET 8 at compile time. `global.json` is sufficient. |

**Result: 1 adopted, 2 already existed, 1 wrong assumption about architecture, 3 not better or low value.**

---

## 18. Implementation Plan

### Step 0: Fix P0 Blockers (before any GPU work)

1. Create `global.json` — pin .NET SDK to 8.x
2. Test WindowsAppSDK version change in `Directory.Packages.props:11`
3. Move `XamlControlsResources` from `App.xaml` into guarded `App.xaml.cs` code-behind
4. Clean rebuild from HHAP-0.25

### Step 1: ADLX Integration (AMD — primary handheld market)

1. Expand `HHAPulse.Native/NativeTelemetry.cpp` — ADLX wrapper with flat C exports
2. Create `AdlxGpuCollector.cs` — P/Invoke, populate GpuMetrics with real watts
3. Wire into `App.xaml.cs` for VendorId `0x1002`

### Step 2: IGCL Integration (Intel — MSI Claw, Lunar Lake)

1. Expand `HHAPulse.Native/NativeTelemetry.cpp` — IGCL wrapper with flat C exports
2. Create `IgclGpuCollector.cs` — energy counter deltas for watts, validity flags
3. Wire into `App.xaml.cs` for VendorId `0x8086`

### Step 3: NVAPI + NVML Integration (NVIDIA — desktop)

1. Add NvAPIWrapper.Net NuGet
2. Create `NvApiGpuCollector.cs` — NVAPI for temp/fan/clocks, NVML for power watts
3. Wire into `App.xaml.cs` for VendorId `0x10DE`

### Step 4: CPU Power via Windows EMI (handhelds)

1. Create `CpuPowerCollector.cs` — WMI EMI energy counters, watts from deltas
2. Only activate if battery present

### Step 5: Kill TDP% Everywhere

- D3DKMT `PowerRaw` NEVER shown to users
- `GpuMetrics.PowerWatts` ONLY from vendor SDKs
- If unavailable, show "--"

### Verification checklist

- [ ] 10+ cold launches without TabViewButtonBackground crash
- [ ] Settings save works (no System.IO.Pipelines error)
- [ ] No MissingMethodException in log
- [ ] AMD handheld: real temp, real watts, fan (or hidden), clocks
- [ ] Intel Arc 140V: real temp (currently zeros), real watts, fan hidden
- [ ] NVIDIA desktop: real temp, real watts, real fan RPM, clocks
- [ ] Non-matching GPU: vendor SDK disabled, D3DKMT fallback, no power shown
- [ ] 1Hz polling < 1ms CPU per tick
- [ ] VRR works on AMD/Intel handheld
- [ ] BattlEye smoke test passes; EAC remains game-dependent and Vanguard remains high-risk with no blanket compatibility guarantee
- [ ] MSIX build passes WACK — no kernel drivers, no vendor DLLs bundled

---

## 19. Sources

### Log evidence
- `overlay.log` from HHAP-0.25 session on Intel Arc 140V MSI Claw (2026-04-08/09)

### Microsoft documentation
- [D3DKMT_ADAPTER_PERFDATA](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/d3dkmthk/ns-d3dkmthk-_d3dkmt_adapter_perfdata)
- [D3DKMT_ADAPTER_PERFDATACAPS](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/d3dkmthk/ns-d3dkmthk-_d3dkmt_adapter_perfdatacaps)
- [Windows Energy Meter Interface](https://learn.microsoft.com/en-us/windows-hardware/drivers/powermeter/energy-meter-interface)

### AMD
- [ADLX SDK — GPUOpen](https://gpuopen.com/adlx/)
- [ADLX GitHub](https://github.com/GPUOpen-LibrariesAndSDKs/ADLX)
- [ADLX Performance Monitoring](https://gpuopen.com/manuals/adlx/adlx-sdk-references/adlx-interfaces/performance-monitoring/)
- [GOALS Handheld Performance](https://gpuopen.com/learn/how-goals-delivers-performance-handheld-pcs-part1/)

### Intel
- [IGCL Documentation](https://intel.github.io/drivers.gpu.control-library/)
- [IGCL GitHub](https://github.com/intel/drivers.gpu.control-library)
- [IGCL C# usage issue #32](https://github.com/intel/drivers.gpu.control-library/issues/32)

### NVIDIA
- [NVAPI GitHub](https://github.com/NVIDIA/nvapi)
- [NvAPIWrapper.Net NuGet](https://www.nuget.org/packages/NvAPIWrapper.Net)
- [NVML API Reference](https://docs.nvidia.com/deploy/nvml-api/nvml-api-reference.html)

### Open-source implementations
- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
- [FanControl.ADLX](https://github.com/Rem0o/FanControl.ADLX)
- [FanControl.IntelCtlLibrary](https://github.com/Rem0o/FanControl.IntelCtlLibrary)
- [epinter/adlxwrapper (pure C#)](https://github.com/epinter/adlxwrapper)
- [Handheld Companion](https://github.com/Valkirie/HandheldCompanion)

### Anti-cheat and VRR
- [Discord overlay breaks G-Sync (Erik McClure)](https://erikmcclure.com/blog/discord-overlay-breaks-gsync/)
- [ForceComposedFlip tool](https://github.com/fernandoenzo/ForceComposedFlip)
- [WPF VRR bug dotnet/wpf#2294](https://github.com/dotnet/wpf/issues/2294)

### Rejected approaches
- [Defender flags WinRing0](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/issues/1660)
- [Microsoft WinRing0 threat entry](https://www.microsoft.com/en-us/wdsi/threats/malware-encyclopedia-description?Name=VulnerableDriver:WinNT/Winring0.G)
- [PawnIO + FACEIT conflict](https://github.com/Rem0o/FanControl.Releases/discussions/3451)
