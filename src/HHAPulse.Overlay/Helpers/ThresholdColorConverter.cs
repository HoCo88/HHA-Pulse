using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace HHAPulse.Overlay.Helpers;

public static class ThresholdColorConverter
{
    public static SolidColorBrush TemperatureBrush(double celsius, double yellowAt = 70, double redAt = 85)
    {
        if (celsius >= redAt)
        {
            return new SolidColorBrush(Colors.IndianRed);
        }

        if (celsius >= yellowAt)
        {
            return new SolidColorBrush(Colors.Goldenrod);
        }

        return new SolidColorBrush(Colors.LightGreen);
    }
}
