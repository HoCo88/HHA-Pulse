using System.Diagnostics;
using HHAPulse.CaptureService;
using HHAPulse.CaptureService.Etw;
using HHAPulse.CaptureService.Ipc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

const string ServiceName = "HHAPulse.Capture";

if (args.Length > 0)
{
    if (string.Equals(args[0], "--install", StringComparison.OrdinalIgnoreCase))
    {
        await ServiceInstaller.InstallAsync(ServiceName).ConfigureAwait(false);
        return;
    }

    if (string.Equals(args[0], "--uninstall", StringComparison.OrdinalIgnoreCase))
    {
        await ServiceInstaller.UninstallAsync(ServiceName).ConfigureAwait(false);
        return;
    }
}

var builder = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options => options.ServiceName = ServiceName)
    .ConfigureServices(services =>
    {
        services.AddSingleton<CaptureCoordinator>();
        services.AddSingleton<EtwFrameCapture>();
        services.AddSingleton<CapturePipeServer>();
        services.AddHostedService<Worker>();
    });

await builder.Build().RunAsync().ConfigureAwait(false);

internal static class ServiceInstaller
{
    public static async Task InstallAsync(string serviceName)
    {
        var exePath = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot resolve service executable path.");
        await RunScAsync("stop", serviceName, ignoreFailure: true).ConfigureAwait(false);
        await RunScAsync("delete", serviceName, ignoreFailure: true).ConfigureAwait(false);
        await RunScAsync("create", $"{serviceName} binPath= \"{exePath}\" start= auto DisplayName= \"HHA Pulse FPS Capture\"").ConfigureAwait(false);
        await RunScAsync("description", $"{serviceName} \"ETW FPS capture service for HHA Pulse.\"").ConfigureAwait(false);
        await RunScAsync("start", serviceName).ConfigureAwait(false);
    }

    public static async Task UninstallAsync(string serviceName)
    {
        await RunScAsync("stop", serviceName, ignoreFailure: true).ConfigureAwait(false);
        await RunScAsync("delete", serviceName, ignoreFailure: true).ConfigureAwait(false);
    }

    private static async Task RunScAsync(string command, string arguments, bool ignoreFailure = false)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"{command} {arguments}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Failed to launch sc.exe.");

        await process.WaitForExitAsync().ConfigureAwait(false);
        if (process.ExitCode != 0 && !ignoreFailure)
        {
            var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            var stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"sc.exe {command} failed with exit code {process.ExitCode}. {stdout} {stderr}");
        }
    }
}
