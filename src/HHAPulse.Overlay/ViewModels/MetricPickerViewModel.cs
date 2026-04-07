namespace HHAPulse.Overlay.ViewModels;

public sealed class MetricPickerViewModel : ViewModelBase
{
    public IList<string> EnabledMetricIds { get; } = new List<string>();

    public void Toggle(string metricId)
    {
        if (EnabledMetricIds.Contains(metricId))
        {
            EnabledMetricIds.Remove(metricId);
        }
        else
        {
            EnabledMetricIds.Add(metricId);
        }
    }
}
