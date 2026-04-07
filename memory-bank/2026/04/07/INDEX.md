# Daily Log - 2026-04-07

## Summary

Project kickoff and architecture correction. The original service/traditional-installer design was replaced with the v4 Microsoft Store architecture.

## Current Truth

- **Two Store products:** paid `HHA Pulse` and free `HHA Pulse Widget`.
- **No custom Windows Service** in v1.
- Paid overlay runs as a WinUI 3 FullTrustProcess in the current user session.
- Free widget is a UWP/Game Bar companion and conversion funnel.
- Enhanced widget mode uses `\\.\pipe\LOCAL\HHAPulse` if cross-package IPC passes spike S2 and Store certification.
- PresentMon is external and required only for advanced FPS/frametime/latency/FrameGen metrics.
- PawnIO is external and optional only.
- handheldally.com is promo/support/privacy policy only; no public EXE/MSI downloads.

## Feature Targets

- Presets: Minimal, Standard, Tuner, Diagnostic.
- Custom mode and metric picker.
- FrameGen detection with confidence.
- Bottleneck indicator with confidence.
- Game-aware battery prediction.
- Per-game profiles, hotkeys, settings.
- Game Bar widget standalone/enhanced modes and Store upsell.
