using System.Text.Json;

namespace AdaptNess;

internal sealed record AppSettings(
    int MinimumBrightness = 28,
    int MaximumBrightness = 50,
    bool AdaptiveEnabled = true,
    bool StartWithWindows = false)
{
    public AppSettings Normalize()
    {
        var minimum = Math.Clamp(MinimumBrightness, 1, 98);
        var maximum = Math.Clamp(MaximumBrightness, minimum + 1, 99);
        return this with { MinimumBrightness = minimum, MaximumBrightness = maximum };
    }
}

internal static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(AppPaths.SettingsPath)) return new AppSettings();
            return (JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsPath), JsonOptions) ?? new AppSettings()).Normalize();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            AppLog.Error("Settings could not be loaded; using defaults.", exception);
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        File.WriteAllText(AppPaths.SettingsPath, JsonSerializer.Serialize(settings.Normalize(), JsonOptions));
    }
}
