#define HHAPULSE_NATIVE_EXPORTS
#include "NativeTelemetry.h"

#include "VendorContracts/AdlxTelemetryContract.h"
#include "VendorContracts/IgclTelemetryContract.h"

#include <Windows.h>
#include <dxgi1_6.h>

#include <cmath>
#include <cstring>
#include <new>

#pragma comment(lib, "dxgi.lib")

namespace
{
    struct PrimaryDxgiAdapterIdentity
    {
        bool valid;
        uint32_t vendorId;
        uint32_t deviceId;
        LUID luid;
    };

    bool TryGetPrimaryDxgiAdapterIdentity(PrimaryDxgiAdapterIdentity* identity)
    {
        if (!identity)
        {
            return false;
        }

        identity->valid = false;
        identity->vendorId = 0;
        identity->deviceId = 0;
        std::memset(&identity->luid, 0, sizeof(identity->luid));

        IDXGIFactory1* factory = nullptr;
        if (FAILED(CreateDXGIFactory1(__uuidof(IDXGIFactory1), reinterpret_cast<void**>(&factory))) || !factory)
        {
            return false;
        }

        IDXGIAdapter1* firstHardwareAdapter = nullptr;
        DXGI_ADAPTER_DESC1 firstHardwareDesc = {};

        for (UINT index = 0;; ++index)
        {
            IDXGIAdapter1* adapter = nullptr;
            if (factory->EnumAdapters1(index, &adapter) == DXGI_ERROR_NOT_FOUND)
            {
                break;
            }

            if (!adapter)
            {
                continue;
            }

            DXGI_ADAPTER_DESC1 desc = {};
            if (FAILED(adapter->GetDesc1(&desc)) || (desc.Flags & DXGI_ADAPTER_FLAG_SOFTWARE) != 0)
            {
                adapter->Release();
                continue;
            }

            if (!firstHardwareAdapter)
            {
                firstHardwareAdapter = adapter;
                firstHardwareDesc = desc;
            }

            IDXGIOutput* output = nullptr;
            HRESULT outputResult = adapter->EnumOutputs(0, &output);
            if (output)
            {
                output->Release();
            }

            if (SUCCEEDED(outputResult))
            {
                identity->valid = true;
                identity->vendorId = desc.VendorId;
                identity->deviceId = desc.DeviceId;
                identity->luid = desc.AdapterLuid;
                if (adapter != firstHardwareAdapter)
                {
                    adapter->Release();
                }
                if (firstHardwareAdapter)
                {
                    firstHardwareAdapter->Release();
                }
                factory->Release();
                return true;
            }

            if (adapter != firstHardwareAdapter)
            {
                adapter->Release();
            }
        }

        if (firstHardwareAdapter)
        {
            identity->valid = true;
            identity->vendorId = firstHardwareDesc.VendorId;
            identity->deviceId = firstHardwareDesc.DeviceId;
            identity->luid = firstHardwareDesc.AdapterLuid;
            firstHardwareAdapter->Release();
        }

        factory->Release();
        return identity->valid;
    }

    template <typename T>
    void AdlxRelease(T*& object)
    {
        if (object)
        {
            object->pVtbl->Release(object);
            object = nullptr;
        }
    }

    bool AdlxIsSupported(
        IADLXGPUMetricsSupport* support,
        ADLX_RESULT(ADLX_STD_CALL* method)(IADLXGPUMetricsSupport*, adlx_bool*))
    {
        if (!support || !method)
        {
            return false;
        }

        adlx_bool supported = false;
        return ADLX_SUCCEEDED(method(support, &supported)) && supported;
    }

