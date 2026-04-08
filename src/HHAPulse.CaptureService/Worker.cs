using HHAPulse.CaptureService.Etw;
using HHAPulse.CaptureService.Ipc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HHAPulse.CaptureService;

public sealed class Worker : BackgroundService
{
    private readonly CapturePipeServer pipeServer;
    private readonly EtwFrameCapture frameCapture;
    private readonly ILogger<Worker> logger;

    public Worker(CapturePipeServer pipeServer, EtwFrameCapture frameCapture, ILogger<Worker> logger)
    {
        this.pipeServer = pipeServer;
        this.frameCapture = frameCapture;
        this.logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting HHA Pulse capture service.");
        pipeServer.TargetChanged += frameCapture.SetTarget;
        frameCapture.FrameMetricsAvailable += pipeServer.BroadcastAsync;
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await pipeServer.StartAsync(stoppingToken).ConfigureAwait(false);
        await frameCapture.StartAsync(stoppingToken).ConfigureAwait(false);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping HHA Pulse capture service.");
        frameCapture.FrameMetricsAvailable -= pipeServer.BroadcastAsync;
        pipeServer.TargetChanged -= frameCapture.SetTarget;
        await frameCapture.DisposeAsync().ConfigureAwait(false);
        await pipeServer.DisposeAsync().ConfigureAwait(false);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
