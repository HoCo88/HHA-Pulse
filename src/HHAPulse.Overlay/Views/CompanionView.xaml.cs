using System.ComponentModel;
using System.Diagnostics;
using HHAPulse.Overlay.Controls;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Overlay.Settings;
using HHAPulse.Overlay.ViewModels;
using HHAPulse.Shared.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel.DataTransfer;

namespace HHAPulse.Overlay.Views;

public sealed partial class CompanionView : UserControl
{
    private readonly Storyboard pulseStoryboard = new();
    private CompanionViewModel? viewModel;
    private string diagnosticsDumpText = string.Empty;
    private bool handlersAttached;

    public CompanionView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        BuildPulseStoryboard();
    }

    public event Action? ToggleOverlayRequested;
    public event Action? ExitRequested;
    public event Action<OverlayShowMode>? ShowModeChanged;
    public event Action<OverlayPreset>? PresetRequested;
    public event Action? ManualRequested;
    public event Action<List<string>>? CustomMetricsChanged;
    public event Action<double, double>? OpacityChanged;
    public event Action<double>? TextSizeChanged;
    public event Action<TopBarPosition>? PositionChanged;

    public CompanionViewModel? ViewModel
    {
        get => viewModel;
        set
        {
            if (ReferenceEquals(viewModel, value))
            {
                ApplyViewModel();
                return;
            }

            if (viewModel is not null)
            {
                viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            viewModel = value;

            if (viewModel is not null)
            {
                viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }

            ApplyViewModel();
        }
    }

    public void ShowManualSheet(IReadOnlyList<string> metricIds)
    {
        viewModel?.SetManualSheetOpen(true);
        ManualMetricSheetControl.ApplySelection(metricIds, viewModel?.ManualSummaryText ?? "Manual starts as a copy of the current preset.");
        ApplyManualSheetVisibility();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!handlersAttached)
        {
            PositionFlyoutControl.PositionSelected += OnPositionSelected;
            PositionFlyoutRoot.Opened += OnPositionFlyoutOpened;
            PositionFlyoutRoot.Closed += OnPositionFlyoutClosed;
            FeelAndFitFlyoutControl.OpacityChanged += OnOpacityFlyoutChanged;
            FeelAndFitFlyoutControl.TextSizeChanged += OnTextSizeFlyoutChanged;
            FeelAndFitFlyoutRoot.Opened += OnFeelAndFitFlyoutOpened;
            FeelAndFitFlyoutRoot.Closed += OnFeelAndFitFlyoutClosed;
            ManualMetricSheetControl.MetricsChanged += OnManualMetricsChanged;
            handlersAttached = true;
        }

        ApplyViewModel();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!handlersAttached)
        {
            return;
        }

        PositionFlyoutControl.PositionSelected -= OnPositionSelected;
        PositionFlyoutRoot.Opened -= OnPositionFlyoutOpened;
        PositionFlyoutRoot.Closed -= OnPositionFlyoutClosed;
        FeelAndFitFlyoutControl.OpacityChanged -= OnOpacityFlyoutChanged;
        FeelAndFitFlyoutControl.TextSizeChanged -= OnTextSizeFlyoutChanged;
        FeelAndFitFlyoutRoot.Opened -= OnFeelAndFitFlyoutOpened;
        FeelAndFitFlyoutRoot.Closed -= OnFeelAndFitFlyoutClosed;
        ManualMetricSheetControl.MetricsChanged -= OnManualMetricsChanged;
        handlersAttached = false;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ApplyViewModel();
    }

    private void ApplyViewModel()
    {
        if (viewModel is null)
        {
            return;
        }

        OverlayChipTextBlock.Text = viewModel.OverlayChipText;
        CaptureChipTextBlock.Text = viewModel.CaptureChipText;
        GameTitleTextBlock.Text = viewModel.GameTitleText;
        PositionSummaryTextBlock.Text = viewModel.PositionText;
        FeelAndFitSummaryTextBlock.Text = viewModel.FeelAndFitText;
        EventLineLongTextBlock.Text = viewModel.EventLineText;
        EventLineShortTextBlock.Text = viewModel.EventLineShort;
        CaptureFooterTextBlock.Text = viewModel.CaptureFooterText;
        GameFooterTextBlock.Text = viewModel.GameFooterText;
        WidgetFooterTextBlock.Text = viewModel.WidgetFooterText;

        PositionFlyoutControl.ApplyPosition(viewModel.Settings.TopBarPosition);
        FeelAndFitFlyoutControl.ApplySettings(viewModel.Settings.BackgroundOpacity, viewModel.Settings.TextOpacity, viewModel.Settings.TextSizePixels);
        NpuStatusCardControl.Apply(viewModel.Snapshot);
        PresetRepeater.ItemsSource = viewModel.PresetCards;

        ApplyChip(CaptureChip, viewModel.CaptureHealth);
        ApplyDot(CaptureDot, viewModel.CaptureHealth);
        ApplyDot(GameDot, viewModel.GameHealth);
        ApplyDot(WidgetDot, viewModel.WidgetHealth);

        ApplyShowModeButtons(viewModel.Settings.ShowMode);
        ApplyOverlayState(viewModel.Settings.ActivePreset != OverlayPreset.Off);
        ApplyManualSheetVisibility();

        var statuses = viewModel.Snapshot.MetricStatuses.Count > 0
            ? viewModel.Snapshot.MetricStatuses
            : MetricStatusFactory.Create(viewModel.Snapshot);
        diagnosticsDumpText = ControlShellTextBuilder.BuildDiagnosticsDump(viewModel.Snapshot, statuses, viewModel.Snapshot.MeasurementTraces);
    }

    private void ApplyShowModeButtons(OverlayShowMode showMode)
    {
        ShowAlwaysButton.Style = showMode == OverlayShowMode.Always
            ? (Style)Application.Current.Resources["HhapPrimaryButtonStyle"]
            : (Style)Application.Current.Resources["HhapChipButtonStyle"];
        ShowInGameButton.Style = showMode == OverlayShowMode.InGameOnly
            ? (Style)Application.Current.Resources["HhapPrimaryButtonStyle"]
            : (Style)Application.Current.Resources["HhapChipButtonStyle"];
    }

    private void ApplyOverlayState(bool isVisible)
    {
        OverlayChipButton.Style = isVisible
            ? (Style)Application.Current.Resources["HhapPrimaryButtonStyle"]
            : (Style)Application.Current.Resources["HhapChipButtonStyle"];

        if (isVisible)
        {
            pulseStoryboard.Begin();
        }
        else
        {
            pulseStoryboard.Stop();
            OverlayPulseDot.Opacity = 0.5;
        }
    }

    private void ApplyManualSheetVisibility()
    {
        var isVisible = viewModel?.IsManualSheetOpen == true;
        ManualMetricSheetControl.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

        if (!isVisible || viewModel is null)
        {
            return;
        }

        var metricIds = viewModel.Settings.EnabledMetricIds.Count > 0
            ? viewModel.Settings.EnabledMetricIds
            : viewModel.ActiveMetricIds;
        ManualMetricSheetControl.ApplySelection(metricIds, viewModel.ManualSummaryText);
    }

    private void ApplyChip(Border chip, CompanionHealth health)
    {
        chip.Style = (Style)Application.Current.Resources[health switch
        {
            CompanionHealth.Healthy => "HhapChipGreenStyle",
            CompanionHealth.Limited => "HhapChipAmberStyle",
            _ => "HhapChipStyle"
        }];
    }

    private static void ApplyDot(Shape dot, CompanionHealth health)
    {
        var key = health switch
        {
            CompanionHealth.Healthy => "HhapBandGood",
            CompanionHealth.Limited => "HhapBandOkay",
            _ => "HhapBandWarn"
        };
        dot.Fill = (Brush)Application.Current.Resources[key];
    }

    private void BuildPulseStoryboard()
    {
        var animation = new DoubleAnimation
        {
            From = 1,
            To = 0.25,
            Duration = new Duration(TimeSpan.FromSeconds(1.4)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        Storyboard.SetTarget(animation, OverlayPulseDot);
        Storyboard.SetTargetProperty(animation, "Opacity");
        pulseStoryboard.Children.Add(animation);
    }

    private void OnToggleOverlayClicked(object sender, RoutedEventArgs e) => ToggleOverlayRequested?.Invoke();
    private void OnCloseOverlayClicked(object sender, RoutedEventArgs e) => ExitRequested?.Invoke();
    private void OnShowAlwaysClicked(object sender, RoutedEventArgs e) => ShowModeChanged?.Invoke(OverlayShowMode.Always);
    private void OnShowInGameClicked(object sender, RoutedEventArgs e) => ShowModeChanged?.Invoke(OverlayShowMode.InGameOnly);
    private void OnManualClicked(object sender, RoutedEventArgs e) => ManualRequested?.Invoke();

    private void OnPresetCardClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: OverlayPreset preset })
        {
            viewModel?.SetManualSheetOpen(false);
            ApplyManualSheetVisibility();
            PresetRequested?.Invoke(preset);
        }
    }

    private void OnCopyReportClicked(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(diagnosticsDumpText);
        Clipboard.SetContent(package);
    }

    private void OnOpenLogClicked(object sender, RoutedEventArgs e)
    {
        var directory = Path.GetDirectoryName(AppLogger.LogPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = directory,
            UseShellExecute = true,
        });
    }

    private void OnOpenWebsiteClicked(object sender, RoutedEventArgs e) => OpenUrl("https://handheldally.com");
    private void OnOpenCommunityClicked(object sender, RoutedEventArgs e) => OpenUrl("https://handheldally.com/games");

    private void OnPositionSelected(TopBarPosition position)
    {
        viewModel?.SetPositionFlyoutOpen(false);
        PositionChanged?.Invoke(position);
    }

    private void OnOpacityFlyoutChanged(double backgroundOpacity, double textOpacity) => OpacityChanged?.Invoke(backgroundOpacity, textOpacity);
    private void OnTextSizeFlyoutChanged(double size) => TextSizeChanged?.Invoke(size);

    private void OnManualMetricsChanged(List<string> metricIds)
    {
        viewModel?.SetManualSheetOpen(true);
        CustomMetricsChanged?.Invoke(metricIds);
    }

    private void OnPositionFlyoutOpened(object sender, object e) => viewModel?.SetPositionFlyoutOpen(true);
    private void OnPositionFlyoutClosed(object sender, object e) => viewModel?.SetPositionFlyoutOpen(false);
    private void OnFeelAndFitFlyoutOpened(object sender, object e) => viewModel?.SetFeelAndFitOpen(true);
    private void OnFeelAndFitFlyoutClosed(object sender, object e) => viewModel?.SetFeelAndFitOpen(false);

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
    }
}
