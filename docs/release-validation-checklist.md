# HHA Pulse Release Validation Checklist

## Scope

This checklist is for the current validated repo state as of 2026-04-09.

Known-good from this machine:
- `HHAPulse.Shared`, `HHAPulse.CaptureService`, and `HHAPulse.Overlay` build and test cleanly
- managed tests are green: `55/55`
- frame generation is detect-only and diagnostic-only

Projects that still require Visual Studio workloads and machine-side validation:
- `src/HHAPulse.Native/HHAPulse.Native.vcxproj`
- `src/HHAPulse.Widget/HHAPulse.Widget.csproj`
- `src/HHAPulse.Widget.Packaging/HHAPulse.Widget.Packaging.wapproj`
- `src/HHAPulse.Overlay.Packaging/HHAPulse.Overlay.Packaging.wapproj`

## Required Tooling

Release validation machine must have:
- .NET 8 SDK
- Visual Studio 2026 with Desktop development with C++
- Visual Studio 2026 with UWP/Game Bar tooling for the widget project
- Windows App SDK packaging/Desktop Bridge tooling for the `wapproj` projects
- `dumpbin` available from the Visual Studio developer tools
- Xbox Game Bar installed for widget activation testing

## Gate 0: Core Managed Validation

Expected result: pass cleanly before any native or packaging work.

Run:

```powershell
dotnet test tests\HHAPulse.Shared.Tests\HHAPulse.Shared.Tests.csproj -c Release
dotnet test tests\HHAPulse.CaptureService.Tests\HHAPulse.CaptureService.Tests.csproj -c Release
dotnet test tests\HHAPulse.Overlay.Tests\HHAPulse.Overlay.Tests.csproj -c Release
```

Pass criteria:
- Shared tests pass: `4/4`
- Capture service tests pass: `8/8`
- Overlay tests pass: `43/43`
- Total managed tests pass: `55/55`
- No warnings or errors in `HHAPulse.Shared`, `HHAPulse.CaptureService`, or `HHAPulse.Overlay`

Record:
- command output
- commit SHA
- machine name / OS build

## Gate 1: Native Bridge Build

Project:
- `src/HHAPulse.Native/HHAPulse.Native.vcxproj`

Run:

```powershell
msbuild src\HHAPulse.Native\HHAPulse.Native.vcxproj /p:Configuration=Release /p:Platform=x64
```

Expected output:
- `src\HHAPulse.Native\bin\x64\Release\HHAPulse.Native.dll`

Verify exports:

```powershell
dumpbin /exports src\HHAPulse.Native\bin\x64\Release\HHAPulse.Native.dll
```

Required exports:
- `HhaPulseAdlxProbe`
- `HhaPulseAdlxInit`
- `HhaPulseAdlxReadGpu`
- `HhaPulseAdlxShutdown`
- `HhaPulseIgclProbe`
- `HhaPulseIgclInit`
- `HhaPulseIgclReadGpu`
- `HhaPulseIgclShutdown`

Pass criteria:
- build succeeds in `Release|x64`
- `HHAPulse.Native.dll` exists at the expected path
- all required exports are present

Blockers:
- platform toolset mismatch
- missing vendor SDK runtime DLLs on test hardware

## Gate 2: Overlay Release Build

Project:
- `src/HHAPulse.Overlay/HHAPulse.Overlay.csproj`

The overlay project already enforces the native dependency:
- it expects `HHAPulse.Native.dll` at `src\HHAPulse.Native\bin\x64\Release\HHAPulse.Native.dll`
- it copies the native DLL into build/publish output
- it throws an error before packaging if the native DLL is missing

Run:

```powershell
dotnet build src\HHAPulse.Overlay\HHAPulse.Overlay.csproj -c Release -p:UseSharedCompilation=false --disable-build-servers
dotnet publish src\HHAPulse.Overlay\HHAPulse.Overlay.csproj -c Release -r win-x64 -p:UseSharedCompilation=false --disable-build-servers
```

Pass criteria:
- overlay build succeeds
- publish succeeds
- published output contains:
  - `HHAPulse.Overlay.exe`
  - `HHAPulse.CaptureService` output copied into the overlay output
  - `HHAPulse.Native.dll`

Check:
- launch overlay from published output
- open settings shell
- verify `About` shows version/build info
- verify diagnostics render without layout breakage

## Gate 3: Overlay Packaging Validation

Project:
- `src/HHAPulse.Overlay.Packaging/HHAPulse.Overlay.Packaging.wapproj`

Manifest:
- `src/HHAPulse.Overlay.Packaging/Package.appxmanifest`

Run:

```powershell
msbuild src\HHAPulse.Overlay.Packaging\HHAPulse.Overlay.Packaging.wapproj /p:Configuration=Release /p:Platform=x64 /p:GenerateAppxPackageOnBuild=true
```

Pass criteria:
- package build succeeds
- Desktop Bridge tooling resolves correctly
- manifest identity is valid:
  - Name: `HandheldAlly.HHAPulse`
  - full-trust capability present
  - startup task present
- generated package installs and launches

Validation after install:
- overlay launches from Start menu
- startup task can be enabled/disabled
- settings shell opens from runtime UI

## Gate 4: Widget Build

Project:
- `src/HHAPulse.Widget/HHAPulse.Widget.csproj`

Manifest:
- `src/HHAPulse.Widget/Package.appxmanifest`

Run from Visual Studio or MSBuild with UWP tooling available.

