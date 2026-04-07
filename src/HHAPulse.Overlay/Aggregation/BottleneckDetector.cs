using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Aggregation;

public static class BottleneckDetector
{
    public static BottleneckMetrics Detect(double frameTimeMilliseconds, double gpuBusyMilliseconds, bool gpuBusyReliable)
    {
        if (!gpuBusyReliable || frameTimeMilliseconds <= 0 || gpuBusyMilliseconds <= 0)
        {
            return new BottleneckMetrics
            {
                Kind = BottleneckKind.Unknown,
                Confidence = ConfidenceLevel.Low,
                FrameTimeMilliseconds = frameTimeMilliseconds,
                GpuBusyMilliseconds = gpuBusyMilliseconds
            };
        }

        var ratio = gpuBusyMilliseconds / frameTimeMilliseconds;
        var kind = ratio switch
        {
            >= 0.85 => BottleneckKind.GpuBound,
            <= 0.60 => BottleneckKind.CpuBound,
            _ => BottleneckKind.Balanced
        };

        return new BottleneckMetrics
        {
            Kind = kind,
            Confidence = ConfidenceLevel.High,
            FrameTimeMilliseconds = frameTimeMilliseconds,
            GpuBusyMilliseconds = gpuBusyMilliseconds
        };
    }
}
