using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

static class SelectionConverter
{
    const string Sentinel = "CLS_SENTINEL_9f3c2a7e";

    const byte VK_SHIFT = 0x10;
    const byte VK_CONTROL = 0x11;
    const byte VK_C = 0x43;
    const byte VK_V = 0x56;
    const uint KEYEVENTF_KEYUP = 0x0002;
    const uint OwnInputMarker = 0x434C53;

    const string EnKeys = "`qwertyuiop[]asdfghjkl;'zxcvbnm,.";
    const string RuKeys = "ёйцукенгшщзхъфывапролджэячсмитьбю";

    static readonly Dictionary<char, char> EnToRu = BuildMap(EnKeys, RuKeys);
    static readonly Dictionary<char, char> RuToEn = BuildMap(RuKeys, EnKeys);

    public static void Convert()
    {
        string savedClipboard = Win32Clipboard.GetText();

        try
        {
            Up(VK_CONTROL);
            Up(VK_SHIFT);

            Win32Clipboard.SetText(Sentinel);

            Down(VK_CONTROL);
            Tap(VK_C);
            Up(VK_CONTROL);

            string original = WaitForCopy();

            if (string.IsNullOrEmpty(original))
                return;

            string converted = TransliterateWords(original);

            if (converted == original)
                return;

            Win32Clipboard.SetText(converted);

            Down(VK_CONTROL);
            Tap(VK_V);
            Up(VK_CONTROL);

            Thread.Sleep(300);
        }
        finally
        {
            Win32Clipboard.SetText(savedClipboard);
        }
    }

    static Dictionary<char, char> BuildMap(string from, string to)
    {
        var map = new Dictionary<char, char>();

        for (int i = 0; i < from.Length; i++)
        {
            map[from[i]] = to[i];

            if (char.IsLetter(from[i]))
                map[char.ToUpperInvariant(from[i])] = char.ToUpperInvariant(to[i]);
        }

        return map;
    }

    static string TransliterateWords(string text)
    {
        return Regex.Replace(text, @"\S+", match => TransliterateWord(match.Value));
    }

    static string TransliterateWord(string word)
    {
        int latin = 0;
        int cyrillic = 0;

        foreach (char c in word)
        {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                latin++;
            else if (IsCyrillic(c))
                cyrillic++;
        }

        if (latin == 0 && cyrillic == 0)
            return word;

        var map = latin >= cyrillic ? EnToRu : RuToEn;
        var result = new StringBuilder(word.Length);

        foreach (char c in word)
            result.Append(map.TryGetValue(c, out char mapped) ? mapped : c);

        return result.ToString();
    }

    static bool IsCyrillic(char c)
    {
        return (c >= 'а' && c <= 'я') || (c >= 'А' && c <= 'Я') || c == 'ё' || c == 'Ё';
    }

    static string WaitForCopy()
    {
        for (int waited = 0; waited < 500; waited += 20)
        {
            Thread.Sleep(20);

            string text = Win32Clipboard.GetText();

            if (text != null && text != Sentinel)
                return text;
        }

        return null;
    }

    static void Tap(byte vk)
    {
        Down(vk);
        Up(vk);
    }

    static void Down(byte vk)
    {
        NativeMethods.keybd_event(vk, 0, 0, new UIntPtr(OwnInputMarker));
    }

    static void Up(byte vk)
    {
        NativeMethods.keybd_event(vk, 0, KEYEVENTF_KEYUP, new UIntPtr(OwnInputMarker));
    }
}
