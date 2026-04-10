using System.Buffers.Binary;
using System.Text;

namespace HHAPulse.Overlay.Collectors.Cpu;

/// <summary>
/// Which RAPL rail role we want to select out of the EMI metadata.
/// Every collector uses the same EMI device enumeration; they differ
/// only in which channel name they match.
/// </summary>
internal enum EmiChannelRole
{
    /// <summary>CPU package / SoC package power (PKG best, PP0 fallback).</summary>
    Cpu = 0,
    /// <summary>Integrated GPU power (PP1 on Intel client SKUs).</summary>
    IGpu = 1,
    /// <summary>DRAM / memory power.</summary>
    Dram = 2,
}

internal static class EmiContract
{
    internal const int MeasurementUnitPicowattHours = 0;
    internal static readonly Guid DeviceInterfaceGuid = new(0x45BD8344, 0x7ED6, 0x49CF, 0xA4, 0x40, 0xC2, 0x76, 0xC9, 0x33, 0xB0, 0x53);

    internal const ushort VersionV1 = 1;
    internal const ushort VersionV2 = 2;
    internal const int NameMax = 16;

    internal const uint IoctlGetVersion = 0x00224000;
    internal const uint IoctlGetMetadataSize = 0x00224004;
    internal const uint IoctlGetMetadata = 0x00224008;
    internal const uint IoctlGetMeasurement = 0x0022400C;

    private const int ChannelMeasurementSize = sizeof(ulong) + sizeof(ulong);
    private const int V1HeaderSize = sizeof(int) + (NameMax * sizeof(char)) + (NameMax * sizeof(char)) + sizeof(ushort) + sizeof(ushort);
    private const int V2HeaderSize = (NameMax * sizeof(char)) + (NameMax * sizeof(char)) + sizeof(ushort) + sizeof(ushort);

    internal static EmiSelection? TryParseSelection(ushort version, ReadOnlySpan<byte> metadata, EmiChannelRole role = EmiChannelRole.Cpu)
    {
        if (version <= VersionV1)
        {
            return TryParseV1(metadata, role);
        }

        return TryParseV2(version, metadata, role);
    }

    /// <summary>
    /// Diagnostic helper: walks the EMI metadata and returns every channel
    /// name verbatim (no rank filtering) so that when the keyword filter in
    /// <see cref="GetChannelRank"/> rejects a device we can log what the
    /// hardware actually exposed. Returns an empty list if the metadata is
    /// malformed or the version is unsupported.
    /// </summary>
    internal static IReadOnlyList<string> ExtractChannelNames(ushort version, ReadOnlySpan<byte> metadata)
    {
        if (version <= VersionV1)
        {
            return ExtractV1ChannelNames(metadata);
        }

        return ExtractV2ChannelNames(metadata);
    }

    private static List<string> ExtractV1ChannelNames(ReadOnlySpan<byte> metadata)
    {
        var names = new List<string>();
        if (metadata.Length < V1HeaderSize)
        {
            return names;
        }

        ushort nameSize = BinaryPrimitives.ReadUInt16LittleEndian(metadata.Slice(V1HeaderSize - sizeof(ushort), sizeof(ushort)));
        if (nameSize == 0 || V1HeaderSize + nameSize > metadata.Length || (nameSize % 2) != 0)
        {
            return names;
        }

        names.Add(ReadSizedUnicodeString(metadata.Slice(V1HeaderSize, nameSize)));
        return names;
    }

