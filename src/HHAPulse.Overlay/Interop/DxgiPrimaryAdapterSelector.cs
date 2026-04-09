using HHAPulse.Overlay.Diagnostics;
using Vortice.DXGI;

namespace HHAPulse.Overlay.Interop;

internal static class DxgiPrimaryAdapterSelector
{
    public static bool TryGetPrimaryAdapter(out IDXGIAdapter1? adapter, out AdapterDescription1 description)
    {
        adapter = null;
        description = default;

        var result = DXGI.CreateDXGIFactory1(out IDXGIFactory1? factory);
        if (result.Failure || factory is null)
        {
            AppLogger.Info($"DXGI primary adapter: CreateDXGIFactory1 failed 0x{result.Code:X8}.");
            return false;
        }

        using (factory)
        {
            IDXGIAdapter1? firstHardwareAdapter = null;
            AdapterDescription1 firstHardwareDescription = default;

            for (uint index = 0; ; index++)
            {
                var enumResult = factory.EnumAdapters1(index, out var currentAdapter);
                if (enumResult.Failure || currentAdapter is null)
                {
                    break;
                }

                var currentDescription = currentAdapter.Description1;
                if ((currentDescription.Flags & AdapterFlags.Software) == AdapterFlags.Software)
                {
                    currentAdapter.Dispose();
                    continue;
                }

                if (firstHardwareAdapter is null)
                {
                    firstHardwareAdapter = currentAdapter;
                    firstHardwareDescription = currentDescription;
                }

                var outputResult = currentAdapter.EnumOutputs(0, out var output);
                if (outputResult.Success && output is not null)
                {
                    output.Dispose();
                    if (!ReferenceEquals(firstHardwareAdapter, currentAdapter))
                    {
                        firstHardwareAdapter?.Dispose();
                    }

                    adapter = currentAdapter;
                    description = currentDescription;
                    return true;
                }

                currentAdapter.Dispose();
            }

            if (firstHardwareAdapter is null)
            {
                AppLogger.Info("DXGI primary adapter: no hardware adapters found.");
                return false;
            }

            adapter = firstHardwareAdapter;
            description = firstHardwareDescription;
            return true;
        }
    }
}
