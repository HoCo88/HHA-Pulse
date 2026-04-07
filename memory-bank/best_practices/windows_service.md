# Windows Service Best Practices — Reference Only

HHA Pulse v1 does **not** implement or ship a custom Windows Service. The paid overlay app runs as a packaged WinUI 3 FullTrustProcess in the current user session.

Keep this file only as historical reference for future non-Store experiments. Do not add service install, service recovery, LocalSystem, `sc.exe`, or packaged service work to v1.

## v1 Rules

- No HHA Pulse NT service.
- No LocalSystem helper process.
- No `sc.exe create` or service recovery configuration.
- No bundled driver install flow.
- PresentMon Service and PawnIO are external user-installed dependencies only.