    bool TryExtractIgclDouble(const ctl_oc_telemetry_item_t& item, double* value)
    {
        if (!value || !item.bSupported)
        {
            return false;
        }

        switch (item.type)
        {
        case CTL_DATA_TYPE_INT8:
            *value = item.value.data8;
            return true;
        case CTL_DATA_TYPE_UINT8:
            *value = item.value.datau8;
            return true;
        case CTL_DATA_TYPE_INT16:
            *value = item.value.data16;
            return true;
        case CTL_DATA_TYPE_UINT16:
            *value = item.value.datau16;
            return true;
        case CTL_DATA_TYPE_INT32:
            *value = item.value.data32;
            return true;
        case CTL_DATA_TYPE_UINT32:
            *value = item.value.datau32;
            return true;
        case CTL_DATA_TYPE_INT64:
            *value = static_cast<double>(item.value.data64);
            return true;
        case CTL_DATA_TYPE_UINT64:
            *value = static_cast<double>(item.value.datau64);
            return true;
        case CTL_DATA_TYPE_FLOAT:
            *value = item.value.datafloat;
            return true;
        case CTL_DATA_TYPE_DOUBLE:
            *value = item.value.datadouble;
            return true;
        default:
            return false;
        }
    }

    bool TryReadIgclTelemetryValue(const ctl_oc_telemetry_item_t& item, ctl_units_t expectedUnits, double* value)
    {
        return item.units == expectedUnits && TryExtractIgclDouble(item, value);
    }

    // ADLX global state.
    HMODULE s_adlxDll = nullptr;
    IADLXSystem* s_adlxSystem = nullptr;
    IADLXGPU* s_adlxGpu = nullptr;
    IADLXPerformanceMonitoringServices* s_adlxPerfMon = nullptr;
    IADLXGPUMetricsSupport* s_adlxMetricsSupport = nullptr;
    ADLXInitialize_Fn s_adlxInitialize = nullptr;
    ADLXTerminate_Fn s_adlxTerminate = nullptr;
    bool s_adlxTrackingStarted = false;

    // IGCL global state.
    HMODULE s_igclDll = nullptr;
    ctl_api_handle_t s_igclApi = nullptr;
    ctl_device_adapter_handle_t s_igclDevice = nullptr;
    ctlInitFn s_igclInit = nullptr;
    ctlCloseFn s_igclClose = nullptr;
    ctlEnumerateDevicesFn s_igclEnumerateDevices = nullptr;
    ctlGetDevicePropertiesFn s_igclGetDeviceProperties = nullptr;
    ctlPowerTelemetryGetFn s_igclPowerTelemetryGet = nullptr;
    bool s_igclHasBaseline = false;
    double s_igclPreviousEnergyJoules = 0.0;
    double s_igclPreviousTimeSeconds = 0.0;

    ADLX_RESULT SelectAdlxGpu(IADLXGPUList* gpuList, IADLXGPU** selectedGpu)
    {
        if (!gpuList || !selectedGpu)
        {
            return ADLX_INVALID_ARGS;
        }

        *selectedGpu = nullptr;
        adlx_uint gpuCount = gpuList->pVtbl->Size(gpuList);
        if (gpuCount == 0)
        {
            return ADLX_NOT_FOUND;
        }

        if (gpuCount == 1)
        {
            return gpuList->pVtbl->At_GPUList(gpuList, 0, selectedGpu);
        }

        IADLXGPU* candidateWithDesktop = nullptr;
        for (adlx_uint index = 0; index < gpuCount; ++index)
        {
            IADLXGPU* candidate = nullptr;
            if (!ADLX_SUCCEEDED(gpuList->pVtbl->At_GPUList(gpuList, index, &candidate)) || !candidate)
            {
                continue;
            }

            adlx_bool hasDesktops = false;
            bool isDisplayAttached = ADLX_SUCCEEDED(candidate->pVtbl->HasDesktops(candidate, &hasDesktops)) && hasDesktops;
            if (!isDisplayAttached)
            {
                candidate->pVtbl->Release(candidate);
                continue;
            }

            if (candidateWithDesktop)
            {
                candidate->pVtbl->Release(candidate);
                candidateWithDesktop->pVtbl->Release(candidateWithDesktop);
                return ADLX_NOT_SUPPORTED;
            }

            candidateWithDesktop = candidate;
        }

        if (!candidateWithDesktop)
        {
            return ADLX_NOT_FOUND;
        }

        *selectedGpu = candidateWithDesktop;
        return ADLX_OK;
    }