    private static List<string> ExtractV2ChannelNames(ReadOnlySpan<byte> metadata)
    {
        var names = new List<string>();
        if (metadata.Length < V2HeaderSize)
        {
            return names;
        }

        ushort channelCount = BinaryPrimitives.ReadUInt16LittleEndian(metadata.Slice(V2HeaderSize - sizeof(ushort), sizeof(ushort)));
        if (channelCount == 0)
        {
            return names;
        }

        int offset = V2HeaderSize;
        for (int channelIndex = 0; channelIndex < channelCount; channelIndex++)
        {
            if (offset + sizeof(int) + sizeof(ushort) > metadata.Length)
            {
                return names;
            }

            int unit = BinaryPrimitives.ReadInt32LittleEndian(metadata.Slice(offset, sizeof(int)));
            offset += sizeof(int);

            ushort nameSize = BinaryPrimitives.ReadUInt16LittleEndian(metadata.Slice(offset, sizeof(ushort)));
            offset += sizeof(ushort);

            if (nameSize == 0 || (nameSize % 2) != 0 || offset + nameSize > metadata.Length)
            {
                return names;
            }

            var name = ReadSizedUnicodeString(metadata.Slice(offset, nameSize));
            offset += nameSize;

            var unitLabel = unit == MeasurementUnitPicowattHours ? "pWh" : $"unit={unit}";
            names.Add($"'{name}' ({unitLabel})");
        }

        return names;
    }

    internal static bool TryReadMeasurement(ushort version, ushort channelCount, int channelIndex, ReadOnlySpan<byte> measurement, out EmiMeasurementPoint point)
    {
        point = default;

        if (version <= VersionV1)
        {
            if (measurement.Length < ChannelMeasurementSize)
            {
                return false;
            }

            point = new EmiMeasurementPoint(
                BinaryPrimitives.ReadUInt64LittleEndian(measurement),
                BinaryPrimitives.ReadUInt64LittleEndian(measurement[sizeof(ulong)..]));
            return true;
        }

        if (channelIndex < 0 || channelIndex >= channelCount)
        {
            return false;
        }

        int requiredLength = channelCount * ChannelMeasurementSize;
        if (measurement.Length < requiredLength)
        {
            return false;
        }

        int offset = channelIndex * ChannelMeasurementSize;
        point = new EmiMeasurementPoint(
            BinaryPrimitives.ReadUInt64LittleEndian(measurement.Slice(offset, sizeof(ulong))),
            BinaryPrimitives.ReadUInt64LittleEndian(measurement.Slice(offset + sizeof(ulong), sizeof(ulong))));
        return true;
    }

    internal static double? TryComputeWatts(EmiMeasurementPoint previous, EmiMeasurementPoint current)
    {
        if (current.AbsoluteTime100Nanoseconds <= previous.AbsoluteTime100Nanoseconds ||
            current.AbsoluteEnergyPicowattHours < previous.AbsoluteEnergyPicowattHours)
        {
            return null;
        }

        double elapsedSeconds = (current.AbsoluteTime100Nanoseconds - previous.AbsoluteTime100Nanoseconds) / 10_000_000d;
        if (elapsedSeconds <= 0d)
        {
            return null;
        }

        double deltaEnergyPicowattHours = current.AbsoluteEnergyPicowattHours - previous.AbsoluteEnergyPicowattHours;
        return (deltaEnergyPicowattHours * 3.6e-9d) / elapsedSeconds;
    }

    private static EmiSelection? TryParseV1(ReadOnlySpan<byte> metadata, EmiChannelRole role)
    {
        if (metadata.Length < V1HeaderSize)
        {
            return null;
        }

        int measurementUnit = BinaryPrimitives.ReadInt32LittleEndian(metadata);
        if (measurementUnit != MeasurementUnitPicowattHours)
        {
            return null;
        }

        ushort nameSize = BinaryPrimitives.ReadUInt16LittleEndian(metadata.Slice(V1HeaderSize - sizeof(ushort), sizeof(ushort)));
        if (nameSize == 0 || V1HeaderSize + nameSize > metadata.Length || (nameSize % 2) != 0)
        {
            return null;
        }

        string name = ReadSizedUnicodeString(metadata.Slice(V1HeaderSize, nameSize));
        if (GetChannelRank(name, role) == int.MaxValue)
        {
            return null;
        }

        return new EmiSelection(VersionV1, 1, 0, name);
    }

