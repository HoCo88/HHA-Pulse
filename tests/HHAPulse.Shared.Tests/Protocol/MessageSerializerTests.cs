using HHAPulse.Shared.Models;
using HHAPulse.Shared.Protocol;
using Xunit;

namespace HHAPulse.Shared.Tests.Protocol;

public sealed class MessageSerializerTests
{
    [Fact]
    public void TelemetryEnvelope_RoundTripsSnapshotPayload()
    {
        var snapshot = new TelemetrySnapshot
        {
            TimestampUnixMilliseconds = 1_777_777_777,
            AvailableMetrics = MetricFlags.Fps | MetricFlags.Battery | MetricFlags.GpuUsage,
            Performance = new PerformanceMetrics
            {
                FramesPerSecond = 73,
                AverageFramesPerSecond = 68,
                OnePercentLowFramesPerSecond = 52,
                FrameTimeMilliseconds = 13.7,
                AppFramesPerSecond = 70,
                PresentFramesPerSecond = 73
            },
            Gpu = new GpuMetrics
            {
                FanRpm = 2200,
                PowerWatts = 18.5
            },
            Battery = new BatteryMetrics
            {
                ChargePercent = 72,
                EstimatedMinutesRemaining = 108,
                DischargeWatts = 15.2
            },
            MetricStatuses =
            {
                new MetricStatus
                {
                    MetricId = "gpu_fan",
                    Source = "D3DKMT",
                    IsAvailable = true,
                    LastSuccessUnixMilliseconds = 1_777_777_777,
                    StatusMessage = "Available."
                }
            },
            MeasurementTraces =
            {
                new MeasurementTrace
                {
                    MetricId = "gpu_power",
                    Collector = "NvApiGpuCollector",
                    Source = "NVML",
                    IsAvailable = true,
                    ValueText = "18.5W",
                    PipelineText = "flagGpuPower=True; source=NVML",
                    StatusMessage = "GPU power from NVIDIA NVML."
                }
            }
        };

        var envelope = MessageSerializer.CreateEnvelope(IpcMessageType.TelemetrySnapshot, snapshot);
        var bytes = MessageSerializer.SerializeEnvelope(envelope);
        var decoded = MessageSerializer.DeserializeEnvelope(bytes);
        var roundTripped = MessageSerializer.DeserializePayload<TelemetrySnapshot>(decoded);

        Assert.Equal(PipeConstants.ProtocolVersion, decoded.Version);
        Assert.Equal(IpcMessageType.TelemetrySnapshot, decoded.MessageType);
        Assert.Equal(snapshot.TimestampUnixMilliseconds, roundTripped.TimestampUnixMilliseconds);
        Assert.Equal(73, roundTripped.Performance.FramesPerSecond);
        Assert.Equal(52, roundTripped.Performance.OnePercentLowFramesPerSecond);
        Assert.Equal(70, roundTripped.Performance.AppFramesPerSecond);
        Assert.Equal(73, roundTripped.Performance.PresentFramesPerSecond);
        Assert.Equal(2200, roundTripped.Gpu.FanRpm);
        Assert.Equal(18.5, roundTripped.Gpu.PowerWatts);
        Assert.Equal(72, roundTripped.Battery.ChargePercent);
        Assert.Single(roundTripped.MetricStatuses);
        Assert.Equal("gpu_fan", roundTripped.MetricStatuses[0].MetricId);
        Assert.Single(roundTripped.MeasurementTraces);
        Assert.Equal("gpu_power", roundTripped.MeasurementTraces[0].MetricId);
        Assert.Equal("NVML", roundTripped.MeasurementTraces[0].Source);
    }

    [Fact]
    public void CaptureFrameMetrics_RoundTripsExtendedServiceTelemetryFields()
    {
        var payload = new CaptureFrameMetrics
        {
            HasFrameMetrics = false,
            TimestampUnixMilliseconds = 1_777_777_888,
            ServiceTelemetryTimestampUnixMilliseconds = 1_777_777_889,
            CpuTemperatureCelsius = 51,
            CpuTemperatureSource = "cpu-source",
            CpuTemperatureStatusMessage = "cpu-status",
            FanRpms = new[] { 2424, 2462 },
            DeviceFanSource = "fan-source",
            DeviceFanStatusMessage = "fan-status",
            DeviceTemperatureCelsius = 64,
            DeviceTemperatureSource = "device-temp-source",
            DeviceTemperatureStatusMessage = "device-temp-status"
        };

        var envelope = MessageSerializer.CreateEnvelope(IpcMessageType.CaptureFrameMetrics, payload);
        var bytes = MessageSerializer.SerializeEnvelope(envelope);
        var decoded = MessageSerializer.DeserializeEnvelope(bytes);
        var roundTripped = MessageSerializer.DeserializePayload<CaptureFrameMetrics>(decoded);

        Assert.False(roundTripped.HasFrameMetrics);
        Assert.Equal(1_777_777_889, roundTripped.ServiceTelemetryTimestampUnixMilliseconds);
        Assert.Equal(51, roundTripped.CpuTemperatureCelsius);
        Assert.Equal("cpu-source", roundTripped.CpuTemperatureSource);
        Assert.Equal(new[] { 2424, 2462 }, roundTripped.FanRpms);
        Assert.Equal(64, roundTripped.DeviceTemperatureCelsius);
        Assert.Equal("device-temp-source", roundTripped.DeviceTemperatureSource);
    }
}
