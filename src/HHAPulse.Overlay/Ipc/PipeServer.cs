using System.Collections.Concurrent;
using System.IO.Pipes;
using HHAPulse.Shared.Models;
using HHAPulse.Shared.Protocol;

namespace HHAPulse.Overlay.Ipc;

public sealed class PipeServer : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, NamedPipeServerStream> clients = new();
    private readonly CancellationTokenSource shutdown = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = AcceptLoopAsync(shutdown.Token);
        return Task.CompletedTask;
    }

    public async Task BroadcastAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        var envelope = MessageSerializer.CreateEnvelope(IpcMessageType.TelemetrySnapshot, snapshot);
        var frame = PipeFrameCodec.Encode(MessageSerializer.SerializeEnvelope(envelope));

        foreach (var pair in clients.ToArray())
        {
            try
            {
                await pair.Value.WriteAsync(frame, 0, frame.Length, cancellationToken).ConfigureAwait(false);
                await pair.Value.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                if (clients.TryRemove(pair.Key, out var disconnected))
                {
                    disconnected.Dispose();
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        shutdown.Cancel();
        foreach (var client in clients.Values)
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }

        shutdown.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var server = CreateServerStream();
            try
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                clients.TryAdd(Guid.NewGuid(), server);
            }
            catch
            {
                server.Dispose();
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private static NamedPipeServerStream CreateServerStream()
    {
        return new NamedPipeServerStream(
            PipeConstants.PipeLocalName,
            PipeDirection.Out,
            maxNumberOfServerInstances: 8,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }
}
