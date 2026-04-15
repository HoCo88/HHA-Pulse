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
        OverlayPresetCatalog.CpuUsage, OverlayPresetCatalog.CpuTemp, OverlayPresetCatalog.CpuPower, OverlayPresetCatalog.CpuClock
    };

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

    private static readonly string[] StorageMetrics =
    {
        OverlayPresetCatalog.StorageTemp, OverlayPresetCatalog.StorageWear
    };

    private static readonly string[] SystemMetrics =
    {
        OverlayPresetCatalog.DeviceTemp, OverlayPresetCatalog.GpuFan,
        OverlayPresetCatalog.RefreshRate, OverlayPresetCatalog.Battery,
        OverlayPresetCatalog.TotalPower
    };

    private static readonly Dictionary<string, string> FpsSubLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        [OverlayPresetCatalog.Fps] = "FPS",
        [OverlayPresetCatalog.AvgFps] = "AVG",
        [OverlayPresetCatalog.OnePercentLow] = "1%",
        [OverlayPresetCatalog.ZeroPointOneLow] = "0.1%",
    };

    private static readonly Dictionary<string, (string glyph, bool isIcon)> SystemLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        [OverlayPresetCatalog.RefreshRate] = ("\uE7F8", true),
        [OverlayPresetCatalog.Battery] = ("BAT", false),
        [OverlayPresetCatalog.TotalPower] = ("\uEC48", true),
    };

    private static readonly Size Unbounded = new(double.PositiveInfinity, double.PositiveInfinity);
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons");

    // Role-based brushes resolved from theme resources. Fallback values match
    // HhapTheme.xaml so the HUD still renders correctly if resources are not
    // yet attached (e.g. during designer / unit-test construction).
    private static readonly SolidColorBrush PerfBrush =
        ResolveBrush("HhapBlue2Brush", 0xFF, 0x9A, 0xE7, 0xFF);
    private static readonly SolidColorBrush ThermalBrush =
        ResolveBrush("HhapAmber2Brush", 0xFF, 0xFF, 0xCB, 0x6C);
    private static readonly SolidColorBrush NeutralBrush =
        ResolveBrush("HhapTextBrush", 0xFF, 0xF4, 0xF7, 0xFF);
    private static readonly SolidColorBrush MutedBrush =
        ResolveBrush("HhapMutedBrush", 0xFF, 0x95, 0xA0, 0xBD);
    private static readonly SolidColorBrush ActiveBrush =
        ResolveBrush("HhapGreenBrush", 0xFF, 0x55, 0xE0, 0x8E);
    private static readonly SolidColorBrush SepBrush =
        new(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));

    // Typography tokens resolved from HhapTheme.xaml.
    private static readonly double ValueFontSize = ResolveDouble("HhapFontHudValue", 23);
    private static readonly double LabelFontSize = ResolveDouble("HhapFontHudLabel", 11);
    private static readonly int ValueTrack = ResolveInt("HhapTrackHudValue", -40);
    private static readonly int LabelTrack = ResolveInt("HhapTrackHudLabel", 80);
    private static readonly double IconFontSize = LabelFontSize + 1;
    private static readonly FontFamily Font = new("Segoe UI");

    private readonly Dictionary<string, TextBlock> valueBlocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TextBlock> labelBlocks = new();
    private SparklineGraph? fpsSparkline;
    private SparklineGraph? ftSparkline;
    private OverlayViewModel? viewModel;
    private bool rebuilding;
    private double textOpacity = 1.0;

    // Value typography is mutable: the user can change the HUD text size at
    // runtime. Minimum widths for each metric's value block are measured —
    // never hardcoded — so the whole HUD shrinks or grows with the font.
    private double currentValueFontSize = ValueFontSize;
    private double digitAdvance;

    // Base HUD background = HhapHudBgBrush (rgba 10,16,31,0.85). The
    // background is a mutable SolidColorBrush owned by this control so
    // ApplyOpacity can modulate its alpha without touching text opacity
    // or inheriting it through the whole control tree.
    private static readonly byte HudBgR = 0x0A;
    private static readonly byte HudBgG = 0x10;
    private static readonly byte HudBgB = 0x1F;
    private const byte HudBgDefaultAlpha = 0xD9; // 0.85 * 255
    private readonly SolidColorBrush hudBackgroundBrush =
        new(Color.FromArgb(HudBgDefaultAlpha, HudBgR, HudBgG, HudBgB));

    public event EventHandler? LayoutMetricsChanged;
    public int RowCount { get; private set; } = 1;
    public bool FpsGroupIsTwoRow { get; private set; }
    public int EstimatedWidth { get; private set; } = 200;
    public int EstimatedHeight { get; private set; } = 32;

    public TopBarControl()
    {
        InitializeComponent();
        RootBorder.Background = hudBackgroundBrush;
        UpdateDigitAdvance();
        Unloaded += (_, _) => DetachViewModel();
    }

    // Measure one digit at the current value-font settings. Used to derive
    // per-metric minimum widths as an integer number of digit advances,
    // instead of hardcoded pixel tables that stop matching when the user
    // changes the HUD text size.
    private void UpdateDigitAdvance()
    {
        var probe = new TextBlock
        {
            Text = "0",
            FontFamily = Font,
            FontWeight = FontWeights.ExtraBold,
            FontSize = currentValueFontSize,
            CharacterSpacing = ValueTrack,
        };
        probe.Measure(Unbounded);
        digitAdvance = probe.DesiredSize.Width;
        if (digitAdvance <= 0)
        {
            digitAdvance = currentValueFontSize * 0.55;
        }
    }

    private double PixelMinWidth(string id) => Math.Ceiling(ValueMinCharCount(id) * digitAdvance);

    public void ApplyTextSize(double size)
    {
        currentValueFontSize = size;
        UpdateDigitAdvance();

        foreach (var pair in valueBlocks)
        {
            pair.Value.FontSize = size;
            pair.Value.MinWidth = PixelMinWidth(pair.Key);
        }

        var labelSize = Math.Max(size * 0.72, 9);
        foreach (var lb in labelBlocks)
            lb.FontSize = labelSize;

        if (!rebuilding)
        {
            var (w, h) = MeasureContent();
            if (w != EstimatedWidth || h != EstimatedHeight) Notify(RowCount, w, h);
        }
    }

    public void ApplyOpacity(double bgOpacity, double newTextOpacity)
    {
        // Background opacity = alpha of the HUD background brush only.
        // Do NOT set RootBorder.Opacity — that cascades to all children
        // including text, so at bg=0 the entire HUD vanishes.
        var alpha = (byte)(Math.Clamp(bgOpacity, 0.0, 1.0) * 255);
        hudBackgroundBrush.Color = Color.FromArgb(alpha, HudBgR, HudBgG, HudBgB);

        textOpacity = Math.Clamp(newTextOpacity, 0.0, 1.0);
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
                or nameof(OverlayViewModel.ActivePreset)
                or nameof(OverlayViewModel.Position)
                or nameof(OverlayViewModel.LineCount))
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

            if (viewModel is null) { Notify(1, 200, 32); return; }

            var ids = new HashSet<string>(viewModel.TopBarMetricIds, StringComparer.OrdinalIgnoreCase);

            if (ids.Count == 0)
            {
                var r = Row();
                r.Children.Add(Value("Overlay off", NeutralBrush));
                RowsPanel.Children.Add(r);
                var (w0, h0) = MeasureContent();
                Notify(1, w0, h0);
                return;
            }

            var perf = new List<UIElement>();
            var hw = new List<UIElement>();

            AddComp(perf, BuildFps(ids));
            AddComp(perf, BuildFrametime(ids));
            AddComp(hw, BuildHw("CPU", CpuMetrics, ids));
            AddComp(hw, BuildGpu(ids));
            AddComp(hw, BuildMemory(ids));
            AddComp(hw, BuildStorage(ids));
            AddComp(hw, BuildSystem(ids));

            UpdateValues();

            var layoutMode = ResolveLayoutMode(viewModel.Position, viewModel.LineCount);
            if (layoutMode == TopBarLayoutMode.SideDock)
            {
                foreach (var element in perf.Concat(hw))
                {
                    RowsPanel.Children.Add(WrapVerticalRow(element));
                }

                var (wDock, hDock) = MeasureContent();
                Notify(Math.Max(1, RowsPanel.Children.Count), wDock, hDock);
            }
            else if (layoutMode == TopBarLayoutMode.Tall)
            {
                var r1 = Row(); foreach (var e in perf) r1.Children.Add(e);
                var r2 = Row(); foreach (var e in hw) r2.Children.Add(e);
                RowsPanel.Children.Add(r1);
                if (r2.Children.Count > 0) RowsPanel.Children.Add(r2);
                var (w2, h2) = MeasureContent();
                Notify(r2.Children.Count > 0 ? 2 : 1, w2, h2);
            }
            else
            {
                var singleRow = Row();
                foreach (var e in perf) singleRow.Children.Add(e);
                if (perf.Count > 0 && hw.Count > 0) singleRow.Children.Add(Sep());
                foreach (var e in hw) singleRow.Children.Add(e);

                singleRow.Measure(Unbounded);
                var singleWidth = (int)Math.Ceiling(singleRow.DesiredSize.Width + RootBorder.Padding.Left + RootBorder.Padding.Right);
                var screenWidth = GetScreenWidth();

                if (singleWidth <= screenWidth)
                {
                    RowsPanel.Children.Add(singleRow);
                    var (_, h1) = MeasureContent();
                    Notify(1, singleWidth, h1);
                }
                else
                {
                    var r1 = Row(); foreach (var e in perf) r1.Children.Add(e);
                    var r2 = Row(); foreach (var e in hw) r2.Children.Add(e);
                    RowsPanel.Children.Add(r1);
                    if (r2.Children.Count > 0) RowsPanel.Children.Add(r2);
                    var (w2, h2) = MeasureContent();
                    Notify(r2.Children.Count > 0 ? 2 : 1, w2, h2);
                }
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

        var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
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
        cell.Children.Add(Label(lbl));
        var vb = Value("--fps", PerfBrush);
        vb.MinWidth = PixelMinWidth(id);
        valueBlocks[id] = vb;
        cell.Children.Add(vb);
        return cell;
    }

    // ── Frametime component ──

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

        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var id in active)
        {
            panel.Children.Add(Label("Frametime"));
            var vb = Value("--", PerfBrush);
            vb.MinWidth = PixelMinWidth(id);
            vb.Margin = new Thickness(3, 0, 6, 0);
            valueBlocks[id] = vb;
            panel.Children.Add(vb);
        }

        panel.Children.Add(ftSparkline);
        return panel;
    }

    // ── Hardware group ──

    private UIElement? BuildHw(string label, string[] metrics, HashSet<string> ids)
    {
        var active = metrics.Where(ids.Contains).ToArray();
        if (active.Length == 0) return null;

        var cell = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        cell.Children.Add(Label(label));

        foreach (var id in active)
        {
            var vb = Value("--", RoleBrush(id));
            vb.MinWidth = PixelMinWidth(id);
            vb.Margin = new Thickness(2, 0, 2, 0);
            valueBlocks[id] = vb;
            cell.Children.Add(vb);
        }

        return cell;
    }

    private UIElement? BuildGpu(HashSet<string> ids) =>
        BuildHw("GPU", GpuMetrics, ids);

    private UIElement? BuildMemory(HashSet<string> ids)
    {
        if (!ids.Contains(OverlayPresetCatalog.Ram)) return null;

        var cell = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        cell.Children.Add(Label("RAM"));
        var vb = Value("--", NeutralBrush);
        vb.MinWidth = PixelMinWidth(OverlayPresetCatalog.Ram);
        vb.Margin = new Thickness(2, 0, 0, 0);
        valueBlocks[OverlayPresetCatalog.Ram] = vb;
        cell.Children.Add(vb);
        return cell;
    }

    private UIElement? BuildStorage(HashSet<string> ids)
    {
        var active = StorageMetrics.Where(ids.Contains).ToArray();
        if (active.Length == 0)
        {
            return null;
        }

        var cell = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        cell.Children.Add(Label("SSD"));

        foreach (var id in active)
        {
            var vb = Value("--", RoleBrush(id));
            vb.MinWidth = PixelMinWidth(id);
            vb.Margin = new Thickness(2, 0, 2, 0);
            valueBlocks[id] = vb;
            cell.Children.Add(vb);
        }

        return cell;
    }

    private UIElement? BuildSystem(HashSet<string> ids)
    {
        var active = SystemMetrics.Where(ids.Contains).ToArray();
        if (active.Length == 0) return null;

        var cell = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        var deviceMetrics = active.Where(id => id is OverlayPresetCatalog.DeviceTemp or OverlayPresetCatalog.GpuFan).ToArray();
        if (deviceMetrics.Length > 0)
        {
            cell.Children.Add(Label("SYS"));
            foreach (var id in deviceMetrics)
            {
                var vb = Value("--", RoleBrush(id));
                vb.MinWidth = PixelMinWidth(id);
                vb.Margin = new Thickness(2, 0, 2, 0);
                valueBlocks[id] = vb;
                cell.Children.Add(vb);
            }
        }

        foreach (var id in active.Where(id => id is not OverlayPresetCatalog.DeviceTemp and not OverlayPresetCatalog.GpuFan))
        {
            var (glyph, isIcon) = SystemLabels.GetValueOrDefault(id, (id, false));
            var lbl = isIcon ? IconLabel(glyph) : Label(glyph);
            cell.Children.Add(lbl);

            var vb = Value("--", RoleBrush(id));
            vb.MinWidth = PixelMinWidth(id);
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
    }

    private void UpdateFpsGraph()
    {
        if (fpsSparkline is null || viewModel is null) return;
        var ids = new HashSet<string>(viewModel.TopBarMetricIds, StringComparer.OrdinalIgnoreCase);
        var series = new List<(IReadOnlyList<double> values, Color color)>();

        // All FPS family series share a single gradient stroke (see SparklineGraph);
        // the Color argument here is preserved for the legacy API but ignored downstream.
        var defaultColor = Color.FromArgb(0xFF, 0x9A, 0xE7, 0xFF);
        if (ids.Contains(OverlayPresetCatalog.Fps) && viewModel.FpsHistory.Count > 0)
            series.Add((viewModel.FpsHistory, defaultColor));
        if (ids.Contains(OverlayPresetCatalog.AvgFps) && viewModel.AvgFpsHistory.Count > 0)
            series.Add((viewModel.AvgFpsHistory, defaultColor));
        if (ids.Contains(OverlayPresetCatalog.OnePercentLow) && viewModel.OnePercentLowHistory.Count > 0)
            series.Add((viewModel.OnePercentLowHistory, defaultColor));
        if (ids.Contains(OverlayPresetCatalog.ZeroPointOneLow) && viewModel.ZeroPointOneLowHistory.Count > 0)
            series.Add((viewModel.ZeroPointOneLowHistory, defaultColor));

        fpsSparkline.SetSeries(series);
    }

    private void UpdateFtGraph()
    {
        if (ftSparkline is null || viewModel is null) return;
        if (viewModel.FrameTimeHistory.Count > 0)
            ftSparkline.SetValues(viewModel.FrameTimeHistory);
    }

    // ── Construction helpers ──

    private static TextBlock Value(string text, SolidColorBrush fg) => new()
    {
        Text = text,
        Foreground = fg,
        FontSize = ValueFontSize,
        FontFamily = Font,
        FontWeight = FontWeights.ExtraBold,
        CharacterSpacing = ValueTrack,
        VerticalAlignment = VerticalAlignment.Center
    };

    private TextBlock Label(string text)
    {
        var lbl = new TextBlock
        {
            Text = text,
            Foreground = MutedBrush,
            FontSize = LabelFontSize,
            FontFamily = Font,
            CharacterSpacing = LabelTrack,
            VerticalAlignment = VerticalAlignment.Center
        };
        labelBlocks.Add(lbl);
        return lbl;
    }

    private TextBlock IconLabel(string glyph)
    {
        var lbl = new TextBlock
        {
            Text = glyph,
            Foreground = MutedBrush,
            FontSize = IconFontSize,
            FontFamily = IconFont,
            VerticalAlignment = VerticalAlignment.Center
        };
        labelBlocks.Add(lbl);
        return lbl;
    }

    private static StackPanel Row() => new()
    {
        Orientation = Orientation.Horizontal,
        VerticalAlignment = VerticalAlignment.Center,
        Spacing = 6
    };

    private static StackPanel WrapVerticalRow(UIElement element)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 1, 0, 1)
        };
        row.Children.Add(element);
        return row;
    }

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

    private (int width, int height) MeasureContent()
    {
        double w = 0, h = 0;
        foreach (var c in RowsPanel.Children)
        {
            c.Measure(Unbounded);
            w = Math.Max(w, c.DesiredSize.Width);
            h += c.DesiredSize.Height;
        }
        var width = (int)Math.Ceiling(w + RootBorder.Padding.Left + RootBorder.Padding.Right);
        var height = (int)Math.Ceiling(
            h
            + RootBorder.Padding.Top + RootBorder.Padding.Bottom
            + RootBorder.BorderThickness.Top + RootBorder.BorderThickness.Bottom);
        return (Math.Max(1, width), Math.Max(1, height));
    }

    private void Notify(int rows, int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (RowCount == rows && EstimatedWidth == width && EstimatedHeight == height) return;
        RowCount = rows;
        EstimatedWidth = width;
        EstimatedHeight = height;
        LayoutMetricsChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Role-based brush mapping ──
    //
    // Reduced from 15 hardcoded neon shades to 4 roles from the design tokens:
    //   - PerfBrush   (HhapBlue2Brush)  → frames, frametime, usage loads
    //   - ThermalBrush(HhapAmber2Brush) → temperatures, power, battery
    //   - NeutralBrush(HhapTextBrush)   → memory (RAM, VRAM, GPU clock)
    //   - MutedBrush  (HhapMutedBrush)  → refresh rate and other passive metrics
    //   - ActiveBrush (HhapGreenBrush)  → fan when spinning
    //
    // This matches preview.html's .hud-value.blue / .amber / .green classes.

    private static SolidColorBrush RoleBrush(string id) => id switch
    {
        OverlayPresetCatalog.Fps
            or OverlayPresetCatalog.AvgFps
            or OverlayPresetCatalog.OnePercentLow
            or OverlayPresetCatalog.ZeroPointOneLow
            or OverlayPresetCatalog.FrameTime
            or OverlayPresetCatalog.CpuUsage
            or OverlayPresetCatalog.GpuUsage => PerfBrush,

        OverlayPresetCatalog.CpuTemp
            or OverlayPresetCatalog.GpuTemp
            or OverlayPresetCatalog.DeviceTemp
            or OverlayPresetCatalog.StorageTemp
            or OverlayPresetCatalog.CpuPower
            or OverlayPresetCatalog.GpuPower
            or OverlayPresetCatalog.TotalPower
            or OverlayPresetCatalog.Battery => ThermalBrush,

        OverlayPresetCatalog.Ram
            or OverlayPresetCatalog.Vram
            or OverlayPresetCatalog.StorageWear
            or OverlayPresetCatalog.CpuClock
            or OverlayPresetCatalog.GpuClock => NeutralBrush,

        OverlayPresetCatalog.GpuFan => ActiveBrush,

        OverlayPresetCatalog.RefreshRate => MutedBrush,

        _ => NeutralBrush
    };

    // Maximum number of digit-advances each metric's value can occupy.
    // Pixel width is derived at runtime from the measured width of one
    // digit at the current HUD font size (see UpdateDigitAdvance +
    // PixelMinWidth). No pixel constants — the whole HUD scales cleanly
    // when the user moves the text-size slider.
    internal static int ValueMinCharCount(string id) => id switch
    {
        OverlayPresetCatalog.Fps => 6,
        OverlayPresetCatalog.AvgFps => 6,
        OverlayPresetCatalog.OnePercentLow => 6,
        OverlayPresetCatalog.ZeroPointOneLow => 6,
        OverlayPresetCatalog.FrameTime => 6,
        OverlayPresetCatalog.CpuUsage or OverlayPresetCatalog.GpuUsage => 4,
        OverlayPresetCatalog.CpuTemp or OverlayPresetCatalog.GpuTemp or OverlayPresetCatalog.DeviceTemp or OverlayPresetCatalog.StorageTemp => 4,
        OverlayPresetCatalog.CpuPower or OverlayPresetCatalog.GpuPower or OverlayPresetCatalog.TotalPower => 5,
        OverlayPresetCatalog.GpuClock or OverlayPresetCatalog.CpuClock => 8,
        OverlayPresetCatalog.GpuFan => 7,
        OverlayPresetCatalog.Ram or OverlayPresetCatalog.Vram or OverlayPresetCatalog.StorageWear => 7,
        OverlayPresetCatalog.Battery => 10,
        OverlayPresetCatalog.RefreshRate => 3,
        _ => 4,
    };

    internal static TopBarLayoutMode ResolveLayoutMode(TopBarPosition position, int lineCount) =>
        position switch
        {
            TopBarPosition.LeftDock or TopBarPosition.RightDock => TopBarLayoutMode.SideDock,
            TopBarPosition.TopTall or TopBarPosition.BottomTall => TopBarLayoutMode.Tall,
            _ when lineCount >= 2 => TopBarLayoutMode.Tall,
            _ => TopBarLayoutMode.Thin
        };

    // ── Resource lookup helpers ──

    private static SolidColorBrush ResolveBrush(string key, byte a, byte r, byte g, byte b)
    {
        try
        {
            if (Application.Current?.Resources is { } res &&
                res.TryGetValue(key, out var obj) &&
                obj is SolidColorBrush brush)
            {
                return brush;
            }
        }
        catch
        {
            // Fall through to fallback when resources are not yet available.
        }
        return new SolidColorBrush(Color.FromArgb(a, r, g, b));
    }

    private static double ResolveDouble(string key, double fallback)
    {
        try
        {
            if (Application.Current?.Resources is { } res &&
                res.TryGetValue(key, out var obj) &&
                obj is double d)
            {
                return d;
            }
        }
        catch
        {
        }
        return fallback;
    }

    private static int ResolveInt(string key, int fallback)
    {
        try
        {
            if (Application.Current?.Resources is { } res &&
                res.TryGetValue(key, out var obj) &&
                obj is int i)
            {
                return i;
            }
        }
        catch
        {
        }
        return fallback;
    }
}

internal enum TopBarLayoutMode
{
    Thin = 0,
    Tall = 1,
    SideDock = 2
}
