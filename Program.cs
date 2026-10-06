using System.Runtime.Versioning;

using System.Windows.Forms;

namespace AdaptNess;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            SelfTests.Run();
            return;
        }

        using var instanceMutex = new Mutex(true, @"Local\AdaptNess", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("AdaptNess is already running.", "AdaptNess", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        AppLog.Info("AdaptNess starting.");
        Application.Run(new AdaptNessApplicationContext());
        AppLog.Info("AdaptNess stopped.");
    }
}
