using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using HHAPulse.Overlay.ViewModels;
using HHAPulse.Shared;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI;

namespace HHAPulse.Overlay.Views;

/// <summary>
/// Four-step Custom composer drawer. Owns its own view-model; host wires the
/// outgoing events (CloseRequested / PresetApplied / LayoutChanged) to the
/// rest of the app.
/// </summary>
public sealed partial class ComposerOverlay : UserControl
{
    private const double FullscreenThreshold = 900.0;
    private const double CompactThreshold = 1280.0;

    private readonly ComposerViewModel viewModel = new();

    public ComposerOverlay()
    {
        InitializeComponent();
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.SelectedModuleIds.CollectionChanged += OnSelectedModulesCollectionChanged;
        viewModel.OrderedMetricIds.CollectionChanged += OnOrderedMetricsCollectionChanged;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSelfSizeChanged;

        RenderAll();
    }

    public event Action? CloseRequested;
    public event Action<CustomPresetDraft>? PresetApplied;
    public event Action<ComposerLayout>? LayoutChanged;

    /// <summary>
    /// Reset the composer state. Callers use this when opening the drawer so
    /// the user always starts at step 1.
    /// </summary>
    public void ResetToStart()
    {
        viewModel.Reset();
        RenderAll();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout(ActualWidth);
        RootGrid.Focus(FocusState.Programmatic);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.SelectedModuleIds.CollectionChanged -= OnSelectedModulesCollectionChanged;
        viewModel.OrderedMetricIds.CollectionChanged -= OnOrderedMetricsCollectionChanged;
    }

