using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Shared.Models;
using Xunit;

namespace HHAPulse.Overlay.Tests.Diagnostics;

public sealed class SystemPowerValidatorTests
{
    [Fact]
    public void ApplyTo_SetsSystemPowerForBatteryDischarge()
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.Battery,
            Battery =
            {
                IsCharging = false,
                DischargeWatts = 21.4
            }
        };

        SystemPowerValidator.ApplyTo(snapshot);

        Assert.True(snapshot.AvailableMetrics.HasFlag(MetricFlags.SystemPower));
    }

    [Fact]
    public void ApplyTo_DoesNotSetSystemPowerForCpuAndGpuSum()
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.CpuPower | MetricFlags.GpuPower,
            Cpu = { PowerWatts = 11.2 },
            Gpu = { PowerWatts = 17.8 }
        };

        SystemPowerValidator.ApplyTo(snapshot);

        Assert.False(snapshot.AvailableMetrics.HasFlag(MetricFlags.SystemPower));
        Assert.Equal(29.0, snapshot.Dependencies.ComponentPowerSumWatts, 1);
        Assert.Contains("diagnostics-only", snapshot.Dependencies.ComponentPowerStatusMessage);
    }

    [Fact]
    public void ApplyTo_ClearsSystemPowerWhenOnlyOneComponentExists()
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.SystemPower | MetricFlags.CpuPower,
            Cpu = { PowerWatts = 11.2 }
        };

        SystemPowerValidator.ApplyTo(snapshot);

        Assert.False(snapshot.AvailableMetrics.HasFlag(MetricFlags.SystemPower));
    }

    [Fact]
    public void ApplyTo_RecordsValidatorTraceForBatteryDischarge()
    {
        var snapshot = new TelemetrySnapshot
        {
            AvailableMetrics = MetricFlags.Battery,
            Battery =
            {
                IsCharging = false,
                DischargeWatts = 21.4
            }
        };

        SystemPowerValidator.ApplyTo(snapshot);

        var trace = Assert.Single(snapshot.MeasurementTraces);
        Assert.Equal("total_power", trace.MetricId);
        Assert.Equal("SystemPowerValidator", trace.Collector);
        Assert.Equal("Verified", trace.ValidationState);
    }
}
