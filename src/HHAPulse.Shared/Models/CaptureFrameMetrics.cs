using MessagePack;

namespace HHAPulse.Shared.Models;

[MessagePackObject]
public sealed class CaptureFrameMetrics
{
    [Key(0)]
    public double FramesPerSecond { get; set; }

    [Key(1)]
    public double AverageFramesPerSecond { get; set; }

    [Key(2)]
    public double OnePercentLowFramesPerSecond { get; set; }

    [Key(3)]
    public double ZeroPointOnePercentLowFramesPerSecond { get; set; }

    [Key(4)]
    public double FrameTimeMilliseconds { get; set; }

    [Key(5)]
    public double GpuBusyMilliseconds { get; set; }

    [Key(6)]
    public uint GameProcessId { get; set; }

    [Key(7)]
    public string GameProcessName { get; set; } = string.Empty;

    [Key(8)]
    public long TimestampUnixMilliseconds { get; set; }

    [Key(9)]
    public double AppFramesPerSecond { get; set; }

    [Key(10)]
    public double PresentFramesPerSecond { get; set; }

    [Key(11)]
    public double DisplayFramesPerSecond { get; set; }

    [Key(12)]
    public bool HybridPresentDetected { get; set; }
}
