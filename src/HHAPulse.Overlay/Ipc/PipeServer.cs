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

    public int ConnectedClientCount => clients.Count;

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
        using var currentIdentity = WindowsIdentity.GetCurrent();

        if (currentIdentity.User is not null)
        {
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                currentIdentity.User,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));
        }

        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        // AppContainer clients such as the Game Bar widget need explicit pipe access.
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier("S-1-15-2-1"),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier("S-1-15-2-2"),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

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
