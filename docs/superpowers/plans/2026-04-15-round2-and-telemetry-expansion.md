# HHA Pulse — Spec Round-2 Reality Pass + Telemetry Expansion

**Date:** 2026-04-15
**Targets (this plan):**
- `/Users/johny/Handheld Pulse/docs/superpowers/specs/2026-04-15-hha-pulse-ux-redesign-design.md` (Part A — four reviewer findings)
- `/Users/johny/Handheld Pulse/memory-bank/systems/telemetry-trace-map.md` (Part B — new metric sections)
- `/Users/johny/Handheld Pulse/CLAUDE.md` (Part B — metric policy decomposition)
- `src/HHAPulse.Shared/Models/*` (Part B — `TelemetrySnapshot` extensions)
- `src/HHAPulse.Overlay/Settings/OverlayPresetCatalog.cs` (Part B — new metric IDs)
- `src/HHAPulse.Overlay/Collectors/Storage/*` (Part B — new)
- `src/HHAPulse.Overlay/Collectors/Cpu/*` (Part B — new CPU clock collector)
- `src/HHAPulse.Overlay/Collectors/Npu/*` (Part B — new NPU detection)
- `src/HHAPulse.Overlay/Helpers/MetricFormatterCompact.cs` (Part B — new compact formatters)
- `src/HHAPulse.Overlay/Controls/TopBarControl.xaml.cs` (Part B — new builders)
- `src/HHAPulse.CaptureService/*` (Part B — elevated WMI reliability path)
- `tests/HHAPulse.Shared.Tests/*` (Part B — new contract/migration tests)
- `tests/HHAPulse.Overlay.Tests/*` (Part B — new formatter/collector tests)

**Research grounding (both reports read first-hand, 2026-04-15):**
- `docs/superpowers/research/windows-platform-april2026.md` — Microsoft Learn + Windows Insider April 2026 source-of-truth
- `docs/superpowers/research/vendor-telemetry-april2026.md` — Intel / AMD / Qualcomm vendor surface

---

## Context

**The reviewer rejected the Round-1 spec reality pass with four concrete findings**, all verified against the spec file and all genuine misses by the spec-editor teammate:

1. **High: stale battery/power availability policy.** §5.5 *"Metric availability gate"* at L396-398 still says `"battery charge rate W, battery time remaining — ship as -- until the Battery collector surfaces real values"` and `"Temperature, TDP/power, fan, latency, frame generation stay hidden across all presets"`. Both contradict the §5.6 rewrite and the 2026-04-10 telemetry delta at `telemetry-trace-map.md:15-35`. The spec-editor forgot to update the Metric availability gate when they rewrote §5.6.
2. **Medium: Fix C incomplete.** "1:1 with Valve" still appears at L96 (`§2b.1 finding #3 design implication`) and L380 (`§5.5 Mapping rationale`). Fix C softened §2b.5 only.
3. **Medium: grep acceptance gate not actually met.** `"fact strip"` still appears at L280 (`"No live fact strip anywhere in the companion"`) and L333 (`"No hero, no fact strip"`) — they're denial sentences, but the acceptance gate said zero occurrences, not "zero unintended occurrences".
4. **Medium: success / non-success contradiction.** L384 labels the Manual link *"Or build your own ▸ Manual"*; L609 lists *"A reviewer clicking something labelled 'Build your own'"* as a non-success failure mode. One must change.

**Plus the user's telemetry expansion ask** (verbatim):
> *"ok plan this pls and properly so we would have everything fully operational do not assume based on [facts] and research like intel amd and each npu have specific specs that are reachable and we going by tha not by vibe or outdated data it is april 2026 and even windows is more open now and provides more data like i want as well SSD temperature to be there if users whant to see how the cores working like MGHZ and so on detailed and well desinged based on the lates we did"*

Two research agents returned citation-grounded April-2026 surveys. The short version:

- **SSD temperature is cleanly shippable.** `IOCTL_STORAGE_QUERY_PROPERTY` + `StorageDeviceTemperatureProperty` returning `STORAGE_TEMPERATURE_DATA_DESCRIPTOR` is documented on Microsoft Learn, user-mode, no admin, MSIX-safe, works on Windows 10+. Ship it. (`learn.microsoft.com/en-us/windows/win32/api/winioctl/ns-winioctl-storage_temperature_data_descriptor`)
- **SSD wear / reliability lives in `MSFT_StorageReliabilityCounter`** (`root\Microsoft\Windows\Storage` WMI). Documented fields include `Temperature`, `TemperatureMax`, `Wear` (percentage used), `PowerOnHours`, `Read/Write/FlushLatencyMax`, error counters. (`learn.microsoft.com/en-us/windows-hardware/drivers/storage/msft-storagereliabilitycounter`) MSIX AppContainer access is **unverified** — routing through the elevated capture service is the safer architectural choice.
- **CPU per-core live throttle-aware frequency is NOT freely available** on April 2026 Windows. `CallNtPowerInformation(ProcessorInformation)` returning `PROCESSOR_POWER_INFORMATION[]` is documented and user-mode, but per Microsoft's own `Windows-Dev-Performance` GitHub issue #100, `CurrentMhz` *"now always equals MaxMhz on modern Windows 11"* — it cannot detect throttling. PDH `\Processor Information(*)\Processor Frequency` returns nominal/requested, not live. **This is a hard platform limitation, not a vibe.** Vendor paths that do give live frequency require elevation (Intel PCM) or vendor-SDK redistribution with licensing complications (AMD RyzenMaster, Qualcomm Snapdragon Profiler).
- **NPU utilization is NOT a public API.** Windows 11 24H2+ Task Manager shows NPU utilization (builds `26220.8138` / `26300.8142`), but the counter is internal. DXCore enumerates NPU adapters via `IDXCoreAdapterFactory1::CreateAdapterListByWorkload(MachineLearning, NPU)` — detection works, utilization does not. Cross-vendor: same answer for Intel AI Boost, AMD XDNA, Qualcomm Hexagon. *"Advocate for public ETW provider GUIDs in Windows 11 25H2+"* per the vendor research report.
- **April 2026 Windows KB5083769** adds **zero** new telemetry APIs. It's a maintenance release. "Windows is more open now" is aspirational, not backed by new APIs in this specific release.

**Consequence for the telemetry expansion:** we can ship SSD temperature honestly, SSD wear via the capture service, aggregate + per-core nominal CPU clock (with an explicit honesty label about throttle-awareness), and NPU **present/absent** detection. We cannot ship live throttle-aware per-core MHz via universal Windows APIs, and we cannot ship NPU utilization. The spec and `CLAUDE.md` must say so out loud.

