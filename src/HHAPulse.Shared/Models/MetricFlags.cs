using System;

namespace HHAPulse.Shared.Models;

[Flags]
public enum MetricFlags : ulong
{
    None = 0,
    Fps = 1UL << 0,
    FrameTime = 1UL << 1,
    Battery = 1UL << 5,
    CpuUsage = 1UL << 6,
    CpuTemperature = 1UL << 7,
    CpuPower = 1UL << 8,
    GpuUsage = 1UL << 9,
    GpuTemperature = 1UL << 10,
    GpuPower = 1UL << 11,
    Memory = 1UL << 12,
    Vram = 1UL << 13,
    Display = 1UL << 14,
    Fan = 1UL << 15
}
