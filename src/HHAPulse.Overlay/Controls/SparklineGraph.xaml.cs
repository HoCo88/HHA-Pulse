using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace HHAPulse.Overlay.Controls;

public sealed partial class SparklineGraph : UserControl
{
    private IReadOnlyList<double> values = Array.Empty<double>();

    public SparklineGraph()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Render();
    }

    public void SetValues(IReadOnlyList<double> samples)
    {
        values = samples;
        Render();
    }

    private void Render()
    {
        GraphCanvas.Children.Clear();
        if (values.Count < 2)
        {
            return;
        }

        var width = ActualWidth > 0 ? ActualWidth : 96;
        var height = ActualHeight > 0 ? ActualHeight : 22;
        var min = values.Min();
        var max = values.Max();
        var range = Math.Max(1, max - min);
        var points = new PointCollection();

        for (var index = 0; index < values.Count; index++)
        {
            var x = values.Count == 1 ? 0 : index * width / (values.Count - 1);
            var normalized = (values[index] - min) / range;
            var y = height - normalized * height;
            points.Add(new Point(x, y));
        }

        GraphCanvas.Children.Add(new Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 255, 136)),
            StrokeThickness = 2
        });
    }
}
