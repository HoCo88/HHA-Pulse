using System.Runtime.InteropServices;
using HHAPulse.Overlay.Collectors.Storage;
using Xunit;

namespace HHAPulse.Overlay.Tests.Collectors;

public sealed class StorageTempCollectorTests
{
    [Fact]
    public void ParseTemperatureDescriptor_ReturnsFirstSensorTemperature()
    {
        var headerSize = Marshal.SizeOf<TestStorageTemperatureDataDescriptorHeader>();
        var infoSize = Marshal.SizeOf<TestStorageTemperatureInfo>();
        var buffer = new byte[headerSize + infoSize];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(new TestStorageTemperatureDataDescriptorHeader
            {
                Version = (uint)headerSize,
                Size = (uint)(headerSize + infoSize),
                WarningTemperature = 70,
                InfoCount = 1
            }, handle.AddrOfPinnedObject(), false);

            Marshal.StructureToPtr(new TestStorageTemperatureInfo
            {
                Index = 0,
                Temperature = 46
            }, handle.AddrOfPinnedObject() + headerSize, false);
        }
        finally
        {
            handle.Free();
        }

        var result = StorageTempCollector.ParseTemperatureDescriptor(buffer, 2);

        Assert.NotNull(result);
        Assert.Equal((uint)2, result.Value.DeviceNumber);
        Assert.Equal((short)46, result.Value.TemperatureCelsius);
        Assert.Equal((short)70, result.Value.TemperatureMaxCelsius);
    }

    [Fact]
    public void ParseTemperatureDescriptor_ReturnsNullWhenNoSensorsExist()
    {
        var headerSize = Marshal.SizeOf<TestStorageTemperatureDataDescriptorHeader>();
        var buffer = new byte[headerSize];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(new TestStorageTemperatureDataDescriptorHeader
            {
                Version = (uint)headerSize,
                Size = (uint)headerSize,
                InfoCount = 0
            }, handle.AddrOfPinnedObject(), false);
        }
        finally
        {
            handle.Free();
        }

        Assert.Null(StorageTempCollector.ParseTemperatureDescriptor(buffer, 0));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TestStorageTemperatureDataDescriptorHeader
    {
        public uint Version;
        public uint Size;
        public short CriticalTemperature;
        public short WarningTemperature;
        public ushort InfoCount;
        public byte Reserved0_0;
        public byte Reserved0_1;
        public uint Reserved1_0;
        public uint Reserved1_1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TestStorageTemperatureInfo
    {
        public ushort Index;
        public short Temperature;
        public short OverThreshold;
        public short UnderThreshold;
        public byte OverThresholdChangable;
        public byte UnderThresholdChangable;
        public byte EventGenerated;
        public byte Reserved0;
        public uint Reserved1;
    }
}
