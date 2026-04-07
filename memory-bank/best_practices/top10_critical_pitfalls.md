# Top 10 Most Critical Pitfalls for HHA Pulse

Check this list before every PR / major change.

1. **Named Pipe Security for UWP** — Must explicitly grant Package SID access. Widget CANNOT connect without it.
2. **ETW Session Orphaning** — Sessions survive process death. ALWAYS clean up on service startup.
3. **WinUI 3 x:Bind Window Leak** — Known bug. Use x:Bind in child controls, NEVER in Window XAML.
4. **P/Invoke Delegate GC** — Store native callbacks in STATIC fields. GC WILL collect them.
5. **Async Deadlocks** — NEVER `.Result` or `.Wait()`. Keep async all the way up.
6. **Pipe Squatting** — Use `FILE_FLAG_FIRST_PIPE_INSTANCE` to prevent hijacking.
7. **DispatcherQueue Thread Safety** — ALL UI updates from pipe callbacks MUST go through `TryEnqueue`.
8. **PresentMon DLL Compatibility** — NEVER ship your own copy of PresentMonAPI2.dll.
9. **Service Recovery** — Configure restart-on-failure via sc.exe. Crashes must not be permanent.
10. **SmartScreen + EV Certificate** — Get EV cert before first release. Standard certs need weeks to build reputation.
