# Windows Service Best Practices

HHA Pulse uses a local Windows service for ETW FPS capture.

Rules:

- The overlay detects the foreground game PID in the interactive user session.
- The service never calls `GetForegroundWindow` from Session 0 to decide the target game.
- The service owns the ETW session and stops capture when no target PID/client is active.
- Service IPC must allow the interactive user to connect while preserving LocalSystem/Admin control.
- The in-game HUD must fail closed: stale or missing service frames show `FPS --`, never fake values.