This plan is split into two parts executed in order:

- **Part A (Round-2 Spec Reality Pass)** — the four reviewer-found fixes. Small, scoped to the one spec file. Teammate Alpha-2 runs this.
- **Part B (Telemetry Expansion)** — the grounded-in-research feature addition. Touches Shared models, overlay collectors, capture service, formatters, preset catalog, HUD builder, companion NPU card, memory-bank trace map, and CLAUDE.md. Decomposed into three subparts (data layer, display layer, tests + validation) for parallel teammate execution.

---

## Part A — Round-2 Spec Reality Pass (four reviewer fixes)

### Fix R2-A — High: delete the stale Metric availability gate (L396-398)

**Current spec text** (verified by me at `docs/superpowers/specs/2026-04-15-hha-pulse-ux-redesign-design.md:396-398`):

```markdown
**Metric availability gate:**
- The Level 3 / Level 4 presets declare metrics that are not yet wired up (`battery charge rate W`, `battery time remaining`) — ship them as `--` until the Battery collector surfaces real values. CLAUDE.md already requires this for missing data.
- Temperature, TDP/power, fan, latency, frame generation stay hidden across **all** presets including Manual until real data exists. This is the core product rule from CLAUDE.md and §2b.3 — it is what separates HHA Pulse from vendor utilities that fabricate values.
```

**Both bullets are stale.** Bullet 1 contradicts §5.6 and `BatteryCollector.cs:40-55` (battery rate and time are already wired). Bullet 2 contradicts the `telemetry-trace-map.md:15-35` 2026-04-10 delta (CPU temp, MSI fan, MSI device temp, AMD/Intel/NVIDIA GPU power are all operational).

**Replacement** (the spec-editor writes exactly this):

```markdown
**Metric availability gate:**
- The Level 3 / Level 4 presets declare metrics whose availability depends on hardware, not on a future collector. Battery percent + charge rate + time remaining are **already live** via `BatteryCollector.cs:20-55` and `MetricFormatterCompact.FormatBatteryCompact()` at `:196-234` (see §5.6). CPU temperature (WMI `MSAcpi_ThermalZoneTemperature`), CPU package power (EMI), and system RAM are **universal**. MSI chassis fan RPM and MSI device/SoC temperature are **MSI hardware-gated** per the 2026-04-10 delta at `telemetry-trace-map.md:22-23`. AMD/Intel/NVIDIA GPU power/temp/clock/fan are **vendor-SDK-gated** (ADLX / IGCL / NVAPI) — operational where the SDK reports a value, `--` where it does not.
- Metrics that stay hidden because no real data source exists: `input_latency` (reserved in `OverlayPresetCatalog.cs:11` but never in a preset), numeric frame-generation FPS (detection-only via Intel-PresentMon ETW — `IntelPresentMonFrameTypeEvidence`), and Intel Lunar Lake / Arc 140V GPU temperature (the path is explicitly unavailable on that hardware per `telemetry-trace-map.md:27-28`, not a policy).
- The CLAUDE.md missing-data rule still applies: when a hardware-gated metric does not return a value, render `--` — never fabricate.
- **Cross-reference:** §9 "CLAUDE.md — rule revision" decomposes this policy into a per-metric statement; the gate above is the spec-internal summary.
```

**Acceptance:** both stale bullets are deleted, replacement text lands, §5.6 and §9 stay internally consistent.

### Fix R2-B — Medium: complete Fix C (remove "1:1 with Valve" at L96 and L380)

**Current spec text at L96** (`§2b.1 finding #3 design implication`):

```markdown
**Design implication:** the handheld community already has a mental model called "Levels". HHA Pulse's 4 curated presets should map 1:1 to Level 1 → Level 4 so users instantly understand what they're picking. See §5.5.
```

**Replacement at L96:**

```markdown
**Design implication:** the handheld community already has a mental model called "Levels". HHA Pulse's 4 curated presets mirror the Valve Level 1→4 density gradient so Steam Deck users recognise the shape instantly. It is a spiritual parallel, not a literal clone — HHA Pulse's Standard drops the Gamescope marker (Linux-only) and adds battery charge rate per g-helper #3620 (§2b.5). See §5.5.
```

**Current spec text at L380** (`§5.5 Mapping rationale`):

```markdown
**Mapping rationale:** this is 1:1 with the Valve Steam Deck performance overlay levels users already know. A user coming from Steam Deck will instantly understand what each preset does. A user new to handhelds will read the names left-to-right and get a clean density gradient.
```

**Replacement at L380:**

```markdown
**Mapping rationale:** the preset names mirror Valve's Steam Deck Level 1-4 density gradient so Steam Deck users recognise the shape. The metric sets themselves are not literal copies of Valve's — Standard drops Gamescope (Linux-only), Advanced reflects HHA Pulse's 2026-04-10 telemetry delta (CPU temp + CPU power on all handhelds), and Full is HHA Pulse's kitchen sink across 19 metrics from `OverlayPresetCatalog.AllMetricIds`. Users new to handhelds read the names left-to-right and get a clean density gradient.
```

**Acceptance:** a final grep for `1:1 with Valve` returns zero hits.

### Fix R2-C — Medium: rephrase "fact strip" denials so the literal phrase disappears

**Current L280:**

```markdown
- **No live fact strip anywhere in the companion.** There is no live metric display anywhere in the companion window. Users who want numbers always-visible turn on the HUD itself.
```

**Replacement at L280:**

```markdown
- **No live metric readout anywhere in the companion window.** Every earlier round tried to bolt a numeric strip or preview card onto the companion surface; all of them are deleted. Users who want numbers always-visible turn on the HUD itself. The companion is a configuration surface, not a second display.
```

**Current L333** (inside the `§5.3` file table row for `Views/CompanionView.xaml`):

```markdown
| `Views/CompanionView.xaml` + `.cs` | **new** | The single-surface page: topbar + toolbar row (Position + Feel-&-fit pill buttons) + preset row (4 cards with mini HUD previews) + Manual link + event line + support footer. No hero, no fact strip. Replaces HubView + SettingsSheet + SupportView + ComposerOverlay. |
```

**Replacement at L333:**

```markdown
| `Views/CompanionView.xaml` + `.cs` | **new** | The single-surface page: topbar + toolbar row (Position + Feel-&-fit pill buttons) + preset row (4 cards with mini HUD previews) + Manual link + event line + support footer. No hero headline. No live-metric readout. Replaces HubView + SettingsSheet + SupportView + ComposerOverlay. |
```

**Acceptance:** a final grep for `fact strip` returns zero hits anywhere in the spec.

### Fix R2-D — Medium: fix the Manual label vs non-success contradiction