    private static EmiSelection? TryParseV2(ushort version, ReadOnlySpan<byte> metadata, EmiChannelRole role)
    {
        if (metadata.Length < V2HeaderSize)
        {
            return null;
        }

        ushort channelCount = BinaryPrimitives.ReadUInt16LittleEndian(metadata.Slice(V2HeaderSize - sizeof(ushort), sizeof(ushort)));
        if (channelCount == 0)
        {
            return null;
        }

        int bestRank = int.MaxValue;
        int bestIndex = -1;
        string bestName = string.Empty;
        bool ambiguous = false;
        int offset = V2HeaderSize;

        for (int channelIndex = 0; channelIndex < channelCount; channelIndex++)
        {
            if (offset + sizeof(int) + sizeof(ushort) > metadata.Length)
            {
                return null;
            }

            int unit = BinaryPrimitives.ReadInt32LittleEndian(metadata.Slice(offset, sizeof(int)));
            offset += sizeof(int);

            ushort nameSize = BinaryPrimitives.ReadUInt16LittleEndian(metadata.Slice(offset, sizeof(ushort)));
            offset += sizeof(ushort);

            if (nameSize == 0 || (nameSize % 2) != 0 || offset + nameSize > metadata.Length)
            {
                return null;
            }

            string name = ReadSizedUnicodeString(metadata.Slice(offset, nameSize));
            offset += nameSize;

            if (unit != MeasurementUnitPicowattHours)
            {
                continue;
            }

            int rank = GetChannelRank(name, role);
            if (rank == int.MaxValue)
            {
                continue;
            }

            if (rank < bestRank)
            {
                bestRank = rank;
                bestIndex = channelIndex;
                bestName = name;
                ambiguous = false;
            }
            else if (rank == bestRank)
            {
                ambiguous = true;
            }
        }

        if (bestIndex < 0 || ambiguous)
        {
            return null;
        }

        return new EmiSelection(version, channelCount, bestIndex, bestName);
    }

    private static int GetChannelRank(string channelName, EmiChannelRole role)
    {
        if (string.IsNullOrWhiteSpace(channelName))
        {
            return int.MaxValue;
        }

        string normalized = channelName.ToUpperInvariant();

        // ── Intel RAPL per-rail naming pattern: "RAPL_Package<N>_<RAIL>" ──
        //
        // Intel's Windows EMI driver (ipmDrv / Energy Meter Interface) exposes
        // per-rail energy counters on Core, Core Ultra, and Atom/Lunar Lake
        // parts. On Lunar Lake (Arc 140V) we observed four channels on the
        // actual device:
        //   RAPL_Package0_PKG   ← package total (best match for "CPU power")
        //   RAPL_Package0_PP0   ← IA cores only (CPU cores, ok fallback)
        //   RAPL_Package0_DRAM  ← DRAM energy (not CPU, REJECT)
        //   RAPL_Package0_PP1   ← GT plane / iGPU (not CPU, REJECT)
        //
        // The distinguishing signature is the UNDERSCORE between tokens —
        // that's Intel's literal channel name format. Generic OEM names like
        // "RAPL Package" (with a space) or "CPU Package" are human-readable
        // labels, not Intel's pattern, and must fall through to the generic
        // OEM matcher below so they still work on non-Intel devices.
        //
        // A naive substring filter that accepts "PACKAGE" on the Intel names
        // matches ALL FOUR (every Intel RAPL channel contains "Package0"),
        // trips the ambiguity guard, and rejects the whole device. We match
        // by the trailing rail suffix instead, and explicitly reject DRAM/PP1.
        // Source: Intel SDM Vol. 3B §15.10.3 (RAPL MSR interfaces) + Intel
        // IPM driver INF channel name scheme (observed via our own EMI
        // diagnostic log on Core Ultra 7 258V on 2026-04-10).
        if (normalized.StartsWith("RAPL_PACKAGE", StringComparison.Ordinal))
        {
            return role switch
            {
                EmiChannelRole.Cpu => MatchIntelRaplCpu(normalized),
                EmiChannelRole.IGpu => MatchIntelRaplIGpu(normalized),
                EmiChannelRole.Dram => MatchIntelRaplDram(normalized),
                _ => int.MaxValue,
            };
        }

        // ── Generic OEM naming ──
        //
        // Other devices (AMD RAPL exposed via OEM drivers, ARM SoCs, etc.)
        // tend to use human-readable names like "CPU Package", "CPU", or
        // "SoC". Fall back to substring matching for those. Only the Cpu
        // role has a generic fallback; IGpu and Dram require the explicit
        // Intel RAPL naming pattern to avoid misidentifying random rails.
        if (role != EmiChannelRole.Cpu)
        {
            return int.MaxValue;
        }

        if (normalized.Contains("PACKAGE", StringComparison.Ordinal))
        {
            return 0;
        }

        if (normalized.Contains("CPU", StringComparison.Ordinal))
        {
            return 1;
        }

        if (normalized.Contains("RAPL", StringComparison.Ordinal))
        {
            return 2;
        }

        if (normalized.Contains("SOC", StringComparison.Ordinal))
        {
            return 3;
        }

        return int.MaxValue;
    }

