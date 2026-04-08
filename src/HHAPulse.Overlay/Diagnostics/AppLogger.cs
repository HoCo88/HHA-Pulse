namespace HHAPulse.Overlay.Diagnostics;

internal static class AppLogger
{
    private static readonly object SyncRoot = new();

    internal static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HHAPulse",
        "logs",
        "overlay.log");

    internal static void Info(string message)
    {
        Write("INFO", message, null);
    }

    internal static void Error(string message, Exception exception)
    {
        Write("ERROR", message, exception);
    }

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = $"{DateTimeOffset.UtcNow:O} [{level}] {message}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            lock (SyncRoot)
            {
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never bring down the overlay.
        }
    }
}
