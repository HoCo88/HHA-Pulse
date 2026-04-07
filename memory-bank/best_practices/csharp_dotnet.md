# C# / .NET 8 Best Practices for HHA Pulse

## Async/Await

- **NEVER** call `.Result` or `.Wait()` on async code — causes deadlocks
- Always use `async Task`, never `async void` (except UI event handlers)
- Use `ConfigureAwait(false)` in library/background code, NOT in WinUI UI code
- Pass `CancellationToken` through async methods — critical for app shutdown and collector loops
- Don't discard Tasks — use fire-and-forget helper that logs exceptions

## IDisposable / Resource Management

- Always `using` or `await using` for streams, pipes, handles
- Unsubscribe event handlers in Dispose — prevents memory leaks
- Dispose timers explicitly — Timer callbacks capture `this`
- Use `SafeHandle` over raw `IntPtr` for unmanaged resources

## P/Invoke

- Use `[LibraryImport]` on .NET 7+ (source-generated, faster than DllImport)
- C++ `bool` = 1 byte, Windows `BOOL` = 4 bytes — use `[MarshalAs(UnmanagedType.U1)]`
- Store native callback delegates in **static fields** — GC will collect them otherwise
- Pin managed objects passed to native code (`GCHandle.Alloc` or `fixed`)
- Use `SafeHandle` for OS handles — auto-releases, exception-safe
- Set `SetLastError = true` and call `Marshal.GetLastWin32Error()` immediately
- Make structs blittable: `[StructLayout(LayoutKind.Sequential)]`, no bool, no string

## Memory in Long-Running App Processes

- `PerformanceCounter` has known memory leak in .NET Core — periodically recreate
- Static collections grow indefinitely — implement eviction (time or size based)
- Lambdas capturing `this` keep entire object alive — capture local variables instead
