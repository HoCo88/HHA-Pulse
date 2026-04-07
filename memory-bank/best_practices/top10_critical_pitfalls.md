# Top 10 Most Critical Pitfalls for HHA Pulse v1

Check this list before every PR / major change.

1. **Named Pipe Path** — Use `\\.\pipe\LOCAL\HHAPulse`; UWP/Game Bar cannot use the non-LOCAL path.
2. **Named Pipe Security** — Must explicitly grant widget Package SID access; use S2 spike result for the final DACL.
3. **Cross-Package IPC Risk** — If Store certification rejects enhanced mode, widget remains standalone-only for v1.
4. **No Windows Service** — Do not add HHA Pulse NT service, LocalSystem, service recovery, or `sc.exe`.
5. **No Bundled External Dependencies** — Never bundle, download, or install PresentMon or PawnIO.
6. **WinUI 3 x:Bind Window Leak** — Use no `x:Bind` in `MainWindow.xaml`.
7. **P/Invoke Delegate GC** — Store native callbacks in static fields.
8. **Async Deadlocks** — Never `.Result` or `.Wait()`.
9. **DispatcherQueue Thread Safety** — All UI updates from collectors/pipe callbacks must go through `TryEnqueue`.
10. **Build Tooling** — Use Windows + Visual Studio MSBuild for UWP/MSIX; `dotnet build` alone is not enough.
