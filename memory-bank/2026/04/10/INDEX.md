# 2026-04-10 - Intel Lunar Lake telemetry final push

## Current tested machine

- Device class: MSI handheld.
- Primary adapter: `Intel(R) Arc(TM) 140V GPU (16GB)`, vendor `0x8086`.
- CPU/GPU platform: Core Ultra 7 258V / Lunar Lake Arc 140V.
- Runtime output path used for validation: `src/HHAPulse.Overlay/bin/Release/net8.0-windows10.0.19041.0/win-x64/`.
- Shared MVID after latest validated run: `6cd25ca5-500e-497f-ab3d-d3230920f017`.
- Native bridge present and copied beside overlay. Native DLL timestamp verified at `2026-04-10 17:14:29`, size `51200` bytes.

## What was fixed and proved

### FPS and capture routing

- Foreground target routing now holds the last valid game target for overlay/menu foreground transitions.
- Evidence from diagnostics:
  - `targetSource=held; target=KingdomCome(25624); heldAgeMs=3197`
  - foreground was `HHAPulse.Overlay(50988)` at `980x740`
  - capture remained `captureFreshness=live`
  - FPS metrics were available: `fps=25.2`, `avg=44.7`, `1%=22.9`, `0.1%=17.9`, `frametime=39.7ms`
- AVG FPS now uses a 5-second rolling window of one-second FPS samples, not the previous 60-sample slow-settling window.
- Diagnostics now state the VRR/anti-cheat safety guardrail: `Capture mode: ETW/PDH read-only, no hooks or VRR changes.`
- Safety line locked: no DLL injection, no swapchain/present hooks, no game-process memory reads, no kernel driver install, no display/VRR setting writes.

### VRAM on Intel UMA/iGPU

- Old bad behavior: Intel iGPU was hidden or showed tiny DXGI local usage such as `0.1/18.0G`.
- New behavior: iGPU used memory comes from PDH GPU memory counters; DXGI stays as capacity/budget/evidence.
- Evidence from diagnostics:
  - metric source: `PDH GPU Process Memory Total Committed`
  - HUD/trace value: `4.5/18.0G`
  - selected used bytes: `4808564736`
  - selected total bytes: `19303997276`
  - DXGI dedicated aperture remains recorded: `134217728` bytes (`128MB`)
  - process PDH counters: `shared=4808491008; local=4808491008; nonLocal=0; totalCommitted=4808564736`
  - adapter PDH counters: `shared=7145996288; totalCommitted=8574238720`
- Decision: keep the 128MB dedicated aperture as diagnostic-only. Do not treat it as the iGPU memory capacity.

### Device/chassis fan

- Old bad behavior: fan was treated as `GPU Fan` and absent on the Intel iGPU paths.
- New behavior: HUD/control label is `Fan`; source is the elevated capture service when present.
- MSI proof:
  - Local WMI class: `root\WMI:MSI_ACPI`
  - read-only method used: `Get_Fan`
  - input package: `Package_32`, subfeature `0x00`
  - no `Set_*` method is called.
- Evidence from first read:
  - raw: `01 00 C6 00 C3 00 00 00 ...`
  - decoded RPM: `[2424, 2462]`
- Evidence under later load:
  - raw: `01 00 9F 00 A1 00 00 00 ...`
  - decoded RPM: `3019/2981rpm`
- Decode rule from Linux kernel MSI WMI Platform docs:
  - first byte `0x01` means success
  - up to four big-endian 16-bit fan readings
  - `RPM = 480000 / raw`
  - raw `0` means fan `0rpm`
  - ACPI WMI method is not thread-safe, so calls are serialized.
- Source: https://docs.kernel.org/wmi/devices/msi-wmi-platform.html

### CPU temperature and device/SoC temperature

- CPU temperature now comes from the elevated capture service:
  - source: `WMI MSAcpi_ThermalZoneTemperature (capture service)`
  - validated value in the latest run: `53.1C`
- MSI device/SoC temperature is now a separate metric:
  - source: `MSI_ACPI.Get_Temperature subfeature 0x00 (capture service)`
  - validated value in the latest run: `51.0C`
  - status text explicitly says the sensor identity is OEM/EC-defined, not GPU-specific
