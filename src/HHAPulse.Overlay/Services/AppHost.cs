using HHAPulse.Overlay.Collectors;
using HHAPulse.Overlay.Ipc;

namespace HHAPulse.Overlay.Services;

public sealed class AppHost : IAsyncDisposable
{
    private readonly CollectorOrchestrator collectorOrchestrator;
    private readonly PipeServer pipeServer;

    public AppHost(CollectorOrchestrator collectorOrchestrator, PipeServer pipeServer)
    {
        this.collectorOrchestrator = collectorOrchestrator;
        this.pipeServer = pipeServer;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await collectorOrchestrator.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await pipeServer.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        var snapshot = await collectorOrchestrator.CollectAsync(cancellationToken).ConfigureAwait(false);
        await pipeServer.BroadcastAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        return pipeServer.DisposeAsync();
    }
}
