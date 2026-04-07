using HHAPulse.Overlay.Aggregation;
using Xunit;

namespace HHAPulse.Overlay.Tests.Aggregation;

public sealed class FpsPercentileCalculatorTests
{
    [Fact]
    public void Calculate_ReturnsAverageAndLowPercentiles()
    {
        var result = FpsPercentileCalculator.Calculate(new double[] { 100, 90, 80, 70, 60, 50, 40, 30, 20, 10 });

        Assert.Equal(55, result.Average);
        Assert.Equal(10, result.OnePercentLow);
        Assert.Equal(10, result.ZeroPointOnePercentLow);
    }

    [Fact]
    public void Calculate_EmptyInputReturnsZeros()
    {
        var result = FpsPercentileCalculator.Calculate(Array.Empty<double>());

        Assert.Equal(0, result.Average);
        Assert.Equal(0, result.OnePercentLow);
        Assert.Equal(0, result.ZeroPointOnePercentLow);
    }
}
