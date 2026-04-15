# 2026-04-15 - Plan A backend telemetry expansion + spec round-2 cleanup

> **Parallel work this day:** Plan B (companion window redesign + 6-position HUD + `Full` preset + new companion controls) ran alongside Plan A. Plan B is documented in the sibling file `plan-b-companion-redesign.md`. The two plans touched disjoint files except for `OverlayPresetCatalog.cs` (Plan B extended the preset catalog shape; Plan A added new metric IDs into `AllMetricIds`) and `docs/superpowers/specs/2026-04-15-hha-pulse-ux-redesign-design.md` (Plan B Round-2 cleanup + Plan A telemetry rule updates — non-overlapping sections).

This entry records what actually landed for Plan A, what was verified from repo code and current research-backed docs, and what remains explicitly unverified until a Windows build/test pass runs.

## Scope completed

- Round-2 cleanup of `docs/superpowers/specs/2026-04-15-hha-pulse-ux-redesign-design.md`
- Telemetry backend expansion for:
  - `storage_temp`
  - `storage_wear`
  - `cpu_clock`
  - per-core nominal CPU frequency detail
  - NPU detection/presence detail
- Shared contract updates:
  - `TelemetrySnapshot`
  - `MetricFlags`
  - `CaptureFrameMetrics`
- Overlay/capture-service wiring:
  - overlay storage temperature collector
  - capture-service storage reliability collector
  - overlay CPU clock collector
  - overlay NPU detection collector
- Diagnostics/formatters/manual-picker plumbing
- `CLAUDE.md` metric policy update
- `memory-bank/systems/telemetry-trace-map.md` extension for storage / CPU clock / NPU detection

## What landed, by subsystem

### Storage temperature

- Lives in the overlay process.
- Source path:
  - `IOCTL_STORAGE_GET_DEVICE_NUMBER` to map the system volume to a physical drive
  - `IOCTL_STORAGE_QUERY_PROPERTY`
  - `StorageDeviceTemperatureProperty`
  - `STORAGE_TEMPERATURE_DATA_DESCRIPTOR`
- Implementation:
  - `src/HHAPulse.Overlay/Collectors/Storage/StorageTempCollector.cs`
- HUD/manual metric id:
  - `storage_temp`

### Storage wear / reliability

- Lives in the elevated capture service, not the overlay.
- Source path:
  - `root\Microsoft\Windows\Storage`
  - `MSFT_StorageReliabilityCounter.Wear`
- Implementation:
  - service collection in `src/HHAPulse.CaptureService/Sensors/CaptureServiceSensorCollector.cs`
  - payload in `src/HHAPulse.Shared/Models/CaptureFrameMetrics.cs`
  - overlay ingest in `src/HHAPulse.Overlay/Collectors/Fps/CaptureServiceCollector.cs`
- HUD/manual metric id:
  - `storage_wear`
- Important correctness note:
  - valid `0%` wear must still count as available. Plan A added `StorageReliabilityAvailable` to the payload to avoid silently dropping a real zero-wear reading.

### CPU clock

- Aggregate CPU clock lives in the overlay.
- Source path:
  - PDH `\Processor Information(_Total)\Processor Frequency`
- Per-core detail path:
  - `CallNtPowerInformation(ProcessorInformation)`
- Implementation:
  - `src/HHAPulse.Overlay/Collectors/Cpu/CpuClockCollector.cs`
- HUD/manual metric id:
  - `cpu_clock`
- Product rule:
  - aggregate clock is nominal/approximate and formatted with the `~` prefix
  - per-core values are diagnostics/detail only, not HUD chips

### NPU detection

- Lives in the overlay.
- Source path:
  - DXCore adapter enumeration
  - adapter driver description classification
- Implementation:
  - `src/HHAPulse.Overlay/Collectors/Npu/NpuDetectionCollector.cs`
- Product rule:
  - detect-only in Plan A
  - no NPU utilization shipped
  - no HUD chip shipped

## Contract / surface changes

- `src/HHAPulse.Shared/Models/TelemetrySnapshot.cs`
  - added `Storage`, `CpuDetail`, `Npu`
- `src/HHAPulse.Shared/Models/MetricFlags.cs`
  - added `StorageTemperature`, `StorageWear`, `CpuClock`, `NpuPresent`
