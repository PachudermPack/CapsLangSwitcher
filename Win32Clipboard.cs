using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

static class Win32Clipboard
{
    const uint CF_UNICODETEXT = 13;
    const uint GMEM_MOVEABLE = 0x0002;

    public static string GetText()
    {
        if (!Open())
            return null;

        try
        {
            if (!IsClipboardFormatAvailable(CF_UNICODETEXT))
                return null;

            IntPtr handle = GetClipboardData(CF_UNICODETEXT);

            if (handle == IntPtr.Zero)
                return null;

            IntPtr pointer = GlobalLock(handle);

            if (pointer == IntPtr.Zero)
                return null;

            try
            {
                return Marshal.PtrToStringUni(pointer);
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    public static void SetText(string text)
    {
        if (!Open())
            return;

        try
        {
            EmptyClipboard();

            if (text == null)
                return;

            byte[] bytes = Encoding.Unicode.GetBytes(text + "\0");
            IntPtr handle = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes.Length);

            if (handle == IntPtr.Zero)
                return;

            IntPtr pointer = GlobalLock(handle);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            GlobalUnlock(handle);

            if (SetClipboardData(CF_UNICODETEXT, handle) == IntPtr.Zero)
                GlobalFree(handle);
        }
        finally
        {
            CloseClipboard();
        }
    }

    static bool Open()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
                return true;

            Thread.Sleep(20);
        }

        return false;
    }

    [DllImport("user32.dll")]
    static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll")]
    static extern IntPtr GetClipboardData(uint format);

    [DllImport("user32.dll")]
    static extern IntPtr SetClipboardData(uint format, IntPtr handle);

    [DllImport("kernel32.dll")]
    static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll")]
    static extern IntPtr GlobalLock(IntPtr handle);

    [DllImport("kernel32.dll")]
    static extern bool GlobalUnlock(IntPtr handle);

    [DllImport("kernel32.dll")]
    static extern IntPtr GlobalFree(IntPtr handle);
}