Pass criteria:
- widget project builds successfully for `Release|x64`
- Game Bar SDK references resolve
- XAML compile succeeds
- manifest still contains:
  - Identity `HandheldAlly.HHAPulseWidget`
  - `microsoft.gameBarUIExtension`
  - width `464`
  - ProxyStub extension block for Xbox Game Bar COM marshaling

Pre-release TODO:
- replace Store placeholder in `src/HHAPulse.Widget/CompactWidget.xaml.cs`
  - current placeholder: `ms-windows-store://pdp/?ProductId=9PLACEHOLDER`

## Gate 5: Widget Packaging And Activation

Project:
- `src/HHAPulse.Widget.Packaging/HHAPulse.Widget.Packaging.wapproj`

Run:

```powershell
msbuild src\HHAPulse.Widget.Packaging\HHAPulse.Widget.Packaging.wapproj /p:Configuration=Release /p:Platform=x64 /p:GenerateAppxPackageOnBuild=true
```

Pass criteria:
- package build succeeds
- package installs successfully
- widget appears inside Xbox Game Bar
- widget can be pinned
- widget opens without activation crash

Runtime validation:
- standalone mode shows only sandbox-safe data
- enhanced mode connects only when overlay telemetry is actually available
- widget support link opens correctly
- Store upsell button opens the Store URI correctly after real product ID replacement

## Gate 6: IPC And Companion Validation

Validate the overlay/widget split exactly as implemented:
- widget is read-only
- overlay remains the truth source when connected
- pipe protocol remains unchanged

Pass criteria:
- overlay running + widget open -> widget shows enhanced state
- overlay not running -> widget shows standalone state
- disconnect/reconnect transitions recover cleanly
- no fake metrics appear in standalone mode

Known current limitation:
- pipe DACL currently allows AppContainer access broadly enough for the widget path
- tighten to the final widget package SID later, after package identity is locked for release

## Gate 7: Hardware Validation Matrix

### AMD handheld

Target example:
- Lenovo Legion Go

Validate:
- ADLX path produces non-placeholder GPU temp
- ADLX path produces GPU clock
- ADLX path produces GPU power
- ADLX path produces GPU fan when supported
- system power shows only from whole-device battery discharge watts; CPU+GPU sum appears in diagnostics only

Record:
- screenshots of settings shell `Metrics`, `FPS Capture`, and `About`
- exported diagnostics text

### Intel handheld

Target example:
- MSI Claw

Validate:
- IGCL path produces non-placeholder GPU temp
- IGCL path produces GPU clock
- IGCL path produces GPU power
- IGCL path produces GPU fan when supported

Record:
- screenshots
- exported diagnostics text

### NVIDIA desktop or laptop

Validate:
- NvAPI/NVML path still reports expected temperature/power/fan/clock
- overlay behavior does not regress

### Frame generation detect-only

Use a title and hardware path with known frame-generation support.

Validate:
- diagnostics show `Frame generation: detected` only when FG is actually active
- diagnostics show `Frame generation: not detected` when capture is healthy and FG is off
- diagnostics show unavailable when capture is stale or disconnected
- HUD does **not** expose numeric frame-gen FPS
- `MetricFlags.FrameGen` remains unset behaviorally

Important open question:
- current implementation uses `PayloadByName("FrameType")` only
- if real hardware never resolves that field through TraceEvent, detection should fail closed rather than inventing data
- if that happens, log the result and plan a separate verified raw-layout fallback

## Gate 8: UX Regression Pass

Desktop shell:
- all six sections load:
  - `General`
  - `Overlay`
  - `Metrics`
  - `FPS Capture`
  - `Widget`
  - `About`
- shell is scrollable at handheld resolutions
- buttons and toggles are reachable on 7-8 inch devices
- diagnostics text is readable and copy/export actions work

HUD:
- preset switching works
- unavailable metrics render safely
- `0%` background opacity no longer renders black
- `total_power` appears only while the battery is discharging and Windows reports a positive whole-device discharge watt rate

Widget:
- compact layout remains readable at width `464`
- standalone/enhanced badge is correct
- no deep control surface has drifted into the widget

## Evidence To Capture For Release Sign-Off

For each gate, save:
- machine name and OS build
- command used
- result: pass/fail
- screenshots where UI or activation is involved
- exported diagnostics text for at least one healthy run and one degraded run
- notes on any hardware-specific limitation

Recommended artifact set:
- one folder per machine under a local validation archive
- screenshots for overlay shell, HUD, widget standalone, widget enhanced
- `dumpbin` export output for `HHAPulse.Native.dll`
- package build logs for both `wapproj` projects

## Release Stop Conditions

Do not call the release validated if any of these are still unresolved:
- native DLL fails to build or misses required exports
- overlay publish/package output is missing `HHAPulse.Native.dll`
- widget package does not activate in Xbox Game Bar
- AMD or Intel vendor telemetry still shows placeholders where the code path is expected to work
- frame-generation diagnostics claim detection without hardware evidence
- any UI surface shows synthetic/fabricated values instead of unavailable state

## Current Recommendation

Next real testing order:
1. Run Gate 1 on the main dev machine and capture `dumpbin` output.
2. Run Gate 2 and Gate 3 for overlay release build/package.
3. Run Gate 4 and Gate 5 for widget build/package/Game Bar activation.
4. Run Gate 7 on AMD and Intel hardware.
5. Only after those pass, tighten the widget pipe DACL to the final package SID and plan numeric frame generation.
