#define HHAPULSE_NATIVE_EXPORTS
#include "NativeTelemetry.h"

extern "C" int HhaPulseAdlxProbe()
{
    // Spike S4 wires this to ADLX on Ryzen Z1 Extreme hardware.
    return 0;
}

extern "C" int HhaPulseIgclProbe()
{
    // Intel telemetry is 64-bit only; the managed caller gates x64 before use.
    return 0;
}
