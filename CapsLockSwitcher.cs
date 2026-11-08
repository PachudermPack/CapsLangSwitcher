using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

sealed class CapsLockSwitcher : IDisposable
{
    const int WH_KEYBOARD_LL = 13;
    const int WM_KEYDOWN = 0x0100;
    const int WM_KEYUP = 0x0101;
    const int WM_INPUTLANGCHANGEREQUEST = 0x0050;
    const int VK_CAPITAL = 0x14;
    const int VK_CONTROL = 0x11;
    const byte VK_LWIN = 0x5B;
    const byte VK_SPACE = 0x20;
    const uint KEYEVENTF_KEYUP = 0x0002;
    const int ExtraInfoOffset = 16;
    const uint OwnInputMarker = 0x434C53;

    readonly IntPtr hklRu;
    readonly IntPtr hklEnUs;
    readonly NativeMethods.LowLevelKeyboardProc hookProc;
    readonly HashSet<uint> nativeFailedThreads = new HashSet<uint>();
    readonly object gate = new object();

    IntPtr hookId = IntPtr.Zero;
    bool capsHandled;

    public bool TextConversionEnabled { get; set; } = true;

    public CapsLockSwitcher()
    {
        hklRu = NativeMethods.LoadKeyboardLayout("00000419", 1);
        hklEnUs = NativeMethods.LoadKeyboardLayout("00000409", 1);
        hookProc = HookCallback;
    }

    public void Install()
    {
        EnsureCapsOff();

        hookId = NativeMethods.SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, NativeMethods.GetModuleHandle(null), 0);

        if (hookId == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Dispose()
    {
        if (hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(hookId);
            hookId = IntPtr.Zero;
        }

    }

    IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && Marshal.ReadInt32(lParam) == VK_CAPITAL && !IsOwnInput(lParam))
        {
            int message = wParam.ToInt32();

            if (message == WM_KEYDOWN)
            {
                if (!capsHandled)
                {
                    capsHandled = true;

                    StartCapsAction(TextConversionEnabled && IsDown(VK_CONTROL));
                }

                return (IntPtr)1;
            }

            if (message == WM_KEYUP)
            {
                capsHandled = false;
                EnsureCapsOff();
                return (IntPtr)1;
            }
        }

        return NativeMethods.CallNextHookEx(hookId, nCode, wParam, lParam);
    }

    void StartCapsAction(bool shift)
    {
        var worker = new Thread(() => RunCapsAction(shift)) { IsBackground = true };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }

    void RunCapsAction(bool shift)
    {
        lock (gate)
        {
            if (shift)
            {
                try
                {
                    SelectionConverter.Convert();
                }
                catch (Exception)
                {
                }
            }

            try
            {
                SwitchLanguage();
            }
            catch (Exception)
            {
            }
        }
    }

    void SwitchLanguage()
    {
        int before = LanguageMonitor.ReadForegroundLangId(out IntPtr hwnd);

        if (before >= 0)
        {
            uint threadId = NativeMethods.GetWindowThreadProcessId(hwnd, IntPtr.Zero);
            bool toRu = before != LanguageMonitor.RussianLangId;
            IntPtr target = toRu ? hklRu : hklEnUs;

            if (!nativeFailedThreads.Contains(threadId))
            {
                NativeMethods.ActivateKeyboardLayout(target, 0);
                NativeMethods.SendMessageTimeout(hwnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, target, NativeMethods.SMTO_ABORTIFHUNG, 100, out _);

                if (WaitForLanguageChange(before, 150))
                {
                    EnsureCapsOff();
                    return;
                }

                nativeFailedThreads.Add(threadId);
            }

            SendWinSpace();
            WaitForLanguageChange(before, 300);
        }

        EnsureCapsOff();
    }

    static bool WaitForLanguageChange(int before, int timeoutMs)
    {
        int start = Environment.TickCount;

        do
        {
            int now = LanguageMonitor.ReadForegroundLangId(out _);

            if (now >= 0 && now != before)
                return true;

            System.Threading.Thread.Sleep(10);
        }
        while (Environment.TickCount - start < timeoutMs);

        return false;
    }

    static bool IsOwnInput(IntPtr lParam)
    {
        return Marshal.ReadIntPtr(lParam, ExtraInfoOffset) == new IntPtr(OwnInputMarker);
    }

    static bool IsDown(int vk)
    {
        return (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    static void SendWinSpace()
    {
        NativeMethods.keybd_event(VK_LWIN, 0, 0, OwnInput);
        NativeMethods.keybd_event(VK_SPACE, 0, 0, OwnInput);
        NativeMethods.keybd_event(VK_SPACE, 0, KEYEVENTF_KEYUP, OwnInput);
        NativeMethods.keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, OwnInput);
    }

    static void EnsureCapsOff()
    {
        if ((NativeMethods.GetKeyState(VK_CAPITAL) & 1) != 0)
        {
            NativeMethods.keybd_event(VK_CAPITAL, 0x45, 0, OwnInput);
            NativeMethods.keybd_event(VK_CAPITAL, 0x45, KEYEVENTF_KEYUP, OwnInput);
        }
    }

    static readonly UIntPtr OwnInput = new UIntPtr(OwnInputMarker);
}
