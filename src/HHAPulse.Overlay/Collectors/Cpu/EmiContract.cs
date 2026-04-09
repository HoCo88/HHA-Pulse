using System.Buffers.Binary;
using System.Text;

namespace HHAPulse.Overlay.Collectors.Cpu;

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

    internal static EmiSelection? TryParseSelection(ushort version, ReadOnlySpan<byte> metadata)
    {
        if (version <= VersionV1)
        {
            return TryParseV1(metadata);
        }

        return TryParseV2(version, metadata);
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

    private static EmiSelection? TryParseV1(ReadOnlySpan<byte> metadata)
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
        if (GetChannelRank(name) == int.MaxValue)
        {
            return null;
        }

        return new EmiSelection(VersionV1, 1, 0, name);
    }

    private static EmiSelection? TryParseV2(ushort version, ReadOnlySpan<byte> metadata)
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

            int rank = GetChannelRank(name);
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

    private static int GetChannelRank(string channelName)
    {
        if (string.IsNullOrWhiteSpace(channelName))
        {
            return int.MaxValue;
        }

        string normalized = channelName.ToUpperInvariant();
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

    private static string ReadSizedUnicodeString(ReadOnlySpan<byte> bytes)
    {
        string raw = Encoding.Unicode.GetString(bytes);
        int terminator = raw.IndexOf('\0');
        return terminator >= 0 ? raw[..terminator] : raw;
    }
}

internal sealed record EmiSelection(ushort Version, ushort ChannelCount, int ChannelIndex, string ChannelName);

internal readonly record struct EmiMeasurementPoint(ulong AbsoluteEnergyPicowattHours, ulong AbsoluteTime100Nanoseconds);
