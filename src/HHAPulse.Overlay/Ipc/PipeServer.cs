using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
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
        bool isFirstInstance = true;

        while (!cancellationToken.IsCancellationRequested)
        {
            var server = CreateServerStream(isFirstInstance);
            isFirstInstance = false;

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

    private static NamedPipeServerStream CreateServerStream(bool firstInstance)
    {
        var pipeSecurity = new PipeSecurity();

        // Current user = full control
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            WindowsIdentity.GetCurrent().Owner!,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        // TODO: For cross-package IPC (e.g. Game Bar widget in a separate MSIX),
        // add the Package SID here. Same-MSIX-package processes share identity.

        var options = PipeOptions.Asynchronous;
        if (firstInstance)
        {
            options |= PipeOptions.FirstPipeInstance;
        }

        return NamedPipeServerStreamAcl.Create(
            PipeConstants.PipeLocalName,
            PipeDirection.Out,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            options,
            0, 0,
            pipeSecurity);
    }
}
