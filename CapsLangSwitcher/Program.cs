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

    // Добавлено: клавиши для Win+Space
    const byte VK_LWIN = 0x5B;
    const byte VK_SPACE = 0x20;

    static void Main()
    {
        hklRu = LoadKeyboardLayout("00000419", 1);
        hklEnUs = LoadKeyboardLayout("00000409", 1);

        EnsureCapsOff();

        hookId = SetHook(HookCallback);

        System.Windows.Forms.Application.Run();

        UnhookWindowsHookEx(hookId);
    }

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;

        string moduleName = curModule?.ModuleName ?? string.Empty;

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
            int vkCode = Marshal.ReadInt32(lParam);

            if (vkCode == VK_CAPITAL)
            {
                if ((int)wParam == WM_KEYDOWN)
                {
                    if (!capsHandled)
                    {
                        capsHandled = true;

                        // ---------- НОВОЕ: проверка на проблемные окна ----------
                        IntPtr hwnd = GetForegroundWindow();
                        if (IsProblematicWindow(hwnd))
                        {
                            SwitchLanguageViaWinSpace(hwnd); // Отправляем Win+Space
                        }
                        else
                        {
                            SwitchLanguage(); // Старое поведение
                        }
                        // -------------------------------------------------------

                        EnsureCapsOff();
                    }

                    return (IntPtr)1; // Блокируем CapsLock
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

    /// <summary>
    /// Отправляет системное сочетание Win+Space для переключения раскладки,
    /// сохраняя уведомление оверлея.
    /// </summary>
    static void SwitchLanguageViaWinSpace(IntPtr hwnd)
    {
        // Определяем, на какой язык мы собираемся переключиться
        bool toRu = IsSwitchingToRussian(hwnd);

        SendWinSpace();               // Симуляция Win+Space
        NotifyOverlay(toRu);          // Уведомить оверлей о новом языке
    }

    /// <summary>
    /// Определяет, должен ли следующий язык быть русским (true), 
    /// исходя из текущей раскладки окна.
    /// </summary>
    static bool IsSwitchingToRussian(IntPtr hwnd)
    {
        uint threadId = GetWindowThreadProcessId(hwnd, IntPtr.Zero);
        IntPtr current = GetKeyboardLayout(threadId);
        long lang = current.ToInt64() & 0xFFFF;
        // Если сейчас не русский (0x0419) – переключаемся на русский
        return lang != 0x0419;
    }

    /// <summary>
    /// Симулирует нажатие клавиш Win+Space через keybd_event.
    /// </summary>
    static void SendWinSpace()
    {
        // Нажимаем Win
        keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
        // Нажимаем Space
        keybd_event(VK_SPACE, 0, 0, UIntPtr.Zero);
        // Отпускаем Space
        keybd_event(VK_SPACE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        // Отпускаем Win
        keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    // ---------- Старая логика SwitchLanguage (без изменений) ----------
    static void SwitchLanguage()
    {
        IntPtr hwnd = GetForegroundWindow();

        if (hwnd == IntPtr.Zero)
            return;

        string className = GetWindowClass(hwnd);

        bool isTaskbar = className == "Shell_TrayWnd";

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

        uint threadId = GetWindowThreadProcessId(
            hwnd,
            IntPtr.Zero
        );

        IntPtr current = GetKeyboardLayout(threadId);

        long lang = current.ToInt64() & 0xFFFF;

        bool toRu = lang != 0x0419;

        IntPtr target = toRu ? hklRu : hklEnUs;

        ActivateKeyboardLayout(target, 0);

        PostMessage(
            hwnd,
            WM_INPUTLANGCHANGEREQUEST,
            IntPtr.Zero,
            target
        );

        NotifyOverlay(toRu);
    }

    // ---------- Определение проблемных окон ----------
    static bool IsProblematicWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return false;

        string className = GetWindowClass(hwnd);
        string title = GetWindowTitle(hwnd);

        // 1. Панель задач
        if (className == "Shell_TrayWnd")
            return true;

        // 2. Рабочий стол (классы Progman или WorkerW)
        if (className == "Progman" || className == "WorkerW")
            return true;

        // 3. Диалог "Выполнить" (русская и английская версии)
        if (className == "#32770" && (title == "Run" || title == "Выполнить"))
            return true;

        // 4. Старые панели звука (Volume Control / Recording Control и их локализации)
        if (className == "#32770" && (
            title == "Volume Control" ||
            title == "Громкость" ||
            title == "Recording Control" ||
            title == "Запись" ||
            title == "Sound" ||
            title == "Звук"))
            return true;

        // При необходимости можно добавить другие окна по классу/заголовку

        return false;
    }

    static string GetWindowClass(IntPtr hwnd)
    {
        var buffer = new System.Text.StringBuilder(256);
        GetClassName(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    // ---------- Новая функция получения заголовка окна ----------
    static string GetWindowTitle(IntPtr hwnd)
    {
        var buffer = new System.Text.StringBuilder(256);
        GetWindowText(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    static void NotifyOverlay(bool ru)
    {
        IntPtr overlay = FindWindow(null, "CapsLangOverlay");

        if (overlay == IntPtr.Zero)
            return;

        PostMessage(
            overlay,
            WM_LANG_UPDATE,
            ru ? (IntPtr)1 : IntPtr.Zero,
            IntPtr.Zero
        );
    }

    static void EnsureCapsOff()
    {
        if ((GetKeyState(VK_CAPITAL) & 1) != 0)
        {
            keybd_event(VK_CAPITAL, 0x45, 0, UIntPtr.Zero);
            keybd_event(VK_CAPITAL, 0x45, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }

    // ---------- Все необходимые WinAPI (добавлен GetWindowText) ----------
    [DllImport("user32.dll")]
    static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount); // новое

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

    [DllImport("user32.dll")]
    static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

    [DllImport("user32.dll")]
    static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint Flags);

    [DllImport("user32.dll")]
    static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    static extern IntPtr GetModuleHandle(string lpModuleName);
}
