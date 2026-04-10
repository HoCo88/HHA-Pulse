using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using HHAPulse.Shared.Models;
using HHAPulse.Shared.Protocol;

namespace HHAPulse.Shared.Pipe;

public sealed class PipeClient : IAsyncDisposable
{
    private Stream? _stream;
    private NamedPipeClientStream? _pipe;

    public bool IsConnected => _stream is not null;

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
                _stream = _pipe;
                return; // connected
            }
            catch (TimeoutException)
            {
                await DisposePipeAsync().ConfigureAwait(false);
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
                delayMs = Math.Min(delayMs * 2, maxDelayMs);
            }
            catch (IOException)
            {
                await DisposePipeAsync().ConfigureAwait(false);
                if (TryOpenViaCreateFile(out var fallbackStream))
                {
                    _stream = fallbackStream;
                    return;
                }

                await Task.Delay(delayMs, ct).ConfigureAwait(false);
                delayMs = Math.Min(delayMs * 2, maxDelayMs);
            }
        }

        ct.ThrowIfCancellationRequested();
    }

    public async Task<TelemetrySnapshot> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Pipe client is not connected.");
        }

        try
        {
            var lengthBuffer = new byte[PipeConstants.LengthPrefixBytes];
            await ReadExactlyAsync(_stream, lengthBuffer, cancellationToken).ConfigureAwait(false);
            var payloadLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (payloadLength < 0 || payloadLength > PipeConstants.MaxPayloadBytes)
            {
                throw new InvalidDataException($"Invalid pipe payload length {payloadLength}.");
            }

            var payload = new byte[payloadLength];
            await ReadExactlyAsync(_stream, payload, cancellationToken).ConfigureAwait(false);
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
        _stream?.Dispose();
        _stream = null;

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

    private static bool TryOpenViaCreateFile(out FileStream? stream)
    {
        stream = null;

        var handle = CreateFileW(
            PipeConstants.PipeName,
            GenericRead,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            FileFlagOverlapped,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            handle.Dispose();
            return false;
        }

        stream = new FileStream(handle, FileAccess.Read, 4096, true);
        return true;
    }

    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileFlagOverlapped = 0x40000000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);
}
