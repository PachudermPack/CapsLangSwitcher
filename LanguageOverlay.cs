using System;
using System.Drawing;
using System.Windows.Forms;

using WinTimer = System.Windows.Forms.Timer;

sealed class LanguageOverlay : Form
{
    readonly Label textLabel;
    readonly WinTimer hideTimer;

    public LanguageOverlay()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.Black;
        Opacity = 0.90;

        float scale = DeviceDpi / 96f;

        Width = (int)(320 * scale);
        Height = (int)(110 * scale);

        var area = Screen.PrimaryScreen.WorkingArea;

        Location = new Point(
            area.Right - Width - (int)(40 * scale),
            area.Bottom - Height - (int)(40 * scale)
        );

        textLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            BackColor = Color.Black,
            Font = new Font("Segoe UI", 30f * scale, FontStyle.Bold)
        };

        Controls.Add(textLabel);

        hideTimer = new WinTimer { Interval = 700 };
        hideTimer.Tick += (_, __) =>
        {
            hideTimer.Stop();
            Hide();
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;

            cp.ExStyle |= 0x08000000;
            cp.ExStyle |= 0x00000020;
            cp.ExStyle |= 0x00000080;

            return cp;
        }
    }

    public void ShowLanguage(bool ru)
    {
        textLabel.Text = ru ? "РУС" : "ENG";

        Show();
        BringToFront();

        hideTimer.Stop();
        hideTimer.Start();
    }
}
