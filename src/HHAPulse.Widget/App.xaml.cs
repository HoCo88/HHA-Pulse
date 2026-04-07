using Microsoft.Gaming.XboxGameBar;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace HHAPulse.Widget;

public sealed partial class App : Application
{
    private XboxGameBarWidget? widget;

    public App()
    {
        InitializeComponent();
        Suspending += OnSuspending;
    }

    protected override void OnActivated(IActivatedEventArgs args)
    {
        if (args.Kind == ActivationKind.Protocol)
        {
            var protocolArgs = args as IProtocolActivatedEventArgs;
            if (protocolArgs.Uri.Scheme == "ms-gamebarwidget")
            {
                var widgetArgs = args as XboxGameBarWidgetActivatedEventArgs;
                if (widgetArgs != null && widgetArgs.IsLaunchActivation)
                {
                    var rootFrame = new Frame();
                    Window.Current.Content = rootFrame;
                    widget = new XboxGameBarWidget(widgetArgs, Window.Current.CoreWindow, rootFrame);
                    rootFrame.Navigate(typeof(CompactWidget), widget);
                    Window.Current.Closed += OnWidgetClosed;
                    Window.Current.Activate();
                }
            }
        }
    }

    private void OnWidgetClosed(object sender, Windows.UI.Core.CoreWindowEventArgs e)
    {
        widget = null;
        Window.Current.Closed -= OnWidgetClosed;
    }

    private void OnSuspending(object sender, SuspendingEventArgs e)
    {
        var deferral = e.SuspendingOperation.GetDeferral();
        widget = null;
        deferral.Complete();
    }
}
