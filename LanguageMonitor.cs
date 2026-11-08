using System;
using System.Windows.Forms;

sealed class LanguageMonitor : IDisposable
{
    public const int RussianLangId = 0x0419;

    readonly Timer timer;

    IntPtr lastWindow = IntPtr.Zero;
    int? lastLangId;

    public event Action<bool> LanguageChanged;

    public LanguageMonitor(int intervalMs = 100)
    {
        timer = new Timer { Interval = intervalMs };
        timer.Tick += (_, __) => Poll();
    }

    public void Start()
    {
        timer.Start();
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Dispose();
    }

    public static int LangIdOf(IntPtr hkl)
    {
        return (int)(hkl.ToInt64() & 0xFFFF);
    }

    public static int ReadForegroundLangId(out IntPtr hwnd)
    {
        hwnd = NativeMethods.GetForegroundWindow();

        if (hwnd == IntPtr.Zero)
            return -1;

        uint threadId = NativeMethods.GetWindowThreadProcessId(hwnd, IntPtr.Zero);
        IntPtr hkl = NativeMethods.GetKeyboardLayout(threadId);

        return hkl == IntPtr.Zero ? -1 : LangIdOf(hkl);
    }

    void Poll()
    {
        int langId = ReadForegroundLangId(out IntPtr hwnd);

        if (langId < 0)
            return;

        if (hwnd != lastWindow)
        {
            lastWindow = hwnd;
            lastLangId = langId;
            return;
        }

        if (langId != lastLangId)
        {
            lastLangId = langId;
            LanguageChanged?.Invoke(langId == RussianLangId);
        }
    }
}
