using System;
using MessagePack;

namespace HHAPulse.Shared.Protocol;

public static class MessageSerializer
{
    private static readonly MessagePackSerializerOptions SerializerOptions =
        MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData);

    public static IpcMessageEnvelope CreateEnvelope<TPayload>(IpcMessageType messageType, TPayload payload)
    {
        return new IpcMessageEnvelope
        {
            Version = PipeConstants.ProtocolVersion,
            MessageType = messageType,
            TimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Payload = SerializePayload(payload)
        };
    }

    public static byte[] SerializePayload<TPayload>(TPayload payload)
    {
        return MessagePackSerializer.Serialize(payload, SerializerOptions);
    }

    public static TPayload DeserializePayload<TPayload>(IpcMessageEnvelope envelope)
    {
        if (envelope.Version != PipeConstants.ProtocolVersion)
        {
            throw new InvalidOperationException($"Unsupported IPC protocol version {envelope.Version}.");
        }

        return MessagePackSerializer.Deserialize<TPayload>(envelope.Payload, SerializerOptions);
    }

    public static byte[] SerializeEnvelope(IpcMessageEnvelope envelope)
    {
        return MessagePackSerializer.Serialize(envelope, SerializerOptions);
    }

    public static IpcMessageEnvelope DeserializeEnvelope(byte[] bytes)
    {
        var envelope = MessagePackSerializer.Deserialize<IpcMessageEnvelope>(bytes, SerializerOptions);
        if (envelope.Version != PipeConstants.ProtocolVersion)
        {
            throw new InvalidOperationException($"Unsupported IPC protocol version {envelope.Version}.");
        }

        return envelope;
    }
}
