using Microsoft.Diagnostics.Tracing;

namespace HHAPulse.CaptureService.Etw;

/// <summary>
/// Identifies which kind of frame each Intel-PresentMon frame-type event
/// represents. Used to separately count real app-rendered frames from
/// driver-synthesized frame-generation frames (Intel XeSS Frame Generation,
/// AMD Fluid Motion Frames) so the HUD can show base vs total FPS.
/// </summary>
public enum PresentFrameKind
{
    /// <summary>Frame-type payload was unparseable or from an unknown provider.</summary>
    Unknown,

    /// <summary>
    /// Real app-rendered frame (upstream "Original"). Counts toward the
    /// true application render rate (base FPS).
    /// </summary>
    Original,

    /// <summary>
    /// Driver-synthesized frame from Intel XeSS-FG or AMD AFMF (upstream
    /// "Intel_XEFG" / "AMD_AFMF"). Counts toward the effective presented
    /// rate but NOT toward the base app render rate.
    /// </summary>
    Generated,

    /// <summary>
    /// Vsync-repeated frame (upstream "Repeated"). The same image is shown
    /// twice because the app did not produce a new frame in time. Counts
    /// toward display output but not toward base render or generated.
    /// </summary>
    Repeated,

    /// <summary>
    /// Explicitly unspecified by the upstream provider. Counts as nothing.
    /// </summary>
    Unspecified,
}

public static class IntelPresentMonFrameTypeEvidence
{
    // Evidence lock:
    // - PresentMon release notes confirm Intel-PresentMon was added in v2.1.0 and
    //   frame-generation tracking landed in v2.3.0.
    // - PresentMonTraceConsumer.cpp includes ETW/Intel_PresentMon.h and maps the
    //   upstream frame-type names Unspecified, Original, Repeated, Intel_XEFG,
    //   and AMD_AFMF into PresentMon's internal frame model.
    // - This phase intentionally uses named payload access only. We do not ship
    //   raw-byte offsets until the upstream header/manifests are locally locked
    //   and tested end-to-end.
    public static readonly Guid ProviderGuid = new("ECAA4712-4644-442F-B94C-A32F6CF8A499");

    private const string FrameTypePayloadName = "FrameType";

    /// <summary>
    /// Legacy boolean accessor — kept for the detect-only code path that only
    /// needs "is frame generation active at all". New callers that need to
    /// count frame rates should use <see cref="TryGetFrameKind"/> instead.
    /// </summary>
    public static bool TryGetGeneratedFrameEvidence(TraceEvent data, out bool generatedFrameDetected)
    {
        generatedFrameDetected = false;

        if (!TryGetFrameKind(data, out var kind))
        {
            return false;
        }

        generatedFrameDetected = kind == PresentFrameKind.Generated;
        return true;
    }

    /// <summary>
    /// Reads the Intel-PresentMon frame-type payload from an ETW event and
    /// classifies the frame. Returns false when the event is from a different
    /// provider or the payload cannot be read.
    /// </summary>
    public static bool TryGetFrameKind(TraceEvent data, out PresentFrameKind kind)
    {
        kind = PresentFrameKind.Unknown;

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

        return TryClassifyFrameKind(payloadValue, out kind);
    }

    /// <summary>
    /// Legacy test shim — kept so existing unit tests don't break. New tests
    /// should exercise <see cref="TryClassifyFrameKind"/> directly.
    /// </summary>
    public static bool TryClassifyFrameType(object? payloadValue, out bool generatedFrameDetected)
    {
        generatedFrameDetected = false;

        if (!TryClassifyFrameKind(payloadValue, out var kind))
        {
            return false;
        }

        generatedFrameDetected = kind == PresentFrameKind.Generated;
        return true;
    }

    public static bool TryClassifyFrameKind(object? payloadValue, out PresentFrameKind kind)
    {
        kind = PresentFrameKind.Unknown;

        var normalized = NormalizeFrameType(payloadValue);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        switch (normalized)
        {
            case "Unspecified":
                kind = PresentFrameKind.Unspecified;
                return true;
            case "Original":
                kind = PresentFrameKind.Original;
                return true;
            case "Repeated":
                kind = PresentFrameKind.Repeated;
                return true;
            case "Intel_XEFG":
            case "AMD_AFMF":
                kind = PresentFrameKind.Generated;
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
