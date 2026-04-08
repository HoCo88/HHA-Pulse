# HHA Pulse - Distribution Strategy (April 2026)

## Operational Build

The operational build is not constrained to Microsoft Store packaging because FPS capture requires an elevated ETW owner. HHA Pulse ships its own capture service and asks for a one-time admin approval when the user enables FPS tracking.

## Components

- `HHAPulse.Overlay`: interactive WinUI overlay.
- `HHAPulse.CaptureService`: Windows service for ETW frame capture.
- `HHAPulse.Widget`: optional Game Bar widget consuming overlay telemetry.

## Dependency Policy

HHA Pulse must not ask users to install a separate FPS tool. DWM and D3DKMT FPS shortcuts are not product paths. External driver/vendor telemetry work is separate and must not appear in the HUD until real data exists.