    ctl_result_t GetIgclDeviceProperties(ctl_device_adapter_handle_t device, ctl_device_adapter_properties_t* properties, LUID* luidBuffer)
    {
        if (!s_igclGetDeviceProperties || !properties)
        {
            return CTL_RESULT_ERROR_UNINITIALIZED;
        }

        std::memset(properties, 0, sizeof(*properties));
        properties->Size = sizeof(*properties);
        properties->Version = 1;
        properties->pDeviceID = luidBuffer;
        properties->device_id_size = luidBuffer ? static_cast<uint32_t>(sizeof(LUID)) : 0;

        ctl_result_t result = s_igclGetDeviceProperties(device, properties);
        if (result == CTL_RESULT_ERROR_UNSUPPORTED_VERSION)
        {
            std::memset(properties, 0, sizeof(*properties));
            properties->Size = sizeof(*properties);
            properties->Version = 0;
            properties->pDeviceID = luidBuffer;
            properties->device_id_size = luidBuffer ? static_cast<uint32_t>(sizeof(LUID)) : 0;
            result = s_igclGetDeviceProperties(device, properties);
        }

        return result;
    }

    bool SelectIgclDevice(
        ctl_device_adapter_handle_t* devices,
        uint32_t deviceCount,
        ctl_device_adapter_handle_t* selectedDevice)
    {
        if (!devices || !selectedDevice || deviceCount == 0)
        {
            return false;
        }

        if (deviceCount == 1)
        {
            *selectedDevice = devices[0];
            return true;
        }

        PrimaryDxgiAdapterIdentity primaryDxgi = {};
        if (!TryGetPrimaryDxgiAdapterIdentity(&primaryDxgi) || !primaryDxgi.valid)
        {
            return false;
        }

        int exactLuidMatch = -1;
        int vendorDeviceMatch = -1;
        for (uint32_t index = 0; index < deviceCount; ++index)
        {
            ctl_device_adapter_properties_t properties = {};
            LUID luidBuffer = {};
            if (GetIgclDeviceProperties(devices[index], &properties, &luidBuffer) != CTL_RESULT_SUCCESS)
            {
                continue;
            }

            bool luidMatches = properties.device_id_size == sizeof(LUID) &&
                std::memcmp(&luidBuffer, &primaryDxgi.luid, sizeof(LUID)) == 0;
            if (luidMatches)
            {
                if (exactLuidMatch >= 0)
                {
                    return false;
                }

                exactLuidMatch = static_cast<int>(index);
                continue;
            }

            bool vendorDeviceMatches = properties.pci_vendor_id == primaryDxgi.vendorId &&
                properties.pci_device_id == primaryDxgi.deviceId;
            if (vendorDeviceMatches)
            {
                if (vendorDeviceMatch >= 0)
                {
                    vendorDeviceMatch = -2;
                }
                else
                {
                    vendorDeviceMatch = static_cast<int>(index);
                }
            }
        }

        if (exactLuidMatch >= 0)
        {
            *selectedDevice = devices[exactLuidMatch];
            return true;
        }

        if (vendorDeviceMatch >= 0)
        {
            *selectedDevice = devices[vendorDeviceMatch];
            return true;
        }

        return false;
    }
}

extern "C" int HhaPulseAdlxProbe()
{
    return 0;
}

extern "C" int HhaPulseIgclProbe()
{
    return 0;
}

