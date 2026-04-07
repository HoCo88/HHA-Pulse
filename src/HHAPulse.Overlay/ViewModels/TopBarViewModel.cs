namespace HHAPulse.Overlay.ViewModels;

public sealed class TopBarViewModel : ViewModelBase
{
    private string lineOne = "--";
    private string lineTwo = string.Empty;

    public string LineOne
    {
        get => lineOne;
        set => SetProperty(ref lineOne, value);
    }

    public string LineTwo
    {
        get => lineTwo;
        set => SetProperty(ref lineTwo, value);
    }
}
