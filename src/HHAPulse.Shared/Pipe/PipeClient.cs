using System.IO.Pipes;
using HHAPulse.Shared.Models;
using HHAPulse.Shared.Protocol;

namespace HHAPulse.Shared.Pipe;

public sealed class PipeClient : IDisposable
{
    private NamedPipeClientStream? stream;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        stream = new NamedPipeClientStream(".", PipeConstants.PipeLocalName, PipeDirection.In, PipeOptions.Asynchronous);
        await stream.ConnectAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TelemetrySnapshot> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        if (stream is null)
        {
            throw new InvalidOperationException("Pipe client is not connected.");
        }

        var lengthBuffer = new byte[PipeConstants.LengthPrefixBytes];
        await ReadExactlyAsync(stream, lengthBuffer, cancellationToken).ConfigureAwait(false);
        var payloadLength = BitConverter.ToInt32(lengthBuffer, 0);
        if (payloadLength < 0 || payloadLength > PipeConstants.MaxPayloadBytes)
        {
            throw new InvalidDataException($"Invalid pipe payload length {payloadLength}.");
        }

        var payload = new byte[payloadLength];
        await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        var envelope = MessageSerializer.DeserializeEnvelope(payload);
        return MessageSerializer.DeserializePayload<TelemetrySnapshot>(envelope);
    }

    public void Dispose()
    {
        stream?.Dispose();
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
