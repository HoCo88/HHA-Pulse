using System.IO.Pipes;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Shared.Models;
using HHAPulse.Shared.Protocol;

namespace HHAPulse.Overlay.Collectors.Fps;

public sealed class CaptureServiceCollector : IMetricCollector
{
    private static readonly TimeSpan FreshnessWindow = TimeSpan.FromSeconds(2);
    private readonly object syncRoot = new();
    private readonly CancellationTokenSource shutdown = new();
    private CaptureFrameMetrics? latestMetrics;
    private CaptureTarget? target;
    private Task? outputTask;
    private Task? controlTask;
    private bool isConnected;
    private string lastStatus = "Capture service not connected.";

    public string Name => "Capture Service";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        outputTask = Task.Run(() => RunOutputLoopAsync(shutdown.Token), CancellationToken.None);
        controlTask = Task.Run(() => RunControlLoopAsync(shutdown.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public void SetTarget(CaptureTarget? captureTarget)
    {
        lock (syncRoot)
        {
            target = captureTarget;
        }
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        CaptureFrameMetrics? metrics;
        bool connected;
        string status;
        lock (syncRoot)
        {
            metrics = latestMetrics;
            connected = isConnected;
            status = lastStatus;
        }

        snapshot.Dependencies.CaptureServiceConnected = connected;
        snapshot.Dependencies.CaptureServiceStatusMessage = status;

        if (metrics is null)
            return Task.CompletedTask;

        var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(metrics.TimestampUnixMilliseconds);
        if (age > FreshnessWindow || metrics.FramesPerSecond <= 0)
            return Task.CompletedTask;

        snapshot.AvailableMetrics |= MetricFlags.Fps | MetricFlags.FrameTime;
        snapshot.Performance.FramesPerSecond = metrics.FramesPerSecond;
        snapshot.Performance.AverageFramesPerSecond = metrics.AverageFramesPerSecond;
        snapshot.Performance.OnePercentLowFramesPerSecond = metrics.OnePercentLowFramesPerSecond;
        snapshot.Performance.ZeroPointOnePercentLowFramesPerSecond = metrics.ZeroPointOnePercentLowFramesPerSecond;
        snapshot.Performance.FrameTimeMilliseconds = metrics.FrameTimeMilliseconds;
        snapshot.Performance.GpuBusyMilliseconds = metrics.GpuBusyMilliseconds;
        snapshot.Dependencies.CaptureTargetProcessId = metrics.GameProcessId;
        snapshot.Dependencies.CaptureTargetProcessName = metrics.GameProcessName;
        return Task.CompletedTask;
    }

    private async Task RunOutputLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeClientStream(
                    ".",
                    PipeConstants.CaptureOutputPipeLocalName,
                    PipeDirection.In,
                    PipeOptions.Asynchronous);

                await pipe.ConnectAsync(2000, cancellationToken).ConfigureAwait(false);
                SetConnectionState(true, "FPS capture connected.");

                while (pipe.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    var frame = await ReadFrameAsync(pipe, cancellationToken).ConfigureAwait(false);
                    var envelope = MessageSerializer.DeserializeEnvelope(frame);
                    if (envelope.MessageType == IpcMessageType.CaptureFrameMetrics)
                    {
                        var metrics = MessageSerializer.DeserializePayload<CaptureFrameMetrics>(envelope);
                        lock (syncRoot)
                        {
                            latestMetrics = metrics;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or TimeoutException)
            {
                SetConnectionState(false, "Capture service not connected.");
                await DelayReconnectAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Capture service output pipe failed.", ex);
                SetConnectionState(false, "Capture service connection failed.");
                await DelayReconnectAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task RunControlLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeClientStream(
                    ".",
                    PipeConstants.CaptureControlPipeLocalName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous);

                await pipe.ConnectAsync(2000, cancellationToken).ConfigureAwait(false);

                CaptureTarget? previous = null;
                while (pipe.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    CaptureTarget? current;
                    lock (syncRoot)
                    {
                        current = target;
                    }

                    if (!SameTarget(previous, current))
                    {
                        var targetMessage = current ?? new CaptureTarget
                        {
                            TimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        };
                        var envelope = MessageSerializer.CreateEnvelope(IpcMessageType.CaptureTarget, targetMessage);
                        var bytes = PipeFrameCodec.Encode(MessageSerializer.SerializeEnvelope(envelope));
                        await pipe.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                        await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
                        previous = current;
                    }

                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or TimeoutException)
            {
                await DelayReconnectAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Capture service control pipe failed.", ex);
                await DelayReconnectAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void SetConnectionState(bool connected, string status)
    {
        lock (syncRoot)
        {
            isConnected = connected;
            lastStatus = status;
        }
    }

    private static bool SameTarget(CaptureTarget? left, CaptureTarget? right)
    {
        return left?.ProcessId == right?.ProcessId && string.Equals(left?.ProcessName, right?.ProcessName, StringComparison.Ordinal);
    }

    private static async Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[PipeConstants.LengthPrefixBytes];
        await ReadExactlyAsync(stream, lengthBuffer, cancellationToken).ConfigureAwait(false);
        var payloadLength = BitConverter.ToInt32(lengthBuffer, 0);
        if (payloadLength < 0 || payloadLength > PipeConstants.MaxPayloadBytes)
            throw new InvalidDataException($"Invalid capture pipe payload length {payloadLength}.");

        var payload = new byte[payloadLength];
        await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("Capture pipe closed.");

            offset += read;
        }
    }

    private static async Task DelayReconnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