The original non-success bullet was written against the Composer wizard (deleted). Round 05 deliberately reclaimed the phrase *"Or build your own"* as the Manual link label. The ban must be narrowed, not the link renamed (Johny validated the label in the mockup).

**Current L609:**

```markdown
- A reviewer clicking something labelled "Customize" or "Build your own".
```

**Replacement at L609:**

```markdown
- A reviewer clicking something labelled "Customize" or opening a wizard-style multi-step setup. The Round 05 "Or build your own ▸ Manual" link is **not** the failure mode — it's a secondary entry that reveals an inline toggle sheet on the same surface, per §5.5.2. The failure mode is a separate wizard drawer or a multi-step composer like the deleted `ComposerOverlay.xaml`.
```

**Acceptance:** §11 non-success list is internally consistent with §5.5's Manual link design; Manual link label stays unchanged at L384.

### Part A verification grep (must return zero hits, including inside denials)

```
grep -nE '1:1 with Valve|fact strip|HudLayout enum|BatteryPercent.*MetricId|GetSystemPowerStatus|CAPTURE LIVE chip|HeroHeadline|HeroHost|HeroPickerGrid|FactStrip\.ItemsPanel|Round 03 comp|PositionPicker\.xaml|180 dip|"24 18 24 18"|30 × 30' docs/superpowers/specs/2026-04-15-hha-pulse-ux-redesign-design.md
```

If any line matches, the fix is incomplete. The rewrite of denials must avoid the literal banned phrase entirely — this is a stricter interpretation than Round 1 (which permitted "legitimate denials") and is the Round-2 acceptance contract.

---

## Part B — Telemetry Expansion (research-grounded)

### Part B source of truth

Every design decision below cites one of the following first-hand-read files:

| Source | Purpose |
|---|---|
| `docs/superpowers/research/windows-platform-april2026.md` | Microsoft Learn + Windows Insider April 2026 platform APIs |
| `docs/superpowers/research/vendor-telemetry-april2026.md` | Intel / AMD / Qualcomm vendor surface |
| `memory-bank/systems/telemetry-trace-map.md` | Current HHA Pulse pipeline state (the 2026-04-10 delta) |
| `memory-bank/best_practices/collectors_pinvoke.md` | Existing P/Invoke patterns in the repo |
| `memory-bank/best_practices/winui3_overlay.md` | Measured-sizing pattern for HUD |
| `memory-bank/best_practices/code_signing.md` | Store/MSIX packaging guidance |
| `src/HHAPulse.Overlay/Settings/OverlayPresetCatalog.cs` | Current preset catalog (read first-hand) |
| `src/HHAPulse.Overlay/Collectors/Battery/BatteryCollector.cs` | Pattern for a collector with elevated fallback |
| `src/HHAPulse.Overlay/Controls/TopBarControl.xaml.cs` | `RebuildMetricViews` insertion point for new builders |
| `src/HHAPulse.Overlay/Helpers/MetricFormatterCompact.cs` | Pattern for compact formatter additions |

No claim in Part B is based on training-data recall. If a claim cannot be traced to one of the files above, it is marked `(assumed — needs verification on hardware)` and goes into the open-items list.

### Part B metric inventory — what ships, what's deferred, what's never-faked

| New metric | MetricId key | Source API | Host | Status | Preset inclusion |
|---|---|---|---|---|---|
| SSD temperature (°C) | `storage_temp` | `IOCTL_STORAGE_QUERY_PROPERTY` + `StorageDeviceTemperatureProperty` → `STORAGE_TEMPERATURE_DATA_DESCRIPTOR` | **Overlay** (user-mode, no admin) | **Ship** | Full only |
| SSD wear (% used) | `storage_wear` | `MSFT_StorageReliabilityCounter.Wear` (`root\Microsoft\Windows\Storage` WMI) | **Capture service** (elevated, for AppContainer safety) | **Ship** | Full only |
| Aggregate CPU effective clock (GHz) | `cpu_clock` | PDH `\Processor Information(_Total)\Processor Frequency` | **Overlay** | **Ship** with honesty label | Advanced + Full |
| Per-core CPU clock (MHz, nominal) | `cpu_per_core_mhz` | `CallNtPowerInformation(ProcessorInformation)` → `PROCESSOR_POWER_INFORMATION[]` | **Overlay** | **Ship** as companion diagnostics only (not HUD) | Companion-only |
| NPU adapter present | `npu_present` | `IDXCoreAdapterFactory1::CreateAdapterListByWorkload(MachineLearning, NPU)` + `DXCoreAdapterProperty::DriverDescription` | **Overlay** | **Ship** as detection boolean + adapter name in companion card | Companion-only — **no HUD chip** |
| NPU utilization (%) | `npu_usage` | **None** — no public API exists in April 2026 Windows | — | **Deferred** — HUD stays `--` / companion shows "utilization unavailable on current Windows" | Never |
| CPU live throttle-aware per-core frequency | — | Intel PCM (elevation + license), AMD RyzenMaster SDK (license unclear), Qualcomm Snapdragon Profiler (not redistributable) | — | **Deferred** — universal Windows path is nominal-only per `Windows-Dev-Performance` issue #100 | Never in v1 |

**Honesty rule for `cpu_clock`:** the HUD label is `CPU ~3.2GHz` (with the `~` prefix) to signal to attentive users that this is an aggregate/nominal reading, not a live turbo-accurate measurement. The full explanation lives in the companion diagnostics card (§ Part B display-layer below) and in `telemetry-trace-map.md`. The user asked for *"how the cores working like MGHZ"* — the honest answer is that April 2026 Windows gives us aggregate-effective via PDH and per-core-nominal via `PROCESSOR_POWER_INFORMATION`, and anything more accurate requires vendor SDKs we are not shipping. We surface what's honest and label it honestly.

### Part B — B.1 Data layer changes

**New `TelemetrySnapshot` sections** in `src/HHAPulse.Shared/Models/`:

