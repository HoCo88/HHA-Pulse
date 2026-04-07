using HHAPulse.Overlay.Collectors;
using HHAPulse.Overlay.Collectors.Battery;
using HHAPulse.Overlay.Collectors.Cpu;
using HHAPulse.Overlay.Collectors.Display;
using HHAPulse.Overlay.Collectors.Gpu;
using HHAPulse.Overlay.Collectors.Memory;
using HHAPulse.Overlay.Collectors.PawnIO;
using HHAPulse.Overlay.Collectors.PresentMon;
using HHAPulse.Overlay.Collectors.Vendor;
using HHAPulse.Overlay.Ipc;
using HHAPulse.Overlay.Services;
using HHAPulse.Overlay.Settings;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace HHAPulse.Overlay;

public partial class App : Application
{
    private const string MutexName = @"Global\HHAPulse_Overlay";

    private Mutex? singleInstanceMutex;
    private MainWindow? window;
    private AppHost? appHost;
    private DispatcherTimer? tickTimer;
    private CancellationTokenSource? shutdownCts;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Single-instance enforcement.
        singleInstanceMutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            Environment.Exit(0);
            return;
        }

        shutdownCts = new CancellationTokenSource();

        // Load settings.
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HHAPulse", "settings.json");
        var settingsService = new SettingsService(settingsPath);
        var settings = await settingsService.LoadAsync(shutdownCts.Token);

        // Create collectors.
        var presentMonLoader = new PresentMonLoader();
        presentMonLoader.TryInitialize();

        var collectors = new IMetricCollector[]
        {
            new BatteryCollector(),
            new CpuUsageCollector(),
            new RamCollector(),
            new GpuUsageCollector(),
            new DisplayCollector(),
            new PresentMonCollector(presentMonLoader),
            new AdlxCollector(),
            new IgclCollector(),
            new PawnIoCollector()
        };

        var orchestrator = new CollectorOrchestrator(collectors);
        var pipeServer = new PipeServer();
        appHost = new AppHost(orchestrator, pipeServer);

        await appHost.StartAsync(shutdownCts.Token);

        // Create and show the overlay window.
        window = new MainWindow();
        window.Closed += OnWindowClosed;
        window.Activate();

        // Start the tick loop on the UI thread dispatcher.
        tickTimer = new DispatcherTimer
        {
            Interval = settings.UpdateInterval
        };
        tickTimer.Tick += OnTick;
        tickTimer.Start();
    }

    private async void OnTick(object? sender, object e)
    {
        if (appHost is null || shutdownCts is null || shutdownCts.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await appHost.TickAsync(shutdownCts.Token);
        }
        catch
        {
            // Tick failures are transient; the next tick will retry.
        }
    }

    private async void OnWindowClosed(object sender, WindowEventArgs args)
    {
        tickTimer?.Stop();
        shutdownCts?.Cancel();

        if (appHost is not null)
        {
            await appHost.DisposeAsync();
        }

        singleInstanceMutex?.ReleaseMutex();
        singleInstanceMutex?.Dispose();
    }
}
