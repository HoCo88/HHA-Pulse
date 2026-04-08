using System.Text.Json;

namespace HHAPulse.Overlay.Profiles;

public sealed class ProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string profilesPath;

    public ProfileService(string profilesPath)
    {
        this.profilesPath = profilesPath;
    }

    public async Task<IReadOnlyList<GameProfile>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(profilesPath))
        {
            return Array.Empty<GameProfile>();
        }

        await using var stream = File.OpenRead(profilesPath);
        var result = await JsonSerializer.DeserializeAsync<List<GameProfile>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return result ?? (IReadOnlyList<GameProfile>)Array.Empty<GameProfile>();
    }

    public async Task SaveAsync(IEnumerable<GameProfile> profiles, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(profilesPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(profilesPath);
        await JsonSerializer.SerializeAsync(stream, profiles.ToArray(), JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}
