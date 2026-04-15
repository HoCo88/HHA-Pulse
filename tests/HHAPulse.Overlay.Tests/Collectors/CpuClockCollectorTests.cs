using System.Runtime.InteropServices;
using HHAPulse.Overlay.Collectors.Cpu;
using Xunit;

namespace HHAPulse.Overlay.Tests.Collectors;

public sealed class CpuClockCollectorTests
{
    [Fact]
    public void ParseProcessorPowerInformation_ReturnsPerCoreNominalData()
    {
        var size = Marshal.SizeOf<TestProcessorPowerInformation>();
        var buffer = new byte[size * 2];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(new TestProcessorPowerInformation
            {
                Number = 0,
                MaxMhz = 3200,
                CurrentMhz = 3200,
                MhzLimit = 5000
            }, handle.AddrOfPinnedObject(), false);

            Marshal.StructureToPtr(new TestProcessorPowerInformation
            {
                Number = 1,
                MaxMhz = 3200,
                CurrentMhz = 3100,
                MhzLimit = 5000
            }, handle.AddrOfPinnedObject() + size, false);
        }
        finally
        {
            handle.Free();
        }

        var result = CpuClockCollector.ParseProcessorPowerInformation(buffer);

        Assert.Equal(2, result.Count);
        Assert.Equal(0, result[0].CoreIndex);
        Assert.Equal((uint)3200, result[0].CurrentMhz);
        Assert.Equal((uint)3100, result[1].CurrentMhz);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TestProcessorPowerInformation
    {
        public uint Number;
        public uint MaxMhz;
        public uint CurrentMhz;
        public uint MhzLimit;
        public uint MaxIdleState;
        public uint CurrentIdleState;
    }
}
