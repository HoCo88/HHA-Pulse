using MessagePack;

namespace HHAPulse.Shared.Models;

[MessagePackObject]
public sealed class CaptureTarget
{
    [Key(0)]
    public uint ProcessId { get; set; }

    [Key(1)]
    public string ProcessName { get; set; } = string.Empty;

    [Key(2)]
    public long TimestampUnixMilliseconds { get; set; }
}
