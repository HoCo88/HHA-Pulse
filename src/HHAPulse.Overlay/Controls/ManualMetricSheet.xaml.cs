using System.Linq;
using HHAPulse.Overlay.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HHAPulse.Overlay.Controls;

public sealed partial class ManualMetricSheet : UserControl
{
    private static readonly (string Title, string[] MetricIds)[] Groups =
    {
        ("Performance", new[] { OverlayPresetCatalog.Fps, OverlayPresetCatalog.AvgFps, OverlayPresetCatalog.OnePercentLow, OverlayPresetCatalog.ZeroPointOneLow, OverlayPresetCatalog.FrameTime }),
        ("CPU", new[] { OverlayPresetCatalog.CpuUsage, OverlayPresetCatalog.CpuTemp, OverlayPresetCatalog.CpuPower, OverlayPresetCatalog.CpuClock }),
        ("GPU", new[] { OverlayPresetCatalog.GpuUsage, OverlayPresetCatalog.GpuTemp, OverlayPresetCatalog.GpuClock, OverlayPresetCatalog.GpuPower, OverlayPresetCatalog.GpuFan, OverlayPresetCatalog.Vram }),
        ("Memory", new[] { OverlayPresetCatalog.Ram }),
        ("Storage", new[] { OverlayPresetCatalog.StorageTemp, OverlayPresetCatalog.StorageWear }),
        ("System", new[] { OverlayPresetCatalog.DeviceTemp, OverlayPresetCatalog.RefreshRate, OverlayPresetCatalog.Battery, OverlayPresetCatalog.TotalPower })
    };

    private readonly HashSet<string> selectedMetricIds = new(StringComparer.OrdinalIgnoreCase);
    private bool suppressEvents;

    public ManualMetricSheet()
    {
        InitializeComponent();
    }

    public event Action<List<string>>? MetricsChanged;

    public void ApplySelection(IReadOnlyList<string> metricIds, string summaryText)
    {
        suppressEvents = true;
        try
        {
            selectedMetricIds.Clear();
            foreach (var metricId in metricIds)
            {
                selectedMetricIds.Add(metricId);
            }

            SummaryText.Text = summaryText;
            RenderGroups();
        }
        finally
        {
            suppressEvents = false;
        }
    }

    private void RenderGroups()
    {
        GroupHost.Children.Clear();
        foreach (var (title, metricIds) in Groups)
        {
            var section = new StackPanel { Spacing = 8 };
            section.Children.Add(new TextBlock
            {
                Text = title,
                Style = (Style)Application.Current.Resources["HhapMutedStyle"]
            });

            var wrap = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 };
            foreach (var metricId in metricIds)
            {
                var checkBox = new CheckBox
                {
                    Content = FriendlyMetricName(metricId),
                    IsChecked = selectedMetricIds.Contains(metricId),
                    Tag = metricId,
                    MinWidth = 148
                };
                checkBox.Checked += OnMetricCheckedChanged;
                checkBox.Unchecked += OnMetricCheckedChanged;
                wrap.Children.Add(checkBox);
            }

            section.Children.Add(wrap);
            GroupHost.Children.Add(section);
        }
    }

    private void OnMetricCheckedChanged(object sender, RoutedEventArgs e)
    {
        if (suppressEvents || sender is not CheckBox { Tag: string metricId } checkBox)
        {
            return;
        }

        if (checkBox.IsChecked == true)
        {
            selectedMetricIds.Add(metricId);
        }
        else
        {
            selectedMetricIds.Remove(metricId);
        }

        MetricsChanged?.Invoke(OverlayPresetCatalog.AllMetricIds.Where(selectedMetricIds.Contains).ToList());
    }

    private static string FriendlyMetricName(string metricId) => metricId switch
    {
        var id when id == OverlayPresetCatalog.Fps => "FPS",
        var id when id == OverlayPresetCatalog.AvgFps => "Average FPS",
        var id when id == OverlayPresetCatalog.OnePercentLow => "1% low",
        var id when id == OverlayPresetCatalog.ZeroPointOneLow => "0.1% low",
        var id when id == OverlayPresetCatalog.FrameTime => "Frametime",
        var id when id == OverlayPresetCatalog.CpuUsage => "CPU usage",
        var id when id == OverlayPresetCatalog.CpuTemp => "CPU temp",
        var id when id == OverlayPresetCatalog.CpuPower => "CPU power",
        var id when id == OverlayPresetCatalog.CpuClock => "CPU clock",
        var id when id == OverlayPresetCatalog.GpuUsage => "GPU usage",
        var id when id == OverlayPresetCatalog.GpuTemp => "GPU temp",
        var id when id == OverlayPresetCatalog.GpuClock => "GPU clock",
        var id when id == OverlayPresetCatalog.GpuPower => "GPU power",
        var id when id == OverlayPresetCatalog.GpuFan => "GPU fan",
        var id when id == OverlayPresetCatalog.Ram => "RAM",
        var id when id == OverlayPresetCatalog.Vram => "VRAM",
        var id when id == OverlayPresetCatalog.StorageTemp => "SSD temp",
        var id when id == OverlayPresetCatalog.StorageWear => "SSD wear",
        var id when id == OverlayPresetCatalog.DeviceTemp => "Device temp",
        var id when id == OverlayPresetCatalog.RefreshRate => "Refresh rate",
        var id when id == OverlayPresetCatalog.Battery => "Battery",
        var id when id == OverlayPresetCatalog.TotalPower => "Total power",
        _ => metricId
    };
}
