using System.Drawing;
using System.Windows.Forms;

namespace AdaptNess;

internal sealed class AdaptNessApplicationContext : ApplicationContext
{
    private readonly AdaptiveEngineHost engineHost = new();
    private readonly NotifyIcon trayIcon;
    private readonly ToolStripMenuItem enabledItem;
    private readonly ToolStripMenuItem pauseItem;
    private readonly System.Windows.Forms.Timer pauseTimer;
    private AppSettings settings;
    private SettingsForm? settingsForm;
    private bool paused;
    private bool exiting;

    public AdaptNessApplicationContext()
    {
        settings = SettingsStore.Load();
        enabledItem = new ToolStripMenuItem();
        pauseItem = new ToolStripMenuItem("Pause for 15 minutes", null, async (_, _) => await RunUiActionAsync(PauseAsync));
        var menu = new ContextMenuStrip();
        enabledItem.Click += async (_, _) => await RunUiActionAsync(() => SetEnabledAsync(!settings.AdaptiveEnabled || paused));
        menu.Items.Add(enabledItem);
        menu.Items.Add(pauseItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Exit", null, async (_, _) => await RunUiActionAsync(ExitAsync));

        trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = "AdaptNess",
            ContextMenuStrip = menu,
            Visible = true
        };
        trayIcon.MouseUp += async (_, eventArgs) =>
        {
            if (eventArgs.Button == MouseButtons.Left)
                await RunUiActionAsync(() => SetEnabledAsync(!settings.AdaptiveEnabled || paused));
        };

        pauseTimer = new System.Windows.Forms.Timer { Interval = (int)TimeSpan.FromMinutes(15).TotalMilliseconds };
        pauseTimer.Tick += async (_, _) =>
        {
            pauseTimer.Stop();
            paused = false;
            await RunUiActionAsync(() => SetEnabledAsync(true));
        };

        UpdateTrayState();
        if (settings.AdaptiveEnabled) engineHost.Start(settings);
    }

    private async Task SetEnabledAsync(bool enabled)
    {
        if (exiting) return;
        paused = false;
        pauseTimer.Stop();
        settings = settings with { AdaptiveEnabled = enabled };
        SettingsStore.Save(settings);
        if (enabled) engineHost.Start(settings);
        else await engineHost.StopAsync();
        UpdateTrayState();
    }

    private async Task PauseAsync()
    {
        if (!settings.AdaptiveEnabled || paused) return;
        paused = true;
        await engineHost.StopAsync();
        pauseTimer.Start();
        UpdateTrayState();
    }

    private void ShowSettings()
    {
        if (settingsForm is { IsDisposed: false })
        {
            settingsForm.Activate();
            return;
        }
        settingsForm = new SettingsForm(settings, ApplySettingsAsync);
        settingsForm.FormClosed += (_, _) => settingsForm = null;
        settingsForm.Show();
    }

    private async Task ApplySettingsAsync(AppSettings proposed)
    {
        proposed = proposed.Normalize();
        var restartRequired = settings.MinimumBrightness != proposed.MinimumBrightness ||
                              settings.MaximumBrightness != proposed.MaximumBrightness;
        try
        {
            StartupRegistration.SetEnabled(proposed.StartWithWindows);
            settings = proposed;
            SettingsStore.Save(settings);
            paused = false;
            pauseTimer.Stop();
            if (!settings.AdaptiveEnabled)
                await engineHost.StopAsync();
            else if (restartRequired && engineHost.IsRunning)
            {
                await engineHost.StopAsync();
                engineHost.Start(settings);
            }
            else if (!engineHost.IsRunning)
                engineHost.Start(settings);
            UpdateTrayState();
        }
        catch
        {
            UpdateTrayState();
            throw;
        }
    }

    private async Task ExitAsync()
    {
        if (exiting) return;
        exiting = true;
        pauseTimer.Stop();
        trayIcon.Visible = false;
        try { await engineHost.StopAsync(); }
        finally
        {
            trayIcon.Dispose();
            pauseTimer.Dispose();
            engineHost.DisposeAsync().AsTask().GetAwaiter().GetResult();
            ExitThread();
        }
    }

    private void UpdateTrayState()
    {
        var active = settings.AdaptiveEnabled && !paused;
        enabledItem.Text = active ? "Disable adaptive brightness" : "Enable adaptive brightness";
        pauseItem.Enabled = active;
        trayIcon.Text = paused ? "AdaptNess (paused)" : active ? "AdaptNess (enabled)" : "AdaptNess (disabled)";
    }

    private async Task RunUiActionAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception exception)
        {
            AppLog.Error("Tray action failed.", exception);
            trayIcon.ShowBalloonTip(3000, "AdaptNess", "The requested action could not be completed. See the local log for details.", ToolTipIcon.Error);
        }
    }
}
