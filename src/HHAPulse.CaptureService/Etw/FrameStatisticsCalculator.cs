namespace HHAPulse.CaptureService.Etw;

internal static class FrameStatisticsCalculator
{
    public static double CalculateFramesPerSecond(double[] frameTimesMs)
    {
        if (frameTimesMs.Length == 0)
        {
            return 0;
        }

        var avgFrameTime = frameTimesMs.Average();
        return avgFrameTime > 0 ? 1000.0 / avgFrameTime : 0;
    }

    public static double CalculateAverageFrameTime(double[] frameTimesMs)
    {
        return frameTimesMs.Length == 0 ? 0 : frameTimesMs.Average();
    }

    public static double PercentileLowFps(double[] frameTimesMs, double percentile)
    {
        if (frameTimesMs.Length == 0)
            return 0;

        Array.Sort(frameTimesMs);
        var index = Math.Clamp((int)Math.Ceiling((frameTimesMs.Length - 1) * percentile), 0, frameTimesMs.Length - 1);
        return 1000.0 / frameTimesMs[index];
    }
}
