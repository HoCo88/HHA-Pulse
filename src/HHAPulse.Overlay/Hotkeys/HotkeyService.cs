namespace HHAPulse.Overlay.Hotkeys;

public sealed class HotkeyService : IDisposable
{
    private bool disposed;

    public void RegisterDefaults()
    {
        ThrowIfDisposed();
    }

    public void Dispose()
    {
        disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(HotkeyService));
        }
    }
}
