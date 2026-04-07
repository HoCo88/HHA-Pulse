namespace HHAPulse.Overlay.Helpers;

public static class LayoutCalculator
{
    public static double CalculateTopBarWidth(int enabledMetricCount, double fontSize)
    {
        var safeMetricCount = Math.Max(1, enabledMetricCount);
        return Math.Clamp(120 + safeMetricCount * fontSize * 5, 240, 1920);
    }
}
