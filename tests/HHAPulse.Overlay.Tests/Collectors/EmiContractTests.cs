using HHAPulse.Overlay.Collectors.Cpu;
using Xunit;

namespace HHAPulse.Overlay.Tests.Collectors;

public sealed class EmiContractTests
{
    [Fact]
    public void TryParseSelection_V1PackageChannel_IsAccepted()
    {
        byte[] metadata = BuildV1Metadata("CPU Package");

        EmiSelection? selection = EmiContract.TryParseSelection(EmiContract.VersionV1, metadata);

        Assert.NotNull(selection);
        Assert.Equal(0, selection!.ChannelIndex);
        Assert.Equal((ushort)1, selection.ChannelCount);
        Assert.Equal("CPU Package", selection.ChannelName);
    }

    [Fact]
    public void TryParseSelection_V2PrefersPackageOverCpu()
    {
        byte[] metadata = BuildV2Metadata("CPU IA", "RAPL Package");

        EmiSelection? selection = EmiContract.TryParseSelection(EmiContract.VersionV2, metadata);

        Assert.NotNull(selection);
        Assert.Equal(1, selection!.ChannelIndex);
        Assert.Equal("RAPL Package", selection.ChannelName);
    }

    [Fact]
    public void TryParseSelection_V2AmbiguousBestMatch_FailsClosed()
    {
        byte[] metadata = BuildV2Metadata("CPU Package", "RAPL Package");

        EmiSelection? selection = EmiContract.TryParseSelection(EmiContract.VersionV2, metadata);

        Assert.Null(selection);
    }

    [Fact]
    public void TryComputeWatts_UsesPicowattHoursAndDeviceTime()
    {
        var previous = new EmiMeasurementPoint(1_000_000_000, 10_000_000);
        var current = new EmiMeasurementPoint(2_000_000_000, 30_000_000);

        double? watts = EmiContract.TryComputeWatts(previous, current);

        Assert.NotNull(watts);
        Assert.Equal(1.8d, watts!.Value, 6);
    }

    private static byte[] BuildV1Metadata(string name)
    {
        byte[] nameBytes = EncodeWideString(name);
        byte[] buffer = new byte[72 + nameBytes.Length];
        BitConverter.GetBytes(0).CopyTo(buffer, 0);
        BitConverter.GetBytes((ushort)1).CopyTo(buffer, 68);
        BitConverter.GetBytes((ushort)nameBytes.Length).CopyTo(buffer, 70);
        nameBytes.CopyTo(buffer, 72);
        return buffer;
    }

    private static byte[] BuildV2Metadata(params string[] channelNames)
    {
        int totalLength = 68;
        byte[][] channelBuffers = new byte[channelNames.Length][];
        for (int i = 0; i < channelNames.Length; i++)
        {
            byte[] nameBytes = EncodeWideString(channelNames[i]);
            byte[] channel = new byte[6 + nameBytes.Length];
            BitConverter.GetBytes(0).CopyTo(channel, 0);
            BitConverter.GetBytes((ushort)nameBytes.Length).CopyTo(channel, 4);
            nameBytes.CopyTo(channel, 6);
            channelBuffers[i] = channel;
            totalLength += channel.Length;
        }

        byte[] buffer = new byte[totalLength];
        BitConverter.GetBytes((ushort)1).CopyTo(buffer, 64);
        BitConverter.GetBytes((ushort)channelNames.Length).CopyTo(buffer, 66);

        int offset = 68;
        foreach (byte[] channel in channelBuffers)
        {
            channel.CopyTo(buffer, offset);
            offset += channel.Length;
        }

        return buffer;
    }

    private static byte[] EncodeWideString(string value)
    {
        return System.Text.Encoding.Unicode.GetBytes(value + '\0');
    }
}
