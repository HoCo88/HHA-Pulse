using HHAPulse.Overlay.Interop;
using Microsoft.UI.Xaml;

namespace HHAPulse.Overlay;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Activated += OnActivated;
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        TransparentWindowHelper.MakeOverlay(this);
    }
}
