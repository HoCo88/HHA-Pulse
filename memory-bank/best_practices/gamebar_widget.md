# Game Bar Widget Best Practices (April 2026)

## Widget Activation (CRITICAL — without this widget won't appear)

Must override `OnActivated` in App.xaml.cs:

```csharp
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
                rootFrame.Navigate(typeof(WidgetMainPage));
                
                Window.Current.Closed += OnWidgetClosed;
                Window.Current.Activate();
            }
        }
    }
}
```

Protocol: `ms-gamebarwidget`. Each activation = new CoreWindow + new XboxGameBarWidget instance.

## Manifest Requirements

### AppExtension (widget registration)
```xml
<uap3:Extension Category="windows.appExtension">
  <uap3:AppExtension Name="microsoft.gameBarUIExtension"
                     Id="HHAPulseWidget"
                     DisplayName="HHA Pulse"
                     Description="Performance overlay for Windows handhelds"
                     PublicFolder="GameBar">
    <uap3:Properties>
      <GameBarWidget Type="Standard">
        <HomeMenuVisible>true</HomeMenuVisible>
        <PinningSupported>true</PinningSupported>
        <Window>
          <Size>
            <Height>300</Height><Width>464</Width>
            <MinHeight>150</MinHeight><MinWidth>464</MinWidth>
            <MaxHeight>500</MaxHeight><MaxWidth>900</MaxWidth>
          </Size>
        </Window>
      </GameBarWidget>
    </uap3:Properties>
  </uap3:AppExtension>
</uap3:Extension>
```

### ProxyStub (REQUIRED — COM marshaling)
Must include full ProxyStub section from XboxGameBarSamples with all IXboxGameBar*Private interfaces. Without this, widget crashes on activation.

Source: github.com/microsoft/XboxGameBarSamples — copy the entire Extensions/ProxyStub block.

## Game Bar SDK
- NuGet: `Microsoft.Gaming.XboxGameBar` v7.3.2511061 (Feb 2026)
- UWP XAML only (NOT WinUI 3)
- Compact Mode: min width 464px
- Pinnable, transparent, click-through supported
- Controller navigation: LB/RB to switch widgets

## Widget Lifecycle
- Game Bar creates new CoreWindow per widget activation
- On close: CoreWindow fires `Closed` — null out XboxGameBarWidget reference
- On suspend: `OnSuspending` fires — cleanup
- WidgetActivity API prevents idle shutdown for long-running monitoring

## IPC with Overlay (Same MSIX Package)
- Named Pipe: `\\.\pipe\LOCAL\HHAPulse`
- Same package = no Store policy exceptions needed
- DACL: Package SID sufficient (same package)
- UWP side: standard `NamedPipeClientStream` works
- Reconnection: exponential backoff if overlay not running yet

## Standalone vs Enhanced Mode
- Standalone (overlay not running): battery via Windows.System.Power, basic CPU/GPU/RAM
- Enhanced (pipe connected): all metrics from overlay
- Auto-switch based on pipe availability
- Show "Connected to HHA Pulse" / "Standalone Mode" indicator

## Upsell (Store Policy Compliant)
- Button: "Unlock full overlay" → IAP purchase flow
- "Powered by Handheld Ally" branding = allowed
- handheldally.com link for info/support = allowed
- Direct purchase link to external site = NOT allowed (use IAP)
