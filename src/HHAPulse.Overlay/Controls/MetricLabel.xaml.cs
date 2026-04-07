using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HHAPulse.Overlay.Controls;

public sealed partial class MetricLabel : UserControl
{
    public MetricLabel()
    {
        InitializeComponent();
    }

    public string Label
    {
        get => LabelText.Text;
        set => LabelText.Text = value;
    }

    public string Value
    {
        get => ValueText.Text;
        set => ValueText.Text = value;
    }

    public string Unit
    {
        get => UnitText.Text;
        set => UnitText.Text = value;
    }
}
