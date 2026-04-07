using HHAPulse.Overlay.Collectors;
using HHAPulse.Shared.Models;
using Xunit;

namespace HHAPulse.Overlay.Tests.Collectors;

public sealed class CollectorOrchestratorTests
{
    [Fact]
    public async Task CollectAsync_ContinuesWhenCollectorThrows()
    {
        var orchestrator = new CollectorOrchestrator(new IMetricCollector[]
        {
            new ThrowingCollector(),
            new SuccessfulCollector()
        });

        var snapshot = await orchestrator.CollectAsync(CancellationToken.None);

        Assert.True(snapshot.AvailableMetrics.HasFlag(MetricFlags.Battery));
        Assert.Equal(42, snapshot.Battery.ChargePercent);
    }

    private sealed class ThrowingCollector : IMetricCollector
    {
        public string Name => "throwing";

        public bool IsAvailable => true;

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("collector failed");
        }
    }

    private sealed class SuccessfulCollector : IMetricCollector
    {
        public string Name => "successful";

        public bool IsAvailable => true;

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task CollectAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken)
        {
            snapshot.AvailableMetrics |= MetricFlags.Battery;
            snapshot.Battery.ChargePercent = 42;
            return Task.CompletedTask;
        }
    }
}
