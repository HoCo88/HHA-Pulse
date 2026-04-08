using System.Text.Json;

namespace HHAPulse.Overlay.Settings;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string settingsPath;

    public SettingsService(string settingsPath)
    {
        this.settingsPath = settingsPath;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(settingsPath))
        {
            return AppSettings.CreateDefault();
        }

        try
        {
            await using var stream = File.OpenRead(settingsPath);
            if (stream.Length == 0)
            {
                return AppSettings.CreateDefault();
            }

            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            return Normalize(settings ?? AppSettings.CreateDefault());
        }
        catch (JsonException)
        {
            // Corrupt or empty settings file — start fresh.
            return AppSettings.CreateDefault();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        // Accept all valid preset values; default to Standard for unrecognized.
        if (!Enum.IsDefined(settings.ActivePreset))
        {
            settings.ActivePreset = OverlayPreset.Standard;
        }

        if (settings.UpdateInterval <= TimeSpan.Zero)
        {
            settings.UpdateInterval = TimeSpan.FromSeconds(1);
        }

        return settings;
    }
}
