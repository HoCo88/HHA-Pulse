namespace HHAPulse.Shared.Models;

public enum BottleneckKind
{
    Unknown = 0,
    CpuBound = 1,
    GpuBound = 2,
    Balanced = 3
}
