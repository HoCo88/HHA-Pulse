using MessagePack;

namespace HHAPulse.Shared.Models;

[MessagePackObject]
public sealed class CaptureStatus
{
    [Key(0)]
    public bool ServiceConnected { get; set; }

    [Key(1)]
    public bool IsCapturing { get; set; }

    [Key(2)]
    public uint TargetProcessId { get; set; }

    [Key(3)]
    public string TargetProcessName { get; set; } = string.Empty;

    [Key(4)]
    public string LastErrorCode { get; set; } = string.Empty;

    [Key(5)]
    public string LastErrorMessage { get; set; } = string.Empty;

    [Key(6)]
    public long TimestampUnixMilliseconds { get; set; }
}
