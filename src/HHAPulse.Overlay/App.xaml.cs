using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using HHAPulse.Overlay.Collectors;
using HHAPulse.Overlay.Collectors.Battery;
using HHAPulse.Overlay.Collectors.Cpu;
using HHAPulse.Overlay.Collectors.Display;
using HHAPulse.Overlay.Collectors.Fps;
using HHAPulse.Overlay.Collectors.Gpu;
using HHAPulse.Overlay.Collectors.Memory;
using HHAPulse.Overlay.Collectors.Npu;
using HHAPulse.Overlay.Collectors.Storage;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Overlay.Hotkeys;
using HHAPulse.Overlay.Interop;
using HHAPulse.Overlay.Ipc;
using HHAPulse.Overlay.Services;
using HHAPulse.Overlay.Settings;
using HHAPulse.Overlay.ViewModels;
using HHAPulse.Shared.Protocol;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Vortice.DXGI;

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
    private readonly ForegroundCaptureTargetRouter captureTargetRouter = new();
    private readonly InGameVisibilityGate inGameVisibilityGate = new();
    private DispatcherTimer? tickTimer;
    private CancellationTokenSource? shutdownCts;
    private int tickFailureCount;

    public App()
    {
        // Bootstrap a legacy Windows.System.DispatcherQueueController on this thread
        // BEFORE InitializeComponent() so that any composition API that relies on the
        // legacy dispatcher queue can initialize. WinUI 3 gives the UI thread a
        // Microsoft.UI.Dispatching.DispatcherQueue, but that is NOT the same type as
        // Windows.System.DispatcherQueue — and Windows.UI.Composition.Compositor..ctor
        // explicitly requires the legacy one. Without this, TransparentBackdrop throws
        // "UnauthorizedAccessException: Access is denied. The caller must initialize
        // DispatcherQueue on this thread before this operation." See HHAP-0.4 log.
        //
        // Documented Microsoft pattern — WinAppSDK custom backdrop samples use the same
        // P/Invoke to CreateDispatcherQueueController in CoreMessaging.dll.
        EnsureLegacyDispatcherQueueController();

        InitializeComponent();
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    // Kept in a static field so the controller (and therefore the legacy dispatcher
    // queue it owns) stays alive for the lifetime of the process.
    private static IntPtr s_dispatcherQueueController = IntPtr.Zero;

    private static void EnsureLegacyDispatcherQueueController()
    {
        if (s_dispatcherQueueController != IntPtr.Zero)
        {
            return;
        }

        // DQTYPE_THREAD_CURRENT = 2 — attach the queue to the calling thread.
        // DQTAT_COM_STA          = 2 — WinUI 3 UI threads are STA.
        var options = new DispatcherQueueOptions
        {
            dwSize = Marshal.SizeOf<DispatcherQueueOptions>(),
            threadType = 2,
            apartmentType = 2
        };

        int hr = CreateDispatcherQueueController(options, out var controller);
        if (hr < 0)
        {
            // Do not throw — the overlay must still launch. TransparentBackdrop will
            // log the resulting compositor failure if transparency cannot be set up.
            AppLogger.Error($"Failed to create legacy DispatcherQueueController (HRESULT=0x{hr:X8}). Transparent backdrop will be disabled.", new COMException("CreateDispatcherQueueController failed", hr));
            return;
        }

        s_dispatcherQueueController = controller;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        public int dwSize;
        public int threadType;
        public int apartmentType;
    }

    [DllImport("CoreMessaging.dll", ExactSpelling = true)]
    private static extern int CreateDispatcherQueueController(
        DispatcherQueueOptions options,
        out IntPtr dispatcherQueueController);

    /// <summary>
    /// Loads XamlControlsResources in code-behind so a framework XamlParseException
    /// (e.g. TabViewButtonBackground in WinAppSDK 1.7+) is recoverable instead of
    /// crashing during XAML parse before any try-catch can run.
    /// </summary>
    private void LoadControlsResources()
    {
        try
        {
            var xcr = new Microsoft.UI.Xaml.Controls.XamlControlsResources();
            Resources.MergedDictionaries.Add(xcr);
        }
        catch (Exception ex)
        {
            AppLogger.Error("XamlControlsResources failed to load (WinAppSDK theme bug). " +
                            "The overlay will continue with default resources.", ex);
        }
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            LoadControlsResources();
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
        TelemetryContractGuard.ThrowIfInvalid();
        shutdownCts = new CancellationTokenSource();

        // Load settings.
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HHAPulse", "settings.json");
        settingsService = new SettingsService(settingsPath);
        settings = await settingsService.LoadAsync(shutdownCts.Token);
        AppLogger.Info($"Loaded settings from {settingsPath}. Tick interval: {settings.UpdateInterval}.");
        ApplyStartupPresetCommand(args.Arguments);

        // Create collectors with vendor-specific GPU telemetry.
        captureServiceCollector = new CaptureServiceCollector();

        var collectors = new List<IMetricCollector>
        {
            captureServiceCollector,
            new BatteryCollector(),
            new CpuUsageCollector(),
            new CpuPowerCollector(),
            new CpuClockCollector(),
            new DramPowerEmiCollector(),
            new RamCollector(),
            new GpuUsageCollector(),
            new GpuPerfDataCollector(),
            new VramCollector(),
            new DisplayCollector(),
            new StorageTempCollector(),
            new NpuDetectionCollector()
        };

        // Detect GPU vendor via DXGI and add the matching SDK collector.
        var vendorId = DetectPrimaryGpuVendor();
        switch (vendorId)
        {
            case 0x1002:
                collectors.Add(new AdlxGpuCollector());
                AppLogger.Info("GPU vendor: AMD (0x1002). Added ADLX collector.");
                break;
            case 0x8086:
                // Register EMI RAPL PP1 BEFORE IGCL so that if a future
                // system has both an Intel iGPU and a discrete Arc dGPU,
                // IGCL's (correct for dGPU) reading overwrites the PP1
                // (iGPU rail) reading. On Lunar Lake iGPU-only, IGCL
                // does not populate power (validFlags=0x08 observed),
                // so PP1 persists as the valid source.
                collectors.Add(new GpuPowerEmiCollector());
                collectors.Add(new IgclGpuCollector());
                AppLogger.Info("GPU vendor: Intel (0x8086). Added IGCL + EMI RAPL PP1 collectors.");
                break;
            case 0x10DE:
                collectors.Add(new NvApiGpuCollector());
                AppLogger.Info("GPU vendor: NVIDIA (0x10DE). Added NvAPI collector.");
                break;
            default:
                AppLogger.Info($"GPU vendor: unknown (0x{vendorId:X4}). No vendor SDK collector added; D3DKMT fallback only.");
                break;
        }

        AppLogger.Info($"Active collectors: {string.Join(", ", collectors.Select(c => c.Name))}.");

        var orchestrator = new CollectorOrchestrator(collectors);
        var pipeServer = new PipeServer();
        appHost = new AppHost(orchestrator, pipeServer);

        await appHost.StartAsync(shutdownCts.Token);
        AppLogger.Info("AppHost started.");

        // Auto-install capture service on first launch if not already running.
        EnsureCaptureServiceInstalled();

        // Create and show the overlay window.
        window = new MainWindow();
        window.Closed += OnWindowClosed;
        window.Activate();
        window.ApplyPosition(settings.TopBarPosition);
        if (settings.ShowMode == OverlayShowMode.InGameOnly)
        {
            window.SetOverlayVisible(false);
        }

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
            var targetRoute = captureTargetRouter.Resolve(ForegroundGameDetector.TryGetForegroundWindowInfo(), DateTimeOffset.UtcNow);
            captureServiceCollector?.SetTarget(targetRoute.Target, targetRoute.ToStatusText());

            var snapshot = await appHost.TickAsync(shutdownCts.Token);
            tickFailureCount = 0;

            // In-game only mode: auto show/hide based on real ETW frame telemetry,
            // not foreground-process target guesses.
            if (settings?.ShowMode == OverlayShowMode.InGameOnly && window is not null)
            {
                window.SetOverlayVisible(inGameVisibilityGate.ShouldBeVisible(snapshot, DateTimeOffset.UtcNow));
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
        window?.ApplyPosition(settings.TopBarPosition);
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
            controlWindow.PresetChanged += OnPresetChanged;
            controlWindow.CustomMetricsChanged += OnCustomMetricsChanged;
            controlWindow.OpacityChanged += OnOpacityChanged;
            controlWindow.TextSizeChanged += OnTextSizeChanged;
            controlWindow.PositionChanged += OnPositionChanged;
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
            controlWindow.PresetChanged -= OnPresetChanged;
            controlWindow.CustomMetricsChanged -= OnCustomMetricsChanged;
            controlWindow.OpacityChanged -= OnOpacityChanged;
            controlWindow.TextSizeChanged -= OnTextSizeChanged;
            controlWindow.PositionChanged -= OnPositionChanged;
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
        else
        {
            inGameVisibilityGate.Reset();
            window?.SetOverlayVisible(false);
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

    private void OnPositionChanged(TopBarPosition position)
    {
        if (settings is null)
        {
            return;
        }

        settings.TopBarPosition = position;
        window?.ApplyPosition(position);
        controlWindow?.ApplySettings(settings);
        _ = SaveSettingsAsync();
    }

    private void OnExitRequested()
    {
        controlWindow?.Close();
        window?.Close();
    }

    private void EnsureCaptureServiceInstalled()
    {
        try
        {
            var serviceExe = Path.Combine(AppContext.BaseDirectory, "HHAPulse.CaptureService.exe");
            if (!File.Exists(serviceExe))
            {
                AppLogger.Info($"Capture service not found at {serviceExe}. FPS will show -- until installed.");
                return;
            }

            // Check if the service is already running by trying to connect to its output pipe.
            // If the pipe exists, the service is already installed and running.
            if (File.Exists(@"\\.\pipe\LOCAL\HHAPulse.Capture.Out"))
            {
                AppLogger.Info("Capture service already running.");
                return;
            }

            // Check if the service is installed (even if stopped) via sc query.
            var sc = Process.Start(new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"query {PipeConstants.CaptureServiceName}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });

            if (sc is not null)
            {
                var output = sc.StandardOutput.ReadToEnd();
                sc.WaitForExit(3000);

                if (output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase))
                {
                    AppLogger.Info("Capture service is installed and running.");
                    return;
                }

                if (output.Contains("STOPPED", StringComparison.OrdinalIgnoreCase))
                {
                    // Service is installed but stopped — start it.
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "sc.exe",
                        Arguments = $"start {PipeConstants.CaptureServiceName}",
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = true
                    });
                    AppLogger.Info("Capture service was stopped. Starting it.");
                    return;
                }
            }

            // Service not installed — install it (one-time UAC prompt).
            AppLogger.Info("Capture service not installed. Installing now (one-time UAC prompt).");
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
            AppLogger.Error("Auto-install of capture service failed. FPS will show -- until manually enabled.", ex);
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

    /// <summary>
    /// Enumerates DXGI adapters and returns the VendorId of the primary GPU.
    /// Returns 0 if detection fails.
    /// </summary>
    private static uint DetectPrimaryGpuVendor()
    {
        try
        {
            if (!DxgiPrimaryAdapterSelector.TryGetPrimaryAdapter(out var adapter, out var description) || adapter is null)
            {
                return 0;
            }

            using (adapter)
            {
                AppLogger.Info($"Vendor detect: Primary adapter '{description.Description}' VendorId=0x{description.VendorId:X4}.");
                return (uint)description.VendorId;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Vendor detect: DXGI enumeration failed.", ex);
            return 0;
        }
    }
}