extern "C" int HhaPulseAdlxInit()
{
    if (s_adlxSystem && s_adlxGpu && s_adlxPerfMon && s_adlxMetricsSupport)
    {
        return 0;
    }

    HhaPulseAdlxShutdown();

    s_adlxDll = LoadLibraryA("amdadlx64.dll");
    if (!s_adlxDll)
    {
        return -1;
    }

    s_adlxInitialize = reinterpret_cast<ADLXInitialize_Fn>(GetProcAddress(s_adlxDll, "ADLXInitialize"));
    s_adlxTerminate = reinterpret_cast<ADLXTerminate_Fn>(GetProcAddress(s_adlxDll, "ADLXTerminate"));
    if (!s_adlxInitialize || !s_adlxTerminate)
    {
        HhaPulseAdlxShutdown();
        return -2;
    }

    ADLX_RESULT result = s_adlxInitialize(ADLX_FULL_VERSION, &s_adlxSystem);
    if (!ADLX_SUCCEEDED(result) || !s_adlxSystem)
    {
        HhaPulseAdlxShutdown();
        return -3;
    }

    IADLXGPUList* gpuList = nullptr;
    result = s_adlxSystem->pVtbl->GetGPUs(s_adlxSystem, &gpuList);
    if (!ADLX_SUCCEEDED(result) || !gpuList)
    {
        HhaPulseAdlxShutdown();
        return -4;
    }

    result = SelectAdlxGpu(gpuList, &s_adlxGpu);
    gpuList->pVtbl->Release(gpuList);
    if (!ADLX_SUCCEEDED(result) || !s_adlxGpu)
    {
        HhaPulseAdlxShutdown();
        return -5;
    }

    result = s_adlxSystem->pVtbl->GetPerformanceMonitoringServices(s_adlxSystem, &s_adlxPerfMon);
    if (!ADLX_SUCCEEDED(result) || !s_adlxPerfMon)
    {
        HhaPulseAdlxShutdown();
        return -6;
    }

    result = s_adlxPerfMon->pVtbl->GetSupportedGPUMetrics(s_adlxPerfMon, s_adlxGpu, &s_adlxMetricsSupport);
    if (!ADLX_SUCCEEDED(result) || !s_adlxMetricsSupport)
    {
        HhaPulseAdlxShutdown();
        return -7;
    }

    s_adlxPerfMon->pVtbl->SetSamplingInterval(s_adlxPerfMon, 1000);
    result = s_adlxPerfMon->pVtbl->StartPerformanceMetricsTracking(s_adlxPerfMon);
    if (!ADLX_SUCCEEDED(result))
    {
        HhaPulseAdlxShutdown();
        return -8;
    }

    s_adlxTrackingStarted = true;
    return 0;
}

extern "C" int HhaPulseAdlxReadGpu(HhaPulseGpuReading* out)
{
    if (!out)
    {
        return -1;
    }

    std::memset(out, 0, sizeof(*out));
    if (!s_adlxGpu || !s_adlxPerfMon || !s_adlxMetricsSupport)
    {
        return -2;
    }

    IADLXGPUMetrics* metrics = nullptr;
    ADLX_RESULT result = s_adlxPerfMon->pVtbl->GetCurrentGPUMetrics(s_adlxPerfMon, s_adlxGpu, &metrics);
    if (!ADLX_SUCCEEDED(result) || !metrics)
    {
        return -3;
    }

    if (AdlxIsSupported(s_adlxMetricsSupport, s_adlxMetricsSupport->pVtbl->IsSupportedGPUTemperature))
    {
        adlx_double temperature = 0.0;
        if (ADLX_SUCCEEDED(metrics->pVtbl->GPUTemperature(metrics, &temperature)) && temperature > -50.0 && temperature < 200.0)
        {
            out->temperatureCelsius = temperature;
            out->validFlags |= HHAPULSE_GPU_VALID_TEMP;
        }
    }

    if (AdlxIsSupported(s_adlxMetricsSupport, s_adlxMetricsSupport->pVtbl->IsSupportedGPUPower))
    {
        adlx_double watts = 0.0;
        if (ADLX_SUCCEEDED(metrics->pVtbl->GPUPower(metrics, &watts)) && watts >= 0.0 && watts < 1000.0)
        {
            out->powerWatts = watts;
            out->validFlags |= HHAPULSE_GPU_VALID_POWER;
        }
    }

    if (AdlxIsSupported(s_adlxMetricsSupport, s_adlxMetricsSupport->pVtbl->IsSupportedGPUFanSpeed))
    {
        adlx_int fanRpm = 0;
        if (ADLX_SUCCEEDED(metrics->pVtbl->GPUFanSpeed(metrics, &fanRpm)) && fanRpm > 0 && fanRpm < 20000)
        {
            out->fanRpm = fanRpm;
            out->validFlags |= HHAPULSE_GPU_VALID_FAN;
        }
    }

    if (AdlxIsSupported(s_adlxMetricsSupport, s_adlxMetricsSupport->pVtbl->IsSupportedGPUClockSpeed))
    {
        adlx_int clockMHz = 0;
        if (ADLX_SUCCEEDED(metrics->pVtbl->GPUClockSpeed(metrics, &clockMHz)) && clockMHz > 0 && clockMHz < 10000)
        {
            out->clockMegahertz = static_cast<double>(clockMHz);
            out->validFlags |= HHAPULSE_GPU_VALID_CLOCK;
        }
    }

    metrics->pVtbl->Release(metrics);
    return 0;
}

