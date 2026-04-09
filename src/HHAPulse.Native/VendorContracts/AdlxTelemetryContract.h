// Minimal ADLX contract vendored from AMD's official SDK headers.
// Provenance:
// - Upstream files: ADLXDefines.h, ADLXVersion.h, ADLX.h, ICollections.h, ISystem.h, IPerformanceMonitoring.h
// - Upstream version: 1.4.0.110
// - Retrieved: 2026-04-09
// - Source root: https://raw.githubusercontent.com/GPUOpen-LibrariesAndSDKs/ADLX/main/SDK/Include/

#pragma once

#include <cstdint>
#include <cstddef>

typedef int64_t adlx_int64;
typedef int32_t adlx_int32;
typedef uint64_t adlx_uint64;
typedef uint32_t adlx_uint32;
typedef uint8_t adlx_uint8;
typedef size_t adlx_size;
typedef void* adlx_handle;
typedef double adlx_double;
typedef int32_t adlx_int;
typedef uint32_t adlx_uint;
typedef unsigned long adlx_ulong;
typedef bool adlx_bool;
typedef long adlx_long;

#define ADLX_STD_CALL __stdcall
#define ADLX_CDECL_CALL __cdecl
#define ADLX_MAKE_FULL_VER(VERSION_MAJOR, VERSION_MINOR, VERSION_RELEASE, VERSION_BUILD_NUM) (((adlx_uint64)(VERSION_MAJOR) << 48ull) | ((adlx_uint64)(VERSION_MINOR) << 32ull) | ((adlx_uint64)(VERSION_RELEASE) << 16ull) | (adlx_uint64)(VERSION_BUILD_NUM))
#define ADLX_FULL_VERSION ADLX_MAKE_FULL_VER(1, 4, 0, 110)

typedef enum
{
    ADLX_OK = 0,
    ADLX_ALREADY_ENABLED,
    ADLX_ALREADY_INITIALIZED,
    ADLX_FAIL,
    ADLX_INVALID_ARGS,
    ADLX_BAD_VER,
    ADLX_UNKNOWN_INTERFACE,
    ADLX_TERMINATED,
    ADLX_ADL_INIT_ERROR,
    ADLX_NOT_FOUND,
    ADLX_INVALID_OBJECT,
    ADLX_ORPHAN_OBJECTS,
    ADLX_NOT_SUPPORTED,
    ADLX_PENDING_OPERATION,
    ADLX_GPU_INACTIVE,
    ADLX_GPU_IN_USE,
    ADLX_TIMEOUT_OPERATION,
    ADLX_NOT_ACTIVE
} ADLX_RESULT;

#define ADLX_SUCCEEDED(x) ((x) == ADLX_OK || (x) == ADLX_ALREADY_ENABLED || (x) == ADLX_ALREADY_INITIALIZED)

typedef struct IADLXInterface IADLXInterface;
typedef struct IADLXGPU IADLXGPU;
typedef struct IADLXGPUList IADLXGPUList;
typedef struct IADLXSystem IADLXSystem;
typedef struct IADLXDisplayServices IADLXDisplayServices;
typedef struct IADLXDesktopServices IADLXDesktopServices;
typedef struct IADLXGPUsChangedHandling IADLXGPUsChangedHandling;
typedef struct IADLXLog IADLXLog;
typedef struct IADLX3DSettingsServices IADLX3DSettingsServices;
typedef struct IADLXGPUTuningServices IADLXGPUTuningServices;
typedef struct IADLXPerformanceMonitoringServices IADLXPerformanceMonitoringServices;
typedef struct IADLXI2C IADLXI2C;
typedef struct IADLXAllMetricsList IADLXAllMetricsList;
typedef struct IADLXGPUMetricsList IADLXGPUMetricsList;
typedef struct IADLXSystemMetricsList IADLXSystemMetricsList;
typedef struct IADLXFPSList IADLXFPSList;
typedef struct IADLXAllMetrics IADLXAllMetrics;
typedef struct IADLXGPUMetrics IADLXGPUMetrics;
typedef struct IADLXSystemMetrics IADLXSystemMetrics;
typedef struct IADLXFPS IADLXFPS;
typedef struct IADLXGPUMetricsSupport IADLXGPUMetricsSupport;
typedef struct IADLXSystemMetricsSupport IADLXSystemMetricsSupport;
typedef struct IADLMapping IADLMapping;
typedef struct ADLX_IntRange ADLX_IntRange;

