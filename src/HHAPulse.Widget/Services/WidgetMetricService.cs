using HHAPulse.Shared.Models;
using HHAPulse.Shared.Pipe;

namespace HHAPulse.Widget.Services;

public sealed class WidgetMetricService : IDisposable
{
    private readonly PipeClient pipeClient = new();

    public bool EnhancedModeConnected { get; private set; }

    public async Task<TelemetrySnapshot?> TryReadEnhancedSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!EnhancedModeConnected)
            {
                await pipeClient.ConnectAsync(cancellationToken).ConfigureAwait(false);
                EnhancedModeConnected = true;
            }

            return await pipeClient.ReadSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            EnhancedModeConnected = false;
            return null;
        }
    }

    public void Dispose()
    {
        pipeClient.Dispose();
    }
}
