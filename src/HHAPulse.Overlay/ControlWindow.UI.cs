using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using HHAPulse.Overlay.Settings;

namespace HHAPulse.Overlay;

public sealed partial class ControlWindow
{
    private static readonly SolidColorBrush ConnectedBrush = new(Colors.LimeGreen);
    private static readonly SolidColorBrush DisconnectedBrush = new(Color.FromArgb(255, 85, 85, 102));
    private static readonly SolidColorBrush VerifiedBrush = new(Color.FromArgb(255, 56, 189, 120));
    private static readonly SolidColorBrush PendingBrush = new(Color.FromArgb(255, 249, 115, 22));
    private static readonly SolidColorBrush UnavailableBrush = new(Color.FromArgb(255, 98, 108, 127));
    private static readonly SolidColorBrush RejectedBrush = new(Color.FromArgb(255, 220, 78, 78));
    private static readonly Thickness CardPadding = new(18);

    private static readonly Dictionary<string, string> MetricDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        [OverlayPresetCatalog.Fps] = "FPS",
        [OverlayPresetCatalog.AvgFps] = "Average FPS",
        [OverlayPresetCatalog.OnePercentLow] = "1% Low FPS",
        [OverlayPresetCatalog.ZeroPointOneLow] = "0.1% Low FPS",
        [OverlayPresetCatalog.FrameTime] = "Frame Time",
        [OverlayPresetCatalog.CpuUsage] = "CPU Usage",
        [OverlayPresetCatalog.CpuTemp] = "CPU Temperature",
        [OverlayPresetCatalog.CpuPower] = "CPU Power",
        [OverlayPresetCatalog.GpuUsage] = "GPU Usage",
        [OverlayPresetCatalog.GpuTemp] = "GPU Temperature",
        [OverlayPresetCatalog.GpuClock] = "GPU Clock",
        [OverlayPresetCatalog.GpuPower] = "GPU Power",
        [OverlayPresetCatalog.GpuFan] = "Fan",
        [OverlayPresetCatalog.DeviceTemp] = "Device Temperature",
        [OverlayPresetCatalog.Ram] = "RAM",
        [OverlayPresetCatalog.Vram] = "VRAM",
        [OverlayPresetCatalog.TotalPower] = "Device Power",
        [OverlayPresetCatalog.RefreshRate] = "Refresh Rate",
        [OverlayPresetCatalog.Battery] = "Battery",
    };

    private static readonly (string Title, string[] Metrics)[] MetricGroups =
    {
        ("Performance", new[]
        {
            OverlayPresetCatalog.Fps,
            OverlayPresetCatalog.AvgFps,
            OverlayPresetCatalog.OnePercentLow,
            OverlayPresetCatalog.ZeroPointOneLow,
            OverlayPresetCatalog.FrameTime
        }),
        ("CPU", new[]
        {
            OverlayPresetCatalog.CpuUsage,
            OverlayPresetCatalog.CpuTemp,
            OverlayPresetCatalog.CpuPower
        }),
        ("GPU", new[]
        {
            OverlayPresetCatalog.GpuUsage,
            OverlayPresetCatalog.GpuTemp,
            OverlayPresetCatalog.GpuClock,
            OverlayPresetCatalog.GpuPower,
            OverlayPresetCatalog.Vram
        }),
        ("System", new[]
        {
            OverlayPresetCatalog.DeviceTemp,
            OverlayPresetCatalog.GpuFan,
            OverlayPresetCatalog.Ram,
            OverlayPresetCatalog.TotalPower,
            OverlayPresetCatalog.RefreshRate,
            OverlayPresetCatalog.Battery
        })
    };

    private readonly Dictionary<string, ToggleSwitch> metricToggles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBlock> metricDetailBlocks = new(StringComparer.OrdinalIgnoreCase);

    private NavigationView ShellNav = null!;
    private ScrollViewer GeneralSection = null!;
    private ScrollViewer OverlaySection = null!;
    private ScrollViewer MetricsSection = null!;
    private ScrollViewer FpsCaptureSection = null!;
    private ScrollViewer WidgetSection = null!;
    private ScrollViewer AboutSection = null!;

    private TextBlock GeneralPresetText = null!;
    private TextBlock GeneralRuntimeText = null!;
    private TextBlock GeneralQuickStateText = null!;
    private Button PresetMinimalButton = null!;
    private Button PresetStandardButton = null!;
    private Button PresetTunerButton = null!;
    private Button PresetCustomButton = null!;
    private Button PresetOffButton = null!;
    private TextBlock PresetDescription = null!;
    private Button ShowAlwaysButton = null!;
    private Button ShowInGameButton = null!;
    private TextBlock ShowModeDescription = null!;
    private TextBlock HotkeyStatusText = null!;
    private TextBlock BgOpacityValue = null!;
    private Slider BgOpacitySlider = null!;
    private TextBlock TextOpacityValue = null!;
    private Slider TextOpacitySlider = null!;
    private TextBlock TextSizeValue = null!;
    private Slider TextSizeSlider = null!;
    private TextBlock OverlayLayoutHintText = null!;
    private TextBlock MetricsPresetSummaryText = null!;
    private TextBlock CustomModeStateText = null!;
    private Button SwitchToCustomButton = null!;
    private StackPanel MetricStatusPanel = null!;
    private Border CustomModePanel = null!;
    private TextBlock CustomModeHintText = null!;
    private StackPanel CustomMetricGroupsPanel = null!;
    private Ellipse CaptureStatusDot = null!;
    private TextBlock CaptureStatusText = null!;
    private TextBlock CaptureFreshnessText = null!;
    private TextBlock TargetStatusText = null!;
    private TextBlock CaptureProtocolText = null!;
    private TextBlock RuntimeStatusText = null!;
    private TextBlock GpuStatusText = null!;
    private TextBlock CaptureHelpText = null!;
    private TextBlock WidgetConnectionText = null!;
    private TextBlock WidgetSummaryText = null!;
    private TextBlock WidgetPackagingText = null!;
    private TextBlock AboutVersionText = null!;
    private TextBlock AboutContractText = null!;
    private TextBlock AboutNativeBridgeText = null!;
    private TextBlock AboutTraceabilityText = null!;
    private TextBlock LogPathText = null!;
    private TextBlock DiagnosticsDumpText = null!;

    private void BuildShell()
    {
        EnsureShellResources();

        RootHost.Background = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1),
            GradientStops =
            {
                new GradientStop { Color = Color.FromArgb(255, 10, 13, 18), Offset = 0 },
                new GradientStop { Color = Color.FromArgb(255, 17, 20, 28), Offset = 0.45 },
                new GradientStop { Color = Color.FromArgb(255, 26, 19, 16), Offset = 1 }
            }
        };

        ShellNav = new NavigationView
        {
            Background = new SolidColorBrush(Colors.Transparent),
            CompactModeThresholdWidth = 480,
            ExpandedModeThresholdWidth = 800,
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsSettingsVisible = false,
            PaneDisplayMode = NavigationViewPaneDisplayMode.Auto,
            PaneTitle = "HHA Pulse"
        };
        ShellNav.SelectionChanged += OnNavSelectionChanged;

        ShellNav.MenuItems.Add(CreateNavItem("General", "general"));
        ShellNav.MenuItems.Add(CreateNavItem("Overlay", "overlay"));
        ShellNav.MenuItems.Add(CreateNavItem("Metrics", "metrics"));
        ShellNav.MenuItems.Add(CreateNavItem("FPS Capture", "capture"));
        ShellNav.MenuItems.Add(CreateNavItem("Widget", "widget"));
        ShellNav.MenuItems.Add(CreateNavItem("About", "about"));

        GeneralSection = CreateSectionScrollViewer();
        OverlaySection = CreateSectionScrollViewer();
        MetricsSection = CreateSectionScrollViewer();
        FpsCaptureSection = CreateSectionScrollViewer();
        WidgetSection = CreateSectionScrollViewer();
        AboutSection = CreateSectionScrollViewer();

        var contentHost = new Grid { Margin = new Thickness(0, 0, 0, 8), VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch };
        contentHost.Children.Add(GeneralSection);
        contentHost.Children.Add(OverlaySection);
        contentHost.Children.Add(MetricsSection);
        contentHost.Children.Add(FpsCaptureSection);
        contentHost.Children.Add(WidgetSection);
        contentHost.Children.Add(AboutSection);
        ShellNav.Content = contentHost;

        var container = new Border
        {
            Margin = new Thickness(10),
            CornerRadius = new CornerRadius(20),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush("ShellBorderBrush"),
            Background = new SolidColorBrush(Color.FromArgb(204, 11, 14, 21)),
            Child = ShellNav
        };

        RootHost.Children.Clear();
        RootHost.Children.Add(container);

        GeneralSection.Content = BuildGeneralSectionContent();
        OverlaySection.Content = BuildOverlaySectionContent();
        MetricsSection.Content = BuildMetricsSectionContent();
        FpsCaptureSection.Content = BuildFpsCaptureSectionContent();
        WidgetSection.Content = BuildWidgetSectionContent();
        AboutSection.Content = BuildAboutSectionContent();

        if (ShellNav.MenuItems.Count > 0)
        {
            ShellNav.SelectedItem = ShellNav.MenuItems[0];
        }
    }

    private UIElement BuildGeneralSectionContent()
    {
        GeneralPresetText = CreateHeadline();
        GeneralRuntimeText = CreateMuted();
        GeneralQuickStateText = CreateMuted();
        PresetDescription = CreateMuted();
        ShowModeDescription = CreateMuted();
        HotkeyStatusText = CreateMuted();

        PresetMinimalButton = CreateButton("Minimal", OnPresetMinimalClicked);
        PresetStandardButton = CreateButton("Standard", OnPresetStandardClicked);
        PresetTunerButton = CreateButton("Tuner", OnPresetTunerClicked);
        PresetCustomButton = CreateButton("Custom", OnPresetCustomClicked);
        PresetOffButton = CreateButton("Off", OnPresetOffClicked, 72);
        ShowAlwaysButton = CreateButton("Always", OnShowAlwaysClicked);
        ShowInGameButton = CreateButton("In-game only", OnShowInGameClicked, 132);

        return CreateSectionLayout(
            "General",
            "Preset quick-switches, show mode, and the live runtime summary.",
            CreateCard("Current State", GeneralPresetText, GeneralRuntimeText, GeneralQuickStateText),
            CreateCard("Preset Quick Switch",
                CreateButtonRows(
                    new[] { PresetMinimalButton, PresetStandardButton, PresetTunerButton },
                    new[] { PresetCustomButton, PresetOffButton }),
                PresetDescription),
            CreateCard("Quick Actions",
                CreateButtonRows(
                    new[] { CreateButton("Toggle overlay", OnToggleOverlayClicked, 132), CreateButton("Cycle preset", OnCyclePresetClicked, 122) }),
                CreateMuted("Quick actions stay intentionally small: toggle visibility, cycle presets, then come back here for everything deeper.")),
            CreateCard("Visibility",
                CreateButtonRows(new[] { ShowAlwaysButton, ShowInGameButton }),
                ShowModeDescription),
            CreateCard("Hotkeys", HotkeyStatusText));
    }

    private UIElement BuildOverlaySectionContent()
    {
        BgOpacityValue = CreateMuted();
        TextOpacityValue = CreateMuted();
        TextSizeValue = CreateMuted();
        OverlayLayoutHintText = CreateMuted();
        BgOpacitySlider = CreateSlider(0, 100, 85, 5, OnBgOpacityChanged);
        TextOpacitySlider = CreateSlider(20, 100, 100, 5, OnTextOpacityChanged);
        TextSizeSlider = CreateSlider(10, 22, 15, 1, OnTextSizeChanged);

        return CreateSectionLayout(
            "Overlay",
            "Appearance stays tied to the current live HUD. This page changes presentation, never metric meaning.",
            CreateCard("Transparency",
                CreateLabeledSlider("Background", BgOpacityValue, BgOpacitySlider),
                CreateLabeledSlider("Text", TextOpacityValue, TextOpacitySlider)),
            CreateCard("Sizing",
                CreateLabeledSlider("Text size", TextSizeValue, TextSizeSlider),
                OverlayLayoutHintText));
    }

    private UIElement BuildMetricsSectionContent()
    {
        MetricsPresetSummaryText = CreateMuted();
        CustomModeStateText = CreateMuted();
        SwitchToCustomButton = CreateButton("Switch to Custom preset", OnPresetCustomClicked, 196);
        MetricStatusPanel = new StackPanel { Spacing = 10 };
        CustomModeHintText = CreateMuted();
        CustomMetricGroupsPanel = new StackPanel { Spacing = 16 };
        CustomModePanel = CreateCard("Custom Metric Picker", CustomModeHintText, CustomMetricGroupsPanel);

        BuildMetricPicker();

        return CreateSectionLayout(
            "Metrics",
            "Presets come first. Custom mode is opt-in and every metric row shows live source and availability.",
            CreateCard("Preset Strategy", MetricsPresetSummaryText, CustomModeStateText, SwitchToCustomButton),
            CreateCard("Live Metric Validation",
                CreateMuted("Verified means the source path is established. Pending hardware proof means the code path exists but still needs hardware validation."),
                MetricStatusPanel),
            CustomModePanel);
    }

    private UIElement BuildFpsCaptureSectionContent()
    {
        CaptureStatusDot = new Ellipse { Width = 10, Height = 10, Fill = DisconnectedBrush, VerticalAlignment = VerticalAlignment.Center };
        CaptureStatusText = CreateHeadline(18);
        CaptureFreshnessText = CreateMuted();
        TargetStatusText = CreateMuted();
        CaptureProtocolText = CreateMuted();
        RuntimeStatusText = CreateMuted();
        GpuStatusText = CreateMuted();
        CaptureHelpText = CreateMuted();

        var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        statusRow.Children.Add(CaptureStatusDot);
        statusRow.Children.Add(CaptureStatusText);

        return CreateSectionLayout(
            "FPS Capture",
            "The capture service owns ETW FPS collection. This page makes freshness, target routing, and failure states explicit.",
            CreateCard("Capture State",
                statusRow,
                CaptureFreshnessText,
                TargetStatusText,
                CaptureProtocolText,
                CreateButton("Enable FPS capture", OnEnableCaptureClicked, 160)),
            CreateCard("Boundary State", RuntimeStatusText, GpuStatusText, CaptureHelpText));
    }

    private UIElement BuildWidgetSectionContent()
    {
        WidgetConnectionText = CreateHeadline();
        WidgetSummaryText = CreateMuted();
        WidgetPackagingText = CreateMuted();

        return CreateSectionLayout(
            "Widget",
            "The Game Bar widget stays companion-only: glanceable telemetry, clear standalone versus enhanced state, and no duplicated deep controls.",
            CreateCard("Companion State", WidgetConnectionText, WidgetSummaryText, WidgetPackagingText),
            CreateCard("Scope Guardrails",
                CreateMuted("Standalone mode should only expose sandbox-safe metrics. Enhanced mode reads overlay telemetry only when the read-only pipe is connected. Deep customization stays in the desktop shell.")));
    }

    private UIElement BuildAboutSectionContent()
    {
        AboutVersionText = CreateMuted();
        AboutContractText = CreateMuted();
        AboutNativeBridgeText = CreateMuted();
        AboutTraceabilityText = CreateMuted();
        LogPathText = CreateMuted();
        DiagnosticsDumpText = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Foreground = Brush("ShellStrongBrush"),
            TextWrapping = TextWrapping.Wrap
        };

        var supportButtons = CreateButtonRows(
            new[]
            {
                CreateButton("Copy diagnostics", OnCopyDiagnosticsClicked, 138),
                CreateButton("Open log folder", OnOpenLogClicked, 132)
            },
            new[]
            {
                CreateButton("Support", OnOpenSupportClicked, 88),
                CreateButton("Community", OnOpenCommunityClicked, 98),
                CreateButton("Visit Handheld Ally", OnOpenWebsiteClicked, 156)
            });

        var diagnosticsScroller = new ScrollViewer
        {
            MaxHeight = 280,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = DiagnosticsDumpText
        };

        return CreateSectionLayout(
            "About",
            "Build info, traceability, support, and the full runtime diagnostics export live here.",
            CreateCard("Build and Contract", AboutVersionText, AboutContractText, AboutNativeBridgeText, AboutTraceabilityText),
            CreateCard("Logs and Support", LogPathText, supportButtons, CreateMuted("Powered by Handheld Ally.")),
            CreateCard("Diagnostics Dump", diagnosticsScroller),
            CreateButton("Exit HHA Pulse", OnExitClicked, 140));
    }

    private void BuildMetricPicker()
    {
        metricToggles.Clear();
        metricDetailBlocks.Clear();
        CustomMetricGroupsPanel.Children.Clear();

        foreach (var group in MetricGroups)
        {
            var groupPanel = new StackPanel { Spacing = 8 };
            groupPanel.Children.Add(new TextBlock
            {
                Text = group.Title,
                Foreground = Brush("AccentBrush"),
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });

            foreach (var metricId in group.Metrics)
            {
                var toggle = new ToggleSwitch
                {
                    Header = MetricDisplayNames.GetValueOrDefault(metricId, metricId),
                    MinWidth = 240,
                    OnContent = "On",
                    OffContent = "Off"
                };

                toggle.Toggled += (_, _) =>
                {
                    if (!suppressToggleEvents)
                    {
                        OnMetricToggled();
                    }
                };

                var detail = CreateMuted();
                metricToggles[metricId] = toggle;
                metricDetailBlocks[metricId] = detail;

                groupPanel.Children.Add(CreateMetricPickerRow(toggle, detail));
            }

            CustomMetricGroupsPanel.Children.Add(groupPanel);
        }
    }

    private static Border CreateMetricPickerRow(ToggleSwitch toggle, TextBlock detail)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(toggle);
        panel.Children.Add(detail);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(60, 23, 25, 35)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 77, 85, 106)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12),
            Child = panel
        };
    }

    private static NavigationViewItem CreateNavItem(string content, string tag)
    {
        return new NavigationViewItem
        {
            Content = content,
            Tag = tag
        };
    }

    private static ScrollViewer CreateSectionScrollViewer()
    {
        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Visibility = Visibility.Collapsed
        };
    }

    private static void EnsureShellResources()
    {
        Application.Current.Resources["AccentBrush"] = new SolidColorBrush(Color.FromArgb(255, 249, 115, 22));
        Application.Current.Resources["ShellCardBrush"] = new SolidColorBrush(Color.FromArgb(204, 23, 25, 35));
        Application.Current.Resources["ShellBorderBrush"] = new SolidColorBrush(Color.FromArgb(51, 77, 85, 106));
        Application.Current.Resources["ShellMutedBrush"] = new SolidColorBrush(Color.FromArgb(255, 152, 160, 181));
        Application.Current.Resources["ShellStrongBrush"] = new SolidColorBrush(Color.FromArgb(255, 245, 247, 250));
    }

    private UIElement CreateSectionLayout(string title, string subtitle, params UIElement[] children)
    {
        var panel = new StackPanel { Padding = new Thickness(28, 24, 28, 32), Spacing = 18 };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Brush("ShellStrongBrush"),
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        panel.Children.Add(CreateMuted(subtitle));
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }

    private Border CreateCard(string title, params UIElement[] children)
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Brush("AccentBrush"),
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        return new Border
        {
            Background = Brush("ShellCardBrush"),
            BorderBrush = Brush("ShellBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = CardPadding,
            Child = panel
        };
    }

    private static UIElement CreateLabeledSlider(string label, TextBlock value, Slider slider)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Colors.White) });
        panel.Children.Add(value);
        panel.Children.Add(slider);
        return panel;
    }

    private static StackPanel CreateButtonRows(params IEnumerable<Button>[] rows)
    {
        var panel = new StackPanel { Spacing = 8 };
        foreach (var rowButtons in rows)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            foreach (var button in rowButtons)
            {
                row.Children.Add(button);
            }

            panel.Children.Add(row);
        }

        return panel;
    }

    private static Button CreateButton(string content, RoutedEventHandler onClick, double minWidth = 96)
    {
        var button = new Button
        {
            Content = content,
            MinWidth = minWidth,
            MinHeight = 40
        };
        button.Click += onClick;
        return button;
    }

    private static Slider CreateSlider(double min, double max, double value, double step, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventHandler handler)
    {
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            StepFrequency = step
        };
        slider.ValueChanged += handler;
        return slider;
    }

    private static TextBlock CreateHeadline(double size = 20)
    {
        return new TextBlock
        {
            Foreground = Brush("ShellStrongBrush"),
            FontSize = size,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static TextBlock CreateMuted(string? text = null)
    {
        return new TextBlock
        {
            Text = text ?? string.Empty,
            Foreground = Brush("ShellMutedBrush"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static SolidColorBrush Brush(string key)
    {
        return (SolidColorBrush)Application.Current.Resources[key];
    }
}