typedef adlx_int32 ADLX_HG_TYPE;
typedef adlx_int32 ADLX_LOG_DESTINATION;
typedef adlx_int32 ADLX_LOG_SEVERITY;
typedef void (ADLX_STD_CALL* ADLX_ADL_Main_Memory_Free)(void** buffer);

typedef struct IADLXInterfaceVtbl
{
    adlx_long (ADLX_STD_CALL *Acquire)(IADLXInterface* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLXInterface* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXInterface* pThis, const wchar_t* interfaceId, void** ppInterface);
} IADLXInterfaceVtbl;

struct IADLXInterface
{
    const IADLXInterfaceVtbl *pVtbl;
};

typedef struct IADLXGPUVtbl
{
    adlx_long (ADLX_STD_CALL *Acquire)(IADLXGPU* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLXGPU* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXGPU* pThis, const wchar_t* interfaceId, void** ppInterface);
    ADLX_RESULT (ADLX_STD_CALL *VendorId)(IADLXGPU* pThis, const char** vendorId);
    ADLX_RESULT (ADLX_STD_CALL *ASICFamilyType)(IADLXGPU* pThis, int* asicFamilyType);
    ADLX_RESULT (ADLX_STD_CALL *Type)(IADLXGPU* pThis, int* gpuType);
    ADLX_RESULT (ADLX_STD_CALL *IsExternal)(IADLXGPU* pThis, adlx_bool* isExternal);
    ADLX_RESULT (ADLX_STD_CALL *Name)(IADLXGPU* pThis, const char** gpuName);
    ADLX_RESULT (ADLX_STD_CALL *DriverPath)(IADLXGPU* pThis, const char** driverPath);
    ADLX_RESULT (ADLX_STD_CALL *PNPString)(IADLXGPU* pThis, const char** pnpString);
    ADLX_RESULT (ADLX_STD_CALL *HasDesktops)(IADLXGPU* pThis, adlx_bool* hasDesktops);
    ADLX_RESULT (ADLX_STD_CALL *TotalVRAM)(IADLXGPU* pThis, adlx_uint* vramMB);
    ADLX_RESULT (ADLX_STD_CALL *VRAMType)(IADLXGPU* pThis, const char** type);
    ADLX_RESULT (ADLX_STD_CALL *BIOSInfo)(IADLXGPU* pThis, const char** partNumber, const char** version, const char** date);
    ADLX_RESULT (ADLX_STD_CALL *DeviceId)(IADLXGPU* pThis, const char** deviceId);
    ADLX_RESULT (ADLX_STD_CALL *RevisionId)(IADLXGPU* pThis, const char** revisionId);
    ADLX_RESULT (ADLX_STD_CALL *SubSystemId)(IADLXGPU* pThis, const char** subSystemId);
    ADLX_RESULT (ADLX_STD_CALL *SubSystemVendorId)(IADLXGPU* pThis, const char** subSystemVendorId);
    ADLX_RESULT (ADLX_STD_CALL *UniqueId)(IADLXGPU* pThis, adlx_int* uniqueId);
} IADLXGPUVtbl;

struct IADLXGPU
{
    const IADLXGPUVtbl *pVtbl;
};

typedef struct IADLXGPUListVtbl
{
    adlx_long (ADLX_STD_CALL *Acquire)(IADLXGPUList* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLXGPUList* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXGPUList* pThis, const wchar_t* interfaceId, void** ppInterface);
    adlx_uint (ADLX_STD_CALL *Size)(IADLXGPUList* pThis);
    adlx_uint8 (ADLX_STD_CALL *Empty)(IADLXGPUList* pThis);
    adlx_uint (ADLX_STD_CALL *Begin)(IADLXGPUList* pThis);
    adlx_uint (ADLX_STD_CALL *End)(IADLXGPUList* pThis);
    ADLX_RESULT (ADLX_STD_CALL *At)(IADLXGPUList* pThis, const adlx_uint location, IADLXInterface** ppItem);
    ADLX_RESULT (ADLX_STD_CALL *Clear)(IADLXGPUList* pThis);
    ADLX_RESULT (ADLX_STD_CALL *Remove_Back)(IADLXGPUList* pThis);
    ADLX_RESULT (ADLX_STD_CALL *Add_Back)(IADLXGPUList* pThis, IADLXInterface* pItem);
    ADLX_RESULT (ADLX_STD_CALL *At_GPUList)(IADLXGPUList* pThis, const adlx_uint location, IADLXGPU** ppItem);
    ADLX_RESULT (ADLX_STD_CALL *Add_Back_GPUList)(IADLXGPUList* pThis, IADLXGPU* pItem);
} IADLXGPUListVtbl;

