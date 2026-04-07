#pragma once

#ifdef HHAPULSE_NATIVE_EXPORTS
#define HHAPULSE_NATIVE_API __declspec(dllexport)
#else
#define HHAPULSE_NATIVE_API __declspec(dllimport)
#endif

extern "C"
{
    HHAPULSE_NATIVE_API int HhaPulseAdlxProbe();
    HHAPULSE_NATIVE_API int HhaPulseIgclProbe();
}
