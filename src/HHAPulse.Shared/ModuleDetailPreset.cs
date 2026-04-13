using System;
using System.Collections.Generic;
using System.Linq;

namespace HHAPulse.Shared;

/// <summary>
/// Detail level for a module inside the composer.
/// </summary>
public enum ComposerDetail
{
    Off = 0,
    Summary = 1,
    Tuning = 2,
    Deep = 3
}

/// <summary>
/// Stable module identifiers used by the composer. String constants so the
/// view-model can key dictionaries without depending on WinUI.
/// </summary>
public static class ComposerModuleIds
{
    public const string Fps = "fps";
    public const string Cpu = "cpu";
    public const string Gpu = "gpu";
    public const string Memory = "memory";
    public const string Power = "power";
    public const string Battery = "battery";
    public const string Storage = "storage";
    public const string Display = "display";
    public const string AdvancedSensors = "advanced_sensors";
}

/// <summary>
/// Describes a single module in the composer: a display name, an icon glyph,
/// and the ordered metric IDs each detail level contributes.
/// Metric IDs are the same constants exposed by
/// <c>HHAPulse.Overlay.Settings.OverlayPresetCatalog</c>. They live as raw
/// strings here so the netstandard2.0 Shared project has no dependency on the
/// Overlay assembly.
/// </summary>
public sealed class ModuleDetailPreset
{
    public ModuleDetailPreset(
        string id,
        string displayName,
        string iconGlyph,
        IReadOnlyList<string> summaryMetricIds,
        IReadOnlyList<string> tuningMetricIds,
        IReadOnlyList<string> deepMetricIds,
        bool supportsTuning = true,
        bool supportsDeep = true)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Module id is required", nameof(id));
        }

        Id = id;
        DisplayName = displayName ?? string.Empty;
        IconGlyph = iconGlyph ?? string.Empty;
        SummaryMetricIds = summaryMetricIds ?? Array.Empty<string>();
        TuningMetricIds = tuningMetricIds ?? Array.Empty<string>();
        DeepMetricIds = deepMetricIds ?? Array.Empty<string>();
        SupportsTuning = supportsTuning;
        SupportsDeep = supportsDeep;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string IconGlyph { get; }

    public IReadOnlyList<string> SummaryMetricIds { get; }
    public IReadOnlyList<string> TuningMetricIds { get; }
    public IReadOnlyList<string> DeepMetricIds { get; }

    public bool SupportsTuning { get; }
    public bool SupportsDeep { get; }

    /// <summary>
    /// Returns the metric IDs this module contributes at the given detail level.
    /// Higher levels include everything from lower levels; <see cref="ComposerDetail.Off"/>
    /// returns an empty list.
    /// </summary>
    public IReadOnlyList<string> GetMetricIdsForDetail(ComposerDetail detail)
    {
        switch (detail)
        {
            case ComposerDetail.Off:
                return Array.Empty<string>();
            case ComposerDetail.Summary:
                return SummaryMetricIds;
            case ComposerDetail.Tuning:
                return SummaryMetricIds.Concat(TuningMetricIds).ToArray();
            case ComposerDetail.Deep:
                return SummaryMetricIds
                    .Concat(TuningMetricIds)
                    .Concat(DeepMetricIds)
                    .ToArray();
            default:
                return Array.Empty<string>();
        }
    }
}

/// <summary>
/// Static catalog of the nine composer modules from the v3 UX spec.
/// Metric ID strings are duplicated here on purpose so Shared stays free of
/// Overlay references; the Overlay catalog and this file are the single source
/// of truth for the string literal used on the wire.
/// </summary>
public static class ComposerModuleCatalog
{
    // Metric IDs — must stay in sync with
    // HHAPulse.Overlay.Settings.OverlayPresetCatalog constants.
    private const string MetricFps = "fps";
    private const string MetricAvgFps = "avg_fps";
    private const string MetricOnePercentLow = "one_percent_low";
    private const string MetricZeroPointOneLow = "zero_point_one_low";
    private const string MetricFrameTime = "frametime";

    private const string MetricCpuUsage = "cpu";
    private const string MetricCpuTemp = "cpu_temp";
    private const string MetricCpuPower = "cpu_power";

    private const string MetricGpuUsage = "gpu";
    private const string MetricGpuTemp = "gpu_temp";
    private const string MetricGpuClock = "gpu_clock";
    private const string MetricGpuPower = "gpu_power";
    private const string MetricGpuFan = "gpu_fan";
    private const string MetricVram = "vram";

    private const string MetricRam = "ram";
    private const string MetricTotalPower = "total_power";
    private const string MetricBattery = "battery";
    private const string MetricRefreshRate = "refresh_rate";
    private const string MetricDeviceTemp = "device_temp";

