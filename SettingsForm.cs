using System.Drawing;
using System.Windows.Forms;

namespace AdaptNess;

internal sealed class SettingsForm : Form
{
    private readonly NumericUpDown minimum;
    private readonly NumericUpDown maximum;
    private readonly CheckBox adaptiveEnabled;
    private readonly CheckBox startWithWindows;
    private readonly Func<AppSettings, Task> save;

    public SettingsForm(AppSettings settings, Func<AppSettings, Task> save)
    {
        this.save = save;
        Text = "AdaptNess Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(330, 190);

        minimum = new NumericUpDown { Minimum = 1, Maximum = 98, Value = settings.MinimumBrightness, Dock = DockStyle.Fill };
        maximum = new NumericUpDown { Minimum = 2, Maximum = 99, Value = settings.MaximumBrightness, Dock = DockStyle.Fill };
        adaptiveEnabled = new CheckBox { Text = "Enable adaptive brightness", Checked = settings.AdaptiveEnabled, AutoSize = true };
        startWithWindows = new CheckBox { Text = "Start with Windows", Checked = settings.StartWithWindows, AutoSize = true };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        layout.Controls.Add(adaptiveEnabled, 0, 0);
        layout.SetColumnSpan(adaptiveEnabled, 2);
        layout.Controls.Add(new Label { Text = "Minimum automatic brightness", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(minimum, 1, 1);
        layout.Controls.Add(new Label { Text = "Maximum automatic brightness", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(maximum, 1, 2);
        layout.Controls.Add(startWithWindows, 0, 3);
        layout.SetColumnSpan(startWithWindows, 2);
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var apply = new Button { Text = "Save", AutoSize = true };
        apply.Click += SaveClicked;
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(apply);
        layout.Controls.Add(buttons, 0, 4);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = apply;
        CancelButton = cancel;
    }

    private async void SaveClicked(object? sender, EventArgs eventArgs)
    {
        if (minimum.Value >= maximum.Value)
        {
            MessageBox.Show("Maximum brightness must be greater than minimum brightness.", "AdaptNess", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Enabled = false;
        try
        {
            await save(new AppSettings((int)minimum.Value, (int)maximum.Value, adaptiveEnabled.Checked, startWithWindows.Checked));
            Close();
        }
        catch (Exception exception)
        {
            AppLog.Error("Settings could not be applied.", exception);
            MessageBox.Show("Settings could not be applied. See the local AdaptNess log for details.", "AdaptNess", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Enabled = true;
        }
    }
}
