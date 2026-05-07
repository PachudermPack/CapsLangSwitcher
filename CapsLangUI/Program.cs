using System;
using System.Drawing;
using System.Windows.Forms;

using WinTimer = System.Windows.Forms.Timer;

class OverlayForm : Form
{
    const int WM_LANG_UPDATE = 0x8001;

    readonly Label textLabel;
    readonly WinTimer hideTimer;

    bool initialized = false;

    public OverlayForm()
    {
        Text = "CapsLangOverlay";

        AutoScaleMode = AutoScaleMode.Dpi;

        ShowInTaskbar = false;

        FormBorderStyle = FormBorderStyle.None;

        StartPosition = FormStartPosition.Manual;

        TopMost = true;

        float scale = DeviceDpi / 96f;

        Width = (int)(320 * scale);

        Height = (int)(110 * scale);

        BackColor = Color.Black;

        Opacity = 0.90;

        var area = Screen.PrimaryScreen!.WorkingArea;

        Location = new Point(
            area.Right - Width - (int)(40 * scale),
            area.Bottom - Height - (int)(40 * scale)
        );

        textLabel = new Label();

        textLabel.Dock = DockStyle.Fill;

        textLabel.TextAlign =
            ContentAlignment.MiddleCenter;

        textLabel.ForeColor = Color.White;

        textLabel.BackColor = Color.Black;

        textLabel.Font = new Font(
            "Segoe UI",
            30f * scale,
            FontStyle.Bold
        );

        Controls.Add(textLabel);

        hideTimer = new WinTimer();

        hideTimer.Interval = 700;

        hideTimer.Tick += (_, _) =>
        {
            hideTimer.Stop();

            Hide();
        };

        Shown += (_, _) =>
        {
            Hide();

            initialized = true;
        };
    }

    protected override bool ShowWithoutActivation
        => true;

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

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_LANG_UPDATE && initialized)
        {
            bool ru = m.WParam == (IntPtr)1;

            textLabel.Text =
                ru
                ? "РУС"
                : "ENG";

            Show();

            BringToFront();

            hideTimer.Stop();

            hideTimer.Start();
        }

        base.WndProc(ref m);
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();

        Application.SetCompatibleTextRenderingDefault(false);

        Application.Run(new OverlayForm());
    }
}