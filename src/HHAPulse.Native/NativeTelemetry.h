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

struct HhaPulseGpuReading {
    double temperatureCelsius;
    double powerWatts;
    int fanRpm;
    double clockMegahertz;
    uint32_t validFlags; // bitmask: 1=temp, 2=power, 4=fan, 8=clock
};

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