```csharp
public sealed class StorageMetrics
{
    public double? TemperatureCelsius { get; set; }          // from IOCTL
    public double? TemperatureMaxCelsius { get; set; }       // from IOCTL (informational)
    public byte? WearPercentUsed { get; set; }               // from MSFT_StorageReliabilityCounter
    public uint? PowerOnHours { get; set; }                  // from MSFT_StorageReliabilityCounter
    public string? DeviceModel { get; set; }                 // for companion card display
    public StorageReliabilityState Reliability { get; set; } // enum: Unknown, Healthy, Warning, Failed
}

public sealed class CpuDetailMetrics
{
    public double? AggregateEffectiveMhz { get; set; }       // PDH _Total Processor Frequency
    public IReadOnlyList<ProcessorCorePower>? PerCoreNominal { get; set; } // CallNtPowerInformation
    // PerCoreNominal is null when collector hasn't run; empty list when no cores reported.
}

public sealed class ProcessorCorePower
{
    public int CoreIndex { get; set; }
    public uint CurrentMhz { get; set; }  // from PROCESSOR_POWER_INFORMATION.CurrentMhz
    public uint MaxMhz { get; set; }      // .MaxMhz
    public uint MhzLimit { get; set; }    // .MhzLimit
}

public sealed class NpuMetrics
{
    public bool Present { get; set; }
    public string? AdapterName { get; set; }          // e.g. "Intel AI Boost", "AMD Ryzen AI", "Qualcomm Hexagon NPU"
    public NpuVendor Vendor { get; set; }             // enum: Unknown, Intel, Amd, Qualcomm, Other
    public string? DriverDescription { get; set; }    // raw DXCore DriverDescription for diagnostics
    // Note: no Utilization field. Windows April 2026 does not expose this publicly (see telemetry-trace-map.md §NPU).
}
```

**`MetricFlags` additions** in the same project:

```csharp
[Flags]
public enum MetricFlags : ulong
{
    // ... existing ...
    StorageTemperature = 1UL << 28,  // next free bit — verify against existing definition
    StorageWear        = 1UL << 29,
    CpuClock           = 1UL << 30,
    NpuPresent         = 1UL << 31,
}
```

Bit positions must be verified against the real `MetricFlags` enum in the repo — the values above are illustrative, not authoritative. **The code-fixer teammate opens the real file, picks the next free bits, and commits the additions.**

**`OverlayPresetCatalog.cs` extensions** (additive to the existing extension list in the spec §5.1):

```csharp
// Storage
public const string StorageTemp = "storage_temp";
public const string StorageWear = "storage_wear";

// CPU aggregate (per-core lives in companion diagnostics, not a MetricId)
public const string CpuClock = "cpu_clock";

// NPU (companion-only; not expected to appear in HUD presets, but the ID exists for diagnostics)
public const string NpuPresent = "npu_present";
```

Add to `AllMetricIds` array in the order: Performance, CPU, GPU, Memory, Storage, System, Sensors.

Extend the `FullMetrics` array (which is added in the Round-1 spec §5.1 extension list) to include `StorageTemp`, `StorageWear`, `CpuClock`. **Full preset count becomes 22** (19 from Round-1 Fix F step 2 + 3 new). Verify the `md-720` vertical-fold math in the spec's §4.1 "above-the-fold promise" still holds after this bump.

Add `CpuClock` to the **Advanced** preset as well (Advanced goes from 11 → 12 metrics).

**Manual picker domain-grouping** (referenced in the GPT plan and kept as a good UX idea):

In `Controls/ManualMetricSheet.xaml`, group the toggle chips by domain:
- Performance: `Fps`, `AvgFps`, `OnePercentLow`, `ZeroPointOneLow`, `FrameTime`
- CPU: `CpuUsage`, `CpuTemp`, `CpuPower`, `CpuClock`
- GPU: `GpuUsage`, `GpuTemp`, `GpuClock`, `GpuPower`, `GpuFan`
- Memory: `Ram`, `Vram`
- Storage: `StorageTemp`, `StorageWear`
- System: `TotalPower`, `DeviceTemp`, `RefreshRate`, `Battery`
- Sensors/NPU (companion-only badge, not a HUD toggle): `NpuPresent` as informational

Grouping is visual only; persistence remains `settings.EnabledMetricIds` as a flat ordered list.

**New collector files:**

#### `Collectors/Storage/StorageTempCollector.cs` (overlay, user-mode)

- Uses `DeviceIoControl` with `IOCTL_STORAGE_QUERY_PROPERTY` and `StorageDeviceTemperatureProperty` (`STORAGE_PROPERTY_ID`).
- Enumerates physical drives via `\\.\PhysicalDrive0`, `\\.\PhysicalDrive1`, … up to a configurable max (default 4).
- For each drive: open handle with `FILE_READ_ATTRIBUTES` (no `GENERIC_READ` needed), send the query, marshal `STORAGE_TEMPERATURE_DATA_DESCRIPTOR`, convert `Temperature` field (signed 16-bit °C) to double.
- Picks the **primary / system drive** for the HUD metric (the drive containing `%SystemDrive%`).
- Populates `snapshot.Storage.TemperatureCelsius` and `snapshot.Storage.TemperatureMaxCelsius`.
- Fails-closed: if `DeviceIoControl` returns zero or `dataDescriptor.Size == 0`, leaves `TemperatureCelsius` null and logs `"storage temp unavailable on primary drive — IOCTL returned no data"`.
- Writes an `MeasurementTraceRecorder.Record()` entry matching the existing BatteryCollector pattern at `BatteryCollector.cs:241-260`.

Source: `windows-platform-april2026.md` §1 IOCTL section; Microsoft Learn at `learn.microsoft.com/en-us/windows/win32/api/winioctl/ns-winioctl-storage_temperature_data_descriptor`.

#### `Collectors/Storage/StorageReliabilityCollector.cs` (**capture service**, elevated)

- Lives in `src/HHAPulse.CaptureService/Collectors/` (new subdirectory if needed).
- Uses `System.Management.ManagementObjectSearcher` against `\\.\ROOT\Microsoft\Windows\Storage:MSFT_StorageReliabilityCounter`.
- Queries: `SELECT Wear, PowerOnHours, Temperature, TemperatureMax, StartStopCycleCount, ReadErrorsTotal, WriteErrorsTotal FROM MSFT_StorageReliabilityCounter`.
- Correlates each result to a `MSFT_PhysicalDisk` via `Get-Related` semantics to pull `FriendlyName`, `MediaType`, `BusType` for the device model.
- Publishes `Wear` (and optionally `Temperature` as a redundant fallback for the IOCTL-based path) through the existing capture service IPC channel to the overlay.
- The reliability counter is hardware-support-gated: NVMe consumer SSDs populate reliably; SATA and USB external enclosures do not. Fail-closed when a field is absent; record `"unavailable on this device"` in the trace.

Source: `windows-platform-april2026.md` §1 `MSFT_StorageReliabilityCounter`; Microsoft Learn at `learn.microsoft.com/en-us/windows-hardware/drivers/storage/msft-storagereliabilitycounter`.

**Why capture service, not overlay:** the `root\Microsoft\Windows\Storage` WMI namespace has unverified MSIX AppContainer compatibility per the research. Routing through the already-elevated capture service sidesteps the question.

#### `Collectors/Cpu/CpuClockCollector.cs` (overlay, user-mode)

