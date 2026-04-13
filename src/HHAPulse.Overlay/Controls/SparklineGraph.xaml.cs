using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace HHAPulse.Overlay.Controls;

public sealed partial class SparklineGraph : UserControl
{
    private readonly List<IReadOnlyList<double>> series = new();
    private Brush? strokeBrush;

    public SparklineGraph()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Render();
        Loaded += (_, _) => Render();
    }

    private Brush GetStrokeBrush()
    {
        if (strokeBrush is not null) return strokeBrush;
        if (Application.Current?.Resources is { } res &&
            res.TryGetValue("HhapSparklineStrokeGradient", out var obj) &&
            obj is Brush b)
        {
            strokeBrush = b;
            return b;
        }
        strokeBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x9A, 0xE7, 0xFF));
        return strokeBrush;
    }

    /// <summary>
    /// Replaces all series and re-renders. Per-series colors are ignored —
    /// every polyline uses the shared sparkline gradient to keep the HUD cohesive.
    /// </summary>
    public void SetSeries(IReadOnlyList<(IReadOnlyList<double> values, Color color)> newSeries)
    {
        series.Clear();
        foreach (var (values, _) in newSeries)
        {
            if (values.Count > 0)
            {
                series.Add(values);
            }
        }

        Render();
    }

    public void SetValues(IReadOnlyList<double> samples)
    {
        series.Clear();
        if (samples.Count > 0)
        {
            series.Add(samples);
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

        var globalMin = double.MaxValue;
        var globalMax = double.MinValue;

        foreach (var values in series)
        {
            foreach (var v in values)
            {
                if (v < globalMin) globalMin = v;
                if (v > globalMax) globalMax = v;
            }
        }

        var range = Math.Max(1, globalMax - globalMin);
        var stroke = GetStrokeBrush();

        foreach (var values in series)
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
                Stroke = stroke,
                StrokeThickness = 1.5
            });
        }
    }
}