    private static readonly ModuleDetailPreset Fps = new(
        id: ComposerModuleIds.Fps,
        displayName: "FPS",
        iconGlyph: "\uE714",
        summaryMetricIds: new[] { MetricFps },
        tuningMetricIds: new[] { MetricAvgFps, MetricOnePercentLow },
        deepMetricIds: new[] { MetricZeroPointOneLow, MetricFrameTime });

    private static readonly ModuleDetailPreset Cpu = new(
        id: ComposerModuleIds.Cpu,
        displayName: "CPU",
        iconGlyph: "\uE950",
        summaryMetricIds: new[] { MetricCpuUsage, MetricCpuTemp, MetricCpuPower },
        tuningMetricIds: Array.Empty<string>(),
        deepMetricIds: Array.Empty<string>());

    private static readonly ModuleDetailPreset Gpu = new(
        id: ComposerModuleIds.Gpu,
        displayName: "GPU",
        iconGlyph: "\uE7F4",
        summaryMetricIds: new[] { MetricGpuUsage, MetricGpuTemp, MetricGpuPower },
        tuningMetricIds: new[] { MetricGpuClock, MetricVram },
        deepMetricIds: new[] { MetricGpuFan });

    private static readonly ModuleDetailPreset Memory = new(
        id: ComposerModuleIds.Memory,
        displayName: "Memory",
        iconGlyph: "\uEEA3",
        summaryMetricIds: new[] { MetricRam },
        tuningMetricIds: new[] { MetricVram },
        deepMetricIds: Array.Empty<string>(),
        supportsDeep: false);

    private static readonly ModuleDetailPreset Power = new(
        id: ComposerModuleIds.Power,
        displayName: "Power",
        iconGlyph: "\uE945",
        summaryMetricIds: new[] { MetricTotalPower },
        tuningMetricIds: new[] { MetricCpuPower, MetricGpuPower },
        deepMetricIds: Array.Empty<string>(),
        supportsDeep: false);

    private static readonly ModuleDetailPreset Battery = new(
        id: ComposerModuleIds.Battery,
        displayName: "Battery",
        iconGlyph: "\uE83F",
        summaryMetricIds: new[] { MetricBattery },
        tuningMetricIds: Array.Empty<string>(),
        deepMetricIds: Array.Empty<string>(),
        supportsTuning: false,
        supportsDeep: false);

    private static readonly ModuleDetailPreset Storage = new(
        id: ComposerModuleIds.Storage,
        displayName: "Storage",
        iconGlyph: "\uEDA2",
        summaryMetricIds: Array.Empty<string>(),
        tuningMetricIds: Array.Empty<string>(),
        deepMetricIds: Array.Empty<string>(),
        supportsTuning: true,
        supportsDeep: true);

    private static readonly ModuleDetailPreset Display = new(
        id: ComposerModuleIds.Display,
        displayName: "Display",
        iconGlyph: "\uE7F4",
        summaryMetricIds: new[] { MetricRefreshRate },
        tuningMetricIds: Array.Empty<string>(),
        deepMetricIds: Array.Empty<string>(),
        supportsTuning: false,
        supportsDeep: false);

    private static readonly ModuleDetailPreset Advanced = new(
        id: ComposerModuleIds.AdvancedSensors,
        displayName: "Advanced sensors",
        iconGlyph: "\uE9D9",
        summaryMetricIds: new[] { MetricDeviceTemp },
        tuningMetricIds: Array.Empty<string>(),
        deepMetricIds: Array.Empty<string>(),
        supportsTuning: false,
        supportsDeep: false);

    public static readonly IReadOnlyList<ModuleDetailPreset> All = new[]
    {
        Fps, Cpu, Gpu, Memory, Power, Battery, Storage, Display, Advanced
    };

    private static readonly Dictionary<string, ModuleDetailPreset> ById =
        All.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);

    public static ModuleDetailPreset? Find(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        return ById.TryGetValue(id, out var preset) ? preset : null;
    }
}

/// <summary>
/// Immutable snapshot of what the composer has produced. Emitted to the host
/// when the user taps Apply so the host can persist or forward it without
/// needing a reference to the internal view-model type.
/// </summary>
public sealed class CustomPresetDraft
{
    public CustomPresetDraft(
        ComposerLayout layout,
        IReadOnlyList<string> orderedMetricIds,
        IReadOnlyDictionary<string, ComposerDetail> moduleDetail,
        bool showGraph)
    {
        Layout = layout;
        OrderedMetricIds = orderedMetricIds ?? Array.Empty<string>();
        ModuleDetail = moduleDetail ?? new Dictionary<string, ComposerDetail>();
        ShowGraph = showGraph;
    }

    public ComposerLayout Layout { get; }
    public IReadOnlyList<string> OrderedMetricIds { get; }
    public IReadOnlyDictionary<string, ComposerDetail> ModuleDetail { get; }
    public bool ShowGraph { get; }
}
