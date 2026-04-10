using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Overlay.Settings;
using HHAPulse.Shared.Models;
using Xunit;

namespace HHAPulse.Overlay.Tests.Diagnostics;

public sealed class MeasurementTraceFactoryTests
{
    [Fact]
    public void Create_IncludesGpuPowerPipelineSemantics()
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.GpuPower,
            Gpu =
            {
                PowerWatts = 2.2
            }
        };
        snapshot.Dependencies.GpuTelemetryAvailable = true;
        snapshot.Dependencies.GpuTelemetrySource = "IGCL";
        snapshot.Dependencies.GpuPowerSource = "IGCL";
        snapshot.Dependencies.GpuPowerStatusMessage = "GPU power from Intel IGCL gpuEnergyCounter/timeStamp delta (2.2W).";
        MeasurementTraceRecorder.Record(
            snapshot,
            OverlayPresetCatalog.GpuPower,
            "IgclGpuCollector",
            "IGCL gpuEnergyCounter/timeStamp",
            true,
            "2.2W",
            "gpuEnergyCounter/timeStamp delta; flagGpuPower=True; snapshotWatts=2.200",
            "GPU power from Intel IGCL gpuEnergyCounter/timeStamp delta (2.2W).",
            "ctlPowerTelemetryGet",
            "deltaEnergy=2.2; deltaTime=1.0",
            "J/s",
            "joules / seconds",
            "2.2",
            "W",
            TelemetryValidationState.HardwareValidationPending,
            "IGCL returned bounded energy/time wattage; handheld hardware validation still pending.");

        var statuses = MetricStatusFactory.Create(snapshot);
        var traces = MeasurementTraceFactory.Create(snapshot, statuses);

        var trace = Assert.Single(traces, item => item.MetricId == OverlayPresetCatalog.GpuPower);
        Assert.Contains("gpuEnergyCounter/timeStamp delta", trace.PipelineText);
        Assert.Equal("2.2W", trace.ValueText);
    }

    [Fact]
    public void Create_MarksUnknownTraceAvailableWhenMetricFlagIsAvailable()
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.CpuUsage,
            Cpu = { UsagePercent = 31 }
        };

        var statuses = MetricStatusFactory.Create(snapshot);
        var traces = MeasurementTraceFactory.Create(snapshot, statuses);

        var trace = Assert.Single(traces, item => item.MetricId == OverlayPresetCatalog.CpuUsage);
        Assert.True(trace.IsAvailable);
        Assert.Equal("unknown collector", trace.Source);
        Assert.Equal("available, provenance missing", trace.ValueText);
    }
}
