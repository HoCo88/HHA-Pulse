using Microsoft.Diagnostics.Tracing;

namespace HHAPulse.CaptureService.Etw;

internal static class EtwPresentEventFilter
{
    // Verified locally via wevtutil provider manifests:
    // - Microsoft-Windows-DXGI: task Present uses event 42 (Start) / 43 (Stop)
    // - Microsoft-Windows-D3D9: task Present uses event 1 (Start) / 2 (Stop)
    // Counting every event whose name contains "Present" across DXGI/D3D9/DxgKrnl
    // overcounts frames because many present-related ETW events are not a single
    // app present boundary.
    private static readonly Guid DxgiProvider = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");
    private static readonly Guid D3D9Provider = new("783ACA0A-790E-4D7F-8451-AA850511C6B9");

    private const int DxgiPresentStartEventId = 42;
    private const int D3D9PresentStartEventId = 1;

    public static bool IsAppPresentStart(TraceEvent data)
    {
        return IsAppPresentStart(data.ProviderGuid, (int)data.ID);
    }

    public static bool IsAppPresentStart(Guid providerGuid, int eventId)
    {
        if (providerGuid == DxgiProvider)
        {
            return eventId == DxgiPresentStartEventId;
        }

        if (providerGuid == D3D9Provider)
        {
            return eventId == D3D9PresentStartEventId;
        }

        return false;
    }
}
