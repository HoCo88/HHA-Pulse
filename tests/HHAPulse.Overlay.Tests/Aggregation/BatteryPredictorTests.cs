using HHAPulse.Overlay.Aggregation;
using Xunit;

namespace HHAPulse.Overlay.Tests.Aggregation;

public sealed class BatteryPredictorTests
{
    [Fact]
    public void EstimateMinutesRemaining_UsesRollingAverageDischargeRate()
    {
        var predictor = new BatteryPredictor(TimeSpan.FromMinutes(5));
        var start = DateTimeOffset.Parse("2026-04-07T12:00:00+00:00");

        predictor.AddSample(start, remainingWh: 30, dischargeWatts: 15);
        predictor.AddSample(start.AddMinutes(1), remainingWh: 29.75, dischargeWatts: 20);
        predictor.AddSample(start.AddMinutes(2), remainingWh: 29.5, dischargeWatts: 10);

        var estimate = predictor.EstimateMinutesRemaining(remainingWh: 30);

        Assert.Equal(120, estimate);
    }

    [Fact]
    public void EstimateMinutesRemaining_ReturnsNullWhenNoPositiveDischargeSamplesExist()
    {
        var predictor = new BatteryPredictor(TimeSpan.FromMinutes(5));

        predictor.AddSample(DateTimeOffset.UtcNow, remainingWh: 30, dischargeWatts: 0);

        Assert.Null(predictor.EstimateMinutesRemaining(remainingWh: 30));
    }
}
