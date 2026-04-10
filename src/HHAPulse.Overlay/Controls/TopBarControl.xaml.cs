using System.ComponentModel;
using HHAPulse.Overlay.Helpers;
using HHAPulse.Overlay.Settings;
using HHAPulse.Overlay.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace HHAPulse.Overlay.Controls;

public sealed partial class TopBarControl : UserControl
{
    // Component metric groups.
    private static readonly string[] FpsMetrics =
    {
        OverlayPresetCatalog.Fps, OverlayPresetCatalog.AvgFps,
        OverlayPresetCatalog.OnePercentLow, OverlayPresetCatalog.ZeroPointOneLow
    };

    private static readonly string[] FrametimeMetrics =
    {
        OverlayPresetCatalog.FrameTime
    };

    private static readonly string[] CpuMetrics =
    {
        OverlayPresetCatalog.CpuUsage, OverlayPresetCatalog.CpuTemp, OverlayPresetCatalog.CpuPower
    };

    // GPU + VRAM grouped together — no separate VRAM label needed.
    private static readonly string[] GpuMetrics =
    {
        OverlayPresetCatalog.GpuUsage, OverlayPresetCatalog.GpuTemp,
        OverlayPresetCatalog.GpuClock, OverlayPresetCatalog.GpuPower,
        OverlayPresetCatalog.Vram
    };

    private static readonly string[] MemoryMetrics =
    {
        OverlayPresetCatalog.Ram
    };

    private static readonly string[] SystemMetrics =
    {
        OverlayPresetCatalog.DeviceTemp, OverlayPresetCatalog.GpuFan,
        OverlayPresetCatalog.RefreshRate, OverlayPresetCatalog.Battery,
        OverlayPresetCatalog.TotalPower
    };

