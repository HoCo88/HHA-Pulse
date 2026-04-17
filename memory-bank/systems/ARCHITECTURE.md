# HHA Pulse - System Architecture (April 2026)

## Operational Build

HHA Pulse has two local executables:

- `HHAPulse.Overlay`: WinUI 3 full-trust overlay running as the interactive user.
- `HHAPulse.CaptureService`: elevated Windows service for ETW FPS capture.

The overlay owns foreground-window detection in the user session and sends the target PID to the capture service. The service owns the elevated ETW session and publishes frame metrics back over named pipes. This avoids DLL injection, game hooks, and game memory reads.

## Current Telemetry Truth Update (2026-04-10)

- `input_latency` is a reserved id only and is hidden from the HUD, picker, status cards, and traces until a real input-to-display latency source exists.
- `total_power` / Device Power means whole-device battery discharge watts only. On AC it returns `--` unless a real platform/whole-device watt source is accepted. CPU+GPU and RAPL PKG+DRAM component sums are diagnostics-only because they exclude display, SSD, fans, radios, and platform losses.
- Battery telemetry now uses `CallNtPowerInformation(SystemBatteryState)` plus battery class `IOCTL_BATTERY_QUERY_INFORMATION` when absolute mWh units are reported; relative capacity units are rejected for Wh conversion.
- AMD GPU power now prefers ADLX `GPUTotalBoardPower` and falls back to `GPUPower`; Intel IGCL remains energy/time watts; NVIDIA NVML remains milliwatts/1000.
- D3DKMT `PowerRaw` remains rejected provenance only because it is a TDP ratio, not watts.
- Intel/UMA iGPU VRAM is visible now: PDH GPU memory counters provide used memory, DXGI provides shared capacity/budget, and the 128 MB dedicated aperture is diagnostic-only.
- Device/chassis fan is a first-class telemetry path. On the MSI test machine it is read by the elevated capture service from `root\WMI:MSI_ACPI.Get_Fan` with read-only calls and displayed as `Fan`, not GPU-only fan.
- CPU temperature now comes from the elevated capture service via `MSAcpi_ThermalZoneTemperature`.
- Device/SoC temperature now comes from the elevated capture service via `MSI_ACPI.Get_Temperature` subfeature `0x00` and remains explicitly separate from `gpu_temp`.
- FPS target routing holds the last valid game target during overlay/Steam/Game Bar foreground switches, and AVG FPS uses a 5-second rolling window.
- Intel Lunar Lake / Arc 140V GPU temperature is still unavailable from proved Intel/D3DKMT paths. Do not infer it from CPU temperature or MSI device temperature bytes.
- `AppFramesPerSecond`, `PresentFramesPerSecond`, and `DisplayFramesPerSecond` stay in the MessagePack contract but are zeroed/not-computed until a real PresentMon-grade split exists.
- Missing runtime provenance is reported as `unknown collector`; diagnostics no longer guess API names from hardcoded fallback literals.
- The diagnostics label is "Last observed boundary state"; widget client count is not a heartbeat.

## Data Sources

