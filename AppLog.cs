using System.Diagnostics;

namespace AdaptNess;

internal static class AppLog
{
    private static readonly object Gate = new();
    private const long MaximumBytes = 512 * 1024;

    public static void Info(string message) => Write("INFO", message, null);
    public static void Error(string message, Exception exception) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        var line = $"{DateTimeOffset.Now:O} [{level}] {message}";
        if (exception is not null) line += $" {exception.GetType().Name}: {exception.Message}";
        Debug.WriteLine(line);
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.DataDirectory);
                if (File.Exists(AppPaths.LogPath) && new FileInfo(AppPaths.LogPath).Length > MaximumBytes)
                    File.WriteAllText(AppPaths.LogPath, string.Empty);
                File.AppendAllText(AppPaths.LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never interfere with display control or shutdown.
        }
    }
}
