using HHAPulse.CaptureService.Etw;
using HHAPulse.CaptureService.Ipc;
using HHAPulse.CaptureService.Sensors;
using HHAPulse.Shared.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HHAPulse.CaptureService;

public sealed class Worker : BackgroundService
{
    private readonly CapturePipeServer pipeServer;
    private readonly EtwFrameCapture frameCapture;
    private readonly CaptureServiceSensorCollector sensorCollector;
    private readonly ILogger<Worker> logger;

    public Worker(CapturePipeServer pipeServer, EtwFrameCapture frameCapture, CaptureServiceSensorCollector sensorCollector, ILogger<Worker> logger)
    {
        this.pipeServer = pipeServer;
        this.frameCapture = frameCapture;
        this.sensorCollector = sensorCollector;
        this.logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting HHA Pulse capture service.");
        pipeServer.TargetChanged += frameCapture.SetTarget;
        frameCapture.FrameMetricsAvailable += BroadcastFrameMetricsAsync;
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await pipeServer.StartAsync(stoppingToken).ConfigureAwait(false);
        await frameCapture.StartAsync(stoppingToken).ConfigureAwait(false);

        var sensorTask = RunSensorBroadcastLoopAsync(stoppingToken);
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        await sensorTask.ConfigureAwait(false);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping HHA Pulse capture service.");
        frameCapture.FrameMetricsAvailable -= BroadcastFrameMetricsAsync;
        pipeServer.TargetChanged -= frameCapture.SetTarget;
        sensorCollector.Dispose();
        await frameCapture.DisposeAsync().ConfigureAwait(false);
        await pipeServer.DisposeAsync().ConfigureAwait(false);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task BroadcastFrameMetricsAsync(CaptureFrameMetrics metrics)
    {
        return pipeServer.BroadcastAsync(sensorCollector.Enrich(metrics));
    }

    private async Task RunSensorBroadcastLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var metrics = sensorCollector.Enrich(new CaptureFrameMetrics
                {
                    HasFrameMetrics = false,
                    TimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
                await pipeServer.BroadcastAsync(metrics).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Capture service sensor telemetry publish failed.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