| Metric | Source | Collector | Elevation |
|--------|--------|-----------|-----------|
| FPS / frametime / 1% / 0.1% lows | ETW `HHAPulse_FrameCapture` | `CaptureServiceCollector` | Service (elevated) |
| Frame generation detection | Intel-PresentMon ETW evidence via `HybridPresentDetected` (diagnostic-only) | `CaptureServiceCollector` | Service (elevated) |
| CPU temperature | WMI `MSAcpi_ThermalZoneTemperature` | `CaptureServiceCollector` | Service (elevated) |
| CPU usage | `GetSystemTimes` delta | `CpuUsageCollector` | User |
| GPU usage | PDH `\GPU Engine(*engtype_3D*)\Utilization Percentage` | `GpuUsageCollector` | User |
| GPU temp / fan (fallback) | `D3DKMTQueryAdapterInfo(KMTQAITYPE_ADAPTERPERFDATA)` via gdi32.dll | `GpuPerfDataCollector` | User (no elevation) |
| GPU temp / power / fan / clock (AMD) | ADLX v1.4 via `HHAPulse.Native.dll` → `amdadlx64.dll` | `AdlxGpuCollector` | User |
| GPU temp / power / fan / clock (Intel) | IGCL v1.1 via `HHAPulse.Native.dll` → `ControlLib.dll` / `igcl64.dll` | `IgclGpuCollector` | User |
| GPU temp / power / fan / clock (NVIDIA) | NvAPIWrapper.Net (NuGet) + NVML P/Invoke → `nvml.dll` | `NvApiGpuCollector` | User |
| CPU power (watts) | Windows EMI device IOCTLs (picowatt-hour energy deltas) | `CpuPowerCollector` | User |
| RAM | `GlobalMemoryStatusEx` | `RamCollector` | User |
| VRAM | PDH GPU memory counters + DXGI `IDXGIAdapter3.QueryVideoMemoryInfo` / `DXGI_ADAPTER_DESC1` | `VramCollector` | User |
| Device/chassis fan (MSI) | read-only WMI `root\WMI:MSI_ACPI.Get_Fan` | `CaptureServiceCollector` | Service (elevated) |
| Device/SoC temperature (MSI) | read-only WMI `root\WMI:MSI_ACPI.Get_Temperature` subfeature `0x00` | `CaptureServiceCollector` | Service (elevated) |
| Battery / charge / discharge | `CallNtPowerInformation` | `BatteryCollector` | User |
| Display Hz | `EnumDisplaySettings` | `DisplayCollector` | User |

### Vendor GPU telemetry architecture (HHAP-0.25+)

GPU vendor is detected at startup via DXGI `VendorId` (unified `DxgiPrimaryAdapterSelector`):
- `0x1002` (AMD): ADLX collector added. D3DKMT stays as temp/fan fallback.
- `0x8086` (Intel): IGCL collector added. D3DKMT returns zeros on iGPU — IGCL is the only path.
- `0x10DE` (NVIDIA): NvAPI+NVML collector added. D3DKMT stays as temp/fan fallback.

Collector ordering: D3DKMT runs first (index 6), vendor collector appended last (index 9+). Last writer wins — vendor SDK overwrites D3DKMT values when available. D3DKMT **never** sets `GpuMetrics.PowerWatts` or `MetricFlags.GpuPower`.

Per-metric source attribution: `DependencyState` has separate `GpuTemperatureSource`, `GpuPowerSource`, `GpuFanSource`, `GpuClockSource` fields (MessagePack Keys 14-21). `MetricStatusFactory` reports the actual source per metric (e.g., "NvAPI" for temp, "NVML" for power).

Native bridge: `HHAPulse.Native.dll` exports flat C functions for AMD and Intel. Vendor DLLs loaded dynamically at runtime — never bundled. Contract headers vendored in `src/HHAPulse.Native/VendorContracts/` with provenance comments.

### Not yet implemented (show `--`)

- Input latency: needs PresentMon ETW parsing — not in capture service yet.
- System total power / Device Power: battery discharge watts when unplugged; on AC, show `--` unless a real platform/whole-device watt source is accepted. Component sums remain diagnostics-only.

Note: frame-gen detection now carries a vendor label (`FrameGenVendor.IntelXeFG` / `AmdAFMF`) propagated from Intel-PresentMon ETW. `MetricFlags.FrameGen` is set by `CaptureServiceCollector` when vendor is not `None`. `AppFramesPerSecond` / `PresentFramesPerSecond` are computed strictly from Intel-PresentMon frame-type counts and stay `0` when the provider did not fire (no DXGI fallback). See `memory-bank/2026/04/17/framegen-handheld-plan.md`.

## GPU Detection

GPU vendor and type are detected via `DXGI_ADAPTER_DESC1`:

| Field | Intel iGPU | Intel Arc | AMD APU | AMD/NVIDIA discrete | eGPU |
|-------|-----------|-----------|---------|---------------------|------|
| `VendorId` | `0x8086` | `0x8086` | `0x1002` | `0x1002`/`0x10DE` | any |
| `DedicatedVideoMemory` | ~128MB | 8GB+ | 512MB–8GB | 4–24GB | 4–24GB |

