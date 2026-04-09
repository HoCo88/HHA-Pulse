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
    }
}