- Product rule remains locked: do not populate `gpu_temp` from the MSI device/SoC temperature without sensor-identity proof.

### AC Device Power / total power

- Old bad behavior: `TotalPower` on AC used `RAPL_Package0_PKG + RAPL_Package0_DRAM`, which is not whole-device power.
- New behavior: Device Power on AC returns `--` unless a true whole-device/platform power source is accepted.
- Evidence from diagnostics:
  - `total_power: Unavailable`
  - source: `CallNtPowerInformation(SystemBatteryState.Rate)`
  - reason: `Device is on AC/charging; no accepted whole-device wall-watt source exists. RAPL PKG + DRAM is only a SoC + memory component rail and is not shown as Device Power.`
  - pipeline includes `acRaplPkgPlusDramRejectedAsDevicePower=True`
- Windows Power Meter probe on this machine:
  - `\Power Meter(power meter (0))\Power = 0`
  - `\Power Meter(_total)\Power = 0`
- MSI WMI `Get_Power` probe:
  - subfeature `0x00` returned constant raw `01 07 00 00 ...`
  - no watt conversion accepted.
- RAPL/EMI remains useful for component rails only:
  - PKG = CPU package / SoC package rail.
  - DRAM = memory rail.
  - PP1 = iGPU rail.
  - PKG + DRAM excludes display panel, SSD, radios, fans, and platform conversion losses.

## Intel Lunar Lake / Arc 140V facts now proven

### D3DKMT fallback

- D3DKMT initialized on the primary Intel adapter but returned zeros for sensors:
  - `Temp=0.0C`
  - `PowerRaw=0`
  - `FanRPM=0`
  - `MemFreq=0Hz`
- Conclusion: D3DKMT is not a usable GPU temp/fan/power source on this Arc 140V runtime.

### IGCL aggregate telemetry

- First-read support mask on Arc 140V:
  - `supportMask=0x0028`
  - `bSupported=[gpuEnergyCounter, gpuCurrentClockFrequency]`
  - `tempSource=<none>`
  - `rawTemp=0.00C`
  - `rawFan=0rpm`
- Meaning:
  - supported: GPU energy counter
  - supported: GPU current clock
  - unsupported: `gpuCurrentTemperature`
  - unsupported: `gpuVrTemp`
  - unsupported: `saVrTemp`
  - unsupported: `totalCardEnergyCounter`
  - unsupported: aggregate `fanSpeed[*]`

### IGCL dedicated fan/temp enumeration

- Native wrapper now probes Intel's dedicated enumeration functions:
  - `ctlEnumTemperatureSensors`
  - `ctlTemperatureGetState`
  - `ctlEnumFans`
  - `ctlFanGetState`
- Evidence from latest run:
  - support mask remained `0x0028`
  - no dedicated temp API bit appeared
  - no dedicated fan API bit appeared
- Conclusion for this machine/driver: Intel IGCL exposes GPU energy + clock only. It does not expose GPU temperature or fan through either the aggregate telemetry struct or the dedicated enumeration APIs in this run.

### iGPU power

- GPU power works:
  - current diagnostic source: `IGCL gpuEnergyCounter/timeStamp`
  - value under load: `5.6W`
  - proof: `validFlags=0xA`, `powerSourceKind=3`
- EMI PP1 also enumerates and is initialized as an iGPU power rail fallback:
  - `RAPL_Package0_PP1`
  - currently IGCL runs after EMI and overwrites the value when IGCL power is valid.

### EMI/RAPL channel map on this machine

- EMI device #0 enumerated four channels:
  - `RAPL_Package0_PKG`
  - `RAPL_Package0_DRAM`
  - `RAPL_Package0_PP0`
  - `RAPL_Package0_PP1`
- No PSys/platform RAPL channel was exposed.
- Therefore there is no RAPL whole-device/platform power source available on this machine through EMI.

## Remaining gaps

### GPU temperature

- Still missing as a true GPU metric.
- Intel paths tried and failed closed:
  - D3DKMT: zero
  - IGCL aggregate `ctlPowerTelemetryGet`: no temp fields supported
  - IGCL dedicated temp enumeration: no usable sensor handle exposed in latest run
