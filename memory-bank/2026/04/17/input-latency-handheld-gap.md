# 2026-04-17 - Input Latency: Handheld Coverage Gap

## Why input latency stays hidden on handhelds

Input latency is deliberately absent from the HUD, metric picker, status surface, and traceability doc. This is not an oversight — the only honest ETW-based source on Windows does not cover handheld input.

### Facts

- **PresentMon 2.2+** ships `click-to-photon` (mouse) and `all-input-to-photon` (mouse + keyboard) metrics via ETW, with no process injection and no game SDK requirement. Source: [PresentMon v2.2.0 release notes](https://github.com/GameTechDev/PresentMon/releases/tag/v2.2.0).
- **XInput / controllers are not covered.** PresentMon's input-latency path intercepts raw-input mouse and keyboard events only; gamepad input does not flow through the same instrumentation. Source: upstream issue [GameTechDev/PresentMon#366](https://github.com/GameTechDev/PresentMon/issues/366).
- **Handheld primary input is the controller.** Steam Deck (trackpads + sticks + buttons), ROG Ally (face buttons + sticks), Legion Go (detachable controllers + sticks), and MSI Claw (face buttons + sticks) all drive games through XInput or vendor HID paths. Mouse/keyboard usage on these devices is the exception, not the rule.
- **Consequence:** on the target hardware, a PresentMon-derived `input_latency` number would render `--` for the overwhelming majority of real sessions.
- **NVIDIA Reflex PC-Latency** requires per-game SDK integration and an NVIDIA discrete GPU. None of the target handhelds ship an NVIDIA dGPU, so this path is off-scope.

## Decision

- `input_latency` remains hidden from HUD, metric picker, status pane, and telemetry traceability doc.
- `MetricFlags.InputLatency` stays reserved in the flags enum but is never set by any collector.
- No input-latency collector ships in this phase.

## Revisit trigger

Revisit when **either** of the following occurs:

1. PresentMon adds XInput / gamepad coverage upstream, **or**
2. A specific handheld OEM (Valve, ASUS, Lenovo, MSI) exposes a driver-level input-to-display timing API with a public, stable contract.
