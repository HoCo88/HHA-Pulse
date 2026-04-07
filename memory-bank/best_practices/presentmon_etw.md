# PresentMon / ETW Integration Best Practices

HHA Pulse v1 consumes the external PresentMon Service API when the user has installed PresentMon. HHA Pulse does not own the ETW session in normal v1 operation.

## ETW Session Management

- **Historical/raw ETW fallback only:** ETW sessions are MACHINE-WIDE and survive process death
  - On service startup: check for and clean up orphaned sessions from previous runs
  - Use deterministic session name: "HHAPulse_ETW"
  - Always clean up in `finally` block and handle process termination signals
- Starting ETW sessions requires **admin privileges**
- Only ONE real-time kernel ETW session is allowed — be aware of conflicts

## PresentMon API

- Use `PresentMonAPI2Loader.dll` from SDK — never manually load PresentMonAPI2.dll
- **NEVER ship your own copy** of PresentMonAPI2.dll — binary compatibility with service not guaranteed
- Default SDK path: `Program Files\Intel\PresentMon\SDK`
- All API functions return `PM_STATUS` — always check return values
- Handle "PresentMon Service not installed" gracefully — user-facing message

## P/Invoke from C#

- Define all `PM_STATUS` enum values in C#
- Use `[LibraryImport]` with `SetLastError = false` (PresentMon uses own error system)
- Store callback delegates in static fields — prevent GC collection
- Use `NativeLibrary.Load()` for dynamic loading with proper error handling

## Performance

- ETW tracing adds CPU overhead — important on battery-powered handhelds
- Recent versions: latency reduced from 1000ms to ~30ms
- Consider adaptive polling: higher frequency when overlay visible, lower when hidden