struct IADLXGPUList
{
    const IADLXGPUListVtbl *pVtbl;
};

typedef struct IADLXSystemVtbl
{
    ADLX_RESULT (ADLX_STD_CALL *GetHybridGraphicsType)(IADLXSystem* pThis, ADLX_HG_TYPE* hgType);
    ADLX_RESULT (ADLX_STD_CALL *GetGPUs)(IADLXSystem* pThis, IADLXGPUList** ppGPUs);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXSystem* pThis, const wchar_t* interfaceId, void** ppInterface);
    ADLX_RESULT (ADLX_STD_CALL *GetDisplaysServices)(IADLXSystem* pThis, IADLXDisplayServices** ppDispServices);
    ADLX_RESULT (ADLX_STD_CALL *GetDesktopsServices)(IADLXSystem* pThis, IADLXDesktopServices** ppDeskServices);
    ADLX_RESULT (ADLX_STD_CALL *GetGPUsChangedHandling)(IADLXSystem* pThis, IADLXGPUsChangedHandling** ppGPUsChangedHandling);
    ADLX_RESULT (ADLX_STD_CALL *EnableLog)(IADLXSystem* pThis, ADLX_LOG_DESTINATION mode, ADLX_LOG_SEVERITY severity, IADLXLog* pLogger, const wchar_t* fileName);
    ADLX_RESULT (ADLX_STD_CALL *Get3DSettingsServices)(IADLXSystem* pThis, IADLX3DSettingsServices** pp3DSettingsServices);
    ADLX_RESULT (ADLX_STD_CALL *GetGPUTuningServices)(IADLXSystem* pThis, IADLXGPUTuningServices** ppGPUTuningServices);
    ADLX_RESULT (ADLX_STD_CALL *GetPerformanceMonitoringServices)(IADLXSystem* pThis, IADLXPerformanceMonitoringServices** ppPerformanceMonitoringServices);
    ADLX_RESULT (ADLX_STD_CALL *TotalSystemRAM)(IADLXSystem* pThis, adlx_uint* ramMB);
    ADLX_RESULT (ADLX_STD_CALL *GetI2C)(IADLXSystem* pThis, IADLXGPU* pGPU, IADLXI2C** ppI2C);
} IADLXSystemVtbl;

struct IADLXSystem
{
    const IADLXSystemVtbl *pVtbl;
};

