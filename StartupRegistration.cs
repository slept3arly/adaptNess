using Microsoft.Win32;
using System.Windows.Forms;

namespace AdaptNess;

internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AdaptNess";

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (enabled) key.SetValue(ValueName, BuildCommand());
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            AppLog.Error("Start-with-Windows registration could not be updated.", exception);
            throw;
        }
    }

    private static string BuildCommand()
    {
        var processPath = Environment.ProcessPath ?? Application.ExecutablePath;
        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            return $"\"{processPath}\" \"{Path.Combine(AppContext.BaseDirectory, "AdaptNess.dll")}\"";
        return $"\"{processPath}\"";
    }
}
