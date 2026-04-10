using HHAPulse.Overlay.Aggregation;
using HHAPulse.Shared.Models;
using HHAPulse.Overlay.Settings;

namespace HHAPulse.Overlay.ViewModels;

public sealed class OverlayViewModel : ViewModelBase
{
    // Sparkline history is deliberately short so the graph reflects the
    // RECENT state (last ~30 seconds at the default 1 Hz tick interval),
    // not the full session. A larger window ages out slowly and startup
    // spikes from collector warm-up or frame-gen ramp-up skew the scale
    // of the sparkline for minutes afterwards, making the recent state
    // unreadable. 30 samples is enough to see short-term trends and
    // low-fps dips without the old noise dominating the display.
    private const int HistorySampleCapacity = 30;
    private readonly RingBuffer<double> fpsHistoryBuffer = new(HistorySampleCapacity);
    private readonly RingBuffer<double> avgFpsHistoryBuffer = new(HistorySampleCapacity);
    private readonly RingBuffer<double> onePercentLowHistoryBuffer = new(HistorySampleCapacity);
    private readonly RingBuffer<double> zeroPointOneLowHistoryBuffer = new(HistorySampleCapacity);
    private readonly RingBuffer<double> frameTimeHistoryBuffer = new(HistorySampleCapacity);

    private TelemetrySnapshot currentSnapshot = new();
    private OverlayPreset activePreset = OverlayPreset.Standard;
    private IReadOnlyList<string> topBarMetricIds = OverlayPresetCatalog.GetLayout(OverlayPreset.Standard, Array.Empty<string>()).TopBarMetricIds;
    private IReadOnlyList<double> fpsHistory = Array.Empty<double>();
    private IReadOnlyList<double> avgFpsHistory = Array.Empty<double>();
    private IReadOnlyList<double> onePercentLowHistory = Array.Empty<double>();
    private IReadOnlyList<double> zeroPointOneLowHistory = Array.Empty<double>();
    private IReadOnlyList<double> frameTimeHistory = Array.Empty<double>();

    public TelemetrySnapshot CurrentSnapshot
    {
        get => currentSnapshot;
        private set => SetProperty(ref currentSnapshot, value);
    }

    public OverlayPreset ActivePreset
    {
        get => activePreset;
        private set => SetProperty(ref activePreset, value);
    }

    public IReadOnlyList<string> TopBarMetricIds
    {
        get => topBarMetricIds;
        private set => SetProperty(ref topBarMetricIds, value);
    }

    public IReadOnlyList<double> FpsHistory
    {
        get => fpsHistory;
        private set => SetProperty(ref fpsHistory, value);
    }

    public IReadOnlyList<double> AvgFpsHistory
    {
        get => avgFpsHistory;
        private set => SetProperty(ref avgFpsHistory, value);
    }

    public IReadOnlyList<double> OnePercentLowHistory
    {
        get => onePercentLowHistory;
        private set => SetProperty(ref onePercentLowHistory, value);
    }

    public IReadOnlyList<double> ZeroPointOneLowHistory
    {
        get => zeroPointOneLowHistory;
        private set => SetProperty(ref zeroPointOneLowHistory, value);
    }

    public IReadOnlyList<double> FrameTimeHistory
    {
        get => frameTimeHistory;
        private set => SetProperty(ref frameTimeHistory, value);
    }

    public void ApplySettings(AppSettings settings)
    {
        ActivePreset = settings.ActivePreset;
        var layout = OverlayPresetCatalog.GetLayout(settings.ActivePreset, settings.EnabledMetricIds);
        TopBarMetricIds = layout.TopBarMetricIds;
    }

    public void ApplyTelemetry(TelemetrySnapshot snapshot)
    {
        UpdateHistory(snapshot);
        CurrentSnapshot = snapshot;
    }

    private void UpdateHistory(TelemetrySnapshot snapshot)
    {
        if (snapshot.AvailableMetrics.HasFlag(MetricFlags.Fps))
        {
            if (snapshot.Performance.FramesPerSecond > 0)
            {
                fpsHistoryBuffer.Add(snapshot.Performance.FramesPerSecond);
                FpsHistory = fpsHistoryBuffer.ToArray();
            }

            if (snapshot.Performance.AverageFramesPerSecond > 0)
            {
                avgFpsHistoryBuffer.Add(snapshot.Performance.AverageFramesPerSecond);
                AvgFpsHistory = avgFpsHistoryBuffer.ToArray();
            }

            if (snapshot.Performance.OnePercentLowFramesPerSecond > 0)
            {
                onePercentLowHistoryBuffer.Add(snapshot.Performance.OnePercentLowFramesPerSecond);
                OnePercentLowHistory = onePercentLowHistoryBuffer.ToArray();
            }

            if (snapshot.Performance.ZeroPointOnePercentLowFramesPerSecond > 0)
            {
                zeroPointOneLowHistoryBuffer.Add(snapshot.Performance.ZeroPointOnePercentLowFramesPerSecond);
                ZeroPointOneLowHistory = zeroPointOneLowHistoryBuffer.ToArray();
            }
        }

        if (snapshot.AvailableMetrics.HasFlag(MetricFlags.FrameTime) &&
            snapshot.Performance.FrameTimeMilliseconds > 0)
        {
            frameTimeHistoryBuffer.Add(snapshot.Performance.FrameTimeMilliseconds);
            FrameTimeHistory = frameTimeHistoryBuffer.ToArray();
        }
    }
}
