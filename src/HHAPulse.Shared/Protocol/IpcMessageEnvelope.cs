using MessagePack;

namespace HHAPulse.Shared.Protocol;

[MessagePackObject]
public sealed class IpcMessageEnvelope
{
    [Key(0)]
    public ushort Version { get; set; }

    [Key(1)]
    public IpcMessageType MessageType { get; set; }

    [Key(2)]
    public long TimestampUnixMilliseconds { get; set; }

    [Key(3)]
    public byte[] Payload { get; set; } = System.Array.Empty<byte>();
}
