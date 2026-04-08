using HHAPulse.Overlay.Diagnostics;
using Microsoft.UI.Dispatching;

namespace HHAPulse.Overlay.Hotkeys;

public sealed class HotkeyService : IDisposable
{
    private const string ToggleEventName = @"Local\HHAPulse_Overlay_Toggle";
    private static readonly TimeSpan ShutdownWait = TimeSpan.FromSeconds(2);

    private readonly DispatcherQueue dispatcherQueue;
    private readonly Action<string> handleCommand;
    private readonly CancellationTokenSource shutdown = new();
    private EventWaitHandle? toggleEvent;
    private Task? listenerTask;
    private bool disposed;

    public HotkeyService(DispatcherQueue dispatcherQueue, Action<string> handleCommand)
    {
        this.dispatcherQueue = dispatcherQueue;
        this.handleCommand = handleCommand;
    }

    public static string CommandPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HHAPulse",
        "overlay-command.txt");

    public static bool SignalExistingInstance(string arguments)
    {
        try
        {
            var directory = Path.GetDirectoryName(CommandPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(CommandPath, arguments);
            using var toggleEvent = EventWaitHandle.OpenExisting(ToggleEventName);
            toggleEvent.Set();
            AppLogger.Info($"Signaled existing overlay instance with command '{arguments}'.");
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            AppLogger.Info("Existing overlay instance was detected, but the toggle signal is not ready yet.");
            return false;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to signal existing overlay instance.", ex);
            return false;
        }
    }

    public void RegisterDefaults()
    {
        ThrowIfDisposed();
        toggleEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ToggleEventName);
        listenerTask = Task.Run(ListenForToggleRequests);
        AppLogger.Info("Registered launch-to-toggle signal. Map a handheld button to launch HHAPulse.Overlay.exe.");
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        shutdown.Cancel();
        toggleEvent?.Set();

        try
        {
            listenerTask?.Wait(ShutdownWait);
        }
        catch (AggregateException)
        {
        }

        toggleEvent?.Dispose();
        shutdown.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(HotkeyService));
        }
    }

    private void ListenForToggleRequests()
    {
        var handles = new WaitHandle[] { toggleEvent!, shutdown.Token.WaitHandle };

        while (!shutdown.IsCancellationRequested)
        {
            var signaledHandle = WaitHandle.WaitAny(handles);
            if (signaledHandle != 0 || shutdown.IsCancellationRequested)
            {
                return;
            }

            dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    handleCommand(ReadPendingCommand());
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Failed to handle launch command.", ex);
                }
            });
        }
    }

    private static string ReadPendingCommand()
    {
        try
        {
            if (File.Exists(CommandPath))
            {
                return File.ReadAllText(CommandPath);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to read launch command file.", ex);
        }

        return string.Empty;
    }
}
