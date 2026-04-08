using HHAPulse.Overlay.Aggregation;
using HHAPulse.Shared.Models;
using HHAPulse.Overlay.Settings;

namespace HHAPulse.Overlay.ViewModels;

public sealed class OverlayViewModel : ViewModelBase
{
    private const int HistorySampleCapacity = 120;
    private readonly RingBuffer<double> fpsHistoryBuffer = new(HistorySampleCapacity);
    private readonly RingBuffer<double> frameTimeHistoryBuffer = new(HistorySampleCapacity);

    private TelemetrySnapshot currentSnapshot = new();
    private OverlayPreset activePreset = OverlayPreset.Hud;
    private IReadOnlyList<string> topBarMetricIds = OverlayPresetCatalog.GetLayout(OverlayPreset.Hud, Array.Empty<string>()).TopBarMetricIds;
    private IReadOnlyList<double> fpsHistory = Array.Empty<double>();
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
        if (snapshot.AvailableMetrics.HasFlag(MetricFlags.Fps) &&
            snapshot.Performance.FramesPerSecond > 0)
        {
            fpsHistoryBuffer.Add(snapshot.Performance.FramesPerSecond);
            FpsHistory = fpsHistoryBuffer.ToArray();
        }

        if (snapshot.AvailableMetrics.HasFlag(MetricFlags.FrameTime) &&
            snapshot.Performance.FrameTimeMilliseconds > 0)
        {
            frameTimeHistoryBuffer.Add(snapshot.Performance.FrameTimeMilliseconds);
            FrameTimeHistory = frameTimeHistoryBuffer.ToArray();
        }
    }
}