Two paths in one collector:

1. **Aggregate via PDH**: open `\Processor Information(_Total)\Processor Frequency` counter via `PdhOpenQuery` / `PdhAddCounter`, call `PdhCollectQueryData` once per snapshot, read `PDH_FMT_DOUBLE` → `AggregateEffectiveMhz`. Convert MHz → GHz for display.
2. **Per-core nominal via `CallNtPowerInformation(ProcessorInformation)`**: returns a `PROCESSOR_POWER_INFORMATION[]` of length `GetActiveProcessorCount(ALL_PROCESSOR_GROUPS)`. For each logical processor, marshal `CurrentMhz`, `MaxMhz`, `MhzLimit` into a `ProcessorCorePower` and populate `snapshot.CpuDetail.PerCoreNominal`.

**Honesty label** written into `snapshot.CpuDetail`:
- If `AggregateEffectiveMhz > 0` from PDH, use it for the HUD aggregate reading.
- If the per-core `CurrentMhz` from `CallNtPowerInformation` always equals `MaxMhz` for every core (the known modern-Win11 behavior), record a diagnostic note `"live throttle-aware per-core frequency unavailable on this Windows build; PROCESSOR_POWER_INFORMATION returns nominal MaxMhz"` so diagnostics make the limitation explicit.

Source: `windows-platform-april2026.md` §2 CPU frequency section; Microsoft Learn at `learn.microsoft.com/en-us/windows/win32/api/powerbase/nf-powerbase-callntpowerinformation` and `learn.microsoft.com/en-us/windows/win32/power/processor-power-information-str`.

#### `Collectors/Npu/NpuDetectionCollector.cs` (overlay, user-mode)

- `CoCreateInstance(CLSID_DXCoreAdapterFactory, IID_IDXCoreAdapterFactory1)`.
- `CreateAdapterListByWorkload(DXCoreWorkload::MachineLearning, DXCoreRuntimeFilterFlags::None, DXCoreHardwareTypeFilterFlags::NPU, &list)`.
- Iterate the list; for each adapter query `DXCoreAdapterProperty::DriverDescription` and `HardwareID`.
- Classify vendor from the description:
  - Contains "Intel" and ("AI Boost" or "NPU") → `NpuVendor.Intel`, adapter name `"Intel AI Boost"`
  - Contains "AMD" and ("Ryzen AI" or "XDNA") → `NpuVendor.Amd`, adapter name `"AMD Ryzen AI"`
  - Contains "Qualcomm" or "Snapdragon" or "Hexagon" → `NpuVendor.Qualcomm`, adapter name `"Qualcomm Hexagon NPU"`
  - Otherwise → `NpuVendor.Other`, adapter name = raw driver description
- Populate `snapshot.Npu.Present = true`, `snapshot.Npu.AdapterName`, `snapshot.Npu.Vendor`, `snapshot.Npu.DriverDescription`.
- If `CreateAdapterListByWorkload` returns zero adapters, `snapshot.Npu.Present = false`.
- **No utilization query.** The DXCore property for NPU utilization does not exist in April 2026 per `windows-platform-april2026.md` §3 — if it lands in a future Windows build, this collector is the place to add it.
- Runs once per overlay startup (not on every snapshot tick) since adapter topology doesn't change at runtime.

Source: `windows-platform-april2026.md` §3 DXCore NPU enumeration; `vendor-telemetry-april2026.md` (F).

### Part B — B.2 Display layer changes

**New compact formatters** in `Helpers/MetricFormatterCompact.cs`:

```csharp
public static string FormatStorageTempCompact(TelemetrySnapshot snapshot)
{
    if (!snapshot.AvailableMetrics.HasFlag(MetricFlags.StorageTemperature) ||
        snapshot.Storage.TemperatureCelsius is not double c)
        return "SSD --";
    return $"SSD {c:0}°C";
}

public static string FormatStorageWearCompact(TelemetrySnapshot snapshot)
{
    if (!snapshot.AvailableMetrics.HasFlag(MetricFlags.StorageWear) ||
        snapshot.Storage.WearPercentUsed is not byte wear)
        return "SSD --";
    return $"SSD {wear}%";
}

public static string FormatCpuClockCompact(TelemetrySnapshot snapshot)
{
    if (!snapshot.AvailableMetrics.HasFlag(MetricFlags.CpuClock) ||
        snapshot.CpuDetail.AggregateEffectiveMhz is not double mhz)
        return "CPU --";
    return mhz >= 1000.0
        ? $"CPU ~{mhz / 1000.0:0.00}GHz"
        : $"CPU ~{mhz:0}MHz";
}
```

The `~` prefix on `CpuClock` is the honesty signal. It matches how the compact formatter handles AC/discharge in battery at `MetricFormatterCompact.cs:206` — a single-character hint that carries meaning without explaining it in the HUD itself.

Route the new formatters through `MetricFormatterCompact.FormatValue(...)`'s switch at `:107-108` (same pattern as battery).

**`TopBarControl.RebuildMetricViews` extensions** at `Controls/TopBarControl.xaml.cs:239-310`:

- Add `BuildStorage(HashSet<string> ids)` that conditionally emits the `storage_temp` / `storage_wear` tiles if the metric IDs are active and the snapshot has the flags.
- Extend `BuildHw` (the CPU component builder at roughly `:271-274` per the Round-1 research) to emit the `cpu_clock` tile alongside `cpu`/`cpu_temp`/`cpu_power`.
- Call `AddComp(hw, BuildStorage(ids))` from `RebuildMetricViews` at the end of the `hw` group construction (`:272-274`).

The existing two-row auto-split at `:287-303` handles the extra tiles without code changes; the measured-width pattern (`element.Measure(Unbounded)` at `:283`) continues to work.

**Companion NPU card** — new XAML user control `Controls/NpuStatusCard.xaml` shown only in the support footer's advanced diagnostics region (NOT inside the main work area per our §4 IA):

```
┌─────────────────────────────────────────┐
│ NPU                                      │
│ ● Intel AI Boost  (or "Not detected")    │
│ Utilization: unavailable on Windows      │
│              April 2026 (no public API)  │
└─────────────────────────────────────────┘
```

The card is collapsed by default; expands when the user clicks an `NPU` chip in the support states row. This avoids cluttering the primary companion surface while still acknowledging the NPU to users on NPU-capable handhelds.

### Part B — B.3 Tests + hardware validation

**Contract tests** in `tests/HHAPulse.Shared.Tests/`:

