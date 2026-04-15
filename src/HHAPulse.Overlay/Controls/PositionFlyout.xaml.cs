using HHAPulse.Overlay.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HHAPulse.Overlay.Controls;

public sealed partial class PositionFlyout : UserControl
{
    public PositionFlyout()
    {
        InitializeComponent();
        Loaded += (_, _) => BuildGrid();
    }

    public event Action<TopBarPosition>? PositionSelected;

    public void ApplyPosition(TopBarPosition position)
    {
        CurrentPositionText.Text = DescribePosition(position);
        SelectionHighlight.HorizontalAlignment = position switch
        {
            TopBarPosition.RightDock => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Left
        };
        SelectionHighlight.VerticalAlignment = position switch
        {
            TopBarPosition.TopThin or TopBarPosition.TopTall => VerticalAlignment.Top,
            TopBarPosition.BottomThin or TopBarPosition.BottomTall => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Stretch
        };
        SelectionHighlight.Width = position switch
        {
            TopBarPosition.LeftDock or TopBarPosition.RightDock => 24,
            _ => 120
        };
        SelectionHighlight.Height = position switch
        {
            TopBarPosition.TopTall or TopBarPosition.BottomTall => 28,
            TopBarPosition.LeftDock or TopBarPosition.RightDock => 108,
            _ => 16
        };
        SelectionHighlight.Margin = position switch
        {
            TopBarPosition.TopThin => new Thickness(12, 12, 12, 0),
            TopBarPosition.TopTall => new Thickness(12, 12, 12, 0),
            TopBarPosition.BottomThin => new Thickness(12, 0, 12, 12),
            TopBarPosition.BottomTall => new Thickness(12, 0, 12, 12),
            TopBarPosition.LeftDock => new Thickness(12, 24, 0, 24),
            TopBarPosition.RightDock => new Thickness(0, 24, 12, 24),
            _ => new Thickness(12)
        };
    }

    private void BuildGrid()
    {
        if (MonitorGrid.Children.Count > 0)
        {
            return;
        }

        double cellSize = 24;
        if (Application.Current?.Resources.TryGetValue("HhapMonitorGridCell", out var resource) == true)
        {
            cellSize = resource switch
            {
                double value => value,
                int value => value,
                _ => 24
            };
        }

        MonitorGrid.Width = cellSize * 5;
        MonitorGrid.Height = cellSize * 4;
        MonitorGrid.HorizontalAlignment = HorizontalAlignment.Center;
        MonitorGrid.VerticalAlignment = VerticalAlignment.Center;

        for (int column = 0; column < 5; column++)
        {
            MonitorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cellSize) });
        }

        for (int row = 0; row < 4; row++)
        {
            MonitorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cellSize) });
        }

        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 5; column++)
            {
                var cell = new Border
                {
                    BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["HhapLineBrush"],
                    BorderThickness = new Thickness(0.5),
                    Opacity = 0.22
                };
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, column);
                MonitorGrid.Children.Add(cell);
            }
        }
    }

    private static string DescribePosition(TopBarPosition position) => position switch
    {
        TopBarPosition.TopThin => "Top · 1 line",
        TopBarPosition.TopTall => "Top · 2 lines",
        TopBarPosition.BottomThin => "Bottom · 1 line",
        TopBarPosition.BottomTall => "Bottom · 2 lines",
        TopBarPosition.LeftDock => "Left dock",
        TopBarPosition.RightDock => "Right dock",
        _ => "Top · 1 line"
    };

    private void OnTopThinClicked(object sender, RoutedEventArgs e) => PositionSelected?.Invoke(TopBarPosition.TopThin);
    private void OnTopTallClicked(object sender, RoutedEventArgs e) => PositionSelected?.Invoke(TopBarPosition.TopTall);
    private void OnBottomThinClicked(object sender, RoutedEventArgs e) => PositionSelected?.Invoke(TopBarPosition.BottomThin);
    private void OnBottomTallClicked(object sender, RoutedEventArgs e) => PositionSelected?.Invoke(TopBarPosition.BottomTall);
    private void OnLeftDockClicked(object sender, RoutedEventArgs e) => PositionSelected?.Invoke(TopBarPosition.LeftDock);
    private void OnRightDockClicked(object sender, RoutedEventArgs e) => PositionSelected?.Invoke(TopBarPosition.RightDock);
}
