using Microsoft.Gaming.XboxGameBar;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace HHAPulse.Widget;

public sealed partial class CompactWidget : Page
{
    public XboxGameBarWidget? Widget { get; private set; }

    public CompactWidget()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is XboxGameBarWidget w)
            Widget = w;
    }
}
