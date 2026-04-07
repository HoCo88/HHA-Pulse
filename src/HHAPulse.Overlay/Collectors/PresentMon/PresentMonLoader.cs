namespace HHAPulse.Overlay.Collectors.PresentMon;

public sealed class PresentMonLoader
{
    public bool IsAvailable { get; private set; }

    public string StatusMessage { get; private set; } = "PresentMon has not been checked.";

    public void TryInitialize()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var sdkPath = Path.Combine(programFiles, "Intel", "PresentMon", "SDK", "PresentMonAPI2Loader.dll");
        if (File.Exists(sdkPath))
        {
            IsAvailable = true;
            StatusMessage = "PresentMon SDK loader detected.";
            return;
        }

        IsAvailable = false;
        StatusMessage = "Install PresentMon for FPS, frametime, latency, and FrameGen metrics.";
    }
}
