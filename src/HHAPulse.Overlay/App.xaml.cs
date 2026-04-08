using System.Diagnostics;
using System.Reflection;
using HHAPulse.Overlay.Collectors;
using HHAPulse.Overlay.Collectors.Battery;
using HHAPulse.Overlay.Collectors.Cpu;
using HHAPulse.Overlay.Collectors.Display;
using HHAPulse.Overlay.Collectors.Fps;
using HHAPulse.Overlay.Collectors.Gpu;
using HHAPulse.Overlay.Collectors.Memory;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Overlay.Hotkeys;
using HHAPulse.Overlay.Interop;
using HHAPulse.Overlay.Ipc;
using HHAPulse.Overlay.Services;
using HHAPulse.Overlay.Settings;
using HHAPulse.Overlay.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace HHAPulse.Overlay;

public partial class App : Application
{
    private const string MutexName = @"Global\HHAPulse_Overlay";

    private Mutex? singleInstanceMutex;
    private MainWindow? window;
    private ControlWindow? controlWindow;
    private AppHost? appHost;
    private HotkeyService? hotkeyService;
    private SettingsService? settingsService;
    private AppSettings? settings;
    private OverlayViewModel? overlayViewModel;
    private CaptureServiceCollector? captureServiceCollector;
    private DispatcherTimer? tickTimer;
    private CancellationTokenSource? shutdownCts;
    private int tickFailureCount;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            await LaunchAsync(args);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Overlay launch failed.", ex);
            throw;
        }
    }

    private async Task LaunchAsync(LaunchActivatedEventArgs args)
    {
        // Single-instance enforcement.
        singleInstanceMutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            HotkeyService.SignalExistingInstance(args.Arguments);
            Environment.Exit(0);
            return;
        }

        var assembly = Assembly.GetExecutingAssembly();
        AppLogger.Info($"Starting HHA Pulse Overlay {assembly.GetName().Version}. Path: {assembly.Location}. Arguments: '{args.Arguments}'. Log: {AppLogger.LogPath}");
        shutdownCts = new CancellationTokenSource();

        // Load settings.
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HHAPulse", "settings.json");
        settingsService = new SettingsService(settingsPath);
        settings = await settingsService.LoadAsync(shutdownCts.Token);
        AppLogger.Info($"Loaded settings from {settingsPath}. Tick interval: {settings.UpdateInterval}.");
        ApplyStartupPresetCommand(args.Arguments);

        // Create collectors.
        captureServiceCollector = new CaptureServiceCollector();

        var collectors = new IMetricCollector[]
        {
            captureServiceCollector,
            new BatteryCollector(),
            new CpuUsageCollector(),
            new RamCollector(),
            new GpuUsageCollector(),
            new GpuPerfDataCollector(),
            new VramCollector(),
            new DisplayCollector()
        };

        var orchestrator = new CollectorOrchestrator(collectors);
        var pipeServer = new PipeServer();
        appHost = new AppHost(orchestrator, pipeServer);

        await appHost.StartAsync(shutdownCts.Token);
        AppLogger.Info("AppHost started.");

        // Create and show the overlay window.
        window = new MainWindow();
        window.Closed += OnWindowClosed;
        window.Activate();

        // Wire AppHost to ViewModel so UI updates with live data.
        overlayViewModel = new OverlayViewModel();
        overlayViewModel.ApplySettings(settings);
        appHost.AttachViewModel(overlayViewModel);
        window.AttachViewModel(overlayViewModel);

        hotkeyService = new HotkeyService(DispatcherQueue.GetForCurrentThread(), HandleLaunchCommand);
        hotkeyService.RegisterDefaults();
        ShowControlWindow();

        // Start the tick loop on the UI thread dispatcher.
        tickTimer = new DispatcherTimer
        {
            Interval = settings.UpdateInterval
        };
        tickTimer.Tick += OnTick;
        tickTimer.Start();
        AppLogger.Info("Overlay window activated and telemetry tick loop started.");
    }

    private async void OnTick(object? sender, object e)
    {
        if (appHost is null || shutdownCts is null || shutdownCts.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var gameTarget = ForegroundGameDetector.TryGetForegroundGameTarget();
            captureServiceCollector?.SetTarget(gameTarget);

            await appHost.TickAsync(shutdownCts.Token);
            tickFailureCount = 0;

            // In-game only mode: auto show/hide based on foreground window.
            if (settings?.ShowMode == OverlayShowMode.InGameOnly && window is not null)
            {
                window.SetOverlayVisible(gameTarget is not null);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            // Tick failures are transient; the next tick will retry.
            tickFailureCount++;
            if (tickFailureCount == 1 || tickFailureCount % 30 == 0)
            {
                AppLogger.Error($"Telemetry tick failed {tickFailureCount} time(s) in a row.", ex);
            }
        }
    }

    private async void OnWindowClosed(object sender, WindowEventArgs args)
    {
        AppLogger.Info("Overlay window closing.");
        tickTimer?.Stop();
        shutdownCts?.Cancel();
        hotkeyService?.Dispose();
        controlWindow?.Close();

        if (appHost is not null)
        {
            await appHost.DisposeAsync();
        }

        singleInstanceMutex?.ReleaseMutex();
        singleInstanceMutex?.Dispose();
        AppLogger.Info("Overlay shutdown completed.");
    }

    private void HandleLaunchCommand(string command)
    {
        var normalized = command.Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Equals("--menu", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("--settings", StringComparison.OrdinalIgnoreCase))
        {
            ShowControlWindow();
            return;
        }

        if (normalized.Equals("--toggle", StringComparison.OrdinalIgnoreCase))
        {
            ToggleOverlayVisibility();
            return;
        }

        if (normalized.Equals("--next-preset", StringComparison.OrdinalIgnoreCase))
        {
            ToggleHudMode();
            return;
        }

        if (normalized.Equals("--off", StringComparison.OrdinalIgnoreCase))
        {
            SetPreset(OverlayPreset.Off);
            return;
        }

        AppLogger.Info($"Unknown launch command '{command}'. Use --toggle, --next-preset, --off, or --menu.");
    }

    private void ApplyStartupPresetCommand(string command)
    {
        if (settings is null || string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        var normalized = command.Trim();
        if (normalized.Equals("--off", StringComparison.OrdinalIgnoreCase))
        {
            settings.ActivePreset = OverlayPreset.Off;
            AppLogger.Info("Applied startup overlay off command.");
        }
    }

    private void SetPreset(OverlayPreset preset)
    {
        if (settings is null || overlayViewModel is null)
        {
            return;
        }

        settings.ActivePreset = preset;
        ApplySettingsToWindows();
        AppLogger.Info($"Overlay preset changed to {preset}.");
        _ = SaveSettingsAsync();
    }

    private void ApplySettingsToWindows()
    {
        if (settings is null)
        {
            return;
        }

        overlayViewModel?.ApplySettings(settings);
        controlWindow?.ApplySettings(settings);
        window?.ApplyOpacity(settings.BackgroundOpacity, settings.TextOpacity);
        if (settings.TextSizePixels > 0) window?.ApplyTextSize(settings.TextSizePixels);
    }

    private void ToggleHudMode()
    {
        SetPreset(OverlayPresetCatalog.NextPreset(settings?.ActivePreset ?? OverlayPreset.Standard));
    }

    private async Task SaveSettingsAsync()
    {
        if (settingsService is null || settings is null || shutdownCts is null)
        {
            return;
        }

        try
        {
            await settingsService.SaveAsync(settings, shutdownCts.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppLogger.Error("Failed to save overlay settings.", ex);
        }
    }

    private void ToggleOverlayVisibility()
    {
        if (window is null)
        {
            return;
        }

        window.ToggleOverlayVisibility();
        AppLogger.Info("Overlay visibility toggled by launch signal.");
    }

    private void ShowControlWindow()
    {
        if (settings is null)
        {
            return;
        }

        if (controlWindow is null)
        {
            controlWindow = new ControlWindow();
            controlWindow.ViewModel = overlayViewModel;
            controlWindow.ToggleOverlayRequested += ToggleOverlayVisibility;
            controlWindow.ExitRequested += OnExitRequested;
            controlWindow.ShowModeChanged += OnShowModeChanged;
            controlWindow.EnableCaptureRequested += OnEnableCaptureRequested;
            controlWindow.PresetChanged += OnPresetChanged;
            controlWindow.CustomMetricsChanged += OnCustomMetricsChanged;
            controlWindow.OpacityChanged += OnOpacityChanged;
            controlWindow.TextSizeChanged += OnTextSizeChanged;
            controlWindow.Closed += OnControlWindowClosed;
        }

        controlWindow.ApplySettings(settings);
        controlWindow.Activate();
        AppLogger.Info("Control window shown.");
    }

    private void OnControlWindowClosed(object sender, WindowEventArgs args)
    {
        if (controlWindow is not null)
        {
            controlWindow.ToggleOverlayRequested -= ToggleOverlayVisibility;
            controlWindow.ExitRequested -= OnExitRequested;
            controlWindow.ShowModeChanged -= OnShowModeChanged;
            controlWindow.EnableCaptureRequested -= OnEnableCaptureRequested;
            controlWindow.PresetChanged -= OnPresetChanged;
            controlWindow.CustomMetricsChanged -= OnCustomMetricsChanged;
            controlWindow.OpacityChanged -= OnOpacityChanged;
            controlWindow.TextSizeChanged -= OnTextSizeChanged;
            controlWindow.Closed -= OnControlWindowClosed;
            controlWindow.ViewModel = null;
            controlWindow = null;
        }
    }

    private void OnShowModeChanged(OverlayShowMode mode)
    {
        if (settings is null)
            return;

        settings.ShowMode = mode;
        _ = SaveSettingsAsync();

        // If switching back to Always, make sure overlay is visible.
        if (mode == OverlayShowMode.Always)
        {
            window?.SetOverlayVisible(true);
        }

        AppLogger.Info($"Overlay show mode changed to {mode}.");
    }

    private void OnPresetChanged(OverlayPreset preset)
    {
        SetPreset(preset);
    }

    private void OnCustomMetricsChanged(List<string> enabledMetricIds)
    {
        if (settings is null || overlayViewModel is null)
        {
            return;
        }

        settings.EnabledMetricIds = enabledMetricIds;
        settings.ActivePreset = OverlayPreset.Custom;
        ApplySettingsToWindows();
        _ = SaveSettingsAsync();
        AppLogger.Info($"Custom metrics updated: {enabledMetricIds.Count} metrics selected.");
    }

    private void OnOpacityChanged(double bgOpacity, double textOpacity)
    {
        if (settings is null) return;

        settings.BackgroundOpacity = bgOpacity;
        settings.TextOpacity = textOpacity;
        window?.ApplyOpacity(bgOpacity, textOpacity);
        _ = SaveSettingsAsync();
    }

    private void OnTextSizeChanged(double size)
    {
        if (settings is null) return;
        settings.TextSizePixels = size;
        window?.ApplyTextSize(size);
        _ = SaveSettingsAsync();
    }

    private void OnExitRequested()
    {
        controlWindow?.Close();
        window?.Close();
    }

    private void OnEnableCaptureRequested()
    {
        try
        {
            var serviceExe = Path.Combine(AppContext.BaseDirectory, "HHAPulse.CaptureService.exe");
            if (!File.Exists(serviceExe))
            {
                AppLogger.Info($"Capture service installer not found at {serviceExe}.");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = serviceExe,
                Arguments = "--install",
                UseShellExecute = true,
                Verb = "runas"
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to start capture service installer.", ex);
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        AppLogger.Error("Unhandled WinUI exception.", args.Exception);
    }

    private static void OnAppDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception ex)
        {
            AppLogger.Error("Unhandled AppDomain exception.", ex);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        AppLogger.Error("Unobserved task exception.", args.Exception);
    }
}