extern "C" void HhaPulseAdlxShutdown()
{
    if (s_adlxPerfMon && s_adlxTrackingStarted)
    {
        s_adlxPerfMon->pVtbl->StopPerformanceMetricsTracking(s_adlxPerfMon);
        s_adlxTrackingStarted = false;
    }

    AdlxRelease(s_adlxMetricsSupport);
    AdlxRelease(s_adlxPerfMon);
    AdlxRelease(s_adlxGpu);

    if (s_adlxTerminate)
    {
        s_adlxTerminate();
    }

    s_adlxSystem = nullptr;
    s_adlxInitialize = nullptr;
    s_adlxTerminate = nullptr;

    if (s_adlxDll)
    {
        FreeLibrary(s_adlxDll);
        s_adlxDll = nullptr;
    }
}

extern "C" int HhaPulseIgclInit()
{
    if (s_igclApi && s_igclDevice && s_igclPowerTelemetryGet)
    {
        return 0;
    }

    HhaPulseIgclShutdown();

    s_igclDll = LoadLibraryA("ControlLib.dll");
    if (!s_igclDll)
    {
        s_igclDll = LoadLibraryA("igcl64.dll");
    }

    if (!s_igclDll)
    {
        return -1;
    }

    s_igclInit = reinterpret_cast<ctlInitFn>(GetProcAddress(s_igclDll, "ctlInit"));
    s_igclClose = reinterpret_cast<ctlCloseFn>(GetProcAddress(s_igclDll, "ctlClose"));
    s_igclEnumerateDevices = reinterpret_cast<ctlEnumerateDevicesFn>(GetProcAddress(s_igclDll, "ctlEnumerateDevices"));
    s_igclGetDeviceProperties = reinterpret_cast<ctlGetDevicePropertiesFn>(GetProcAddress(s_igclDll, "ctlGetDeviceProperties"));
    s_igclPowerTelemetryGet = reinterpret_cast<ctlPowerTelemetryGetFn>(GetProcAddress(s_igclDll, "ctlPowerTelemetryGet"));
    if (!s_igclInit || !s_igclClose || !s_igclEnumerateDevices || !s_igclGetDeviceProperties || !s_igclPowerTelemetryGet)
    {
        HhaPulseIgclShutdown();
        return -2;
    }

    ctl_init_args_t initArgs = {};
    initArgs.Size = sizeof(initArgs);
    initArgs.Version = 1;
    initArgs.AppVersion = CTL_IMPL_VERSION;
    initArgs.flags = CTL_INIT_FLAG_USE_LEVEL_ZERO;
    initArgs.SupportedVersion = 0;
    std::memset(&initArgs.ApplicationUID, 0, sizeof(initArgs.ApplicationUID));

    ctl_result_t result = s_igclInit(&initArgs, &s_igclApi);
    if (result == CTL_RESULT_ERROR_UNSUPPORTED_VERSION)
    {
        initArgs.Version = 0;
        result = s_igclInit(&initArgs, &s_igclApi);
    }

    if (result != CTL_RESULT_SUCCESS || !s_igclApi)
    {
        HhaPulseIgclShutdown();
        return -3;
    }

    uint32_t deviceCount = 0;
    result = s_igclEnumerateDevices(s_igclApi, &deviceCount, nullptr);
    if (result != CTL_RESULT_SUCCESS || deviceCount == 0)
    {
        HhaPulseIgclShutdown();
        return -4;
    }

    ctl_device_adapter_handle_t* devices = new (std::nothrow) ctl_device_adapter_handle_t[deviceCount];
    if (!devices)
    {
        HhaPulseIgclShutdown();
        return -5;
    }

    result = s_igclEnumerateDevices(s_igclApi, &deviceCount, devices);
    if (result != CTL_RESULT_SUCCESS)
    {
        delete[] devices;
        HhaPulseIgclShutdown();
        return -6;
    }

    bool selected = SelectIgclDevice(devices, deviceCount, &s_igclDevice);
    delete[] devices;
    if (!selected || !s_igclDevice)
    {
        HhaPulseIgclShutdown();
        return -7;
    }

    s_igclHasBaseline = false;
    s_igclPreviousEnergyJoules = 0.0;
    s_igclPreviousTimeSeconds = 0.0;
    return 0;
}