- **Vendor IDs**: `0x8086` = Intel, `0x1002` = AMD, `0x10DE` = NVIDIA (confirmed via [MS DXGI docs](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/ns-dxgi-dxgi_adapter_desc1)).
- **VRAM visibility**: Intel/UMA iGPU (`VendorId == 0x8086 && DedicatedVideoMemory <= 256MB`) is shown as shared/system-backed GPU memory. Used memory comes from PDH GPU memory counters, capacity/budget from DXGI. The small dedicated aperture (for example 128 MB on Arc 140V) is diagnostic-only, not capacity.
- **Caveat**: Intel iGPUs can report non-zero `DedicatedVideoMemory` ([wgpu issue #683](https://github.com/gfx-rs/wgpu/issues/683)). The VendorId check prevents false positives. A more robust method would be `D3D12_FEATURE_DATA_ARCHITECTURE.UMA` flag but requires creating a D3D12 device.

## D3DKMT GPU Perf Data

`D3DKMTQueryAdapterInfo` with `KMTQAITYPE_ADAPTERPERFDATA` (type 62) reads GPU temperature, fan RPM, and a raw power field directly from the Windows kernel via gdi32.dll. This is the same API Windows Task Manager uses. No vendor SDK, no elevation.

- **Temperature**: stored in deci-Celsius (raw / 10 = °C).
- **Power**: [MS docs say "tenths of percentage of TDP"](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/d3dkmthk/ns-d3dkmthk-_d3dkmt_adapter_perfdata). The overlay does NOT display this as watts. `PowerRaw` is log/diagnostic data only until a validated true-watts source exists.
- **Fan RPM**: direct RPM value.
- **Struct layout**: Uses `LayoutKind.Explicit` with manual `[FieldOffset]` attributes to match `D3DKMT_ALIGN64` padding in the native header.
- **Intel iGPU support**: unknown — the init test-read gracefully handles failure (shows `--`).
- **WDDM 2.4+ required**: works on any modern AMD, Intel, NVIDIA driver.

## UI Layout (updated 2026-04-15 for Plan B companion redesign)

### In-game HUD bar (`Controls/TopBarControl.xaml.cs`)

Component-grouped HUD bar. Metrics grouped by hardware component:

- **FPS group**: FPS + AVG + 1% + 0.1% + sparkline graph. When AMD AFMF or Intel XeFG is active, the FPS cell shows base-over-effective with vendor: `"120/60fps AFMF"` or `"120/60fps XeFG"`. Otherwise it collapses to the single-rate display (`"60fps"`).
- **Frametime**: separate from FPS, own sparkline graph. Label "Frametime" not "FT".
- **CPU**: `CPU 45% 12W` — usage + power inline under one label.
- **GPU**: `GPU 85% 72°C 2400rpm 4.2/8G` — usage + temp + fan + VRAM under one label. GPU watts stay hidden until real.
- **RAM**: `RAM 14.3/31.5G`.
- **System**: `SYS` cluster for device/SoC temp and chassis fan, plus battery icon + value and Hz icon + value (Segoe Fluent Icons).

**Row logic**: Always tries single row first. Measures actual rendered width. Splits to two rows (Row 1: performance, Row 2: hardware) only if content exceeds screen width. No arbitrary thresholds. Three explicit rendering modes (since Plan B 2026-04-15): thin horizontal (default), forced two-row for `TopTall`/`BottomTall`, vertical side-dock for `LeftDock`/`RightDock`. The measured-sizing pattern at `TopBarControl.xaml.cs:283` (`element.Measure(Unbounded)` → `Notify` via `LayoutMetricsChanged`) applies in all three modes.

### HUD anchor positions (`Settings/AppSettings.cs:TopBarPosition`)

Six values (replaced the binary `OverlayEdge` in Plan B):

- `TopThin = 0` — top edge, single-row metrics
- `BottomThin = 1` — bottom edge, single-row metrics
- `TopTall = 2` — top edge, two-row metrics
- `BottomTall = 3` — bottom edge, two-row metrics
- `LeftDock = 4` — left edge, vertical rail at `HhapSideDockWidth` width, full `WorkArea.Height`
- `RightDock = 5` — right edge, same dimensions as LeftDock

The numeric ordering preserves byte-for-byte JSON round-trip for existing saved settings (`TopBarPosition: 0` loads as `TopThin`, `TopBarPosition: 1` loads as `BottomThin`) without a custom converter.

`MainWindow.ApplyPosition(TopBarPosition)` and `ResizeOverlayWindow` switch on all six values; side-dock branches anchor to left/right edges at the token-defined width.

### Companion window (`ControlWindow.xaml` + `Views/CompanionView.xaml`, Plan B 2026-04-15)

`ControlWindow` is a single-surface host. The previous Hub/Support/SettingsSheet/Composer stack was deleted wholesale. The only content is `Views/CompanionView.xaml`, structured as:

1. **Topbar** — mark + wordmark, three state chips (`OVERLAY ON/OFF`, `CAPTURE READY/OFFLINE/LIMITED`, current game title), minimise + close caption buttons. No telemetry values in the topbar.
2. **Toolbar row** — `PositionPillButton` + `FeelAndFitPillButton` as sibling pill buttons. Each owns a `Flyout` (`PositionFlyout` with monitor canvas + 6 anchor buttons; `FeelAndFitFlyout` with bg opacity / text opacity / text size sliders). Shared `HhapFlyoutPresenterStyle`.
3. **Preset row** — `ItemsRepeater` + `UniformGridLayout MinItemWidth="220"` hosts 4 curated preset cards (Minimal / Standard / Advanced / Full). Natural 4 → 2 × 2 reflow at narrow widths via `UniformGridLayout` semantics, not via `VisualStateManager` setters.
4. **Manual link** — quiet `Or build your own ▸ Manual` text that expands `ManualMetricSheet` inline below the preset row. Manual starts as a copy of the currently-active curated preset, seeded in `ControlWindow.OnManualRequested` **before** the `ActivePreset = Custom` switch.
5. **Event line** — muted mono strip (`Consolas`) showing `timestamp · pid · exe · frametime · sample rate`.
6. **Support footer** — three band-dot status chips (Capture / Game detect / Companion) + four outline action buttons (Copy support report / Open log folder / handheldally.com / Community) + a quiet attribution line linking to `handheldally.com/premium`. No tipping surface inside the product.

`VisualStateManager` groups handle density changes across `Lg (1600+) / Md (1280+) / Sm (1024+) / Xs (0+)` tiers. Order is **top-down from largest `MinWindowWidth` to smallest** — reversing the order is a first-match-wins bug. `Xs` state provides short event-line text, compact support actions layout, and font-size step-down.

`CompanionViewModel` wraps `OverlayViewModel` (not inherits). It holds the HUD VM as a property reference and exposes UI-only state (`IsFeelAndFitOpen`, `IsPositionFlyoutOpen`, `IsManualSheetOpen`, `EventLineShort`). Event subscriptions are torn down symmetrically in `Unloaded` / `Closed` per `best_practices/winui3_overlay.md:133`.

### Fonts (Plan B 2026-04-15)

- `Segoe UI` for UI text
- `Segoe Fluent Icons` for glyphs
- `Consolas` for `EventLine` and any mono preview text

No bundled fonts. `Assets/Fonts/` directory does not exist. If `Inter` / `JetBrains Mono` / `Space Grotesk` bundling becomes a requirement later, it's a separate milestone with its own MSIX packaging verification.

### `HotkeyService` architecture

`Hotkeys/HotkeyService.cs` is **intentionally launch-based**, not Win32 `RegisterHotKey`. The user maps a handheld button to **launch `HHAPulse.Overlay.exe`** with one of five command args (`--toggle`, `--next-preset`, `--off`, `--menu` / `--settings`, or default). The launcher writes the command to `%LOCALAPPDATA%\HHAPulse\overlay-command.txt`, signals a named event `Local\HHAPulse_Overlay_Toggle` that the running instance is listening on, and exits. The running instance's listener dispatches the command on the WinUI dispatcher queue via `App.HandleLaunchCommand`.

Rationale (locked in spec §5.7.1): resilient to sleep/resume by design (no hotkey to re-register), resilient to fullscreen-exclusive games (a new process launch cannot be blocked), resilient to focus steal (no global hotkey for another process to grab). Do not replace with `RegisterHotKey` without reading the spec's architectural justification.

## Presets (updated 2026-04-15 for Plan B)

`OverlayPreset` enum values and display names:

| Enum value | UI label | Metric set | Count |
|---|---|---|---|
| `OverlayPreset.Minimal` (0) | `Minimal` | `Fps, Battery` | 2 |
| `OverlayPreset.Standard` (1, default) | `Standard` | `Fps, OnePercentLow, FrameTime, CpuUsage, GpuUsage, Battery` | 6 |
| `OverlayPreset.Tuner` (2) | `Advanced` (renamed from `Tuner`) | `Fps, OnePercentLow, FrameTime, CpuUsage, CpuTemp, CpuPower, GpuUsage, GpuTemp, Ram, Battery, RefreshRate` | 11 |
| `OverlayPreset.Custom` (3) | `Manual` (renamed from `Custom`) | `settings.EnabledMetricIds`, seeded from the currently-active curated preset when Manual is first opened | ≤ 19 |
| `OverlayPreset.Off` (4) | `Off` | `Array.Empty<string>()` | 0 |
| `OverlayPreset.Full` (5, new in Plan B) | `Full` | `Fps, AvgFps, OnePercentLow, ZeroPointOneLow, FrameTime, CpuUsage, CpuTemp, CpuPower, GpuUsage, GpuTemp, GpuClock, GpuPower, GpuFan, Ram, Vram, TotalPower, DeviceTemp, RefreshRate, Battery` | 19 |

Cycle: Minimal → Standard → Advanced → Full → Off → Minimal. Manual is manual-only (not in the cycle).

**Enum values `Tuner = 2` and `Custom = 3` are preserved from the pre-Plan-B schema** — only the UI display names changed. Existing saved settings round-trip without migration.

`OverlayPreset.Full = 5` reserves `Off = 4` so existing `Off` settings continue to deserialize correctly.

## IPC

- Overlay/widget telemetry pipe: `LOCAL\HHAPulse`.
- Capture metrics pipe: `LOCAL\HHAPulse.Capture.Out`.
- Capture control pipe: `LOCAL\HHAPulse.Capture.Control`.

## Settings

Persisted to `%LOCALAPPDATA%\HHAPulse\settings.json`. Includes:
- Active preset, enabled metric IDs (for Custom).
- Background opacity, text opacity (user-adjustable via Control Window sliders).
- Show mode (Always / In-game only).
- Text size in pixels (user-adjustable via slider, 10-22px).
- `SettingsService` handles empty/corrupt JSON files gracefully (falls back to defaults).

## Known Issues (as of 2026-04-09)

- **C++ build not yet compiled** — VS 2026 Insiders PlatformToolset mismatch being resolved. NativeTelemetry.cpp reviewed line-by-line but needs one honest `msbuild` + `dumpbin /exports` check.
- **Hardware validation pending** — Need to test on Lenovo Legion Go (AMD ADLX path) and MSI Claw (Intel IGCL path). NVIDIA path verified via NuGet API audit only.
- **EMI IOCTL hex values** — Computed from CTL_CODE formula (FILE_DEVICE_UNKNOWN, functions 0-3, METHOD_BUFFERED, FILE_READ_ACCESS). Self-consistent but not yet verified against actual `emi.h` on a machine with Windows SDK headers.
- **VRR on NVIDIA desktop** — Any topmost HWND (our overlay, Discord, etc.) forces Composed Flip, may break G-Sync in borderless windowed. AMD/Intel FreeSync unaffected. Not our bug, not fixable.
- **Anti-cheat caveats** — BattlEye safe. EAC is game-dependent. Vanguard is risky. No injection/hooks in our code.

Frame-generation detection note: `IntelPresentMonFrameTypeEvidence` currently uses `PayloadByName("FrameType")` only. Real hardware validation is still required to prove that TraceEvent exposes named payload fields for the provider on driver-instrumented systems.

## Audit trail

- Full audit: `memory-bank/2026/04/09/audit-report-HHAP-0.25.md`
- Research: `memory-bank/2026/04/09/gpu-telemetry-investigation.md`
- Day log: `memory-bank/2026/04/09/INDEX.md`
