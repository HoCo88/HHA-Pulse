using HHAPulse.Overlay.Aggregation;
using Xunit;

namespace HHAPulse.Overlay.Tests.Aggregation;

public sealed class RingBufferTests
{
    [Fact]
    public void Add_DropsOldestItemsWhenCapacityIsExceeded()
    {
        var buffer = new RingBuffer<int>(3);

        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4);

        Assert.Equal(new[] { 2, 3, 4 }, buffer.ToArray());
    }
}
