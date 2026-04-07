# Windows Service Best Practices

## Service Recovery

Configure restart-on-failure (CRITICAL — don't let crashes be permanent):
```
sc.exe failure HHAPulseService actions= restart/5000/restart/30000/restart/60000 reset= 86400
```
Restarts after 5s, 30s, 60s. Resets failure count after 24h.

## Logging

- `EventLog` for Windows Event Viewer integration (service start/stop, errors)
- File-based logging (Serilog/NLog) with rotation for detailed diagnostics
- Log: service lifecycle, pipe connect/disconnect, driver load, ETW session create/destroy
- NEVER log sensitive data (there shouldn't be any, but enforce the habit)

## Driver Loading

- Validate driver file hash before loading
- Handle `ERROR_SERVICE_DISABLED` (driver blocked by policy)
- Wrap in try/catch — never crash service if driver fails
- Log to Event Log on success and failure
- Only load from verified install path

## Service Install

- `sc.exe create` — space required: `binpath= "C:\path"` (space after `=`)
- Set `start= auto` for auto-start
- Before `sc delete`: close services.msc, stop service
- Use `delayed-auto` for services that don't need to start immediately on boot

## Restricted Token

- Drop unnecessary privileges from service token
- Zero network access — no listening sockets, no outbound connections
- Named Pipes only for IPC
