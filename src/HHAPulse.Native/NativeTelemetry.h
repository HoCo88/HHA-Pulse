#pragma once

#include <cstdint>

#ifdef HHAPULSE_NATIVE_EXPORTS
#define HHAPULSE_NATIVE_API __declspec(dllexport)
#else
#define HHAPULSE_NATIVE_API __declspec(dllimport)
#endif

// Bitmask values for HhaPulseGpuReading.validFlags
#define HHAPULSE_GPU_VALID_TEMP  0x01
#define HHAPULSE_GPU_VALID_POWER 0x02
#define HHAPULSE_GPU_VALID_FAN   0x04
#define HHAPULSE_GPU_VALID_CLOCK 0x08

#define HHAPULSE_GPU_POWER_SOURCE_UNKNOWN                   0
#define HHAPULSE_GPU_POWER_SOURCE_ADLX_GPU                  1
#define HHAPULSE_GPU_POWER_SOURCE_ADLX_TOTAL_BOARD          2
#define HHAPULSE_GPU_POWER_SOURCE_IGCL_GPU_ENERGY           3
// Fallback: IGCL totalCardEnergyCounter from ctl_power_telemetry_t
// when gpuEnergyCounter reported bSupported=false but the card-wide
// energy counter is populated. See IgclTelemetryContract.h line ~219.
#define HHAPULSE_GPU_POWER_SOURCE_IGCL_TOTAL_CARD_ENERGY    4

// Which temperature field of ctl_power_telemetry_t was actually used.
// Zero means no field produced a valid reading on this tick.
#define HHAPULSE_GPU_TEMP_SOURCE_NONE                       0
#define HHAPULSE_GPU_TEMP_SOURCE_GPU_CURRENT                1  // gpuCurrentTemperature (primary)
#define HHAPULSE_GPU_TEMP_SOURCE_GPU_VR                     2  // gpuVrTemp (GPU voltage regulator)
#define HHAPULSE_GPU_TEMP_SOURCE_SA_VR                      3  // saVrTemp  (System Agent voltage regulator)
#define HHAPULSE_GPU_TEMP_SOURCE_IGCL_SENSOR_ENUM           4  // ctlTemperatureGetState

struct HhaPulseGpuReading {
    double temperatureCelsius;
    double powerWatts;
    int fanRpm;
    double clockMegahertz;
    uint32_t validFlags; // bitmask: 1=temp, 2=power, 4=fan, 8=clock
    uint32_t powerSourceKind;
    // Which ctl_power_telemetry_t field produced the temperature
    // reading. Allows the C# layer to log "used gpuVrTemp" vs
    // "used gpuCurrentTemperature" for driver-behavior diagnosis.
    uint32_t temperatureSourceKind;
    // ctl_power_telemetry_t bSupported bitmap for diagnostic logging.
    // Bits follow the order of the fields in the struct. See the
    // HHAPULSE_IGCL_FIELD_* constants below.
    uint32_t igclFieldSupportMask;
};

// Bit positions inside HhaPulseGpuReading.igclFieldSupportMask. These
// are purely diagnostic and let the one-shot IGCL log enumerate which
// fields the driver reported as bSupported=true on this adapter.
#define HHAPULSE_IGCL_FIELD_GPU_CURRENT_TEMP     0x00000001
#define HHAPULSE_IGCL_FIELD_GPU_VR_TEMP          0x00000002
#define HHAPULSE_IGCL_FIELD_SA_VR_TEMP           0x00000004
#define HHAPULSE_IGCL_FIELD_GPU_ENERGY_COUNTER   0x00000008
#define HHAPULSE_IGCL_FIELD_TOTAL_CARD_ENERGY    0x00000010
#define HHAPULSE_IGCL_FIELD_GPU_CURRENT_CLOCK    0x00000020
#define HHAPULSE_IGCL_FIELD_GPU_EFFECTIVE_CLOCK  0x00000040
#define HHAPULSE_IGCL_FIELD_FAN_SPEED_ANY        0x00000080
#define HHAPULSE_IGCL_FIELD_DEDICATED_TEMP_API   0x00000100
#define HHAPULSE_IGCL_FIELD_DEDICATED_FAN_API    0x00000200

extern "C"
{
    // Legacy probe stubs (kept for compatibility)
    HHAPULSE_NATIVE_API int HhaPulseAdlxProbe();
    HHAPULSE_NATIVE_API int HhaPulseIgclProbe();

    // AMD ADLX
    HHAPULSE_NATIVE_API int HhaPulseAdlxInit();
    HHAPULSE_NATIVE_API int HhaPulseAdlxReadGpu(HhaPulseGpuReading* out);
    HHAPULSE_NATIVE_API void HhaPulseAdlxShutdown();

    // Intel IGCL
    HHAPULSE_NATIVE_API int HhaPulseIgclInit();
    HHAPULSE_NATIVE_API int HhaPulseIgclReadGpu(HhaPulseGpuReading* out);
    HHAPULSE_NATIVE_API void HhaPulseIgclShutdown();
}
