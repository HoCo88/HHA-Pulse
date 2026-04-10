using HHAPulse.CaptureService.Etw;
using Xunit;

namespace HHAPulse.CaptureService.Tests.Etw;

public sealed class EtwPresentEventFilterTests
{
    [Fact]
    public void RecognizesDxgiPresentStartOnly()
    {
        var dxgi = new Guid("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");

        Assert.True(EtwPresentEventFilter.IsAppPresentStart(dxgi, 42));
        Assert.False(EtwPresentEventFilter.IsAppPresentStart(dxgi, 43));
        Assert.False(EtwPresentEventFilter.IsAppPresentStart(dxgi, 80));
    }

    [Fact]
    public void RecognizesD3D9PresentStartOnly()
    {
        var d3d9 = new Guid("783ACA0A-790E-4D7F-8451-AA850511C6B9");

        Assert.True(EtwPresentEventFilter.IsAppPresentStart(d3d9, 1));
        Assert.False(EtwPresentEventFilter.IsAppPresentStart(d3d9, 2));
    }

    [Fact]
    public void RejectsDxgKrnlPresentFamilyEvents()
    {
        var dxgKrnl = new Guid("802EC45A-1E99-4B83-9920-87C98277BA9D");

        Assert.False(EtwPresentEventFilter.IsAppPresentStart(dxgKrnl, 107));
        Assert.False(EtwPresentEventFilter.IsAppPresentStart(dxgKrnl, 7200));
    }
}
