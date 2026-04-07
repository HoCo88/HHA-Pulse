using HHAPulse.Shared.Models;
using HHAPulse.Shared.Pipe;

namespace HHAPulse.Widget.Services;

public sealed class WidgetMetricService : IAsyncDisposable
{
    private readonly PipeClient pipeClient = new();

    public bool EnhancedModeConnected => pipeClient.IsConnected;

    public async Task<TelemetrySnapshot?> TryReadEnhancedSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            await pipeClient.ReconnectIfBrokenAsync(cancellationToken).ConfigureAwait(false);
            return await pipeClient.ReadSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await pipeClient.DisposeAsync().ConfigureAwait(false);
    }
}
