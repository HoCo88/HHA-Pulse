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

    [Fact]
    public void Create_PreservesPlanATelemetryProvenance()
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.StorageTemperature | MetricFlags.StorageWear | MetricFlags.CpuClock,
            Storage =
            {
                TemperatureCelsius = 47,
                WearPercentUsed = 6
            },
            CpuDetail = { AggregateEffectiveMhz = 3215 }
        };

        MeasurementTraceRecorder.Record(
            snapshot,
            OverlayPresetCatalog.StorageTemp,
            "StorageTempCollector",
            "IOCTL_STORAGE_QUERY_PROPERTY(StorageDeviceTemperatureProperty)",
            true,
            "SSD 47°C",
            "deviceNumber=0; infoCount=1",
            "Storage temperature from IOCTL storage temperature descriptor.",
            "IOCTL_STORAGE_QUERY_PROPERTY + STORAGE_TEMPERATURE_DATA_DESCRIPTOR",
            "47",
            "C",
            "signed short Celsius from sensor index 0",
            "47",
            "C",
            TelemetryValidationState.Verified,
            "Storage temperature collected from the primary/system drive.");

        MeasurementTraceRecorder.Record(
            snapshot,
            OverlayPresetCatalog.StorageWear,
            "CaptureServiceCollector",
            "MSFT_StorageReliabilityCounter",
            true,
            "6%",
            "serviceTelemetryAgeMs=5; powerOnHours=123",
            "Storage reliability from MSFT_StorageReliabilityCounter. wear=6%, powerOnHours=123.",
            "MSFT_StorageReliabilityCounter.Wear",
            "6",
            "%",
            "read-only WMI reliability counter",
            "6",
            "%",
            TelemetryValidationState.HardwareValidationPending,
            "Capture service returned storage wear telemetry.");

        MeasurementTraceRecorder.Record(
            snapshot,
            OverlayPresetCatalog.CpuClock,
            "CpuClockCollector",
            @"PDH \Processor Information(_Total)\Processor Frequency",
            true,
            "CPU ~3.21GHz",
            "PDH query -> Processor Information(_Total) frequency counter",
            "Aggregate CPU clock from PDH processor-frequency counter.",
            @"\Processor Information(_Total)\Processor Frequency",
            "3215",
            "MHz",
            "PDH reports MHz directly",
            "3215",
            "MHz",
            TelemetryValidationState.Verified,
            "Aggregate CPU clock collected from PDH.");

        var statuses = MetricStatusFactory.Create(snapshot);
        var traces = MeasurementTraceFactory.Create(snapshot, statuses);

        Assert.Equal("SSD 47°C", traces.Single(item => item.MetricId == OverlayPresetCatalog.StorageTemp).ValueText);
        Assert.Equal("6%", traces.Single(item => item.MetricId == OverlayPresetCatalog.StorageWear).ValueText);
        Assert.Contains("Processor Frequency", traces.Single(item => item.MetricId == OverlayPresetCatalog.CpuClock).PipelineText);
    }
}
