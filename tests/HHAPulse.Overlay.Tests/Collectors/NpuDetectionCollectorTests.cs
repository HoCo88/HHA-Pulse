using HHAPulse.Overlay.Collectors.Npu;
using HHAPulse.Shared.Models;
using Xunit;

namespace HHAPulse.Overlay.Tests.Collectors;

public sealed class NpuDetectionCollectorTests
{
    [Theory]
    [InlineData("Intel AI Boost", NpuVendor.Intel, "Intel AI Boost")]
    [InlineData("AMD Ryzen AI Engine", NpuVendor.Amd, "AMD Ryzen AI")]
    [InlineData("Qualcomm Hexagon NPU", NpuVendor.Qualcomm, "Qualcomm Hexagon NPU")]
    [InlineData("Contoso NPU", NpuVendor.Other, "Contoso NPU")]
    public void ClassifyDescription_DetectsExpectedVendor(string description, NpuVendor vendor, string adapterName)
    {
        var result = NpuDetectionCollector.ClassifyDescription(description);

        Assert.True(result.Present);
        Assert.Equal(vendor, result.Vendor);
        Assert.Equal(adapterName, result.AdapterName);
    }

    [Fact]
    public void ClassifyDescription_EmptyMeansNotDetected()
    {
        var result = NpuDetectionCollector.ClassifyDescription(string.Empty);

        Assert.False(result.Present);
        Assert.Equal(NpuVendor.Unknown, result.Vendor);
    }
}
