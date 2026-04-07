using HHAPulse.Overlay.Aggregation;
using HHAPulse.Overlay.Collectors;
using HHAPulse.Overlay.Ipc;
using HHAPulse.Overlay.ViewModels;

namespace HHAPulse.Overlay.Services;

public sealed class AppHost : IAsyncDisposable
{
    private readonly CollectorOrchestrator orchestrator;
    private readonly PipeServer pipeServer;
    private OverlayViewModel? viewModel;

    public AppHost(CollectorOrchestrator orchestrator, PipeServer pipeServer)
    {
        this.orchestrator = orchestrator;
        this.pipeServer = pipeServer;
    }

    public void AttachViewModel(OverlayViewModel vm)
    {
        viewModel = vm;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await orchestrator.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await pipeServer.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        // 1. Collect raw telemetry from all collectors.
        var snapshot = await orchestrator.CollectAsync(cancellationToken).ConfigureAwait(false);

        // 2. Run aggregation: bottleneck detection.
        var perf = snapshot.Performance;
        if (perf.FrameTimeMilliseconds > 0 && perf.GpuBusyMilliseconds > 0)
        {
            snapshot.Performance.Bottleneck = BottleneckDetector.Detect(
                perf.FrameTimeMilliseconds,
                perf.GpuBusyMilliseconds,
                gpuBusyReliable: perf.GpuBusyMilliseconds > 0);
        }

        // 3. Broadcast to Game Bar widget via named pipe.
        await pipeServer.BroadcastAsync(snapshot, cancellationToken).ConfigureAwait(false);

        // 4. Push to overlay UI if a view model is attached.
        viewModel?.ApplyTelemetry(snapshot);
    }

    public async ValueTask DisposeAsync()
    {
        await pipeServer.DisposeAsync().ConfigureAwait(false);
    }
}
