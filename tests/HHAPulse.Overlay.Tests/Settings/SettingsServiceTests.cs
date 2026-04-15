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
        settings.ActivePreset = OverlayPreset.Off;
        settings.TopBarPosition = TopBarPosition.BottomThin;
        settings.UpdateInterval = TimeSpan.FromMilliseconds(500);
        settings.TextSizePixels = 18;

        await service.SaveAsync(settings, CancellationToken.None);
        var loaded = await service.LoadAsync(CancellationToken.None);

        Assert.Equal(OverlayPreset.Off, loaded.ActivePreset);
        Assert.Equal(TopBarPosition.BottomThin, loaded.TopBarPosition);
        Assert.Equal(TimeSpan.FromMilliseconds(500), loaded.UpdateInterval);
        Assert.Equal(18, loaded.TextSizePixels);
    }

    [Theory]
    [InlineData(0, TopBarPosition.TopThin)]
    [InlineData(1, TopBarPosition.BottomThin)]
    public async Task LoadAsync_MigratesLegacyTopBottomPositionValues(int persistedValue, TopBarPosition expected)
    {
        var directory = Path.Combine(Path.GetTempPath(), "hha-pulse-settings-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        await File.WriteAllTextAsync(path, $$"""
            {
              "ActivePreset": 1,
              "TopBarPosition": {{persistedValue}},
              "BackgroundOpacity": 0.85,
              "TextOpacity": 1.0,
              "FontSize": 1,
              "TextSizePixels": 15,
              "UpdateInterval": "00:00:01",
              "EnabledMetricIds": [],
              "ShowMode": 0
            }
            """);

        var service = new SettingsService(path);

        var loaded = await service.LoadAsync(CancellationToken.None);

        Assert.Equal(expected, loaded.TopBarPosition);
    }
}
