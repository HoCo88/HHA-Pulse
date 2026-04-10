using HHAPulse.Overlay.Collectors;
using HHAPulse.Overlay.Diagnostics;
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
        TelemetryContractGuard.ApplyTo(snapshot);
        SystemPowerValidator.ApplyTo(snapshot);
        snapshot.Dependencies.WidgetClientCount = pipeServer.ConnectedClientCount;
        snapshot.Dependencies.WidgetPipeStatusMessage = pipeServer.ConnectedClientCount > 0
            ? $"{pipeServer.ConnectedClientCount} companion client(s) last observed during broadcast writes."
            : "No companion widget clients observed.";
        snapshot.MetricStatuses = MetricStatusFactory.Create(snapshot);
        snapshot.MeasurementTraces = MeasurementTraceFactory.Create(snapshot, snapshot.MetricStatuses);

        await pipeServer.BroadcastAsync(snapshot, cancellationToken).ConfigureAwait(false);

        viewModel?.ApplyTelemetry(snapshot);
    }

    public async ValueTask DisposeAsync()
    {
        await pipeServer.DisposeAsync().ConfigureAwait(false);
        orchestrator.Dispose();
    }
}
