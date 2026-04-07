namespace HHAPulse.Overlay.Aggregation;

public static class FpsPercentileCalculator
{
    public static FpsPercentileResult Calculate(IReadOnlyCollection<double> frameRates)
    {
        if (frameRates.Count == 0)
        {
            return new FpsPercentileResult(0, 0, 0);
        }

        var sorted = frameRates.OrderBy(value => value).ToArray();
        return new FpsPercentileResult(
            Average: frameRates.Average(),
            OnePercentLow: sorted[PercentileIndex(sorted.Length, 0.01)],
            ZeroPointOnePercentLow: sorted[PercentileIndex(sorted.Length, 0.001)]);
    }

    private static int PercentileIndex(int count, double percentile)
    {
        var index = (int)Math.Floor((count - 1) * percentile);
        return Math.Clamp(index, 0, count - 1);
    }
}

public readonly struct FpsPercentileResult
{
    public FpsPercentileResult(double average, double onePercentLow, double zeroPointOnePercentLow)
    {
        Average = average;
        OnePercentLow = onePercentLow;
        ZeroPointOnePercentLow = zeroPointOnePercentLow;
    }

    public double Average { get; }

    public double OnePercentLow { get; }

    public double ZeroPointOnePercentLow { get; }
}