- MSI WMI temperature probe is promising but not semantically proven as GPU temperature:
  - `MSI_ACPI.Get_Temperature` subfeature `0x00` returned live byte values around `50-53C`.
  - sample sequence during game/load: `50, 51, 50, 50, 51, 52, 53, 53`
  - raw examples: `01 32 00 ...`, `01 35 00 ...`
- Current rule: do not label MSI WMI temperature as `GPU Temp` until the byte's sensor identity is proven. It may be EC/SoC/system thermal, not GPU die.
- Implemented next product option: `Device Temp` / `SoC Temp` now ships as a separate metric from MSI WMI, with diagnostics explicitly saying `MSI_ACPI.Get_Temperature subfeature 0x00` and warning that the sensor identity is OEM/EC-defined rather than GPU-specific.

### AC whole-device power

- Still missing.
- Rejected as Device Power:
  - `RAPL PKG + DRAM`: SoC + memory component only, not device total.
  - CPU + GPU sum: lower-bound component sum only, excludes memory/display/SSD/fans/radios and risks double-counting on integrated SoCs.
  - MSI `Get_Power` subfeature `0x00`: returned constant `01 07 00 ...`; no watt semantics proven.
  - Windows Power Meter counters: returned `0`.
- Battery discharge remains the only true whole-device power source currently accepted.
- Safe next investigation: inspect MSI ACPI AML / vendor service protocol for documented power subfeatures, or compare against an external USB-C power meter while scanning read-only MSI power subfeatures. Do not infer watts from mode bytes.

## Tests and builds

- `HHAPulse.Shared.Tests`: `4/4` passed.
- `HHAPulse.CaptureService.Tests`: `15/15` passed.
- `HHAPulse.Overlay.Tests`: `70/70` passed.
- Native DLL rebuild:
  - `HHAPulse.Native.vcxproj`
  - Release x64
  - `0 warnings`, `0 errors`
- Final overlay Release build:
  - `0 warnings`, `0 errors`

## Files and systems touched

- Native IGCL contract and wrapper:
  - `src/HHAPulse.Native/VendorContracts/IgclTelemetryContract.h`
  - `src/HHAPulse.Native/NativeTelemetry.cpp`
  - `src/HHAPulse.Native/NativeTelemetry.h`
- FPS:
  - `src/HHAPulse.CaptureService/Etw/EtwFrameCapture.cs`
  - `src/HHAPulse.Overlay/Collectors/Fps/CaptureServiceCollector.cs`
  - `src/HHAPulse.Overlay/Interop/ForegroundCaptureTargetRouter.cs`
- VRAM:
  - `src/HHAPulse.Overlay/Collectors/Gpu/VramCollector.cs`
- Fan:
  - `src/HHAPulse.CaptureService/Sensors/CaptureServiceSensorCollector.cs`
  - `src/HHAPulse.Shared/Models/TelemetrySnapshot.cs`
  - formatters and labels now show `Fan`, not `GPU Fan`.
- CPU/device temp:
  - `src/HHAPulse.CaptureService/Sensors/CaptureServiceSensorCollector.cs`
  - `src/HHAPulse.Overlay/Collectors/Fps/CaptureServiceCollector.cs`
  - `src/HHAPulse.Shared/Models/CaptureFrameMetrics.cs`
  - `src/HHAPulse.Shared/Models/TelemetrySnapshot.cs`
- Power:
  - `src/HHAPulse.Overlay/Diagnostics/SystemPowerValidator.cs`
  - `src/HHAPulse.Overlay/Helpers/MetricFormatterCompact.cs`

## Rules locked by evidence

- Never show `RAPL PKG + DRAM` as `Device Power`.
- Never show D3DKMT `PowerRaw` as watts.
- Never label a temperature as GPU temperature unless the API/source identifies it as GPU/global GPU sensor.
- For Intel Arc 140V on this driver, IGCL gives energy + clock only.
- For MSI handheld fan, `MSI_ACPI.Get_Fan` is valid read-only chassis fan telemetry.
- Keep capture and memory telemetry read-only for VRR/anti-cheat safety: ETW, PDH, DXGI, WMI read methods, vendor SDK read methods only.
