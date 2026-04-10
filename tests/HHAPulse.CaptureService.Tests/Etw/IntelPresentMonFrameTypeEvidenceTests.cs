using HHAPulse.CaptureService.Etw;
using Xunit;

namespace HHAPulse.CaptureService.Tests.Etw;

public sealed class IntelPresentMonFrameTypeEvidenceTests
{
    [Theory]
    [InlineData("Unspecified", false)]
    [InlineData("Original", false)]
    [InlineData("Repeated", false)]
    [InlineData("Intel_XEFG", true)]
    [InlineData("AMD_AFMF", true)]
    [InlineData("Intel_PresentMon.FrameType.Intel_XEFG", true)]
    public void TryClassifyFrameType_ClassifiesKnownFrameTypes(string payloadValue, bool expectedGenerated)
    {
        var parsed = IntelPresentMonFrameTypeEvidence.TryClassifyFrameType(payloadValue, out var generated);

        Assert.True(parsed);
        Assert.Equal(expectedGenerated, generated);
    }

    [Fact]
    public void TryClassifyFrameType_RejectsUnknownFrameTypes()
    {
        var parsed = IntelPresentMonFrameTypeEvidence.TryClassifyFrameType("FutureVendorFG", out var generated);

        Assert.False(parsed);
        Assert.False(generated);
    }
}
