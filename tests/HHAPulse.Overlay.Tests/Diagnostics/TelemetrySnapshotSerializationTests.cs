using MessagePack;
using HHAPulse.Shared.Models;
using Xunit;

namespace HHAPulse.Overlay.Tests.Diagnostics;

public sealed class TelemetrySnapshotSerializationTests
{
    [Fact]
    public void DependencyState_RoundTripsPerMetricGpuTelemetryFields()
    {
        var snapshot = new TelemetrySnapshot();
        snapshot.Dependencies.GpuTelemetrySource = "legacy";
        snapshot.Dependencies.GpuTemperatureSource = "D3DKMT fallback";
        snapshot.Dependencies.GpuTemperatureStatusMessage = "temp";
        snapshot.Dependencies.GpuPowerSource = "NVML";
        snapshot.Dependencies.GpuPowerStatusMessage = "power";
        snapshot.Dependencies.GpuFanSource = "NvAPI";
        snapshot.Dependencies.GpuFanStatusMessage = "fan";
        snapshot.Dependencies.GpuClockSource = "IGCL";
        snapshot.Dependencies.GpuClockStatusMessage = "clock";
        snapshot.Dependencies.CapturePayloadAgeMilliseconds = 321;
        snapshot.Dependencies.WidgetClientCount = 2;
        snapshot.Dependencies.WidgetPipeStatusMessage = "2 companion client(s) connected.";
        snapshot.MeasurementTraces.Add(new MeasurementTrace
        {
            MetricId = "gpu_power",
            Collector = "IgclGpuCollector",
            Source = "IGCL",
            IsAvailable = true,
            ValueText = "2.2W",
            PipelineText = "flagGpuPower=True",
            StatusMessage = "GPU power from Intel IGCL gpuEnergyCounter/timeStamp delta (2.2W)."
        });

        var bytes = MessagePackSerializer.Serialize(snapshot);
        var roundTripped = MessagePackSerializer.Deserialize<TelemetrySnapshot>(bytes);

        Assert.Equal("legacy", roundTripped.Dependencies.GpuTelemetrySource);
        Assert.Equal("D3DKMT fallback", roundTripped.Dependencies.GpuTemperatureSource);
        Assert.Equal("temp", roundTripped.Dependencies.GpuTemperatureStatusMessage);
        Assert.Equal("NVML", roundTripped.Dependencies.GpuPowerSource);
        Assert.Equal("power", roundTripped.Dependencies.GpuPowerStatusMessage);
        Assert.Equal("NvAPI", roundTripped.Dependencies.GpuFanSource);
        Assert.Equal("fan", roundTripped.Dependencies.GpuFanStatusMessage);
        Assert.Equal("IGCL", roundTripped.Dependencies.GpuClockSource);
        Assert.Equal("clock", roundTripped.Dependencies.GpuClockStatusMessage);
        Assert.Equal(321, roundTripped.Dependencies.CapturePayloadAgeMilliseconds);
        Assert.Equal(2, roundTripped.Dependencies.WidgetClientCount);
        Assert.Equal("2 companion client(s) connected.", roundTripped.Dependencies.WidgetPipeStatusMessage);
        Assert.Single(roundTripped.MeasurementTraces);
        Assert.Equal("gpu_power", roundTripped.MeasurementTraces[0].MetricId);
        Assert.Equal("IGCL", roundTripped.MeasurementTraces[0].Source);
    }
}
