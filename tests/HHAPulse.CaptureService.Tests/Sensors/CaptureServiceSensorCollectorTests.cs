using HHAPulse.CaptureService.Sensors;
using Xunit;

namespace HHAPulse.CaptureService.Tests.Sensors;

public sealed class CaptureServiceSensorCollectorTests
{
    [Fact]
    public void DecodeMsiFanRpms_DecodesBigEndianTachometerReadings()
    {
        var raw = new byte[]
        {
            0x01, 0x00, 0xC6, 0x00, 0xC3, 0x00, 0x00, 0x00, 0x00
        };

        var rpms = CaptureServiceSensorCollector.DecodeMsiFanRpms(raw);

        Assert.Equal(new[] { 2424, 2462 }, rpms);
    }

    [Fact]
    public void DecodeMsiDeviceTemperatureCelsius_ReadsSecondByteAsBoundedCelsius()
    {
        var raw = new byte[]
        {
            0x01, 0x40, 0x00, 0x00
        };

        Assert.Equal(64, CaptureServiceSensorCollector.DecodeMsiDeviceTemperatureCelsius(raw));
    }
}
