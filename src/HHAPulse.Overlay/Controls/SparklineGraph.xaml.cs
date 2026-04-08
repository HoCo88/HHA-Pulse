using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace HHAPulse.Overlay.Controls;

public sealed partial class SparklineGraph : UserControl
{
    private readonly List<(IReadOnlyList<double> values, SolidColorBrush brush)> series = new();

    public SparklineGraph()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Render();
    }

    /// <summary>
    /// Replaces all series and re-renders.
    /// Each series is a (values, color) tuple drawn as a separate line.
    /// All series share the same Y-axis scale.
    /// </summary>
    public void SetSeries(IReadOnlyList<(IReadOnlyList<double> values, Color color)> newSeries)
    {
        series.Clear();
        foreach (var (values, color) in newSeries)
        {
            if (values.Count > 0)
            {
                series.Add((values, new SolidColorBrush(color)));
            }
        }

        Render();
    }

    /// <summary>
    /// Simple single-series API for backward compat.
    /// </summary>
    public void SetValues(IReadOnlyList<double> samples)
    {
        series.Clear();
        if (samples.Count > 0)
        {
            series.Add((samples, new SolidColorBrush(Color.FromArgb(255, 0, 255, 136))));
        }

        Render();
    }

    private void Render()
    {
        GraphCanvas.Children.Clear();
        if (series.Count == 0)
        {
            return;
        }

        var width = ActualWidth > 0 ? ActualWidth : 80;
        var height = ActualHeight > 0 ? ActualHeight : 20;

        // Compute global min/max across all series for a shared Y-axis.
        var globalMin = double.MaxValue;
        var globalMax = double.MinValue;

        foreach (var (values, _) in series)
        {
            foreach (var v in values)
            {
                if (v < globalMin) globalMin = v;
                if (v > globalMax) globalMax = v;
            }
        }

        var range = Math.Max(1, globalMax - globalMin);

        // Draw each series as a polyline.
        foreach (var (values, brush) in series)
        {
            if (values.Count < 2)
            {
                continue;
            }

            var points = new PointCollection();
            for (var i = 0; i < values.Count; i++)
            {
                var x = values.Count == 1 ? 0 : i * width / (values.Count - 1);
                var normalized = (values[i] - globalMin) / range;
                var y = height - normalized * height;
                points.Add(new Point(x, y));
            }

            GraphCanvas.Children.Add(new Polyline
            {
                Points = points,
                Stroke = brush,
                StrokeThickness = 1.5
            });
        }
    }
}
