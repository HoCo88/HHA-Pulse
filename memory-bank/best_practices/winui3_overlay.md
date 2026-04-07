# WinUI 3 Overlay Best Practices

## Threading

- ALL UI updates must happen on UI thread via `DispatcherQueue.TryEnqueue()`
- Named pipe callbacks fire on worker threads — always dispatch to UI
- Check `DispatcherQueue.HasThreadAccess` before dispatching to avoid overhead
- `TryEnqueue` returns bool, not Task — use `TaskCompletionSource` if you need to await

## Transparent Window

WinUI 3 has NO native transparent window support. Required Win32 interop:
1. `DwmExtendFrameIntoClientArea(hwnd, MARGINS(0))`
2. `DwmEnableBlurBehindWindow(hwnd, blurBehind)` with empty region
3. Transparent brush via Compositor
4. `WS_EX_TRANSPARENT` for click-through
5. `OverlappedPresenter.IsAlwaysOnTop = true` for topmost

## XAML Memory Leaks (CRITICAL)

- **x:Bind in Window XAML leaks** — known WinUI 3 bug. Subscribes to Window.Activated, never unsubscribes. ~1000 objects leaked per window.
  - Fix: Use x:Bind in child Pages/UserControls, NOT in Window XAML
- Event handlers in Window XAML create circular references — subscribe/unsubscribe in code-behind
- DataTemplate event handlers leak — use x:Bind inside template or commands
- SwapChainPanel: unsubscribe all events in Unloaded handler

## Deployment

- Unpackaged WinUI 3 app silently fails if Windows App SDK runtime is missing — no error dialog
- Runtime version must match: built with 1.2.3 requires 1.2.x (x >= 3)
- Self-contained adds ~200MB but eliminates runtime dependency
- Use `dotnet publish` from command line, not VS "Publish"