- `TelemetrySnapshotSerializationTests.StorageMetrics_round_trips()` — MessagePack serialization of the new `StorageMetrics` section, including null-temperature / null-wear cases.
- `TelemetrySnapshotSerializationTests.CpuDetailMetrics_round_trips()` — with 0, 1, 8, 16 per-core entries.
- `TelemetrySnapshotSerializationTests.NpuMetrics_round_trips()` — present / absent / unknown-vendor.
- `MetricFlagsTests.NewFlagsDoNotCollide()` — confirm the four new bit positions don't collide with existing flags.

**Collector tests** in `tests/HHAPulse.Overlay.Tests/`:

- `StorageTempCollectorTests.ReturnsNull_WhenDriveHasNoSensor()` — mock `DeviceIoControl` to return `ERROR_INVALID_FUNCTION`, verify `snapshot.Storage.TemperatureCelsius is null`.
- `StorageTempCollectorTests.ParsesTemperatureCorrectly()` — feed a known `STORAGE_TEMPERATURE_DATA_DESCRIPTOR` buffer.
- `CpuClockCollectorTests.AggregateFromPdh()` — verify PDH counter path with an in-memory fake.
- `CpuClockCollectorTests.PerCoreFromPowerInformation_RecordsNominalWarning()` — when `CurrentMhz == MaxMhz` for all cores, verify the diagnostic note is emitted.
- `NpuDetectionCollectorTests.EmptyAdapterList_YieldsNotPresent()`.
- `NpuDetectionCollectorTests.IntelAdapter_Classifies()`, `AmdAdapter_Classifies()`, `QualcommAdapter_Classifies()`.
- `FormatStorageTempCompactTests.*` — all five battery-formatter-style branches (charging/discharging/unknown/AC/default equivalents).
- `FormatCpuClockCompactTests.UsesTildePrefix()` — verify the `~` honesty signal is always present.

**Hardware validation matrix** (documented in `memory-bank/2026/04/15/INDEX.md` as the validation log for this expansion):

| Device | Platform | SSD temp (IOCTL) | SSD wear (WMI) | CPU clock (PDH) | Per-core (CallNtPowerInfo) | NPU detected |
|---|---|---|---|---|---|---|
| MSI Claw | Intel Core Ultra | expected | expected if NVMe | expected | nominal only | Intel AI Boost |
| ROG Ally X | AMD Ryzen Z1E | expected | expected if NVMe | expected | nominal only | **none — Z1E has no NPU** |
| Legion Go S | AMD Ryzen AI 350 | expected | expected | expected | nominal only | AMD Ryzen AI (XDNA) |
| AYANEO Kun | AMD Ryzen 7 8840U | expected | expected | expected | nominal only | AMD Ryzen AI |
| Steam Deck OLED (Windows dual boot) | AMD Van Gogh | expected | unknown (APU-integrated storage) | expected | nominal only | **none — Van Gogh has no NPU** |
| Desktop reference (for regression) | any x86 | expected | expected | expected | nominal only | adapter if present |

Each cell is a smoke test, not an end-to-end test — visually confirm the HUD reading matches what `HWiNFO` or `CrystalDiskInfo` reports for SSD temp, and that NPU shows present/absent correctly.

### Part B — B.4 `telemetry-trace-map.md` additions

Four new sections in `memory-bank/systems/telemetry-trace-map.md`, appended to the existing numbered list:

- **§9. Storage** — `storage_temp` (IOCTL pipeline), `storage_wear` (WMI pipeline), hardware-support matrix from the validation log, per-device gaps. Citations to Microsoft Learn.
- **§10. CPU clock (aggregate + per-core)** — PDH path, `CallNtPowerInformation` path, the explicit `CurrentMhz == MaxMhz` limitation on modern Windows 11 cited from `Windows-Dev-Performance` issue #100, the `~` prefix honesty signal.
- **§11. NPU** — DXCore enumeration pipeline, adapter classification rules, the "utilization unavailable" note, Microsoft Task Manager internal counter reference.
- **§12. Explicitly not faked (v2)** — updated list after the expansion: `input_latency`, numeric frame-generation FPS, NPU utilization, live throttle-aware per-core frequency, Intel Lunar Lake / Arc 140V GPU temperature.

### Part B — B.5 `CLAUDE.md` additions

The round-1 spec Fix I decomposed the old blanket "temperature/power/fan stay hidden" rule into per-metric statements. Round-2 extends the list:

```markdown
> **Operational (universal, real data, not hidden):** FPS … battery … **SSD temperature (IOCTL_STORAGE_QUERY_PROPERTY), aggregate CPU effective clock (PDH Processor Information(_Total)\Processor Frequency).**
>
> **Operational (hardware-gated):** GPU temp/clock/power/fan (vendor SDKs) … MSI fan/device temp … **SSD wear (MSFT_StorageReliabilityCounter via capture service — NVMe only, USB enclosures not populated).**
>
> **Operational (nominal, with honesty signal):** **Per-core CPU clock (CallNtPowerInformation ProcessorInformation — returns nominal MaxMhz on modern Windows 11; live throttle-aware frequency requires vendor SDKs not shipped in v1).**
>
> **Detected only (presence, no utilization):** **NPU adapter (DXCore CreateAdapterListByWorkload MachineLearning NPU) — utilization not exposed by any public Windows API in April 2026; companion diagnostics shows "utilization unavailable".**
>
> **Still hidden / never faked:** input_latency, numeric frame-generation FPS, NPU utilization, live throttle-aware per-core CPU frequency, Intel Lunar Lake GPU temperature.
```

### Part B — B.6 Store / MSIX eligibility check per new subsystem

| Subsystem | Process host | API class | MSIX AppContainer compat | Admin req | Verdict |
|---|---|---|---|---|---|
| `IOCTL_STORAGE_QUERY_PROPERTY` (storage temp) | Overlay | Win32 DeviceIoControl on `\\.\PhysicalDriveN` | **Yes** (user-mode, no restricted capability needed per Microsoft Learn) | No | Ship |
| `MSFT_StorageReliabilityCounter` WMI | **Capture service** | WMI `root\Microsoft\Windows\Storage` | **Unverified** in AppContainer — routed through elevated service to sidestep | Yes (WMI privileged namespace) | Ship |
| PDH `\Processor Information\Processor Frequency` | Overlay | `Pdh.h` | **Yes** | No | Ship |
| `CallNtPowerInformation(ProcessorInformation)` | Overlay | `powrprof.dll` | **Yes** per Microsoft Learn documentation | No | Ship |
| DXCore `IDXCoreAdapterFactory1::CreateAdapterListByWorkload` | Overlay | `dxcore.h` | **Yes** | No | Ship |
| DXCore NPU utilization | — | none exists | N/A | N/A | **Deferred** |

