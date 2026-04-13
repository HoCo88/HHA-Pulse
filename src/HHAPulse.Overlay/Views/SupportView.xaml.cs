using System.Diagnostics;
using System.Reflection;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Shared.Models;
using HHAPulse.Shared.Protocol;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace HHAPulse.Overlay.Views;

public sealed partial class SupportView : UserControl
{
    private const string TipJarPlaceholderUrl = "https://handheldally.com/support-development";

    public SupportView()
    {
        InitializeComponent();
    }

    public event Action? ExitRequested;

    public void ApplyTelemetryStatus(TelemetrySnapshot snapshot)
    {
        var deps = snapshot.Dependencies;
        var statuses = snapshot.MetricStatuses.Count > 0
            ? snapshot.MetricStatuses
            : MetricStatusFactory.Create(snapshot);

        // Capture card
        CaptureStatusText.Text = deps.CaptureServiceConnected ? "Connected" : "Offline";
        CaptureDetailText.Text = ControlShellTextBuilder.BuildCaptureHelpText(snapshot, deps);
        ApplyChip(
            CaptureChip,
            CaptureChipText,
            deps.CaptureServiceConnected
                ? (deps.CapturePayloadAgeMilliseconds > 2000 ? ChipHealth.Limited : ChipHealth.Healthy)
                : ChipHealth.Offline);

        // Game detect card
        var detect = ControlShellTextBuilder.BuildGameDetectSummary(snapshot, deps);
        GameDetectStatusText.Text = detect.Headline;
        GameDetectDetailText.Text = detect.Summary;
        ApplyChip(
            GameDetectChip,
            GameDetectChipText,
            detect.Headline switch
            {
                "Active" => ChipHealth.Healthy,
                "Ready" => ChipHealth.Healthy,
                "Limited" => ChipHealth.Limited,
                _ => ChipHealth.Offline,
            });

        // Companion card
        WidgetStatusText.Text = deps.WidgetClientCount > 0 ? "Linked" : "Standalone";
        WidgetDetailText.Text = deps.WidgetClientCount > 0
            ? "The companion is reading live overlay telemetry."
            : "The companion still works on its own.";
        ApplyChip(
            WidgetChip,
            WidgetChipText,
            deps.WidgetClientCount > 0 ? ChipHealth.Healthy : ChipHealth.Limited);

        // Advanced diagnostics expander content
        var assembly = Assembly.GetExecutingAssembly();
        AboutVersionText.Text = $"Overlay {assembly.GetName().Version} | log at {AppLogger.LogPath}";

        ContractStatusText.Text = deps.TelemetryContractValid
            ? $"Telemetry contract v{PipeConstants.ProtocolVersion} valid."
            : $"Telemetry contract invalid: {ControlShellTextBuilder.Blank(deps.TelemetryContractStatusMessage)}";

        var nativePath = Path.Combine(AppContext.BaseDirectory, "HHAPulse.Native.dll");
        NativeBridgeText.Text = File.Exists(nativePath)
            ? "Native bridge present. Vendor SDKs (ADLX, IGCL, EMI) still require hardware validation."
            : "Native bridge missing. Vendor-native collectors are quarantined.";

        var gpuSummary = ControlShellTextBuilder.BuildGpuTelemetrySummary(deps);
        TraceabilityText.Text = string.IsNullOrWhiteSpace(gpuSummary)
            ? "GPU telemetry sources unavailable."
            : $"GPU telemetry sources: {gpuSummary}.";

        DiagnosticsDumpText.Text = ControlShellTextBuilder.BuildDiagnosticsDump(snapshot, statuses, snapshot.MeasurementTraces);
    }

    private static void ApplyChip(Border chip, TextBlock chipText, ChipHealth health)
    {
        var resources = Application.Current.Resources;
        switch (health)
        {
            case ChipHealth.Healthy:
                chip.Style = (Style)resources["HhapChipGreenStyle"];
                chipText.Text = "HEALTHY";
                chipText.Foreground = (Microsoft.UI.Xaml.Media.Brush)resources["HhapGreenBrush"];
                break;
            case ChipHealth.Limited:
                chip.Style = (Style)resources["HhapChipAmberStyle"];
                chipText.Text = "LIMITED";
                chipText.Foreground = (Microsoft.UI.Xaml.Media.Brush)resources["HhapAmberBrush"];
                break;
            case ChipHealth.Offline:
            default:
                chip.Style = (Style)resources["HhapChipStyle"];
                chipText.Text = "OFFLINE";
                chipText.Foreground = (Microsoft.UI.Xaml.Media.Brush)resources["HhapRedBrush"];
                break;
        }
    }

    private void OnCopyReport(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(DiagnosticsDumpText.Text ?? string.Empty);
        Clipboard.SetContent(package);
    }

    private void OnOpenLog(object sender, RoutedEventArgs e)
    {
        var directory = Path.GetDirectoryName(AppLogger.LogPath);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = directory,
            UseShellExecute = true,
        });
    }

    private void OnOpenWebsite(object sender, RoutedEventArgs e) => OpenUrl("https://handheldally.com");
    private void OnOpenCommunity(object sender, RoutedEventArgs e) => OpenUrl("https://handheldally.com/games");
    private void OnOpenTipJar(object sender, RoutedEventArgs e) => OpenUrl(TipJarPlaceholderUrl);

    private void OnExitClicked(object sender, RoutedEventArgs e) => ExitRequested?.Invoke();

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
    }

    private enum ChipHealth
    {
        Healthy,
        Limited,
        Offline,
    }
}