typedef struct IADLXGPUMetricsSupportVtbl
{
    adlx_long (ADLX_STD_CALL* Acquire)(IADLXGPUMetricsSupport* pThis);
    adlx_long (ADLX_STD_CALL* Release)(IADLXGPUMetricsSupport* pThis);
    ADLX_RESULT (ADLX_STD_CALL* QueryInterface)(IADLXGPUMetricsSupport* pThis, const wchar_t* interfaceId, void** ppInterface);
    ADLX_RESULT (ADLX_STD_CALL* IsSupportedGPUUsage)(IADLXGPUMetricsSupport* pThis, adlx_bool* supported);
    ADLX_RESULT (ADLX_STD_CALL* IsSupportedGPUClockSpeed)(IADLXGPUMetricsSupport* pThis, adlx_bool* supported);
    ADLX_RESULT (ADLX_STD_CALL* IsSupportedGPUVRAMClockSpeed)(IADLXGPUMetricsSupport* pThis, adlx_bool* supported);
    ADLX_RESULT (ADLX_STD_CALL* IsSupportedGPUTemperature)(IADLXGPUMetricsSupport* pThis, adlx_bool* supported);
    ADLX_RESULT (ADLX_STD_CALL* IsSupportedGPUHotspotTemperature)(IADLXGPUMetricsSupport* pThis, adlx_bool* supported);
    ADLX_RESULT (ADLX_STD_CALL* IsSupportedGPUPower)(IADLXGPUMetricsSupport* pThis, adlx_bool* supported);
    ADLX_RESULT (ADLX_STD_CALL* IsSupportedGPUTotalBoardPower)(IADLXGPUMetricsSupport* pThis, adlx_bool* supported);
    ADLX_RESULT (ADLX_STD_CALL* IsSupportedGPUFanSpeed)(IADLXGPUMetricsSupport* pThis, adlx_bool* supported);
    ADLX_RESULT (ADLX_STD_CALL* IsSupportedGPUVRAM)(IADLXGPUMetricsSupport* pThis, adlx_bool* supported);
    ADLX_RESULT (ADLX_STD_CALL* IsSupportedGPUVoltage)(IADLXGPUMetricsSupport* pThis, adlx_bool* supported);
    ADLX_RESULT (ADLX_STD_CALL* GetGPUUsageRange)(IADLXGPUMetricsSupport* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT (ADLX_STD_CALL* GetGPUClockSpeedRange)(IADLXGPUMetricsSupport* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT (ADLX_STD_CALL* GetGPUVRAMClockSpeedRange)(IADLXGPUMetricsSupport* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT (ADLX_STD_CALL* GetGPUTemperatureRange)(IADLXGPUMetricsSupport* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT (ADLX_STD_CALL* GetGPUHotspotTemperatureRange)(IADLXGPUMetricsSupport* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT (ADLX_STD_CALL* GetGPUPowerRange)(IADLXGPUMetricsSupport* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT (ADLX_STD_CALL* GetGPUFanSpeedRange)(IADLXGPUMetricsSupport* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT (ADLX_STD_CALL* GetGPUVRAMRange)(IADLXGPUMetricsSupport* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT (ADLX_STD_CALL* GetGPUVoltageRange)(IADLXGPUMetricsSupport* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT (ADLX_STD_CALL* GetGPUTotalBoardPowerRange)(IADLXGPUMetricsSupport* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT (ADLX_STD_CALL* GetGPUIntakeTemperatureRange)(IADLXGPUMetricsSupport* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT (ADLX_STD_CALL* IsSupportedGPUIntakeTemperature)(IADLXGPUMetricsSupport* pThis, adlx_bool* supported);
} IADLXGPUMetricsSupportVtbl;

struct IADLXGPUMetricsSupport
{
    const IADLXGPUMetricsSupportVtbl *pVtbl;
};

typedef struct IADLXGPUMetricsVtbl
{
    adlx_long (ADLX_STD_CALL* Acquire)(IADLXGPUMetrics* pThis);
    adlx_long (ADLX_STD_CALL* Release)(IADLXGPUMetrics* pThis);
    ADLX_RESULT (ADLX_STD_CALL* QueryInterface)(IADLXGPUMetrics* pThis, const wchar_t* interfaceId, void** ppInterface);
    ADLX_RESULT (ADLX_STD_CALL* TimeStamp)(IADLXGPUMetrics* pThis, adlx_int64* ms);
    ADLX_RESULT (ADLX_STD_CALL* GPUUsage)(IADLXGPUMetrics* pThis, adlx_double* data);
    ADLX_RESULT (ADLX_STD_CALL* GPUClockSpeed)(IADLXGPUMetrics* pThis, adlx_int* data);
    ADLX_RESULT (ADLX_STD_CALL* GPUVRAMClockSpeed)(IADLXGPUMetrics* pThis, adlx_int* data);
    ADLX_RESULT (ADLX_STD_CALL* GPUTemperature)(IADLXGPUMetrics* pThis, adlx_double* data);
    ADLX_RESULT (ADLX_STD_CALL* GPUHotspotTemperature)(IADLXGPUMetrics* pThis, adlx_double* data);
    ADLX_RESULT (ADLX_STD_CALL* GPUPower)(IADLXGPUMetrics* pThis, adlx_double* data);
    ADLX_RESULT (ADLX_STD_CALL* GPUTotalBoardPower)(IADLXGPUMetrics* pThis, adlx_double* data);
    ADLX_RESULT (ADLX_STD_CALL* GPUFanSpeed)(IADLXGPUMetrics* pThis, adlx_int* data);
    ADLX_RESULT (ADLX_STD_CALL* GPUVRAM)(IADLXGPUMetrics* pThis, adlx_int* data);
    ADLX_RESULT (ADLX_STD_CALL* GPUVoltage)(IADLXGPUMetrics* pThis, adlx_int* data);
    ADLX_RESULT (ADLX_STD_CALL* GPUIntakeTemperature)(IADLXGPUMetrics* pThis, adlx_double* data);
} IADLXGPUMetricsVtbl;

struct IADLXGPUMetrics
{
    const IADLXGPUMetricsVtbl *pVtbl;
};

typedef struct IADLXPerformanceMonitoringServicesVtbl
{
    adlx_long (ADLX_STD_CALL *Acquire)(IADLXPerformanceMonitoringServices* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLXPerformanceMonitoringServices* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXPerformanceMonitoringServices* pThis, const wchar_t* interfaceId, void** ppInterface);
    ADLX_RESULT (ADLX_STD_CALL *GetSamplingIntervalRange)(IADLXPerformanceMonitoringServices* pThis, ADLX_IntRange* range);
    ADLX_RESULT (ADLX_STD_CALL *SetSamplingInterval)(IADLXPerformanceMonitoringServices* pThis, adlx_int intervalMs);
    ADLX_RESULT (ADLX_STD_CALL *GetSamplingInterval)(IADLXPerformanceMonitoringServices* pThis, adlx_int* intervalMs);
    ADLX_RESULT (ADLX_STD_CALL *GetMaxPerformanceMetricsHistorySizeRange)(IADLXPerformanceMonitoringServices* pThis, ADLX_IntRange* range);
    ADLX_RESULT (ADLX_STD_CALL *SetMaxPerformanceMetricsHistorySize)(IADLXPerformanceMonitoringServices* pThis, adlx_int sizeSec);
    ADLX_RESULT (ADLX_STD_CALL *GetMaxPerformanceMetricsHistorySize)(IADLXPerformanceMonitoringServices* pThis, adlx_int* sizeSec);
    ADLX_RESULT (ADLX_STD_CALL *ClearPerformanceMetricsHistory)(IADLXPerformanceMonitoringServices* pThis);
    ADLX_RESULT (ADLX_STD_CALL *GetCurrentPerformanceMetricsHistorySize)(IADLXPerformanceMonitoringServices* pThis, adlx_int* sizeSec);
    ADLX_RESULT (ADLX_STD_CALL *StartPerformanceMetricsTracking)(IADLXPerformanceMonitoringServices* pThis);
    ADLX_RESULT (ADLX_STD_CALL *StopPerformanceMetricsTracking)(IADLXPerformanceMonitoringServices* pThis);
    ADLX_RESULT (ADLX_STD_CALL *GetAllMetricsHistory)(IADLXPerformanceMonitoringServices* pThis, adlx_int startMs, adlx_int stopMs, IADLXAllMetricsList** ppMetricsList);
    ADLX_RESULT (ADLX_STD_CALL *GetGPUMetricsHistory)(IADLXPerformanceMonitoringServices* pThis, IADLXGPU* pGPU, adlx_int startMs, adlx_int stopMs, IADLXGPUMetricsList** ppMetricsList);
    ADLX_RESULT (ADLX_STD_CALL *GetSystemMetricsHistory)(IADLXPerformanceMonitoringServices* pThis, adlx_int startMs, adlx_int stopMs, IADLXSystemMetricsList** ppMetricsList);
    ADLX_RESULT (ADLX_STD_CALL *GetFPSHistory)(IADLXPerformanceMonitoringServices* pThis, adlx_int startMs, adlx_int stopMs, IADLXFPSList** ppMetricsList);
    ADLX_RESULT (ADLX_STD_CALL *GetCurrentAllMetrics)(IADLXPerformanceMonitoringServices* pThis, IADLXAllMetrics** ppMetrics);
    ADLX_RESULT (ADLX_STD_CALL *GetCurrentGPUMetrics)(IADLXPerformanceMonitoringServices* pThis, IADLXGPU* pGPU, IADLXGPUMetrics** ppMetrics);
    ADLX_RESULT (ADLX_STD_CALL *GetCurrentSystemMetrics)(IADLXPerformanceMonitoringServices* pThis, IADLXSystemMetrics** ppMetrics);
    ADLX_RESULT (ADLX_STD_CALL *GetCurrentFPS)(IADLXPerformanceMonitoringServices* pThis, IADLXFPS** ppMetrics);
    ADLX_RESULT (ADLX_STD_CALL *GetSupportedGPUMetrics)(IADLXPerformanceMonitoringServices* pThis, IADLXGPU* pGPU, IADLXGPUMetricsSupport** ppMetricsSupported);
    ADLX_RESULT (ADLX_STD_CALL *GetSupportedSystemMetrics)(IADLXPerformanceMonitoringServices* pThis, IADLXSystemMetricsSupport** ppMetricsSupported);
} IADLXPerformanceMonitoringServicesVtbl;

struct IADLXPerformanceMonitoringServices
{
    const IADLXPerformanceMonitoringServicesVtbl *pVtbl;
};

typedef ADLX_RESULT (ADLX_CDECL_CALL *ADLXInitialize_Fn)(adlx_uint64 version, IADLXSystem** ppSystem);
typedef ADLX_RESULT (ADLX_CDECL_CALL *ADLXTerminate_Fn)();
