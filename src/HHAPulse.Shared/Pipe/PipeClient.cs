using System.IO.Pipes;
using HHAPulse.Shared.Models;
using HHAPulse.Shared.Protocol;

namespace HHAPulse.Shared.Pipe;

public sealed class PipeClient : IAsyncDisposable
{
    private NamedPipeClientStream? _pipe;

    public bool IsConnected => _pipe is { IsConnected: true };

    public async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        int delayMs = 100;
        const int maxDelayMs = 5000;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                _pipe = new NamedPipeClientStream(".", PipeConstants.PipeLocalName, PipeDirection.In);
                await _pipe.ConnectAsync(2000, ct).ConfigureAwait(false);
                return; // connected
            }
            catch (TimeoutException)
            {
                _pipe?.Dispose();
                _pipe = null;
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
                delayMs = Math.Min(delayMs * 2, maxDelayMs);
            }
            catch (IOException)
            {
                _pipe?.Dispose();
                _pipe = null;
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
                delayMs = Math.Min(delayMs * 2, maxDelayMs);
            }
        }

        ct.ThrowIfCancellationRequested();
    }

    public async Task<TelemetrySnapshot> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        if (_pipe is null || !_pipe.IsConnected)
        {
            throw new InvalidOperationException("Pipe client is not connected.");
        }

        try
        {
            var lengthBuffer = new byte[PipeConstants.LengthPrefixBytes];
            await ReadExactlyAsync(_pipe, lengthBuffer, cancellationToken).ConfigureAwait(false);
            var payloadLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (payloadLength < 0 || payloadLength > PipeConstants.MaxPayloadBytes)
            {
                throw new InvalidDataException($"Invalid pipe payload length {payloadLength}.");
            }

            var payload = new byte[payloadLength];
            await ReadExactlyAsync(_pipe, payload, cancellationToken).ConfigureAwait(false);
            var envelope = MessageSerializer.DeserializeEnvelope(payload);
            return MessageSerializer.DeserializePayload<TelemetrySnapshot>(envelope);
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException)
        {
            // Pipe is broken — clean up so callers can detect via IsConnected and reconnect.
            await DisposePipeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task ReconnectIfBrokenAsync(CancellationToken ct)
    {
        if (IsConnected)
        {
            return;
        }

        await DisposePipeAsync().ConfigureAwait(false);
        await ConnectWithRetryAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposePipeAsync().ConfigureAwait(false);
    }

    private ValueTask DisposePipeAsync()
    {
        if (_pipe is not null)
        {
            _pipe.Dispose();
            _pipe = null;
        }
        return default;
    }

    private static async Task ReadExactlyAsync(Stream input, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await input.ReadAsync(buffer, offset, buffer.Length - offset, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Pipe closed before the frame was fully read.");
            }

            offset += read;
        }
    }
}