    private static int MatchIntelRaplCpu(string normalized)
    {
        if (normalized.EndsWith("_PKG", StringComparison.Ordinal))
        {
            return 0;   // package total — the canonical CPU package power
        }

        if (normalized.EndsWith("_PP0", StringComparison.Ordinal))
        {
            return 1;   // IA cores only — acceptable fallback
        }

        // DRAM, PP1 (GT), and any other Intel RAPL sub-rail are not CPU
        // power. Reject them explicitly so they cannot be picked up by
        // the generic OEM patterns above.
        return int.MaxValue;
    }

    private static int MatchIntelRaplIGpu(string normalized)
    {
        // Intel Power Management driver exposes the graphics rail as
        // "RAPL_Package<N>_PP1". The PP1 domain corresponds to Power
        // Plane 1 in Intel's RAPL architecture, which on client SKUs
        // (Core / Core Ultra) carries the integrated graphics energy
        // counter. Source: Intel 64 and IA-32 Architectures Software
        // Developer's Manual Vol 3B §15.10.3 "RAPL Domains and
        // Platform Specific Information". Verified against the
        // channel name list the existing CpuPowerCollector already
        // enumerated on Core Ultra 7 258V (Arc 140V) — see the
        // comment block above in MatchIntelRaplCpu / file header.
        if (normalized.EndsWith("_PP1", StringComparison.Ordinal))
        {
            return 0;
        }

        return int.MaxValue;
    }

    private static int MatchIntelRaplDram(string normalized)
    {
        // DRAM energy domain exposed only on client SKUs that include
        // the DRAM RAPL counter (Intel SDM Vol 3B §15.10.3). Not used
        // on the HUD today, but selecting it lets a future "Memory
        // Power" tile read straight from EMI without duplicate
        // enumeration.
        if (normalized.EndsWith("_DRAM", StringComparison.Ordinal))
        {
            return 0;
        }

        return int.MaxValue;
    }

    private static string ReadSizedUnicodeString(ReadOnlySpan<byte> bytes)
    {
        string raw = Encoding.Unicode.GetString(bytes);
        int terminator = raw.IndexOf('\0');
        return terminator >= 0 ? raw[..terminator] : raw;
    }
}

internal sealed record EmiSelection(ushort Version, ushort ChannelCount, int ChannelIndex, string ChannelName);

internal readonly record struct EmiMeasurementPoint(ulong AbsoluteEnergyPicowattHours, ulong AbsoluteTime100Nanoseconds);
