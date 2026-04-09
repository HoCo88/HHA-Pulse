using HHAPulse.Overlay.Collectors.Battery;
using Xunit;

namespace HHAPulse.Overlay.Tests.Collectors;

public sealed class BatteryCollectorTests
{
    [Theory]
    [InlineData(50u, 100u, 50d)]
    [InlineData(120u, 100u, 100d)]
    [InlineData(0u, 100u, 0d)]
    public void ComputeChargePercent_ClampsToValidRange(uint remainingCapacity, uint maxCapacity, double expectedPercent)
    {
        Assert.Equal(expectedPercent, BatteryCollector.ComputeChargePercent(remainingCapacity, maxCapacity));
    }
}