- `src/HHAPulse.Overlay/Settings/OverlayPresetCatalog.cs`
  - added `storage_temp`, `storage_wear`, `cpu_clock`
  - added them to `AllMetricIds`
  - curated presets intentionally unchanged in Plan A
- `src/HHAPulse.Overlay/Helpers/MetricFormatterCompact.cs`
  - added compact renderers for storage temp / storage wear / CPU clock
- `src/HHAPulse.Overlay/Diagnostics/MetricStatusFactory.cs`
  - added status coverage for the new HUD/manual metrics

## Duplicate / dead-code audit

This was checked because the user explicitly asked for no duplicated folders, no parallel implementations of the same metric, and no dead code left behind that would cause confusion.

### No duplicate collector implementations found for Plan A metrics

- `CpuClockCollector` exists once under `src/HHAPulse.Overlay/Collectors/Cpu/`
- `StorageTempCollector` exists once under `src/HHAPulse.Overlay/Collectors/Storage/`
- `NpuDetectionCollector` exists once under `src/HHAPulse.Overlay/Collectors/Npu/`
- storage wear is collected once in the capture service and ingested once in the overlay

There is no second storage-temperature collector, no second CPU-clock collector, and no separate NPU detector elsewhere in the tree.

### New code is actually wired

- `App.xaml.cs` registers:
  - `new CpuClockCollector()`
  - `new StorageTempCollector()`
  - `new NpuDetectionCollector()`
- `CaptureServiceCollector` now ingests storage wear from the service payload
- `TopBarControl` and the formatter path know the new metric ids
- `ComposerOverlay.FormatMetricName()` was updated because the existing Manual/composer path still consumes `OverlayPresetCatalog.AllMetricIds`

That last point is intentional: `ComposerOverlay` is pre-existing code, not a new redesign surface added in Plan A. Updating its metric-name map avoids raw ids like `storage_temp` leaking into the current UI. This is not duplicate logic; it is compatibility glue for an existing path still present in the repo.

### No new runtime folders were duplicated

- Runtime code was added only under:
  - `src/HHAPulse.Overlay/Collectors/Cpu/`
  - `src/HHAPulse.Overlay/Collectors/Storage/`
  - `src/HHAPulse.Overlay/Collectors/Npu/`
- No parallel `Services/Storage/`, `Sensors/Npu/`, or duplicate collector trees were created

### Docs folders

- `docs/superpowers/plans/` and `docs/superpowers/research/` contain planning/research material only.
- They are not runtime paths and do not affect shipped behavior.

## Tests added

- snapshot round-trip coverage for `Storage`, `CpuDetail`, `Npu`
- metric status coverage for `storage_temp`, `storage_wear`, `cpu_clock`
- provenance trace coverage for the new metrics
- compact formatter coverage
- capture-service ingestion coverage for storage wear, including `0%`
- parser/classifier unit coverage for:
  - `CpuClockCollector.ParseProcessorPowerInformation`
  - `StorageTempCollector.ParseTemperatureDescriptor`
  - `NpuDetectionCollector.ClassifyDescription`

## Spec/doc state after the round-2 cleanup

- The spec stale-phrase grep gate was rerun and brought to zero matches.
- The battery section now reflects the real composite battery path and no longer claims missing battery fields that already exist.
- The remaining literal `1:1 with Valve` wording was softened.
- The banned `fact strip` wording was removed entirely.
- The Manual entry wording no longer contradicts the success criteria.

## Still open / not claimed complete

These items are intentionally still open:

- Windows build/test verification:
  - local host used in this pass did not have `dotnet`
  - no WinUI build was run here
- Packaged/MSIX validation still required for:
  - storage IOCTL path
  - PDH CPU clock path
  - `CallNtPowerInformation`
  - DXCore NPU enumeration
- Runtime validation still required on real Windows hardware for:
  - SSD temp availability by device/controller
  - SSD wear availability by device class
  - DXCore NPU detection on Intel / AMD / Qualcomm hardware

## Rules locked by this pass

- Do not add NPU utilization until there is a proved public source.
- Do not present aggregate CPU clock as live throttle-accurate per-core telemetry.
- Do not drop valid `0%` storage wear readings.
- Do not move storage reliability into the overlay process without a fresh Store/AppContainer compatibility decision.
- Do not add the new Plan A metrics to curated presets until a separate preset-density decision is made.
