using HHAPulse.CaptureService.Etw;
using HHAPulse.Shared.Models;
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

    [Fact]
    public void TryClassifyFrameKindAndVendor_IntelXeFG_ReturnsGeneratedWithIntelVendor()
    {
        var parsed = IntelPresentMonFrameTypeEvidence.TryClassifyFrameKindAndVendor(
            "Intel_PresentMon_Provider.Intel_XEFG",
            out var kind,
            out var vendor);

        Assert.True(parsed);
        Assert.Equal(PresentFrameKind.Generated, kind);
        Assert.Equal(FrameGenVendor.IntelXeFG, vendor);
    }

    [Fact]
    public void TryClassifyFrameKindAndVendor_AmdAFMF_ReturnsGeneratedWithAmdVendor()
    {
        var parsed = IntelPresentMonFrameTypeEvidence.TryClassifyFrameKindAndVendor(
            "AMD_AFMF",
            out var kind,
            out var vendor);

        Assert.True(parsed);
        Assert.Equal(PresentFrameKind.Generated, kind);
        Assert.Equal(FrameGenVendor.AmdAFMF, vendor);
    }

    [Fact]
    public void TryClassifyFrameKindAndVendor_Original_ReturnsOriginalWithNoVendor()
    {
        var parsed = IntelPresentMonFrameTypeEvidence.TryClassifyFrameKindAndVendor(
            "Original",
            out var kind,
            out var vendor);

        Assert.True(parsed);
        Assert.Equal(PresentFrameKind.Original, kind);
        Assert.Equal(FrameGenVendor.None, vendor);
    }
}
