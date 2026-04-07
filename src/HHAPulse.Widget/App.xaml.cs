using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;

namespace HHAPulse.Widget;

public sealed partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Window.Current.Content = new CompactWidget();
        Window.Current.Activate();
    }
}
