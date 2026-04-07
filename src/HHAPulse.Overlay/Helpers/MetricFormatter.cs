namespace HHAPulse.Overlay.Helpers;

public static class MetricFormatter
{
    public static string Fps(double value) => value > 0 ? $"{value:0} FPS" : "-- FPS";

    public static string Temperature(double value) => value > 0 ? $"{value:0}C" : "--C";

    public static string Watts(double value) => value > 0 ? $"{value:0.0}W" : "--W";

    public static string Percent(double value) => value >= 0 ? $"{value:0}%" : "--%";

    public static string Minutes(double value)
    {
        if (value <= 0)
        {
            return "--";
        }

        var totalMinutes = (int)Math.Round(value);
        return $"{totalMinutes / 60}h {totalMinutes % 60:00}m";
    }
}
