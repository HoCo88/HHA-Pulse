// Minimal IGCL telemetry contract vendored from Intel's official igcl_api.h.
// Provenance:
// - Upstream file: include/igcl_api.h
// - Upstream version: v1-r1 (copyright header dated 2025)
// - Retrieved: 2026-04-09
// - Source: https://raw.githubusercontent.com/intel/drivers.gpu.control-library/master/include/igcl_api.h

#pragma once

#include <cstdint>

#ifndef CTL_MAKE_VERSION
#define CTL_MAKE_VERSION(_major, _minor) (((_major) << 16) | ((_minor) & 0x0000ffff))
#endif

#define CTL_IMPL_MAJOR_VERSION 1
#define CTL_IMPL_MINOR_VERSION 1
#define CTL_IMPL_VERSION CTL_MAKE_VERSION(CTL_IMPL_MAJOR_VERSION, CTL_IMPL_MINOR_VERSION)
#define CTL_BIT(_i) (1u << (_i))

typedef uint32_t ctl_result_t;
typedef struct _ctl_api_handle_t* ctl_api_handle_t;
typedef struct _ctl_device_adapter_handle_t* ctl_device_adapter_handle_t;

enum : ctl_result_t
{
    CTL_RESULT_SUCCESS = 0x00000000,
    CTL_RESULT_SUCCESS_STILL_OPEN_BY_ANOTHER_CALLER = 0x00000001,
    CTL_RESULT_ERROR_UNINITIALIZED = 0x40000008,
    CTL_RESULT_ERROR_UNSUPPORTED_VERSION = 0x40000009,
};

typedef uint32_t ctl_init_flags_t;
enum : ctl_init_flags_t
{
    CTL_INIT_FLAG_USE_LEVEL_ZERO = CTL_BIT(0),
};

typedef uint32_t ctl_version_info_t;

typedef enum _ctl_units_t
{
    CTL_UNITS_FREQUENCY_MHZ = 0,
    CTL_UNITS_OPERATIONS_GTS = 1,
    CTL_UNITS_OPERATIONS_MTS = 2,
    CTL_UNITS_VOLTAGE_VOLTS = 3,
    CTL_UNITS_POWER_WATTS = 4,
    CTL_UNITS_TEMPERATURE_CELSIUS = 5,
    CTL_UNITS_ENERGY_JOULES = 6,
    CTL_UNITS_TIME_SECONDS = 7,
    CTL_UNITS_MEMORY_BYTES = 8,
    CTL_UNITS_ANGULAR_SPEED_RPM = 9,
    CTL_UNITS_POWER_MILLIWATTS = 10,
    CTL_UNITS_PERCENT = 11,
    CTL_UNITS_MEM_SPEED_GBPS = 12,
    CTL_UNITS_VOLTAGE_MILLIVOLTS = 13,
    CTL_UNITS_BANDWIDTH_MBPS = 14,
    CTL_UNITS_UNKNOWN = 0x4800FFFF,
} ctl_units_t;

typedef enum _ctl_data_type_t
{
    CTL_DATA_TYPE_INT8 = 0,
    CTL_DATA_TYPE_UINT8 = 1,
    CTL_DATA_TYPE_INT16 = 2,
    CTL_DATA_TYPE_UINT16 = 3,
    CTL_DATA_TYPE_INT32 = 4,
    CTL_DATA_TYPE_UINT32 = 5,
    CTL_DATA_TYPE_INT64 = 6,
    CTL_DATA_TYPE_UINT64 = 7,
    CTL_DATA_TYPE_FLOAT = 8,
    CTL_DATA_TYPE_DOUBLE = 9,
    CTL_DATA_TYPE_STRING_ASCII = 10,
    CTL_DATA_TYPE_STRING_UTF16 = 11,
    CTL_DATA_TYPE_STRING_UTF132 = 12,
    CTL_DATA_TYPE_UNKNOWN = 0x4800FFFF,
} ctl_data_type_t;

typedef union _ctl_data_value_t
{
    int8_t data8;
    uint8_t datau8;
    int16_t data16;
    uint16_t datau16;
    int32_t data32;
    uint32_t datau32;
    int64_t data64;
    uint64_t datau64;
    float datafloat;
    double datadouble;
} ctl_data_value_t;

typedef struct _ctl_application_id_t
{
    uint32_t Data1;
    uint16_t Data2;
    uint16_t Data3;
    uint8_t Data4[8];
} ctl_application_id_t;

typedef struct _ctl_init_args_t
{
    uint32_t Size;
    uint8_t Version;
    ctl_version_info_t AppVersion;
    ctl_init_flags_t flags;
    ctl_version_info_t SupportedVersion;
    ctl_application_id_t ApplicationUID;
} ctl_init_args_t;

typedef uint32_t ctl_supported_functions_flags_t;

typedef struct _ctl_firmware_version_t
{
    uint64_t major_version;
    uint64_t minor_version;
    uint64_t build_number;
} ctl_firmware_version_t;

typedef enum _ctl_device_type_t
{
    CTL_DEVICE_TYPE_GRAPHICS = 1,
    CTL_DEVICE_TYPE_SYSTEM = 2,
} ctl_device_type_t;

typedef uint32_t ctl_adapter_properties_flags_t;

typedef struct _ctl_adapter_bdf_t
{
    uint8_t bus;
    uint8_t device;
    uint8_t function;
} ctl_adapter_bdf_t;

