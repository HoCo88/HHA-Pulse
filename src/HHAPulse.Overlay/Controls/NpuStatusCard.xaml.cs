using HHAPulse.Shared.Models;
using Microsoft.UI.Xaml.Controls;

namespace HHAPulse.Overlay.Controls;

public sealed partial class NpuStatusCard : UserControl
{
    public NpuStatusCard()
    {
        InitializeComponent();
    }

    public void Apply(TelemetrySnapshot snapshot)
    {
        if (snapshot.Npu.Present)
        {
            HeadlineText.Text = snapshot.Npu.AdapterName;
            DetailText.Text = $"{snapshot.Npu.DriverDescription}. Utilization unavailable on current Windows.";
        }
        else
        {
            HeadlineText.Text = "NPU not detected";
            DetailText.Text = "Utilization unavailable on current Windows.";
        }
    }
}
