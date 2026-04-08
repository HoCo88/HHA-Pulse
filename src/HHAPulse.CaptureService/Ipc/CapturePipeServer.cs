using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using HHAPulse.Shared.Models;
using HHAPulse.Shared.Protocol;
using Microsoft.Extensions.Logging;

namespace HHAPulse.CaptureService.Ipc;

public sealed class CapturePipeServer : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, NamedPipeServerStream> outputClients = new();
    private readonly CancellationTokenSource shutdown = new();
    private readonly ILogger<CapturePipeServer> logger;

    public CapturePipeServer(ILogger<CapturePipeServer> logger)
    {
        this.logger = logger;
    }

    public event Action<CaptureTarget>? TargetChanged;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = AcceptOutputLoopAsync(shutdown.Token);
        _ = AcceptControlLoopAsync(shutdown.Token);
        return Task.CompletedTask;
    }

    public async Task BroadcastAsync(CaptureFrameMetrics metrics)
    {
        var envelope = MessageSerializer.CreateEnvelope(IpcMessageType.CaptureFrameMetrics, metrics);
        var frame = PipeFrameCodec.Encode(MessageSerializer.SerializeEnvelope(envelope));

        foreach (var pair in outputClients.ToArray())
        {
            try
            {
                await pair.Value.WriteAsync(frame, shutdown.Token).ConfigureAwait(false);
                await pair.Value.FlushAsync(shutdown.Token).ConfigureAwait(false);
            }
            catch
            {
                if (outputClients.TryRemove(pair.Key, out var disconnected))
                {
                    await disconnected.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        shutdown.Cancel();
        foreach (var client in outputClients.Values)
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }

        shutdown.Dispose();
    }

    private async Task AcceptOutputLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var server = CreateServerStream(PipeConstants.CaptureOutputPipeLocalName, PipeDirection.Out);
            try
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                outputClients.TryAdd(Guid.NewGuid(), server);
            }
            catch
            {
                server.Dispose();
                await DelayRetryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task AcceptControlLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var server = CreateServerStream(PipeConstants.CaptureControlPipeLocalName, PipeDirection.In);
            try
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => ReadControlClientAsync(server, cancellationToken), CancellationToken.None);
            }
            catch
            {
                server.Dispose();
                await DelayRetryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ReadControlClientAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
    {
        await using (server.ConfigureAwait(false))
        {
            while (server.IsConnected && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var payload = await ReadFrameAsync(server, cancellationToken).ConfigureAwait(false);
                    var envelope = MessageSerializer.DeserializeEnvelope(payload);
                    if (envelope.MessageType == IpcMessageType.CaptureTarget)
                    {
                        TargetChanged?.Invoke(MessageSerializer.DeserializePayload<CaptureTarget>(envelope));
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex) when (ex is IOException or EndOfStreamException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Invalid capture control message.");
                }
            }
        }
    }

    private static NamedPipeServerStream CreateServerStream(string pipeName, PipeDirection direction)
    {
        var pipeSecurity = new PipeSecurity();
        var worldInteractive = new SecurityIdentifier(WellKnownSidType.InteractiveSid, null);
        var localSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

        pipeSecurity.AddAccessRule(new PipeAccessRule(localSystem, PipeAccessRights.FullControl, AccessControlType.Allow));
        pipeSecurity.AddAccessRule(new PipeAccessRule(administrators, PipeAccessRights.FullControl, AccessControlType.Allow));
        pipeSecurity.AddAccessRule(new PipeAccessRule(worldInteractive, PipeAccessRights.ReadWrite, AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            pipeName,
            direction,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            pipeSecurity);
    }

    private static async Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[PipeConstants.LengthPrefixBytes];
        await ReadExactlyAsync(stream, lengthBuffer, cancellationToken).ConfigureAwait(false);
        var payloadLength = BitConverter.ToInt32(lengthBuffer, 0);
        if (payloadLength < 0 || payloadLength > PipeConstants.MaxPayloadBytes)
            throw new InvalidDataException($"Invalid capture control payload length {payloadLength}.");

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
                throw new EndOfStreamException("Capture control pipe closed.");

            offset += read;
        }
    }

    private static async Task DelayRetryAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
