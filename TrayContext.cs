using System;
using System.Drawing;
using System.Windows.Forms;

sealed class TrayContext : ApplicationContext
{
    readonly NotifyIcon tray;
    readonly ToolStripMenuItem overlayItem;
    readonly ToolStripMenuItem textItem;
    readonly LanguageOverlay overlay;
    readonly LanguageMonitor monitor;
    readonly CapsLockSwitcher switcher;

    public TrayContext()
    {
        overlay = new LanguageOverlay();
        _ = overlay.Handle;

        monitor = new LanguageMonitor();
        monitor.LanguageChanged += ru =>
        {
            if (overlayItem.Checked)
                overlay.ShowLanguage(ru);
        };

        switcher = new CapsLockSwitcher();

        overlayItem = new ToolStripMenuItem("Показывать оверлей")
        {
            CheckOnClick = true,
            Checked = true
        };

        textItem = new ToolStripMenuItem("Конвертация выделения (Ctrl + CapsLock)")
        {
            CheckOnClick = true,
            Checked = true
        };
        textItem.CheckedChanged += (_, __) => switcher.TextConversionEnabled = textItem.Checked;

        var menu = new ContextMenuStrip();
        menu.Items.Add(overlayItem);
        menu.Items.Add(textItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, __) => ExitThread());

        tray = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
            Text = "CLS",
            ContextMenuStrip = menu,
            Visible = true
        };

        switcher.Install();
        monitor.Start();
    }

    protected override void ExitThreadCore()
    {
        monitor.Dispose();
        switcher.Dispose();
        tray.Visible = false;
        tray.Dispose();
        overlay.Dispose();

        base.ExitThreadCore();
    }
}
