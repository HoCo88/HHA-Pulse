using System;
using System.IO;

namespace HHAPulse.Shared.Protocol;

public static class PipeFrameCodec
{
    public static byte[] Encode(byte[] payload)
    {
        if (payload.Length > PipeConstants.MaxPayloadBytes)
        {
            throw new InvalidDataException($"IPC payload exceeds {PipeConstants.MaxPayloadBytes} bytes.");
        }

        var frame = new byte[PipeConstants.LengthPrefixBytes + payload.Length];
        var length = BitConverter.GetBytes(payload.Length);
        Buffer.BlockCopy(length, 0, frame, 0, PipeConstants.LengthPrefixBytes);
        Buffer.BlockCopy(payload, 0, frame, PipeConstants.LengthPrefixBytes, payload.Length);
        return frame;
    }

    public static byte[] Decode(byte[] frame)
    {
        if (frame.Length < PipeConstants.LengthPrefixBytes)
        {
            throw new InvalidDataException("IPC frame is shorter than the length prefix.");
        }

        var payloadLength = BitConverter.ToInt32(frame, 0);
        if (payloadLength < 0 || payloadLength > PipeConstants.MaxPayloadBytes)
        {
            throw new InvalidDataException($"Invalid IPC payload length {payloadLength}.");
        }

        if (frame.Length - PipeConstants.LengthPrefixBytes != payloadLength)
        {
            throw new InvalidDataException("IPC frame length does not match the length prefix.");
        }

        var payload = new byte[payloadLength];
        Buffer.BlockCopy(frame, PipeConstants.LengthPrefixBytes, payload, 0, payloadLength);
        return payload;
    }
}
