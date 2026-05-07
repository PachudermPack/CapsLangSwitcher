using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

class Program
{
    private static IntPtr hookId = IntPtr.Zero;

    static IntPtr hklRu;
    static IntPtr hklEnUs;

    static bool capsHandled = false;

    static IntPtr lastRealWindow = IntPtr.Zero;

    const int WM_KEYDOWN = 0x0100;
    const int WM_KEYUP = 0x0101;

    const int WM_INPUTLANGCHANGEREQUEST = 0x0050;
    const int WM_LANG_UPDATE = 0x8001;

    const int VK_CAPITAL = 0x14;
    const uint KEYEVENTF_KEYUP = 0x0002;

    static void Main()
    {
        hklRu = LoadKeyboardLayout("00000419", 1);

        hklEnUs = LoadKeyboardLayout("00000409", 1);

        EnsureCapsOff();

        hookId = SetHook(HookCallback);

        System.Windows.Forms.Application.Run();

        UnhookWindowsHookEx(hookId);
    }

    private static IntPtr SetHook(
        LowLevelKeyboardProc proc
    )
    {
        using var curProcess =
            Process.GetCurrentProcess();

        using var curModule =
            curProcess.MainModule;

        string moduleName =
            curModule?.ModuleName
            ?? string.Empty;

        return SetWindowsHookEx(
            13,
            proc,
            GetModuleHandle(moduleName),
            0
        );
    }

    private delegate IntPtr LowLevelKeyboardProc(
        int nCode,
        IntPtr wParam,
        IntPtr lParam
    );

    private static IntPtr HookCallback(
        int nCode,
        IntPtr wParam,
        IntPtr lParam
    )
    {
        if (nCode >= 0)
        {
            int vkCode =
                Marshal.ReadInt32(lParam);

            if (vkCode == VK_CAPITAL)
            {
                if ((int)wParam == WM_KEYDOWN)
                {
                    if (!capsHandled)
                    {
                        capsHandled = true;

                        SwitchLanguage();

                        EnsureCapsOff();
                    }

                    return (IntPtr)1;
                }

                if ((int)wParam == WM_KEYUP)
                {
                    capsHandled = false;

                    EnsureCapsOff();

                    return (IntPtr)1;
                }
            }
        }

        return CallNextHookEx(
            hookId,
            nCode,
            wParam,
            lParam
        );
    }

    static void SwitchLanguage()
    {
        IntPtr hwnd =
            GetForegroundWindow();

        if (hwnd == IntPtr.Zero)
            return;

        string className =
            GetWindowClass(hwnd);

        bool isTaskbar =
            className == "Shell_TrayWnd";

        if (!isTaskbar)
        {
            lastRealWindow = hwnd;
        }
        else
        {
            if (lastRealWindow != IntPtr.Zero)
            {
                hwnd = lastRealWindow;
            }
        }

        uint threadId =
            GetWindowThreadProcessId(
                hwnd,
                IntPtr.Zero
            );

        IntPtr current =
            GetKeyboardLayout(threadId);

        long lang =
            current.ToInt64() & 0xFFFF;

        bool toRu =
            lang != 0x0419;

        IntPtr target =
            toRu
            ? hklRu
            : hklEnUs;

        ActivateKeyboardLayout(
            target,
            0
        );

        PostMessage(
            hwnd,
            WM_INPUTLANGCHANGEREQUEST,
            IntPtr.Zero,
            target
        );

        NotifyOverlay(toRu);
    }

    static string GetWindowClass(
        IntPtr hwnd
    )
    {
        var buffer =
            new System.Text.StringBuilder(256);

        GetClassName(
            hwnd,
            buffer,
            buffer.Capacity
        );

        return buffer.ToString();
    }

    static void NotifyOverlay(bool ru)
    {
        IntPtr overlay =
            FindWindow(
                null,
                "CapsLangOverlay"
            );

        if (overlay == IntPtr.Zero)
            return;

        PostMessage(
            overlay,
            WM_LANG_UPDATE,
            ru
            ? (IntPtr)1
            : IntPtr.Zero,
            IntPtr.Zero
        );
    }

    static void EnsureCapsOff()
    {
        if ((GetKeyState(VK_CAPITAL) & 1) != 0)
        {
            keybd_event(
                VK_CAPITAL,
                0x45,
                0,
                UIntPtr.Zero
            );

            keybd_event(
                VK_CAPITAL,
                0x45,
                KEYEVENTF_KEYUP,
                UIntPtr.Zero
            );
        }
    }

    [DllImport("user32.dll")]
    static extern short GetKeyState(
        int nVirtKey
    );

    [DllImport("user32.dll")]
    static extern void keybd_event(
        byte bVk,
        byte bScan,
        uint dwFlags,
        UIntPtr dwExtraInfo
    );

    [DllImport("user32.dll")]
    static extern bool PostMessage(
        IntPtr hWnd,
        int Msg,
        IntPtr wParam,
        IntPtr lParam
    );

    [DllImport("user32.dll")]
    static extern IntPtr FindWindow(
        string? lpClassName,
        string? lpWindowName
    );

    [DllImport("user32.dll")]
    static extern int GetClassName(
        IntPtr hWnd,
        System.Text.StringBuilder lpClassName,
        int nMaxCount
    );

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(
        IntPtr hWnd,
        IntPtr ProcessId
    );

    [DllImport("user32.dll")]
    static extern IntPtr GetKeyboardLayout(
        uint idThread
    );

    [DllImport("user32.dll")]
    static extern IntPtr LoadKeyboardLayout(
        string pwszKLID,
        uint Flags
    );

    [DllImport("user32.dll")]
    static extern IntPtr ActivateKeyboardLayout(
        IntPtr hkl,
        uint Flags
    );

    [DllImport("user32.dll")]
    static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc lpfn,
        IntPtr hMod,
        uint dwThreadId
    );

    [DllImport("user32.dll")]
    static extern bool UnhookWindowsHookEx(
        IntPtr hhk
    );

    [DllImport("user32.dll")]
    static extern IntPtr CallNextHookEx(
        IntPtr hhk,
        int nCode,
        IntPtr wParam,
        IntPtr lParam
    );

    [DllImport("kernel32.dll")]
    static extern IntPtr GetModuleHandle(
        string lpModuleName
    );
}