    private void OnSelfSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout(e.NewSize.Width);
    }

    private void ApplyResponsiveLayout(double width)
    {
        if (width <= 0)
        {
            return;
        }

        if (width < FullscreenThreshold)
        {
            DrawerRoot.HorizontalAlignment = HorizontalAlignment.Stretch;
            DrawerRoot.Width = double.NaN;
            DrawerRoot.BorderThickness = new Thickness(0);
        }
        else if (width < CompactThreshold)
        {
            DrawerRoot.HorizontalAlignment = HorizontalAlignment.Right;
            DrawerRoot.Width = 420;
            DrawerRoot.BorderThickness = new Thickness(1, 0, 0, 0);
        }
        else
        {
            DrawerRoot.HorizontalAlignment = HorizontalAlignment.Right;
            DrawerRoot.Width = 480;
            DrawerRoot.BorderThickness = new Thickness(1, 0, 0, 0);
        }
    }

    // ===== Events -> UI re-render =====

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ComposerViewModel.SelectedLayout):
                RenderLayoutButtons();
                RenderPreview();
                LayoutChanged?.Invoke(viewModel.SelectedLayout);
                break;
            case nameof(ComposerViewModel.CurrentStep):
                RenderStepChrome();
                break;
            case nameof(ComposerViewModel.ShowGraph):
            case nameof(ComposerViewModel.IsGraphSuggested):
                RenderGraphSuggestion();
                break;
            case nameof(ComposerViewModel.ModuleDetail):
                RenderDetailList();
                RenderPreview();
                break;
        }
    }

    private void OnSelectedModulesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RenderModuleChips();
        RenderDetailList();
        RenderPreview();
        RenderReorderList();
    }

    private void OnOrderedMetricsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RenderPreview();
        RenderReorderList();
        RenderGraphSuggestion();
    }

    // ===== Rendering =====

    private void RenderAll()
    {
        RenderLayoutButtons();
        RenderModuleChips();
        RenderDetailList();
        RenderReorderList();
        RenderPreview();
        RenderStepChrome();
        RenderGraphSuggestion();
    }

    private void RenderStepChrome()
    {
        var step = viewModel.CurrentStep;
        Step1Panel.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Panel.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Panel.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step4Panel.Visibility = step == 4 ? Visibility.Visible : Visibility.Collapsed;

        (StepTitleText.Text, StepSubtitleText.Text) = step switch
        {
            1 => ("Choose layout", "Pick how the HUD sits on screen."),
            2 => ("Choose modules", "Add what you want to see."),
            3 => ("Set detail level", "How much each module shows."),
            4 => ("Reorder and finish", "Drag blocks into the order you want."),
            _ => ("Choose layout", "Pick how the HUD sits on screen.")
        };

        SetStepDotActive(StepDot1, step == 1);
        SetStepDotActive(StepDot2, step == 2);
        SetStepDotActive(StepDot3, step == 3);
        SetStepDotActive(StepDot4, step == 4);

        BackButton.IsEnabled = viewModel.CanGoBack;
        PrimaryActionButton.Content = step == 4 ? "Apply" : "Next";
    }

    private void SetStepDotActive(Border dot, bool active)
    {
        dot.Style = active
            ? (Style)Application.Current.Resources["HhapChipBlueStyle"]
            : (Style)Application.Current.Resources["HhapChipStyle"];
        if (dot.Child is TextBlock tb)
        {
            tb.Foreground = active
                ? (Brush)Application.Current.Resources["HhapBlue2Brush"]
                : (Brush)Application.Current.Resources["HhapMutedBrush"];
        }
    }

    private void RenderLayoutButtons()
    {
        SetLayoutButtonActive(LayoutTopBarButton, viewModel.SelectedLayout == ComposerLayout.TopBar);
        SetLayoutButtonActive(LayoutBottomBarButton, viewModel.SelectedLayout == ComposerLayout.BottomBar);
        SetLayoutButtonActive(LayoutDualLineButton, viewModel.SelectedLayout == ComposerLayout.DualLine);
        SetLayoutButtonActive(LayoutLeftDockButton, viewModel.SelectedLayout == ComposerLayout.LeftDock);
        SetLayoutButtonActive(LayoutRightDockButton, viewModel.SelectedLayout == ComposerLayout.RightDock);
        SetLayoutButtonActive(LayoutSidePanelButton, viewModel.SelectedLayout == ComposerLayout.SidePanel);

        PreviewLayoutText.Text = viewModel.SelectedLayout.DisplayName() + " layout";
    }

    private void SetLayoutButtonActive(Button button, bool active)
    {
        button.Style = active
            ? (Style)Application.Current.Resources["HhapPrimaryButtonStyle"]
            : (Style)Application.Current.Resources["HhapGhostButtonStyle"];
    }

    private void RenderModuleChips()
    {
        var buttons = new List<Button>();
        foreach (var module in viewModel.AllModules)
        {
            var selected = viewModel.IsModuleSelected(module.Id);
            var button = new Button
            {
                Style = selected
                    ? (Style)Application.Current.Resources["HhapPrimaryButtonStyle"]
                    : (Style)Application.Current.Resources["HhapChipButtonStyle"],
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Tag = module.Id
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            stack.Children.Add(new TextBlock
            {
                Text = module.IconGlyph,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center
            });
            stack.Children.Add(new TextBlock
            {
                Text = module.DisplayName,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });
            button.Content = stack;
            button.Click += OnModuleChipClicked;
            buttons.Add(button);
        }

        ModulesRepeater.ItemsSource = buttons;
    }

    private void OnModuleChipClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string moduleId })
        {
            viewModel.ToggleModule(moduleId);
        }
    }

    private void RenderDetailList()
    {
        DetailList.Children.Clear();
        var ids = viewModel.SelectedModuleIds.ToArray();
        if (ids.Length == 0)
        {
            DetailEmptyText.Visibility = Visibility.Visible;
            return;
        }

        DetailEmptyText.Visibility = Visibility.Collapsed;

        foreach (var moduleId in ids)
        {
            var preset = ComposerModuleCatalog.Find(moduleId);
            if (preset is null)
            {
                continue;
            }

            DetailList.Children.Add(BuildDetailCard(preset));
        }
    }

    private Border BuildDetailCard(ModuleDetailPreset preset)
    {
        var card = new Border
        {
            Style = (Style)Application.Current.Resources["HhapStatCardStyle"],
            Padding = new Thickness(16)
        };

        var stack = new StackPanel { Spacing = 10 };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        header.Children.Add(new TextBlock
        {
            Text = preset.IconGlyph,
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 18,
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(new TextBlock
        {
            Text = preset.DisplayName,
            Style = (Style)Application.Current.Resources["HhapH3Style"],
            VerticalAlignment = VerticalAlignment.Center
        });
        stack.Children.Add(header);

        var detail = viewModel.GetDetail(preset.Id);
        var metrics = preset.GetMetricIdsForDetail(detail);
        stack.Children.Add(new TextBlock
        {
            Text = metrics.Count == 0
                ? "No metrics at this level."
                : string.Join(" • ", metrics.Select(FormatMetricName)),
            Style = (Style)Application.Current.Resources["HhapMutedStyle"],
            TextWrapping = TextWrapping.Wrap
        });

        var segmented = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        segmented.Children.Add(BuildSegmentButton(preset.Id, "Off", ComposerDetail.Off, detail));
        segmented.Children.Add(BuildSegmentButton(preset.Id, "Summary", ComposerDetail.Summary, detail));
        if (preset.SupportsTuning)
        {
            segmented.Children.Add(BuildSegmentButton(preset.Id, "Tuning", ComposerDetail.Tuning, detail));
        }
        if (preset.SupportsDeep)
        {
            segmented.Children.Add(BuildSegmentButton(preset.Id, "Deep", ComposerDetail.Deep, detail));
        }
        stack.Children.Add(segmented);

        card.Child = stack;
        return card;
    }

    private Button BuildSegmentButton(string moduleId, string label, ComposerDetail target, ComposerDetail current)
    {
        var button = new Button
        {
            Content = label,
            Tag = new SegmentTag(moduleId, target),
            Style = target == current
                ? (Style)Application.Current.Resources["HhapPrimaryButtonStyle"]
                : (Style)Application.Current.Resources["HhapChipButtonStyle"]
        };
        button.Click += OnSegmentClicked;
        return button;
    }

    private void OnSegmentClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SegmentTag tag })
        {
            viewModel.SetDetail(tag.ModuleId, tag.Detail);
            RenderDetailList();
        }
    }

    private void RenderReorderList()
    {
        var rows = viewModel.OrderedMetricIds.Select(FormatMetricName).ToList();
        ReorderList.ItemsSource = rows;
        ReorderEmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (!reorderHandlerHooked)
        {
            ReorderList.DragItemsCompleted += OnReorderDragCompleted;
            reorderHandlerHooked = true;
        }
    }

    private bool reorderHandlerHooked;

    private void OnReorderDragCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (ReorderList.ItemsSource is not List<string> rows)
        {
            return;
        }

        var rebuilt = new List<string>();
        foreach (var displayName in rows)
        {
            var match = viewModel.OrderedMetricIds.FirstOrDefault(id =>
                string.Equals(FormatMetricName(id), displayName, StringComparison.Ordinal)
                && !rebuilt.Contains(id));
            if (match is not null)
            {
                rebuilt.Add(match);
            }
        }

        if (rebuilt.Count != viewModel.OrderedMetricIds.Count)
        {
            return;
        }

        for (var i = 0; i < rebuilt.Count; i++)
        {
            if (!string.Equals(viewModel.OrderedMetricIds[i], rebuilt[i], StringComparison.Ordinal))
            {
                var currentIndex = viewModel.OrderedMetricIds.IndexOf(rebuilt[i]);
                if (currentIndex >= 0 && currentIndex != i)
                {
                    viewModel.MoveMetric(currentIndex, i);
                }
            }
        }
    }

    private void RenderPreview()
    {
        var metrics = viewModel.OrderedMetricIds;
        PreviewMetricsText.Text = metrics.Count == 0
            ? "Add modules to see a preview"
            : string.Join("  ·  ", metrics.Select(FormatMetricName));
        PreviewLayoutText.Text = viewModel.SelectedLayout.DisplayName() + " layout";
    }

    private void RenderGraphSuggestion()
    {
        GraphSuggestionCard.Visibility = viewModel.IsGraphSuggested ? Visibility.Visible : Visibility.Collapsed;
        if (ShowGraphToggle.IsOn != viewModel.ShowGraph)
        {
            suppressGraphToggleHandler = true;
            ShowGraphToggle.IsOn = viewModel.ShowGraph;
            suppressGraphToggleHandler = false;
        }
    }

    private bool suppressGraphToggleHandler;

    private void OnShowGraphToggled(object sender, RoutedEventArgs e)
    {
        if (suppressGraphToggleHandler)
        {
            return;
        }

        viewModel.ShowGraph = ShowGraphToggle.IsOn;
    }

    // ===== Layout step handlers =====

    private void OnLayoutTopBarClicked(object sender, RoutedEventArgs e) => SelectLayout(ComposerLayout.TopBar);
    private void OnLayoutBottomBarClicked(object sender, RoutedEventArgs e) => SelectLayout(ComposerLayout.BottomBar);
    private void OnLayoutDualLineClicked(object sender, RoutedEventArgs e) => SelectLayout(ComposerLayout.DualLine);
    private void OnLayoutLeftDockClicked(object sender, RoutedEventArgs e) => SelectLayout(ComposerLayout.LeftDock);
    private void OnLayoutRightDockClicked(object sender, RoutedEventArgs e) => SelectLayout(ComposerLayout.RightDock);
    private void OnLayoutSidePanelClicked(object sender, RoutedEventArgs e) => SelectLayout(ComposerLayout.SidePanel);

    private void SelectLayout(ComposerLayout layout) => viewModel.SelectedLayout = layout;

    // ===== Step dot shortcuts =====

    private void OnStepDot1Tapped(object sender, TappedRoutedEventArgs e) => GoToStep(1);
    private void OnStepDot2Tapped(object sender, TappedRoutedEventArgs e) => GoToStep(2);
    private void OnStepDot3Tapped(object sender, TappedRoutedEventArgs e) => GoToStep(3);
    private void OnStepDot4Tapped(object sender, TappedRoutedEventArgs e) => GoToStep(4);

    private void GoToStep(int step) => viewModel.CurrentStep = step;

    // ===== Footer actions =====

    private void OnCancelClicked(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();
    private void OnBackClicked(object sender, RoutedEventArgs e) => viewModel.CurrentStep--;
    private void OnResetClicked(object sender, RoutedEventArgs e) => viewModel.Reset();

    private void OnPrimaryActionClicked(object sender, RoutedEventArgs e)
    {
        if (viewModel.CurrentStep < 4)
        {
            viewModel.CurrentStep++;
            return;
        }

        PresetApplied?.Invoke(viewModel.CreateDraft());
        CloseRequested?.Invoke();
    }

    // ===== Scrim / key handling =====

    private void OnScrimTapped(object sender, TappedRoutedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, sender))
        {
            CloseRequested?.Invoke();
        }
    }

    private void OnDrawerTapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            CloseRequested?.Invoke();
            e.Handled = true;
        }
    }

    // ===== Helpers =====

    private static string FormatMetricName(string metricId)
    {
        return metricId switch
        {
            "fps" => "FPS",
            "avg_fps" => "AVG FPS",
            "one_percent_low" => "1% low",
            "zero_point_one_low" => "0.1% low",
            "frametime" => "Frametime",
            "cpu" => "CPU",
            "cpu_temp" => "CPU temp",
            "cpu_power" => "CPU power",
            "gpu" => "GPU",
            "gpu_temp" => "GPU temp",
            "gpu_clock" => "GPU clock",
            "gpu_power" => "GPU power",
            "gpu_fan" => "GPU fan",
            "vram" => "VRAM",
            "ram" => "RAM",
            "total_power" => "Power",
            "battery" => "Battery",
            "refresh_rate" => "Hz",
            "device_temp" => "Device temp",
            _ => metricId
        };
    }

    private readonly record struct SegmentTag(string ModuleId, ComposerDetail Detail);
}
