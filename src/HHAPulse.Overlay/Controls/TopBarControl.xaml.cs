using System.ComponentModel;
using HHAPulse.Overlay.Helpers;
using HHAPulse.Overlay.Settings;
using HHAPulse.Overlay.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace HHAPulse.Overlay.Controls;

public sealed partial class TopBarControl : UserControl
{
    private readonly Dictionary<string, TextBlock> metricLabels = new(StringComparer.OrdinalIgnoreCase);
    private OverlayViewModel? viewModel;

    public TopBarControl()
    {
        InitializeComponent();
        Unloaded += (_, _) => DetachViewModel();
    }

    public OverlayViewModel? ViewModel
    {
        get => viewModel;
        set
        {
            if (ReferenceEquals(viewModel, value))
            {
                return;
            }

            DetachViewModel();
            viewModel = value;
            if (viewModel is not null)
            {
                viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }

            RebuildMetricViews();
        }
    }

    private void DetachViewModel()
    {
        if (viewModel is not null)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (args.PropertyName == nameof(OverlayViewModel.TopBarMetricIds) ||
                args.PropertyName == nameof(OverlayViewModel.ActivePreset))
            {
                RebuildMetricViews();
                return;
            }

            if (args.PropertyName == nameof(OverlayViewModel.CurrentSnapshot))
            {
                UpdateMetricValues();
            }
        });
    }

    private void RebuildMetricViews()
    {
        metricLabels.Clear();
        MetricsPanel.Children.Clear();

        if (viewModel is null)
        {
            return;
        }

        foreach (var metricId in viewModel.TopBarMetricIds)
        {
            var label = new TextBlock
            {
                Foreground = GetMetricColor(metricId),
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center
            };
            metricLabels[metricId] = label;
            MetricsPanel.Children.Add(label);
        }

        if (metricLabels.Count == 0)
        {
            MetricsPanel.Children.Add(new TextBlock
            {
                Text = "Overlay off",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI")
            });
        }

        UpdateMetricValues();
    }

    private void UpdateMetricValues()
    {
        if (viewModel is null)
        {
            return;
        }

        var snapshot = viewModel.CurrentSnapshot;
        foreach (var pair in metricLabels)
        {
            pair.Value.Text = MetricFormatter.FormatMetric(pair.Key, snapshot);
        }

    }

    private static SolidColorBrush GetMetricColor(string metricId)
    {
        return metricId switch
        {
            Settings.OverlayPresetCatalog.Fps or
            Settings.OverlayPresetCatalog.OnePercentLow =>
                new SolidColorBrush(Color.FromArgb(255, 0, 255, 136)),  // green

            Settings.OverlayPresetCatalog.CpuUsage =>
                new SolidColorBrush(Color.FromArgb(255, 0, 200, 255)),  // cyan

            Settings.OverlayPresetCatalog.GpuUsage or
            Settings.OverlayPresetCatalog.Vram =>
                new SolidColorBrush(Color.FromArgb(255, 255, 107, 107)),  // red

            Settings.OverlayPresetCatalog.Battery =>
                new SolidColorBrush(Color.FromArgb(255, 255, 215, 0)),  // gold

            Settings.OverlayPresetCatalog.Ram =>
                new SolidColorBrush(Color.FromArgb(255, 200, 160, 255)),  // purple

            _ => new SolidColorBrush(Colors.White)
        };
    }

}
