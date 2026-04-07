namespace HHAPulse.Overlay.ViewModels;

public sealed class SidePanelViewModel : ViewModelBase
{
    private bool isOpen;

    public bool IsOpen
    {
        get => isOpen;
        set => SetProperty(ref isOpen, value);
    }
}
