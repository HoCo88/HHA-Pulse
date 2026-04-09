using HHAPulse.Overlay.Diagnostics;
using HHAPulse.Shared.Models;
using Xunit;

namespace HHAPulse.Overlay.Tests.Diagnostics;

public sealed class TelemetryContractGuardTests
{
    [Fact]
    public void GetCurrentInfo_ReportsCurrentSharedContractAsValid()
    {
        var info = TelemetryContractGuard.GetCurrentInfo();

        Assert.True(info.IsValid);
        Assert.Contains("Telemetry contract OK", info.StatusMessage);
        Assert.False(string.IsNullOrWhiteSpace(info.AssemblyPath));
        Assert.False(string.IsNullOrWhiteSpace(info.ModuleVersionId));
    }

    [Fact]
    public void ApplyTo_PopulatesRuntimeBinaryIdentity()
    {
        var snapshot = new TelemetrySnapshot();

        TelemetryContractGuard.ApplyTo(snapshot);

        Assert.True(snapshot.Dependencies.TelemetryContractValid);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.Dependencies.SharedAssemblyPath));
        Assert.False(string.IsNullOrWhiteSpace(snapshot.Dependencies.SharedAssemblyMvid));
    }
}