    // FPS sub-labels.
    private static readonly Dictionary<string, string> FpsSubLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        [OverlayPresetCatalog.Fps] = "FPS",
        [OverlayPresetCatalog.AvgFps] = "AVG",
        [OverlayPresetCatalog.OnePercentLow] = "1%",
        [OverlayPresetCatalog.ZeroPointOneLow] = "0.1%",
    };

    // System metric icons (Segoe Fluent Icons).
    private static readonly Dictionary<string, (string glyph, bool isIcon)> SystemLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        [OverlayPresetCatalog.RefreshRate] = ("\uE7F8", true),
        [OverlayPresetCatalog.Battery] = ("BAT", false),
        [OverlayPresetCatalog.TotalPower] = ("\uEC48", true),
    };

    private const double ValueFontSize = 15;
    private const double LabelFontSize = 14;
    private const double IconFontSize = 13;
    private static readonly Size Unbounded = new(double.PositiveInfinity, double.PositiveInfinity);
    private static readonly FontFamily Font = new("Segoe UI");
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons");
    private static readonly SolidColorBrush LabelBrush = new(Color.FromArgb(180, 160, 165, 185));
    private static readonly SolidColorBrush SepBrush = new(Color.FromArgb(50, 255, 255, 255));

    // Value colors.
    private static readonly SolidColorBrush FpsGreen = new(Color.FromArgb(255, 0, 255, 136));
    private static readonly SolidColorBrush AvgGreen = new(Color.FromArgb(200, 0, 200, 100));
    private static readonly SolidColorBrush OnePercentYellow = new(Color.FromArgb(255, 255, 200, 60));
    private static readonly SolidColorBrush ZeroPointOneOrange = new(Color.FromArgb(255, 255, 120, 60));
    private static readonly SolidColorBrush CpuCyan = new(Color.FromArgb(255, 0, 200, 255));
    private static readonly SolidColorBrush GpuRed = new(Color.FromArgb(255, 255, 107, 107));
    private static readonly SolidColorBrush TempOrange = new(Color.FromArgb(255, 255, 140, 80));
    private static readonly SolidColorBrush PowerYellow = new(Color.FromArgb(220, 255, 200, 100));
    private static readonly SolidColorBrush FtTeal = new(Color.FromArgb(255, 0, 220, 180));
    private static readonly SolidColorBrush LatAmber = new(Color.FromArgb(255, 255, 180, 0));
    private static readonly SolidColorBrush RamPurple = new(Color.FromArgb(255, 200, 160, 255));
    private static readonly SolidColorBrush BatGold = new(Color.FromArgb(255, 255, 215, 0));
    private static readonly SolidColorBrush HzSilver = new(Color.FromArgb(255, 180, 180, 200));

    private readonly Dictionary<string, TextBlock> valueBlocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TextBlock> labelBlocks = new();
    private SparklineGraph? fpsSparkline;
    private SparklineGraph? ftSparkline;
    private OverlayViewModel? viewModel;
    private bool rebuilding;
    private double textOpacity = 1.0;

    public event EventHandler? LayoutMetricsChanged;
    public int RowCount { get; private set; } = 1;
    public bool FpsGroupIsTwoRow { get; private set; }
    public int EstimatedWidth { get; private set; } = 200;

    public TopBarControl()
    {
        InitializeComponent();
        Unloaded += (_, _) => DetachViewModel();
    }

    public void ApplyTextSize(double size)
    {
        foreach (var vb in valueBlocks.Values)
            vb.FontSize = size;
        var labelSize = Math.Max(size - 1, 10);
        foreach (var lb in labelBlocks)
            lb.FontSize = labelSize;
        // Trigger re-measure after size change.
        if (!rebuilding)
        {
            var w = Measure();
            if (w != EstimatedWidth) Notify(RowCount, w);
        }
    }

    public void ApplyOpacity(double bgOpacity, double newTextOpacity)
    {
        // With TransparentBackdrop installed on the Window (see
        // TransparentWindowHelper.MakeOverlay step 0), the SwapChain clear
        // color is fully transparent, so alpha=0 on the XAML RootBorder
        // background now composites through to the desktop correctly. No
        // alpha floor workaround is needed — a single clamp(0..255) path
        // covers fully-transparent through fully-opaque in one branch.
        byte a = (byte)Math.Clamp(bgOpacity * 255, 0, 255);
        RootBorder.Background = new SolidColorBrush(Color.FromArgb(a, 16, 16, 32));

        textOpacity = newTextOpacity;
        foreach (var vb in valueBlocks.Values) vb.Opacity = textOpacity;
    }

    public OverlayViewModel? ViewModel
    {
        get => viewModel;
        set
        {
            if (ReferenceEquals(viewModel, value)) return;
            DetachViewModel();
            viewModel = value;
            if (viewModel is not null) viewModel.PropertyChanged += OnVmChanged;
            RebuildMetricViews();
        }
    }

    private void DetachViewModel()
    {
        if (viewModel is not null) { viewModel.PropertyChanged -= OnVmChanged; viewModel = null; }
    }

    private void OnVmChanged(object? s, PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (e.PropertyName is nameof(OverlayViewModel.TopBarMetricIds)
                or nameof(OverlayViewModel.ActivePreset))
            {
                RebuildMetricViews();
                return;
            }

            if (e.PropertyName == nameof(OverlayViewModel.CurrentSnapshot))
                UpdateValues();

            if (e.PropertyName is nameof(OverlayViewModel.FpsHistory)
                or nameof(OverlayViewModel.AvgFpsHistory)
                or nameof(OverlayViewModel.OnePercentLowHistory)
                or nameof(OverlayViewModel.ZeroPointOneLowHistory))
                UpdateFpsGraph();

            if (e.PropertyName == nameof(OverlayViewModel.FrameTimeHistory))
                UpdateFtGraph();
        });
    }

    // ── Layout rebuild ──

    private void RebuildMetricViews()
    {
        if (rebuilding) return;
        rebuilding = true;
        try
        {
            valueBlocks.Clear();
            labelBlocks.Clear();
            fpsSparkline = null;
            ftSparkline = null;
            RowsPanel.Children.Clear();
            FpsGroupIsTwoRow = false;

            if (viewModel is null) { Notify(1, 200); return; }

            var ids = new HashSet<string>(viewModel.TopBarMetricIds, StringComparer.OrdinalIgnoreCase);

            if (ids.Count == 0)
            {
                var r = Row();
                r.Children.Add(Txt("Overlay off", new SolidColorBrush(Colors.White), ValueFontSize));
                RowsPanel.Children.Add(r);
                Notify(1, Measure());
                return;
            }

            // Build component elements.
            var perf = new List<UIElement>();
            var hw = new List<UIElement>();

            AddComp(perf, BuildFps(ids));
            AddComp(perf, BuildFrametime(ids));
            AddComp(hw, BuildHw("CPU", CpuMetrics, ids, CpuCyan));
            AddComp(hw, BuildGpu(ids));
            AddComp(hw, BuildMemory(ids));
            AddComp(hw, BuildSystem(ids));

            // Always try single row first. Measure. Split only if it doesn't fit.
            var singleRow = Row();
            foreach (var e in perf) singleRow.Children.Add(e);
            if (perf.Count > 0 && hw.Count > 0) singleRow.Children.Add(Sep());
            foreach (var e in hw) singleRow.Children.Add(e);

            // Populate values before measuring so widths are realistic.
            UpdateValues();

            singleRow.Measure(Unbounded);
            var singleWidth = (int)Math.Ceiling(singleRow.DesiredSize.Width + RootBorder.Padding.Left + RootBorder.Padding.Right);
            var screenWidth = GetScreenWidth();

            if (singleWidth <= screenWidth)
            {
                // Fits in one row.
                RowsPanel.Children.Add(singleRow);
                Notify(1, singleWidth);
            }
            else
            {
                // Doesn't fit — split: perf on row 1, hw on row 2.
                // Detach elements from singleRow first.
                singleRow.Children.Clear();

                var r1 = Row(); foreach (var e in perf) r1.Children.Add(e);
                var r2 = Row(); foreach (var e in hw) r2.Children.Add(e);
                RowsPanel.Children.Add(r1);
                if (r2.Children.Count > 0) RowsPanel.Children.Add(r2);
                Notify(r2.Children.Count > 0 ? 2 : 1, Measure());
            }

            UpdateFpsGraph();
            UpdateFtGraph();
            foreach (var vb in valueBlocks.Values) vb.Opacity = textOpacity;
        }
        finally { rebuilding = false; }
    }

    private static bool AddComp(List<UIElement> list, UIElement? el)
    {
        if (el is null) return false;
        if (list.Count > 0) list.Add(Sep());
        list.Add(el);
        return true;
    }

    // ── FPS component ──
    // FPS 144fps  AVG 130fps  [===graph===]
    // 1%   98fps  0.1% 72fps

    private UIElement? BuildFps(HashSet<string> ids)
    {
        var active = FpsMetrics.Where(ids.Contains).ToArray();
        if (active.Length == 0) return null;

        bool tall = active.Length >= 3;
        FpsGroupIsTwoRow = tall;

        fpsSparkline = new SparklineGraph
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            MinWidth = tall ? 120 : 80,
            MinHeight = tall ? 36 : 20,
            Margin = new Thickness(6, 2, 0, 2)
        };

        var grid = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (tall)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            int half = (active.Length + 1) / 2;
            var top = Row(); var bot = Row();
            for (int i = 0; i < active.Length; i++)
                (i < half ? top : bot).Children.Add(FpsCell(active[i]));

            Grid.SetRow(top, 0); Grid.SetColumn(top, 0);
            Grid.SetRow(bot, 1); Grid.SetColumn(bot, 0);
            grid.Children.Add(top);
            grid.Children.Add(bot);
            Grid.SetRow(fpsSparkline, 0); Grid.SetRowSpan(fpsSparkline, 2); Grid.SetColumn(fpsSparkline, 1);
        }
        else
        {
            var row = Row();
            foreach (var id in active) row.Children.Add(FpsCell(id));
            Grid.SetRow(row, 0); Grid.SetColumn(row, 0);
            Grid.SetRow(fpsSparkline, 0); Grid.SetColumn(fpsSparkline, 1);
            grid.Children.Add(row);
        }

        grid.Children.Add(fpsSparkline);
        return grid;
    }

    private StackPanel FpsCell(string id)
    {
        var lbl = FpsSubLabels.GetValueOrDefault(id, id);
        var cell = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, Margin = new Thickness(5, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
        cell.Children.Add(Label(lbl, LabelFontSize));
        var vb = Txt("--fps", FpsColor(id), ValueFontSize, FontWeights.SemiBold);
        vb.MinWidth = ValueMinWidth(id);
        valueBlocks[id] = vb;
        cell.Children.Add(vb);
        return cell;
    }

    // ── Frametime component — own graph ──

    private UIElement? BuildFrametime(HashSet<string> ids)
    {
        var active = FrametimeMetrics.Where(ids.Contains).ToArray();
        if (active.Length == 0) return null;

        ftSparkline = new SparklineGraph
        {
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 80, MinHeight = 20,
            Margin = new Thickness(6, 2, 0, 2)
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0) };
        foreach (var id in active)
        {
            var lbl = "Frametime";
            var clr = FtTeal;
            panel.Children.Add(Label(lbl, LabelFontSize));
            var vb = Txt("--", clr, ValueFontSize, FontWeights.SemiBold);
            vb.MinWidth = ValueMinWidth(id);
            vb.Margin = new Thickness(3, 0, 6, 0);
            valueBlocks[id] = vb;
            panel.Children.Add(vb);
        }

        panel.Children.Add(ftSparkline);
        return panel;
    }

    // ── Hardware cell: "CPU 45% 65°C 12W" or "GPU 85% 72°C 15.2W 4.2/8G" ──

    private UIElement? BuildHw(string label, string[] metrics, HashSet<string> ids, SolidColorBrush primary)
    {
        var active = metrics.Where(ids.Contains).ToArray();
        if (active.Length == 0) return null;

        var cell = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0) };
        cell.Children.Add(Label(label, LabelFontSize));

        foreach (var id in active)
        {
            var clr = HwColor(id, primary);
            var vb = Txt("--", clr, ValueFontSize, FontWeights.SemiBold);
            vb.MinWidth = ValueMinWidth(id);
            vb.Margin = new Thickness(2, 0, 2, 0);
            valueBlocks[id] = vb;
            cell.Children.Add(vb);
        }

        return cell;
    }

    // GPU component: GPU usage + temp + power + VRAM all under "GPU" label.
    private UIElement? BuildGpu(HashSet<string> ids)
    {
        return BuildHw("GPU", GpuMetrics, ids, GpuRed);
    }

    // ── Memory: just RAM (VRAM is under GPU) ──

    private UIElement? BuildMemory(HashSet<string> ids)
    {
        if (!ids.Contains(OverlayPresetCatalog.Ram)) return null;

        var cell = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0) };
        cell.Children.Add(Label("RAM", LabelFontSize));
        var vb = Txt("--", RamPurple, ValueFontSize, FontWeights.SemiBold);
        vb.MinWidth = ValueMinWidth(OverlayPresetCatalog.Ram);
        vb.Margin = new Thickness(2, 0, 0, 0);
        valueBlocks[OverlayPresetCatalog.Ram] = vb;
        cell.Children.Add(vb);
        return cell;
    }

    // ── System: Hz icon + Battery icon ──

    private UIElement? BuildSystem(HashSet<string> ids)
    {
        var active = SystemMetrics.Where(ids.Contains).ToArray();
        if (active.Length == 0) return null;

        var cell = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0) };
        var deviceMetrics = active.Where(id => id is OverlayPresetCatalog.DeviceTemp or OverlayPresetCatalog.GpuFan).ToArray();
        if (deviceMetrics.Length > 0)
        {
            cell.Children.Add(Label("SYS", LabelFontSize));
            foreach (var id in deviceMetrics)
            {
                var vb = Txt("--", HwColor(id, HzSilver), ValueFontSize, FontWeights.SemiBold);
                vb.MinWidth = ValueMinWidth(id);
                vb.Margin = new Thickness(2, 0, 2, 0);
                valueBlocks[id] = vb;
                cell.Children.Add(vb);
            }
        }

        foreach (var id in active.Where(id => id is not OverlayPresetCatalog.DeviceTemp and not OverlayPresetCatalog.GpuFan))
        {
            var (glyph, isIcon) = SystemLabels.GetValueOrDefault(id, (id, false));
            var lbl = isIcon ? IconLabel(glyph, IconFontSize) : Label(glyph, LabelFontSize);
            cell.Children.Add(lbl);

            var clr = SysColor(id);
            var vb = Txt("--", clr, ValueFontSize, FontWeights.SemiBold);
            vb.MinWidth = ValueMinWidth(id);
            vb.Margin = new Thickness(2, 0, 5, 0);
            valueBlocks[id] = vb;
            cell.Children.Add(vb);
        }

        return cell;
    }

    // ── Value updates ──

    private void UpdateValues()
    {
        if (viewModel is null) return;
        var snap = viewModel.CurrentSnapshot;
        foreach (var pair in valueBlocks)
            pair.Value.Text = MetricFormatterCompact.FormatValue(pair.Key, snap);

        // Value cells have fixed MinWidth, so normal telemetry changes should
        // not resize or reposition the overlay HWND.
    }

    private void UpdateFpsGraph()
    {
        if (fpsSparkline is null || viewModel is null) return;
        var ids = new HashSet<string>(viewModel.TopBarMetricIds, StringComparer.OrdinalIgnoreCase);
        var series = new List<(IReadOnlyList<double> values, Color color)>();

        if (ids.Contains(OverlayPresetCatalog.Fps) && viewModel.FpsHistory.Count > 0)
            series.Add((viewModel.FpsHistory, Color.FromArgb(255, 0, 255, 136)));
        if (ids.Contains(OverlayPresetCatalog.AvgFps) && viewModel.AvgFpsHistory.Count > 0)
            series.Add((viewModel.AvgFpsHistory, Color.FromArgb(180, 0, 200, 100)));
        if (ids.Contains(OverlayPresetCatalog.OnePercentLow) && viewModel.OnePercentLowHistory.Count > 0)
            series.Add((viewModel.OnePercentLowHistory, Color.FromArgb(255, 255, 200, 60)));
        if (ids.Contains(OverlayPresetCatalog.ZeroPointOneLow) && viewModel.ZeroPointOneLowHistory.Count > 0)
            series.Add((viewModel.ZeroPointOneLowHistory, Color.FromArgb(255, 255, 120, 60)));

        fpsSparkline.SetSeries(series);
    }

    private void UpdateFtGraph()
    {
        if (ftSparkline is null || viewModel is null) return;
        if (viewModel.FrameTimeHistory.Count > 0)
            ftSparkline.SetValues(viewModel.FrameTimeHistory);
    }

    // ── Helpers ──

    private static TextBlock Txt(string text, SolidColorBrush fg, double size, FontWeight? wt = null) => new()
    {
        Text = text, Foreground = fg, FontSize = size, FontFamily = Font,
        FontWeight = wt ?? FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center
    };

    private TextBlock Label(string text, double size)
    {
        var lbl = Txt(text, LabelBrush, size);
        labelBlocks.Add(lbl);
        return lbl;
    }

    private TextBlock IconLabel(string glyph, double size)
    {
        var lbl = Txt(glyph, LabelBrush, size);
        lbl.FontFamily = IconFont;
        labelBlocks.Add(lbl);
        return lbl;
    }

    private static StackPanel Row() => new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

    private static Rectangle Sep() => new()
    {
        Width = 1, Height = 18, Fill = SepBrush,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(4, 0, 4, 0)
    };

    private static int GetScreenWidth()
    {
        try
        {
            var regions = Microsoft.UI.Windowing.DisplayArea.FindAll();
            if (regions.Count > 0) return regions[0].WorkArea.Width;
        }
        catch { }
        return 1920;
    }

    private int Measure()
    {
        double w = 0;
        foreach (var c in RowsPanel.Children) { c.Measure(Unbounded); w = Math.Max(w, c.DesiredSize.Width); }
        return (int)Math.Ceiling(w + RootBorder.Padding.Left + RootBorder.Padding.Right);
    }

    private void Notify(int rows, int width)
    {
        width = Math.Max(1, width);
        if (RowCount == rows && EstimatedWidth == width) return;
        RowCount = rows;
        EstimatedWidth = width;
        LayoutMetricsChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Colors ──

    private static SolidColorBrush FpsColor(string id) => id switch
    {
        OverlayPresetCatalog.Fps => FpsGreen,
        OverlayPresetCatalog.AvgFps => AvgGreen,
        OverlayPresetCatalog.OnePercentLow => OnePercentYellow,
        OverlayPresetCatalog.ZeroPointOneLow => ZeroPointOneOrange,
        _ => FpsGreen
    };

    private static SolidColorBrush HwColor(string id, SolidColorBrush primary) => id switch
    {
        OverlayPresetCatalog.GpuTemp or OverlayPresetCatalog.CpuTemp => TempOrange,
        OverlayPresetCatalog.GpuClock => HzSilver,
        OverlayPresetCatalog.GpuPower or OverlayPresetCatalog.CpuPower => PowerYellow,
        OverlayPresetCatalog.GpuFan => HzSilver,
        OverlayPresetCatalog.DeviceTemp => TempOrange,
        OverlayPresetCatalog.Vram => RamPurple,
        _ => primary
    };

    private static SolidColorBrush SysColor(string id) => id switch
    {
        OverlayPresetCatalog.Battery => BatGold,
        OverlayPresetCatalog.RefreshRate => HzSilver,
        OverlayPresetCatalog.TotalPower => PowerYellow,
        _ => new SolidColorBrush(Colors.White)
    };

    internal static double ValueMinWidth(string id) => id switch
    {
        OverlayPresetCatalog.Fps => 72,
        OverlayPresetCatalog.AvgFps => 72,
        OverlayPresetCatalog.OnePercentLow => 72,
        OverlayPresetCatalog.ZeroPointOneLow => 72,
        OverlayPresetCatalog.FrameTime => 68,
        OverlayPresetCatalog.CpuUsage or OverlayPresetCatalog.GpuUsage => 42,
        OverlayPresetCatalog.CpuTemp or OverlayPresetCatalog.GpuTemp or OverlayPresetCatalog.DeviceTemp => 42,
        OverlayPresetCatalog.CpuPower or OverlayPresetCatalog.GpuPower or OverlayPresetCatalog.TotalPower => 54,
        OverlayPresetCatalog.GpuClock => 72,
        OverlayPresetCatalog.GpuFan => 96,
        OverlayPresetCatalog.Ram or OverlayPresetCatalog.Vram => 92,
        OverlayPresetCatalog.Battery => 144,
        OverlayPresetCatalog.RefreshRate => 28,
        _ => 48
    };
}
