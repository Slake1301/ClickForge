using ClickForge.Core;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClickForge.Lite;

internal sealed class LiteShortcut(Action activate) : IDisposable
{
    private delegate nint HookProc(int code, nint message, nint data);
    private HookProc? _keyboardCallback;
    private HookProc? _mouseCallback;
    private nint _keyboard;
    private nint _mouse;
    private int _key;
    private int _count;
    private bool _down;
    private long _first;

    public void Configure(int key, int count)
    {
        var keyboard = _keyboard;
        var mouse = _mouse;
        try
        {
            if (_keyboard == 0)
            {
                _keyboardCallback = Keyboard;
                _keyboard = Install(13, _keyboardCallback);
            }
            if (_mouse == 0 && (count == 2 || IsMouse(key)))
            {
                _mouseCallback = Mouse;
                _mouse = Install(14, _mouseCallback);
            }
        }
        catch
        {
            if (keyboard == 0 && _keyboard != 0) { UnhookWindowsHookEx(_keyboard); _keyboard = 0; }
            if (mouse == 0 && _mouse != 0) { UnhookWindowsHookEx(_mouse); _mouse = 0; }
            throw;
        }
        _key = key;
        _count = count;
        _first = 0;
        _down = (GetAsyncKeyState(key) & 0x8000) != 0;
        if (count == 1 && !IsMouse(key) && _mouse != 0)
        {
            UnhookWindowsHookEx(_mouse);
            _mouse = 0;
        }
    }

    public static bool IsMouse(int key) => key is 1 or 2 or 4 or 5 or 6;

    public static string Name(int key) => key switch
    {
        1 => "Mouse 1", 2 => "Mouse 2", 4 => "Mouse 3",
        5 => UiText.Get("Mouse 4 (Назад)"), 6 => UiText.Get("Mouse 5 (Вперёд)"),
        >= 0x30 and <= 0x39 => ((char)key).ToString(), _ => ((Keys)key).ToString()
    };

    private static nint Install(int type, HookProc callback)
    {
        var hook = SetWindowsHookEx(type, callback, GetModuleHandle(null), 0);
        if (hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), UiText.Get("Не удалось подключить горячую кнопку."));
        return hook;
    }

    private nint Keyboard(int code, nint message, nint data)
    {
        if (code >= 0 && (Marshal.ReadInt32(data, 8) & 0x10) == 0)
        {
            int kind = message.ToInt32();
            if (kind is 0x100 or 0x101 or 0x104 or 0x105)
                Observe(Marshal.ReadInt32(data), kind is 0x100 or 0x104);
        }
        return CallNextHookEx(0, code, message, data);
    }

    private nint Mouse(int code, nint message, nint data)
    {
        if (code >= 0 && (Marshal.ReadInt32(data, 12) & 1) == 0)
        {
            int kind = message.ToInt32();
            int key = kind switch
            {
                0x201 or 0x202 => 1, 0x204 or 0x205 => 2, 0x207 or 0x208 => 4,
                0x20B or 0x20C => (Marshal.ReadInt32(data, 8) >> 16) == 1 ? 5 : 6, _ => 0
            };
            if (key != 0) Observe(key, kind is 0x201 or 0x204 or 0x207 or 0x20B);
        }
        return CallNextHookEx(0, code, message, data);
    }

    private void Observe(int key, bool down)
    {
        if (key != _key) { if (down) _first = 0; return; }
        if (!down) { _down = false; return; }
        if (_down) return;
        _down = true;
        long now = Stopwatch.GetTimestamp();
        if (_count == 2 && (_first == 0 || Stopwatch.GetElapsedTime(_first, now).TotalMilliseconds > 300))
        {
            _first = now;
            return;
        }
        _first = 0;
        activate();
    }

    public void Dispose()
    {
        if (_keyboard != 0) UnhookWindowsHookEx(_keyboard);
        if (_mouse != 0) UnhookWindowsHookEx(_mouse);
        _keyboard = _mouse = 0;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int type, HookProc callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? module);
}
