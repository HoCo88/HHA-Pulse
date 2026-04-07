namespace HHAPulse.Overlay.Aggregation;

public sealed class BatteryPredictor
{
    private readonly TimeSpan window;
    private readonly Queue<BatterySample> samples = new();

    public BatteryPredictor(TimeSpan window)
    {
        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "Window must be positive.");
        }

        this.window = window;
    }

    public void AddSample(DateTimeOffset timestamp, double remainingWh, double dischargeWatts)
    {
        samples.Enqueue(new BatterySample(timestamp, remainingWh, dischargeWatts));
        Prune(timestamp);
    }

    public double? EstimateMinutesRemaining(double remainingWh)
    {
        var positiveSamples = samples.Where(sample => sample.DischargeWatts > 0).ToArray();
        if (positiveSamples.Length == 0 || remainingWh <= 0)
        {
            return null;
        }

        var averageWatts = positiveSamples.Average(sample => sample.DischargeWatts);
        if (averageWatts <= 0)
        {
            return null;
        }

        return remainingWh / averageWatts * 60;
    }

    private void Prune(DateTimeOffset now)
    {
        while (samples.Count > 0 && now - samples.Peek().Timestamp > window)
        {
            samples.Dequeue();
        }
    }

    private readonly struct BatterySample
    {
        public BatterySample(DateTimeOffset timestamp, double remainingWh, double dischargeWatts)
        {
            Timestamp = timestamp;
            RemainingWh = remainingWh;
            DischargeWatts = dischargeWatts;
        }

        public DateTimeOffset Timestamp { get; }

        public double RemainingWh { get; }

        public double DischargeWatts { get; }
    }
}
