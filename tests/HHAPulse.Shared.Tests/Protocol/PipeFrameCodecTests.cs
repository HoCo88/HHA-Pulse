using HHAPulse.Shared.Protocol;
using Xunit;

namespace HHAPulse.Shared.Tests.Protocol;

public sealed class PipeFrameCodecTests
{
    [Fact]
    public void EncodeDecodeFrame_PreservesPayload()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5 };

        var frame = PipeFrameCodec.Encode(payload);
        var decoded = PipeFrameCodec.Decode(frame);

        Assert.Equal(payload, decoded);
    }

    [Fact]
    public void DecodeFrame_RejectsPayloadLongerThanFrame()
    {
        var invalidFrame = new byte[] { 8, 0, 0, 0, 1, 2 };

        Assert.Throws<InvalidDataException>(() => PipeFrameCodec.Decode(invalidFrame));
    }

    [Fact]
    public void EncodeFrame_RejectsOversizedPayload()
    {
        var payload = new byte[PipeConstants.MaxPayloadBytes + 1];

        Assert.Throws<InvalidDataException>(() => PipeFrameCodec.Encode(payload));
    }
}