#define CTL_MAX_DEVICE_NAME_LEN 100
#define CTL_MAX_RESERVED_SIZE 108
#define CTL_PSU_COUNT 5
#define CTL_FAN_COUNT 5

typedef struct _ctl_device_adapter_properties_t
{
    uint32_t Size;
    uint8_t Version;
    void* pDeviceID;
    uint32_t device_id_size;
    ctl_device_type_t device_type;
    ctl_supported_functions_flags_t supported_subfunction_flags;
    uint64_t driver_version;
    ctl_firmware_version_t firmware_version;
    uint32_t pci_vendor_id;
    uint32_t pci_device_id;
    uint32_t rev_id;
    uint32_t num_eus_per_sub_slice;
    uint32_t num_sub_slices_per_slice;
    uint32_t num_slices;
    char name[CTL_MAX_DEVICE_NAME_LEN];
    ctl_adapter_properties_flags_t graphics_adapter_properties;
    uint32_t Frequency;
    uint16_t pci_subsys_id;
    uint16_t pci_subsys_vendor_id;
    ctl_adapter_bdf_t adapter_bdf;
    uint32_t num_xe_cores;
    char reserved[CTL_MAX_RESERVED_SIZE];
} ctl_device_adapter_properties_t;

typedef struct _ctl_oc_telemetry_item_t
{
    bool bSupported;
    ctl_units_t units;
    ctl_data_type_t type;
    ctl_data_value_t value;
} ctl_oc_telemetry_item_t;

typedef enum _ctl_psu_type_t
{
    CTL_PSU_TYPE_PSU_NONE = 0,
    CTL_PSU_TYPE_PSU_PCIE = 1,
    CTL_PSU_TYPE_PSU_6PIN = 2,
    CTL_PSU_TYPE_PSU_8PIN = 3,
} ctl_psu_type_t;

typedef struct _ctl_psu_info_t
{
    bool bSupported;
    ctl_psu_type_t psuType;
    ctl_oc_telemetry_item_t energyCounter;
    ctl_oc_telemetry_item_t voltage;
} ctl_psu_info_t;

typedef struct _ctl_power_telemetry_t
{
    uint32_t Size;
    uint8_t Version;
    ctl_oc_telemetry_item_t timeStamp;
    ctl_oc_telemetry_item_t gpuEnergyCounter;
    ctl_oc_telemetry_item_t gpuVoltage;
    ctl_oc_telemetry_item_t gpuCurrentClockFrequency;
    ctl_oc_telemetry_item_t gpuCurrentTemperature;
    ctl_oc_telemetry_item_t globalActivityCounter;
    ctl_oc_telemetry_item_t renderComputeActivityCounter;
    ctl_oc_telemetry_item_t mediaActivityCounter;
    bool gpuPowerLimited;
    bool gpuTemperatureLimited;
    bool gpuCurrentLimited;
    bool gpuVoltageLimited;
    bool gpuUtilizationLimited;
    ctl_oc_telemetry_item_t vramEnergyCounter;
    ctl_oc_telemetry_item_t vramVoltage;
    ctl_oc_telemetry_item_t vramCurrentClockFrequency;
    ctl_oc_telemetry_item_t vramCurrentEffectiveFrequency;
    ctl_oc_telemetry_item_t vramReadBandwidthCounter;
    ctl_oc_telemetry_item_t vramWriteBandwidthCounter;
    ctl_oc_telemetry_item_t vramCurrentTemperature;
    bool vramPowerLimited;
    bool vramTemperatureLimited;
    bool vramCurrentLimited;
    bool vramVoltageLimited;
    bool vramUtilizationLimited;
    ctl_oc_telemetry_item_t totalCardEnergyCounter;
    ctl_psu_info_t psu[CTL_PSU_COUNT];
    ctl_oc_telemetry_item_t fanSpeed[CTL_FAN_COUNT];
    ctl_oc_telemetry_item_t gpuVrTemp;
    ctl_oc_telemetry_item_t vramVrTemp;
    ctl_oc_telemetry_item_t saVrTemp;
    ctl_oc_telemetry_item_t gpuEffectiveClock;
    ctl_oc_telemetry_item_t gpuOverVoltagePercent;
    ctl_oc_telemetry_item_t gpuPowerPercent;
    ctl_oc_telemetry_item_t gpuTemperaturePercent;
    ctl_oc_telemetry_item_t vramReadBandwidth;
    ctl_oc_telemetry_item_t vramWriteBandwidth;
} ctl_power_telemetry_t;

typedef ctl_result_t(__cdecl* ctlInitFn)(ctl_init_args_t* pInitArgs, ctl_api_handle_t* phAPIHandle);
typedef ctl_result_t(__cdecl* ctlCloseFn)(ctl_api_handle_t hAPIHandle);
typedef ctl_result_t(__cdecl* ctlEnumerateDevicesFn)(ctl_api_handle_t hAPIHandle, uint32_t* pCount, ctl_device_adapter_handle_t* phDevices);
typedef ctl_result_t(__cdecl* ctlGetDevicePropertiesFn)(ctl_device_adapter_handle_t hDAhandle, ctl_device_adapter_properties_t* pProperties);
typedef ctl_result_t(__cdecl* ctlPowerTelemetryGetFn)(ctl_device_adapter_handle_t hDeviceHandle, ctl_power_telemetry_t* pTelemetryInfo);
