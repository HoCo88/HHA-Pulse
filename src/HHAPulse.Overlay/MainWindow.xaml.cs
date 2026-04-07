using HHAPulse.Overlay.Interop;
using HHAPulse.Overlay.ViewModels;
using Microsoft.UI.Xaml;

namespace HHAPulse.Overlay;

public sealed partial class MainWindow : Window
{
    private OverlayViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        Activated += OnActivated;
    }

    public void AttachViewModel(OverlayViewModel vm)
    {
        _viewModel = vm;
        // TopBarControl will bind to this ViewModel for live data updates
        if (Content is Microsoft.UI.Xaml.Controls.Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Controls.TopBarControl topBar)
                {
                    topBar.ViewModel = vm;
                }
            }
        }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        TransparentWindowHelper.MakeOverlay(this);
    }
}
