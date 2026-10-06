using System.Management;
using System.Runtime.Versioning;

namespace AdaptNess;

internal sealed record BrightnessSettings(int Minimum = 25, int Maximum = 85);

[SupportedOSPlatform("windows")]
internal sealed class BrightnessController : IDisposable
{
    private readonly BrightnessSettings settings;
    private bool failed;

    public BrightnessController(BrightnessSettings settings) => this.settings = settings;
    public bool IsUnavailable => failed;

    public bool TryGetCurrentBrightness(out int percentage)
    {
        percentage = 0;
        if (failed) return false;
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            foreach (ManagementObject monitor in searcher.Get())
            {
                percentage = Convert.ToInt32(monitor["CurrentBrightness"]);
                return true;
            }
        }
        catch (Exception exception) when (exception is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            AppLog.Error("Brightness read failed.", exception);
        }
        return false;
    }

    public bool SetBrightness(int percentage)
    {
        if (failed) return false;
        var clamped = Math.Clamp(percentage, settings.Minimum, settings.Maximum);
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            var changed = false;
            foreach (ManagementObject monitor in searcher.Get())
            {
                monitor.InvokeMethod("WmiSetBrightness", new object[] { 1, (byte)clamped });
                changed = true;
            }
            if (changed)
            {
                AppLog.Info($"Brightness changed to {clamped}%.");
            }
            return changed;
        }
        catch (Exception exception) when (exception is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            failed = true;
            AppLog.Error("Brightness control failed; adaptive changes disabled.", exception);
            return false;
        }
    }

    public void Dispose() { }
}