If any cell turns out to be wrong on a hardware build (e.g. IOCTL storage blocked inside MSIX AppContainer), the fallback is to move the blocked collector into the capture service. The architectural pattern is already in place for `BatteryCollector` → service IPC.

### Part B — B.7 Honest open items (Part B only)

- **`CurrentMhz == MaxMhz` behavior on April 2026 Windows** — the research cites a Microsoft GitHub issue. Needs confirmation on a real Windows 11 24H2 build — if `CurrentMhz` actually *does* vary (i.e., the issue was fixed in an update), the `~` honesty prefix can be dropped. **Owner:** hardware validation pass.
- **MSIX AppContainer + `IOCTL_STORAGE_QUERY_PROPERTY`** — research says "assumed yes" but it's unverified on a packaged build. **Owner:** Store packaging decision (open question #10 from Round 1).
- **MSIX AppContainer + DXCore NPU enumeration** — same caveat.
- **`MSFT_StorageReliabilityCounter` field population on non-NVMe devices** — research says NVMe consumer SSDs populate reliably, SATA and USB are sparse. Our fallback is `--`. Confirmed by HWiNFO and CrystalDiskInfo third-party reports; not confirmed by us.
- **Snapdragon X / Oryon core behavior** with `PROCESSOR_POWER_INFORMATION` — should work per the Windows-universal API design, but no handheld PC ships with Snapdragon X in April 2026 (they're laptops). If HHA Pulse later targets Snapdragon handhelds, re-verify.
- **Intel Lunar Lake / Arc 140V NPU (Intel AI Boost v3)** — DXCore should enumerate it; not verified on a Lunar Lake test device.
- **Future Windows public NPU API** — Microsoft's Task Manager uses internal counters. If they expose a public API in Windows 11 25H2 or later, NPU utilization can be re-enabled. Watch Windows Insider blog + `learn.microsoft.com/en-us/windows/ai` for updates.
- **Store packaging strategy** — still the #1 blocker from Round-1 open question #10. The new collectors don't change the answer but increase the surface area that needs verification.

---

## Teammate assignments

Three teammates run **sequentially** in a specific order. Parallel execution is possible for B.1 + B.2 but B.3 (tests) must wait on both.

### Teammate Alpha-2 — `spec-editor-round2` (general-purpose)

**Sole file to edit:** `/Users/johny/Handheld Pulse/docs/superpowers/specs/2026-04-15-hha-pulse-ux-redesign-design.md`

**Scope:** Part A only — the four reviewer fixes. Absolutely no Part B work.

**Tasks, in order:**

1. Read the plan file fully.
2. Read the spec file. Specifically L96, L280, L333, L384, L396-398, L609.
3. Execute **Fix R2-A** (§5.5 Metric availability gate — delete stale bullets, insert the replacement block as specified above).
4. Execute **Fix R2-B** (two "1:1 with Valve" lines at L96 and L380 — rewrite both).
5. Execute **Fix R2-C** (two "fact strip" denial lines at L280 and L333 — rewrite to remove the literal phrase).
6. Execute **Fix R2-D** (non-success list at L609 — narrow the ban to exclude the Manual link).
7. Run the Part A verification grep (exact regex in Part A § Verification grep). **Zero hits required**, including inside denials.
8. Re-read §5.6 and verify it does not contradict the replacement text in Fix R2-A.

**Acceptance:**
- Part A grep returns zero hits.
- §5.6 and §5.5 Metric availability gate agree.
- Manual link label at L384 unchanged.
- Non-success list at L609 explicitly calls out the composer wizard as the failure mode.

**Report back** (under 300 words): diff summary, grep result, any line number that had to shift during editing.

### Teammate Bravo-2 — `data-layer-implementer` (general-purpose)

**Scope:** Part B.1 — data layer. `TelemetrySnapshot` extensions, `MetricFlags` additions, `OverlayPresetCatalog` metric ID constants, four new collector files.

**Files to edit:**
- `src/HHAPulse.Shared/Models/TelemetrySnapshot.cs` (add `Storage`, `CpuDetail`, `Npu` sections)
- `src/HHAPulse.Shared/Models/MetricFlags.cs` (add four flags — verify bit positions against real file)
- `src/HHAPulse.Overlay/Settings/OverlayPresetCatalog.cs` (add metric ID constants; extend `FullMetrics` and `TunerMetrics` arrays per Part B table)
- `src/HHAPulse.Overlay/Collectors/Storage/StorageTempCollector.cs` (**new**)
- `src/HHAPulse.Overlay/Collectors/Cpu/CpuClockCollector.cs` (**new**)
- `src/HHAPulse.Overlay/Collectors/Npu/NpuDetectionCollector.cs` (**new**)
- `src/HHAPulse.CaptureService/Collectors/StorageReliabilityCollector.cs` (**new**)
- `src/HHAPulse.CaptureService/Ipc/*` — extend the IPC contract to carry storage reliability fields to the overlay.

**Tasks, in order:**

1. Read the plan file fully (Part B especially).
2. Read `BatteryCollector.cs` as the pattern reference — follow its `MeasurementTraceRecorder.Record()`, fail-closed, and trace-recording style.
3. Read `OverlayPresetCatalog.cs` to confirm the insertion points and the exact name of the existing `FullMetrics` / `TunerMetrics` arrays from the Round-1 spec extension.
4. Implement `TelemetrySnapshot` extensions.
5. Implement the four collectors in order: `StorageTempCollector` → `CpuClockCollector` → `NpuDetectionCollector` → `StorageReliabilityCollector`. Each uses the pattern from `BatteryCollector.cs`.
6. Extend `OverlayPresetCatalog` metric ID constants + `FullMetrics` / `TunerMetrics` arrays per the Part B metric inventory table.
7. Extend the capture-service IPC to carry storage reliability fields.
8. Run the overlay Release build and shared tests as in the Round-1 Fix L teammate:
   - `dotnet build "src/HHAPulse.Overlay/HHAPulse.Overlay.csproj" -c Release -p:UseSharedCompilation=false --disable-build-servers`
   - `dotnet build "src/HHAPulse.CaptureService/HHAPulse.CaptureService.csproj" -c Release -p:UseSharedCompilation=false --disable-build-servers`
   - `dotnet test "tests/HHAPulse.Shared.Tests/HHAPulse.Shared.Tests.csproj" -c Release`
9. If a build fails on the macOS host because of Windows SDK dependency, report host-incompatible cleanly — do NOT attempt to work around.

**Acceptance:**
- All four collectors compile.
- `TelemetrySnapshot` MessagePack serialization round-trip tests pass.
- No existing test regresses.
- Build succeeds on Windows (or is explicitly reported as host-incompatible on macOS).

**Report back** (under 400 words): file list touched, build result per project, test result, any open question that surfaced during implementation.

### Teammate Charlie-2 — `display-layer-implementer` (general-purpose)

**Scope:** Part B.2 — display layer. New compact formatters, `TopBarControl.BuildStorage`, companion NPU card, Manual picker domain grouping.

**Files to edit:**
- `src/HHAPulse.Overlay/Helpers/MetricFormatterCompact.cs` (add three new format functions; extend the `FormatValue` switch)
- `src/HHAPulse.Overlay/Controls/TopBarControl.xaml.cs` (add `BuildStorage`; extend `BuildHw` for CPU clock; call sites in `RebuildMetricViews`)
- `src/HHAPulse.Overlay/Controls/NpuStatusCard.xaml` + `.cs` (**new** — companion diagnostics card)
- `src/HHAPulse.Overlay/Controls/ManualMetricSheet.xaml` + `.cs` (add domain grouping for the toggle chips)
- `src/HHAPulse.Overlay/Views/SupportFooter.xaml` (or wherever the support-footer state chip row lives in Round 05 mockup — needs the new "NPU" chip that expands the card)

**Tasks, in order:**

1. Read the plan file fully (Part B.2 especially).
2. Read `MetricFormatterCompact.cs:107-234` to confirm the existing formatter pattern, especially `FormatBatteryCompact` which is the closest analogue.
3. Read `Controls/TopBarControl.xaml.cs:239-310` to confirm where `BuildHw`, `BuildMemory`, `BuildSystem` live and the `AddComp` call pattern.
4. Implement the three compact formatters with the `~` honesty prefix on `FormatCpuClockCompact`.
5. Implement `BuildStorage` and extend `BuildHw` to include `cpu_clock`.
6. Implement the companion `NpuStatusCard` as a collapsible diagnostics card.
7. Extend `ManualMetricSheet` with domain grouping (visual only; persistence unchanged).
8. Build + test on Windows (macOS host expected to fail the WinUI 3 build cleanly; report as host-incompatible).

**Acceptance:**
- Formatter unit tests pass (added by Teammate Delta-2).
- Build succeeds on Windows.
- HUD renders `storage_temp`, `storage_wear`, and `cpu_clock` tiles correctly in the Full preset on a real handheld (manual smoke test during validation).
- Companion NPU card shows the correct adapter name on Intel / AMD / "Not detected" when no NPU is present.

**Report back** (under 400 words): file list touched, build result, manual smoke-test notes on a real handheld if available.

### Teammate Delta-2 — `tests-and-docs` (general-purpose)

**Scope:** Part B.3 (tests) + Part B.4 (telemetry-trace-map.md) + Part B.5 (CLAUDE.md additions).

**Files to edit:**
- `tests/HHAPulse.Shared.Tests/*` (add `TelemetrySnapshotSerializationTests` methods for new sections, `MetricFlagsTests` for bit collision)
- `tests/HHAPulse.Overlay.Tests/*` (add collector tests, formatter tests)
- `memory-bank/systems/telemetry-trace-map.md` (append §9-§12 per Part B.4)
- `CLAUDE.md` (update the metric-policy decomposition per Part B.5)
- `memory-bank/2026/04/15/INDEX.md` (create the daily log for this expansion and the hardware validation matrix)

**Tasks, in order:**

1. Read the plan file fully (Part B.3, B.4, B.5 especially).
2. Read the existing `tests/HHAPulse.Shared.Tests/` and `tests/HHAPulse.Overlay.Tests/` for the current test style — match it.
3. Implement all the contract + collector + formatter tests from Part B.3.
4. Append the §9-§12 sections to `telemetry-trace-map.md` with Microsoft Learn citations.
5. Update `CLAUDE.md` per Part B.5.
6. Create the daily log at `memory-bank/2026/04/15/INDEX.md` with the hardware validation matrix stub (cells filled in during actual validation, not now).
7. Run the shared + overlay test suites.

**Acceptance:**
- All new tests pass.
- No existing test regresses.
- `telemetry-trace-map.md` §9-§12 each have at least one citation URL.
- `CLAUDE.md` metric-policy block is internally consistent.

**Report back** (under 400 words): test counts (added / passed / failed / skipped), doc sections touched, any citation that could not be verified.

### Coordinator (me) after all four teammates complete

1. Verify Part A acceptance (grep clean, §5.6/§5.5 consistent).
2. Run the final grep pass over the spec one more time — this time also run it over `CLAUDE.md` and `telemetry-trace-map.md` to catch anything else that drifted.
3. Present the revised spec + the new collectors + the trace map additions + the updated CLAUDE.md to Johny for review as **one cohesive package**.
4. Wait for explicit approval.
5. On approval, run the hardware validation matrix on whatever devices are available (Johny can do this or instrument a CI device).
6. On validation pass, the Round-2 spec + telemetry expansion is done. Move to the next milestone (implementation of the companion window XAML, from the Round-1 spec's §5.3 new-file list).

---

## Execution order

Strictly sequential:

1. **Teammate Alpha-2** runs first (Part A spec fixes). Must complete cleanly.
2. After Alpha-2 completes, **Bravo-2** and **Charlie-2** run in parallel (Part B.1 and B.2). They touch disjoint files.
3. After Bravo-2 and Charlie-2 both complete, **Delta-2** runs (Part B.3 tests depend on B.1 data layer + B.2 display layer existing; docs depend on both).
4. Coordinator verifies the whole package, presents to user, waits for approval.

---

## Honest open items that survive this plan

- **Microsoft Store packaging strategy** (Round-1 #10, still blocking launch).
- **Palette hex verification + font family verification** (Round-1 pre-implementation tasks).
- **Logo asset** (Johny to provide).
- **Steam-overlay side-dock width** (Round-1 measurement).
- **`(Win-rec)` factory scales** on 8 handheld devices (Round-1).
- **Bazzite / Steam Deck Windows scope** (Round-1).
- **NPU utilization public API** (deferred — watch Windows Insider for 25H2+ additions).
- **Live throttle-aware per-core CPU MHz** (deferred — vendor SDK only).
- **AYANEO Air / GPD Win 4** hardware smoke test on the new collectors (specifically the 960×540 floor tier — want to confirm the new metric chips don't break the narrow layout).
- **MSIX AppContainer verification** for `IOCTL_STORAGE_QUERY_PROPERTY`, DXCore NPU enumeration, PDH counters, `MSFT_StorageReliabilityCounter` — research says "assumed yes" for the first three, "routed through capture service" for the fourth. Needs a packaged-build test.

None of these block the Round-2 spec fix or the telemetry expansion plan execution. They are pre-implementation verification items tied to Store submission.
