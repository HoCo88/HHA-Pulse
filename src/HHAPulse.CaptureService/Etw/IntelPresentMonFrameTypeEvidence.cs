using Microsoft.Diagnostics.Tracing;

namespace HHAPulse.CaptureService.Etw;

public static class IntelPresentMonFrameTypeEvidence
{
    // Evidence lock:
    // - PresentMon release notes confirm Intel-PresentMon was added in v2.1.0 and
    //   frame-generation tracking landed in v2.3.0.
    // - PresentMonTraceConsumer.cpp includes ETW/Intel_PresentMon.h and maps the
    //   upstream frame-type names Unspecified, Original, Repeated, Intel_XEFG,
    //   and AMD_AFMF into PresentMon's internal frame model.
    // - This detect-only phase intentionally uses named payload access only.
    //   We do not ship raw-byte offsets until the upstream header/manifests are
    //   locally locked and tested end-to-end.
    public static readonly Guid ProviderGuid = new("ECAA4712-4644-442F-B94C-A32F6CF8A499");

    private const string FrameTypePayloadName = "FrameType";

    public static bool TryGetGeneratedFrameEvidence(TraceEvent data, out bool generatedFrameDetected)
    {
        generatedFrameDetected = false;

        if (data.ProviderGuid != ProviderGuid)
        {
            return false;
        }

        object? payloadValue;
        try
        {
            payloadValue = data.PayloadByName(FrameTypePayloadName);
        }
        catch
        {
            return false;
        }

        return TryClassifyFrameType(payloadValue, out generatedFrameDetected);
    }

    public static bool TryClassifyFrameType(object? payloadValue, out bool generatedFrameDetected)
    {
        generatedFrameDetected = false;

        var normalized = NormalizeFrameType(payloadValue);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        switch (normalized)
        {
            case "Unspecified":
            case "Original":
            case "Repeated":
                generatedFrameDetected = false;
                return true;
            case "Intel_XEFG":
            case "AMD_AFMF":
                generatedFrameDetected = true;
                return true;
            default:
                return false;
        }
    }

    private static string NormalizeFrameType(object? payloadValue)
    {
        var text = payloadValue?.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var trimmed = text.Trim();
        var finalDot = trimmed.LastIndexOf('.');
        return finalDot >= 0 ? trimmed[(finalDot + 1)..] : trimmed;
    }
}
