using HHAPulse.Shared.Models;

namespace HHAPulse.Overlay.ViewModels;

public sealed class OverlayViewModel : ViewModelBase
{
    private TelemetrySnapshot currentSnapshot = new();

    public TelemetrySnapshot CurrentSnapshot
    {
        get => currentSnapshot;
        private set => SetProperty(ref currentSnapshot, value);
    }

    public void ApplyTelemetry(TelemetrySnapshot snapshot)
    {
        CurrentSnapshot = snapshot;
    }
}
