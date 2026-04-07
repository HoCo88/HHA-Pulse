using HHAPulse.Overlay.Settings;
using Xunit;

namespace HHAPulse.Overlay.Tests.Settings;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task SaveAndLoadAsync_PreservesOverlaySettings()
    {
        var path = Path.Combine(Path.GetTempPath(), "hha-pulse-settings-test", Guid.NewGuid().ToString("N"), "settings.json");
        var service = new SettingsService(path);
        var settings = AppSettings.CreateDefault();
        settings.ActivePreset = OverlayPreset.Tuner;
        settings.TopBarPosition = OverlayEdge.Bottom;
        settings.UpdateInterval = TimeSpan.FromMilliseconds(500);

        await service.SaveAsync(settings, CancellationToken.None);
        var loaded = await service.LoadAsync(CancellationToken.None);

        Assert.Equal(OverlayPreset.Tuner, loaded.ActivePreset);
        Assert.Equal(OverlayEdge.Bottom, loaded.TopBarPosition);
        Assert.Equal(TimeSpan.FromMilliseconds(500), loaded.UpdateInterval);
    }
}
