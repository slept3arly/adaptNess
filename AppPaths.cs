namespace AdaptNess;

internal static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AdaptNess");
    public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");
    public static string LogPath => Path.Combine(DataDirectory, "adaptNess.log");
}
