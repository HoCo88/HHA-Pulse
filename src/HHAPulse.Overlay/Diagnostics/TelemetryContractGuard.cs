using System.Reflection;
using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.Diagnostics;

public static class TelemetryContractGuard
{
    private static readonly string[] RequiredGpuProperties =
    {
        "ClockMegahertz",
        "FanRpm",
        "PowerWatts",
        "TemperatureCelsius",
        "VramUsedMegabytes",
        "VramTotalMegabytes"
    };

    public static TelemetryContractInfo GetCurrentInfo()
    {
        var assembly = typeof(TelemetrySnapshot).Assembly;
        var missing = RequiredGpuProperties
            .Where(propertyName => typeof(GpuMetrics).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public) is null)
            .ToArray();

        return new TelemetryContractInfo(
            assembly.Location,
            assembly.GetName().Version?.ToString() ?? "unknown",
            typeof(TelemetrySnapshot).Module.ModuleVersionId.ToString("D"),
            missing.Length == 0,
            missing.Length == 0
                ? "Telemetry contract OK."
                : $"Stale HHAPulse.Shared.dll: missing {string.Join(", ", missing)}.");
    }

    public static void ThrowIfInvalid()
    {
        var info = GetCurrentInfo();
        Log(info);

        if (!info.IsValid)
        {
            throw new InvalidOperationException(
                $"{info.StatusMessage} Loaded from '{info.AssemblyPath}', version {info.AssemblyVersion}, MVID {info.ModuleVersionId}.");
        }
    }

    public static void ApplyTo(TelemetrySnapshot snapshot)
    {
        var info = GetCurrentInfo();
        snapshot.Dependencies.SharedAssemblyPath = info.AssemblyPath;
        snapshot.Dependencies.SharedAssemblyVersion = info.AssemblyVersion;
        snapshot.Dependencies.SharedAssemblyMvid = info.ModuleVersionId;
        snapshot.Dependencies.TelemetryContractValid = info.IsValid;
        snapshot.Dependencies.TelemetryContractStatusMessage = info.StatusMessage;
        MeasurementTraceRecorder.Record(
            snapshot,
            "telemetry_contract",
            nameof(TelemetryContractGuard),
            "HHAPulse.Shared assembly metadata",
            info.IsValid,
            info.IsValid ? "valid" : "invalid",
            $"path={info.AssemblyPath}; version={info.AssemblyVersion}; mvid={info.ModuleVersionId}",
            info.StatusMessage,
            "Reflection over TelemetrySnapshot/GpuMetrics contract",
            info.ModuleVersionId,
            "MVID",
            "required property presence check",
            info.IsValid.ToString(),
            "bool",
            info.IsValid ? TelemetryValidationState.Verified : TelemetryValidationState.Rejected,
            info.StatusMessage);
    }

    public static void Log(TelemetryContractInfo info)
    {
        AppLogger.Info($"Loaded HHAPulse.Shared.dll: Path='{info.AssemblyPath}', Version={info.AssemblyVersion}, MVID={info.ModuleVersionId}, ContractValid={info.IsValid}. {info.StatusMessage}");
    }
}

public sealed record TelemetryContractInfo(
    string AssemblyPath,
    string AssemblyVersion,
    string ModuleVersionId,
    bool IsValid,
    string StatusMessage);