extern "C" int HhaPulseIgclReadGpu(HhaPulseGpuReading* out)
{
    if (!out)
    {
        return -1;
    }

    std::memset(out, 0, sizeof(*out));
    if (!s_igclDevice || !s_igclPowerTelemetryGet)
    {
        return -2;
    }

    ctl_power_telemetry_t telemetry = {};
    telemetry.Size = sizeof(telemetry);
    telemetry.Version = 1;

    ctl_result_t result = s_igclPowerTelemetryGet(s_igclDevice, &telemetry);
    if (result == CTL_RESULT_ERROR_UNSUPPORTED_VERSION)
    {
        std::memset(&telemetry, 0, sizeof(telemetry));
        telemetry.Size = sizeof(telemetry);
        telemetry.Version = 0;
        result = s_igclPowerTelemetryGet(s_igclDevice, &telemetry);
    }

    if (result != CTL_RESULT_SUCCESS)
    {
        return -3;
    }

    double value = 0.0;
    if (TryReadIgclTelemetryValue(telemetry.gpuCurrentTemperature, CTL_UNITS_TEMPERATURE_CELSIUS, &value) && value > -50.0 && value < 200.0)
    {
        out->temperatureCelsius = value;
        out->validFlags |= HHAPULSE_GPU_VALID_TEMP;
    }

    if (TryReadIgclTelemetryValue(telemetry.gpuCurrentClockFrequency, CTL_UNITS_FREQUENCY_MHZ, &value) && value > 0.0 && value < 10000.0)
    {
        out->clockMegahertz = value;
        out->validFlags |= HHAPULSE_GPU_VALID_CLOCK;
    }

    for (int fanIndex = 0; fanIndex < CTL_FAN_COUNT; ++fanIndex)
    {
        if (TryReadIgclTelemetryValue(telemetry.fanSpeed[fanIndex], CTL_UNITS_ANGULAR_SPEED_RPM, &value) && value > 0.0 && value < 20000.0)
        {
            out->fanRpm = static_cast<int>(value);
            out->validFlags |= HHAPULSE_GPU_VALID_FAN;
            break;
        }
    }

    double energyJoules = 0.0;
    double timeSeconds = 0.0;
    if (TryReadIgclTelemetryValue(telemetry.gpuEnergyCounter, CTL_UNITS_ENERGY_JOULES, &energyJoules) &&
        TryReadIgclTelemetryValue(telemetry.timeStamp, CTL_UNITS_TIME_SECONDS, &timeSeconds))
    {
        if (s_igclHasBaseline)
        {
            double deltaTime = timeSeconds - s_igclPreviousTimeSeconds;
            double deltaEnergy = energyJoules - s_igclPreviousEnergyJoules;
            if (deltaTime > 0.0 && deltaEnergy >= 0.0)
            {
                double watts = deltaEnergy / deltaTime;
                if (watts >= 0.0 && watts < 1000.0)
                {
                    out->powerWatts = watts;
                    out->validFlags |= HHAPULSE_GPU_VALID_POWER;
                }
            }
        }

        s_igclPreviousEnergyJoules = energyJoules;
        s_igclPreviousTimeSeconds = timeSeconds;
        s_igclHasBaseline = true;
    }

    return 0;
}

extern "C" void HhaPulseIgclShutdown()
{
    s_igclDevice = nullptr;
    s_igclHasBaseline = false;
    s_igclPreviousEnergyJoules = 0.0;
    s_igclPreviousTimeSeconds = 0.0;
    s_igclPowerTelemetryGet = nullptr;
    s_igclGetDeviceProperties = nullptr;
    s_igclEnumerateDevices = nullptr;
    s_igclInit = nullptr;

    if (s_igclClose && s_igclApi)
    {
        s_igclClose(s_igclApi);
    }

    s_igclApi = nullptr;
    s_igclClose = nullptr;

    if (s_igclDll)
    {
        FreeLibrary(s_igclDll);
        s_igclDll = nullptr;
    }
}
