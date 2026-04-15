using System.Runtime.InteropServices;
using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Collectors.Npu;

public sealed class NpuDetectionCollector : IMetricCollector
{
    private static readonly Guid IidDxCoreAdapterFactory = new("78EE5945-C36E-4B13-A669-005DD11C0F06");
    private static readonly Guid IidDxCoreAdapterList = new("526C7776-40E9-459B-B711-F32AD76DFC28");
    private static readonly Guid IidDxCoreAdapter = new("F0DB4C7F-FE5A-42A2-BD62-F2A6CF6FC83E");
    private static readonly Guid DxcoreHardwareTypeAttributeNpu = new("D46140C4-ADD7-451B-9E56-06FE8C3B58ED");

    private readonly Lazy<NpuDetectionResult> detection;

    public NpuDetectionCollector()
    {
        detection = new Lazy<NpuDetectionResult>(DetectOnce);
    }

    public string Name => "NPU Detection";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        _ = detection.Value;
        return Task.CompletedTask;
    }

    public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.CompletedTask;
        }

        var result = detection.Value;
        snapshot.Npu.Present = result.Present;
        snapshot.Npu.AdapterName = result.AdapterName;
        snapshot.Npu.DriverDescription = result.DriverDescription;
        snapshot.Npu.Vendor = result.Vendor;

        if (result.Present)
        {
            snapshot.AvailableMetrics |= MetricFlags.NpuPresent;
        }

        MeasurementTraceRecorder.Record(
            snapshot,
            "npu_present",
            nameof(NpuDetectionCollector),
            "DXCore hardware-type adapter enumeration",
            result.Present,
            result.Present ? result.AdapterName : "Not detected",
            "DXCoreCreateAdapterFactory -> CreateAdapterList(NPU attribute) -> GetProperty(DriverDescription)",
            result.StatusMessage,
            "DXCore hardware-type adapter enumeration",
            result.DriverDescription,
            "string",
            "classify vendor from driver description",
            result.AdapterName,
            "string",
            result.Present ? TelemetryValidationState.Verified : TelemetryValidationState.Unavailable,
            result.StatusMessage);

        return Task.CompletedTask;
    }

    internal static NpuDetectionResult ClassifyDescription(string description)
    {
        var text = description?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return new NpuDetectionResult(false, string.Empty, string.Empty, NpuVendor.Unknown, "NPU not detected.");
        }

        var upper = text.ToUpperInvariant();
        if (upper.Contains("INTEL") && (upper.Contains("AI BOOST") || upper.Contains("NPU")))
        {
            return new NpuDetectionResult(true, "Intel AI Boost", text, NpuVendor.Intel, $"NPU detected via DXCore: {text}");
        }

        if (upper.Contains("AMD") && (upper.Contains("RYZEN AI") || upper.Contains("XDNA")))
        {
            return new NpuDetectionResult(true, "AMD Ryzen AI", text, NpuVendor.Amd, $"NPU detected via DXCore: {text}");
        }

        if (upper.Contains("QUALCOMM") || upper.Contains("SNAPDRAGON") || upper.Contains("HEXAGON"))
        {
            return new NpuDetectionResult(true, "Qualcomm Hexagon NPU", text, NpuVendor.Qualcomm, $"NPU detected via DXCore: {text}");
        }

        return new NpuDetectionResult(true, text, text, NpuVendor.Other, $"NPU detected via DXCore: {text}");
    }

    private static NpuDetectionResult DetectOnce()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new NpuDetectionResult(false, string.Empty, string.Empty, NpuVendor.Unknown, "NPU detection requires Windows.");
        }

        IDXCoreAdapterFactory? factory = null;
        IDXCoreAdapterList? list = null;
        IDXCoreAdapter? adapter = null;

        try
        {
            var factoryIid = IidDxCoreAdapterFactory;
            var hr = DXCoreCreateAdapterFactory(ref factoryIid, out factory);
            if (hr < 0 || factory is null)
            {
                return new NpuDetectionResult(false, string.Empty, string.Empty, NpuVendor.Unknown, $"DXCoreCreateAdapterFactory failed (HRESULT=0x{hr:X8}).");
            }

            var attributes = new[] { DxcoreHardwareTypeAttributeNpu };
            var listIid = IidDxCoreAdapterList;
            hr = factory.CreateAdapterList((uint)attributes.Length, attributes, ref listIid, out list);
            if (hr < 0 || list is null || list.GetAdapterCount() == 0)
            {
                return new NpuDetectionResult(false, string.Empty, string.Empty, NpuVendor.Unknown, "No NPU adapters reported by DXCore.");
            }

            var adapterIid = IidDxCoreAdapter;
            hr = list.GetAdapter(0, ref adapterIid, out adapter);
            if (hr < 0 || adapter is null)
            {
                return new NpuDetectionResult(false, string.Empty, string.Empty, NpuVendor.Unknown, $"DXCore adapter retrieval failed (HRESULT=0x{hr:X8}).");
            }

            var description = TryGetUtf8Property(adapter, DxCoreAdapterProperty.DriverDescription);
            return ClassifyDescription(description);
        }
        catch (Exception ex)
        {
            return new NpuDetectionResult(false, string.Empty, string.Empty, NpuVendor.Unknown, $"DXCore NPU detection failed: {ex.Message}");
        }
        finally
        {
            if (adapter is not null)
            {
                Marshal.ReleaseComObject(adapter);
            }

            if (list is not null)
            {
                Marshal.ReleaseComObject(list);
            }

            if (factory is not null)
            {
                Marshal.ReleaseComObject(factory);
            }
        }
    }

    private static string TryGetUtf8Property(IDXCoreAdapter adapter, DxCoreAdapterProperty property)
    {
        if (!adapter.IsPropertySupported(property))
        {
            return string.Empty;
        }

        var hr = adapter.GetPropertySize(property, out var size);
        if (hr < 0 || size == 0)
        {
            return string.Empty;
        }

        var buffer = new byte[(int)size];
        hr = adapter.GetProperty(property, (nuint)buffer.Length, buffer);
        if (hr < 0)
        {
            return string.Empty;
        }

        var terminator = Array.IndexOf(buffer, (byte)0);
        var count = terminator >= 0 ? terminator : buffer.Length;
        return System.Text.Encoding.UTF8.GetString(buffer, 0, count);
    }

    [DllImport("dxcore.dll", ExactSpelling = true)]
    private static extern int DXCoreCreateAdapterFactory(ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IDXCoreAdapterFactory factory);

    [ComImport]
    [Guid("78EE5945-C36E-4B13-A669-005DD11C0F06")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXCoreAdapterFactory
    {
        [PreserveSig] bool IsNotificationTypeSupported(uint notificationType);
        [PreserveSig] int CreateAdapterList(uint numAttributes, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] Guid[] filterAttributes, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IDXCoreAdapterList adapterList);
    }

    [ComImport]
    [Guid("526C7776-40E9-459B-B711-F32AD76DFC28")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXCoreAdapterList
    {
        [PreserveSig] int GetAdapter(uint index, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IDXCoreAdapter adapter);
        [PreserveSig] uint GetAdapterCount();
        [PreserveSig] bool IsStale();
    }

    [ComImport]
    [Guid("F0DB4C7F-FE5A-42A2-BD62-F2A6CF6FC83E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXCoreAdapter
    {
        [PreserveSig] bool IsValid();
        [PreserveSig] bool IsAttributeSupported(ref Guid attributeGuid);
        [PreserveSig] bool IsPropertySupported(DxCoreAdapterProperty property);
        [PreserveSig] int GetProperty(DxCoreAdapterProperty property, nuint bufferSize, [Out] byte[] propertyData);
        [PreserveSig] int GetPropertySize(DxCoreAdapterProperty property, out nuint bufferSize);
    }

    private enum DxCoreAdapterProperty : uint
    {
        DriverDescription = 2
    }

    internal readonly record struct NpuDetectionResult(bool Present, string AdapterName, string DriverDescription, NpuVendor Vendor, string StatusMessage);
}
