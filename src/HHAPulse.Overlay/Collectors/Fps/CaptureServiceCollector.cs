using System.IO.Pipes;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Overlay.Interop;
using HHAPulse.Shared.Models;
using HHAPulse.Shared.Protocol;

namespace HHAPulse.Overlay.Collectors.Fps;

public sealed class CaptureServiceCollector : IMetricCollector
{
    private static readonly TimeSpan LiveFreshnessWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HeldFreshnessWindow = ForegroundCaptureTargetRouter.DefaultHoldWindow;
    private readonly object syncRoot = new();
    private readonly CancellationTokenSource shutdown = new();
    private CaptureFrameMetrics? latestFrameMetrics;
    private CaptureFrameMetrics? latestServiceTelemetry;
    private CaptureTarget? target;
    private Task? outputTask;
    private Task? controlTask;
    private bool isConnected;
    private string lastStatus = "Capture service not connected.";
    private string targetRouteStatus = "targetSource=cleared; no capture target selected";

    public string Name => "Capture Service";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        outputTask = Task.Run(() => RunOutputLoopAsync(shutdown.Token), CancellationToken.None);
        controlTask = Task.Run(() => RunControlLoopAsync(shutdown.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public void SetTarget(CaptureTarget? captureTarget, string? routeStatus = null)
    {
        lock (syncRoot)
        {
            target = captureTarget;
            if (!string.IsNullOrWhiteSpace(routeStatus))
            {
                targetRouteStatus = routeStatus;
            }
        }
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        CaptureFrameMetrics? metrics;
        CaptureFrameMetrics? serviceTelemetry;
        CaptureTarget? currentTarget;
        bool connected;
        string status;
        string routeStatus;
        lock (syncRoot)
        {
            metrics = latestFrameMetrics;
            serviceTelemetry = latestServiceTelemetry;
            currentTarget = target;
            connected = isConnected;
            status = lastStatus;
            routeStatus = targetRouteStatus;
        }

        snapshot.Dependencies.CaptureServiceConnected = connected;
        snapshot.Dependencies.CaptureServiceStatusMessage = $"{status} {routeStatus}";
        if (currentTarget is not null)
        {
            snapshot.Dependencies.CaptureTargetProcessId = currentTarget.ProcessId;
            snapshot.Dependencies.CaptureTargetProcessName = currentTarget.ProcessName;
        }

        ApplyServiceTelemetry(snapshot, serviceTelemetry);

        if (metrics is null)
            return Task.CompletedTask;

        var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(metrics.TimestampUnixMilliseconds);
        snapshot.Dependencies.CapturePayloadAgeMilliseconds = (long)Math.Max(0, age.TotalMilliseconds);

        var captureFreshness = age <= LiveFreshnessWindow
            ? "live"
            : age <= HeldFreshnessWindow && routeStatus.Contains("targetSource=held", StringComparison.OrdinalIgnoreCase)
                ? "held"
                : "stale";

        if (captureFreshness == "stale")
        {
            snapshot.Dependencies.CaptureServiceStatusMessage = $"Capture connected but stale ({age.TotalSeconds:0.0}s old). {routeStatus}";
            return Task.CompletedTask;
        }

        snapshot.Dependencies.CaptureServiceStatusMessage = metrics.HybridPresentDetected
            ? $"{status} captureFreshness={captureFreshness}; {routeStatus} Capture mode: ETW/PDH read-only, no hooks or VRR changes. Frame generation: detected from Intel-PresentMon ETW evidence. fps={metrics.FramesPerSecond:0.0}, avg={metrics.AverageFramesPerSecond:0.0}, 1%={metrics.OnePercentLowFramesPerSecond:0.0}, 0.1%={metrics.ZeroPointOnePercentLowFramesPerSecond:0.0}, ft={metrics.FrameTimeMilliseconds:0.0}ms."
            : $"{status} captureFreshness={captureFreshness}; {routeStatus} Capture mode: ETW/PDH read-only, no hooks or VRR changes. Frame generation: not detected in the current capture window. fps={metrics.FramesPerSecond:0.0}, avg={metrics.AverageFramesPerSecond:0.0}, 1%={metrics.OnePercentLowFramesPerSecond:0.0}, 0.1%={metrics.ZeroPointOnePercentLowFramesPerSecond:0.0}, ft={metrics.FrameTimeMilliseconds:0.0}ms.";

        if (metrics.FramesPerSecond <= 0)
            return Task.CompletedTask;

        snapshot.AvailableMetrics |= MetricFlags.Fps | MetricFlags.FrameTime;
        snapshot.Performance.FramesPerSecond = metrics.FramesPerSecond;
        snapshot.Performance.AverageFramesPerSecond = metrics.AverageFramesPerSecond;
        snapshot.Performance.OnePercentLowFramesPerSecond = metrics.OnePercentLowFramesPerSecond;
        snapshot.Performance.ZeroPointOnePercentLowFramesPerSecond = metrics.ZeroPointOnePercentLowFramesPerSecond;
        snapshot.Performance.FrameTimeMilliseconds = metrics.FrameTimeMilliseconds;
        snapshot.Performance.GpuBusyMilliseconds = metrics.GpuBusyMilliseconds;
        snapshot.Performance.AppFramesPerSecond = metrics.AppFramesPerSecond;
        snapshot.Performance.PresentFramesPerSecond = metrics.PresentFramesPerSecond;
        snapshot.Performance.DisplayFramesPerSecond = metrics.DisplayFramesPerSecond;
        snapshot.Performance.HybridPresentDetected = metrics.HybridPresentDetected;
        snapshot.Dependencies.CaptureTargetProcessId = metrics.GameProcessId;
        snapshot.Dependencies.CaptureTargetProcessName = metrics.GameProcessName;
        RecordFpsTrace(snapshot, "fps", "FPS", metrics.FramesPerSecond, "present-start intervals -> 1000 / average frame time");
        RecordFpsTrace(snapshot, "avg_fps", "Average FPS", metrics.AverageFramesPerSecond, "5-second rolling average of one-second FPS windows");
        RecordFpsTrace(snapshot, "one_percent_low", "1% Low FPS", metrics.OnePercentLowFramesPerSecond, "99th percentile frame time -> FPS");
        RecordFpsTrace(snapshot, "zero_point_one_low", "0.1% Low FPS", metrics.ZeroPointOnePercentLowFramesPerSecond, "99.9th percentile frame time -> FPS");
        MeasurementTraceRecorder.Record(
            snapshot,
            "frametime",
            nameof(CaptureServiceCollector),
            "ETW DXGI/D3D9 PresentStart",
            true,
            $"{metrics.FrameTimeMilliseconds:0.0}ms",
            $"captureConnected={connected}; captureFreshness={captureFreshness}; routeStatus={routeStatus}; payloadAgeMs={snapshot.Dependencies.CapturePayloadAgeMilliseconds}; target={metrics.GameProcessName}({metrics.GameProcessId})",
            snapshot.Dependencies.CaptureServiceStatusMessage,
            "Microsoft-Windows-DXGI/D3D9 ETW PresentStart",
            $"{metrics.FrameTimeMilliseconds:0.000}",
            "ms",
            "average accepted present interval in publish window",
            $"{metrics.FrameTimeMilliseconds:0.000}",
            "ms",
            TelemetryValidationState.Verified,
            "Fresh ETW capture payload accepted.");
        return Task.CompletedTask;
    }

    private static void RecordFpsTrace(TelemetrySnapshot snapshot, string metricId, string label, double value, string conversionRule)
    {
        MeasurementTraceRecorder.Record(
            snapshot,
            metricId,
            nameof(CaptureServiceCollector),
            "ETW DXGI/D3D9 PresentStart",
            true,
            $"{value:0.0}",
            $"captureConnected={snapshot.Dependencies.CaptureServiceConnected}; payloadAgeMs={snapshot.Dependencies.CapturePayloadAgeMilliseconds}; target={snapshot.Dependencies.CaptureTargetProcessName}({snapshot.Dependencies.CaptureTargetProcessId})",
            $"{label} from fresh capture-service ETW payload.",
            "Microsoft-Windows-DXGI/D3D9 ETW PresentStart",
            $"{value:0.000}",
            "fps",
            conversionRule,
            $"{value:0.000}",
            "fps",
            TelemetryValidationState.Verified,
            "Fresh ETW capture payload accepted.");
    }

    private static void ApplyServiceTelemetry(TelemetrySnapshot snapshot, CaptureFrameMetrics? metrics)
    {
        if (metrics is null)
        {
            return;
        }

        if (metrics.CpuTemperatureCelsius > 0)
        {
            snapshot.Cpu.TemperatureCelsius = metrics.CpuTemperatureCelsius;
            snapshot.AvailableMetrics |= MetricFlags.CpuTemperature;
            var source = Blank(metrics.CpuTemperatureSource, "capture service sensor telemetry");
            var status = Blank(metrics.CpuTemperatureStatusMessage, "CPU temperature from capture service sensor telemetry.");
            MeasurementTraceRecorder.Record(
                snapshot,
                "cpu_temp",
                nameof(CaptureServiceCollector),
                source,
                true,
                $"{metrics.CpuTemperatureCelsius:0.0}C",
                $"serviceTelemetryAgeMs={ServiceTelemetryAgeMilliseconds(metrics)}",
                status,
                source,
                $"{metrics.CpuTemperatureCelsius:0.000}",
                "C",
                "capture-service ACPI/WMI read",
                $"{metrics.CpuTemperatureCelsius:0.000}",
                "C",
                TelemetryValidationState.HardwareValidationPending,
                "Capture service returned a bounded CPU temperature.");
        }

        var fans = metrics.FanRpms.Where(rpm => rpm > 0).ToArray();
        if (fans.Length > 0)
        {
            snapshot.AvailableMetrics |= MetricFlags.Fan;
            snapshot.Gpu.FanRpm = fans[0];
            snapshot.Dependencies.FanRpms = fans;
            snapshot.Dependencies.DeviceFanSource = metrics.DeviceFanSource;
            snapshot.Dependencies.DeviceFanStatusMessage = metrics.DeviceFanStatusMessage;
            snapshot.Dependencies.GpuFanSource = metrics.DeviceFanSource;
            snapshot.Dependencies.GpuFanStatusMessage = metrics.DeviceFanStatusMessage;
            MeasurementTraceRecorder.Record(
                snapshot,
                "gpu_fan",
                nameof(CaptureServiceCollector),
                Blank(metrics.DeviceFanSource, "capture service device fan telemetry"),
                true,
                string.Join("/", fans.Select(rpm => rpm.ToString("0"))) + "rpm",
                $"serviceTelemetryAgeMs={ServiceTelemetryAgeMilliseconds(metrics)}; fanCount={fans.Length}",
                Blank(metrics.DeviceFanStatusMessage, "Device/chassis fan tachometers from capture service."),
                Blank(metrics.DeviceFanSource, "capture service device fan telemetry"),
                string.Join("/", fans),
                "rpm",
                "read-only service telemetry",
                string.Join("/", fans),
                "rpm",
                TelemetryValidationState.HardwareValidationPending,
                "Capture service returned non-zero device/chassis fan tachometer readings.");
        }

        if (metrics.DeviceTemperatureCelsius > 0)
        {
            snapshot.AvailableMetrics |= MetricFlags.DeviceTemperature;
            snapshot.Dependencies.DeviceTemperatureCelsius = metrics.DeviceTemperatureCelsius;
            snapshot.Dependencies.DeviceTemperatureSource = metrics.DeviceTemperatureSource;
            snapshot.Dependencies.DeviceTemperatureStatusMessage = metrics.DeviceTemperatureStatusMessage;
            MeasurementTraceRecorder.Record(
                snapshot,
                "device_temp",
                nameof(CaptureServiceCollector),
                Blank(metrics.DeviceTemperatureSource, "capture service device temperature telemetry"),
                true,
                $"{metrics.DeviceTemperatureCelsius:0.0}C",
                $"serviceTelemetryAgeMs={ServiceTelemetryAgeMilliseconds(metrics)}",
                Blank(metrics.DeviceTemperatureStatusMessage, "Device/SoC temperature from capture service."),
                Blank(metrics.DeviceTemperatureSource, "capture service device temperature telemetry"),
                $"{metrics.DeviceTemperatureCelsius:0.000}",
                "C",
                "read-only service telemetry; sensor identity is OEM/EC-defined",
                $"{metrics.DeviceTemperatureCelsius:0.000}",
                "C",
                TelemetryValidationState.HardwareValidationPending,
                "Capture service returned a bounded device/SoC temperature. This does not populate GPU temperature.");
        }

        if (metrics.StorageReliabilityAvailable)
        {
            snapshot.AvailableMetrics |= MetricFlags.StorageWear;
            snapshot.Storage.WearPercentUsed = metrics.StorageWearPercentUsed;
            snapshot.Storage.PowerOnHours = metrics.StoragePowerOnHours;
            if (!string.IsNullOrWhiteSpace(metrics.StorageDeviceModel))
            {
                snapshot.Storage.DeviceModel = metrics.StorageDeviceModel;
            }

            snapshot.Storage.Reliability = metrics.StorageWearPercentUsed >= 100
                ? StorageReliabilityState.Failed
                : metrics.StorageWearPercentUsed >= 90
                    ? StorageReliabilityState.Warning
                    : StorageReliabilityState.Healthy;

            MeasurementTraceRecorder.Record(
                snapshot,
                "storage_wear",
                nameof(CaptureServiceCollector),
                "MSFT_StorageReliabilityCounter",
                true,
                $"{metrics.StorageWearPercentUsed:0}%",
                $"serviceTelemetryAgeMs={ServiceTelemetryAgeMilliseconds(metrics)}; powerOnHours={metrics.StoragePowerOnHours}; model={metrics.StorageDeviceModel}",
                Blank(metrics.StorageReliabilityStatusMessage, "Storage wear from capture service reliability telemetry."),
                "MSFT_StorageReliabilityCounter.Wear",
                metrics.StorageWearPercentUsed.ToString(),
                "%",
                "read-only WMI reliability counter",
                metrics.StorageWearPercentUsed.ToString(),
                "%",
                TelemetryValidationState.HardwareValidationPending,
                "Capture service returned storage wear telemetry.");
        }
    }

    private static long ServiceTelemetryAgeMilliseconds(CaptureFrameMetrics metrics)
    {
        if (metrics.ServiceTelemetryTimestampUnixMilliseconds <= 0)
        {
            return 0;
        }

        var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(metrics.ServiceTelemetryTimestampUnixMilliseconds);
        return (long)Math.Max(0, age.TotalMilliseconds);
    }

    private static string Blank(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private async Task RunOutputLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeClientStream(
                    ".",
                    PipeConstants.CaptureOutputPipeLocalName,
                    PipeDirection.In,
                    PipeOptions.Asynchronous);

                await pipe.ConnectAsync(2000, cancellationToken).ConfigureAwait(false);
                SetConnectionState(true, "FPS capture connected.");

                while (pipe.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    var frame = await ReadFrameAsync(pipe, cancellationToken).ConfigureAwait(false);
                    var envelope = MessageSerializer.DeserializeEnvelope(frame);
                    if (envelope.MessageType == IpcMessageType.CaptureFrameMetrics)
                    {
                        var metrics = MessageSerializer.DeserializePayload<CaptureFrameMetrics>(envelope);
                        lock (syncRoot)
                        {
                            latestServiceTelemetry = metrics;
                            if (metrics.HasFrameMetrics)
                            {
                                latestFrameMetrics = metrics;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or TimeoutException)
            {
                SetConnectionState(false, "Capture service not connected.");
                await DelayReconnectAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Capture service output pipe failed.", ex);
                SetConnectionState(false, "Capture service connection failed.");
                await DelayReconnectAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task RunControlLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeClientStream(
                    ".",
                    PipeConstants.CaptureControlPipeLocalName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous);

                await pipe.ConnectAsync(2000, cancellationToken).ConfigureAwait(false);

                CaptureTarget? previous = null;
                while (pipe.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    CaptureTarget? current;
                    lock (syncRoot)
                    {
                        current = target;
                    }

                    if (!SameTarget(previous, current))
                    {
                        var targetMessage = current ?? new CaptureTarget
                        {
                            TimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        };
                        var envelope = MessageSerializer.CreateEnvelope(IpcMessageType.CaptureTarget, targetMessage);
                        var bytes = PipeFrameCodec.Encode(MessageSerializer.SerializeEnvelope(envelope));
                        await pipe.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                        await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
                        previous = current;
                    }

                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or TimeoutException)
            {
                await DelayReconnectAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Capture service control pipe failed.", ex);
                await DelayReconnectAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void SetConnectionState(bool connected, string status)
    {
        lock (syncRoot)
        {
            isConnected = connected;
            lastStatus = status;
        }
    }

    private static bool SameTarget(CaptureTarget? left, CaptureTarget? right)
    {
        return left?.ProcessId == right?.ProcessId && string.Equals(left?.ProcessName, right?.ProcessName, StringComparison.Ordinal);
    }

    private static async Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[PipeConstants.LengthPrefixBytes];
        await ReadExactlyAsync(stream, lengthBuffer, cancellationToken).ConfigureAwait(false);
        var payloadLength = BitConverter.ToInt32(lengthBuffer, 0);
        if (payloadLength < 0 || payloadLength > PipeConstants.MaxPayloadBytes)
            throw new InvalidDataException($"Invalid capture pipe payload length {payloadLength}.");

        var payload = new byte[payloadLength];
        await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("Capture pipe closed.");

            offset += read;
        }
    }

    private static async Task DelayReconnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
