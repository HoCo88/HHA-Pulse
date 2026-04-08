# ETW FPS Capture Best Practices

HHA Pulse FPS capture uses an elevated ETW service. Do not reintroduce DWM, D3DKMT present stats, or a separate user-installed FPS tool requirement as runtime FPS paths.

Rules:

- ETW session name: `HHAPulse_FrameCapture`.
- The overlay sends target PID over `LOCAL\HHAPulse.Capture.Control`.
- The service publishes frame metrics over `LOCAL\HHAPulse.Capture.Out`.
- No DLL injection, no Present hooks, no game memory reads.
- Stale metrics older than 2 seconds are unavailable.
- ETW parser changes must be validated against a real game trace before UI claims are expanded.

