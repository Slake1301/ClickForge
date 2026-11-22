using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace ClickForge.Hotkeys;

public sealed class GlobalHotkeyService : IDisposable
{
    public const Key MouseLeft = (Key)1001, MouseRight = (Key)1002, MouseMiddle = (Key)1003,
        MouseBack = (Key)1004, MouseForward = (Key)1005;
    public static bool IsMouse(Key key) => key is >= MouseLeft and <= MouseForward;
    public static string DisplayName(Key key) => key switch
    {
        MouseLeft => "Mouse 1", MouseRight => "Mouse 2", MouseMiddle => "Mouse 3",
        MouseBack => "Mouse 4 (Назад)", MouseForward => "Mouse 5 (Вперёд)",
        >= Key.D0 and <= Key.D9 => (key - Key.D0).ToString(), _ => key.ToString()
    };
    private const int WmHotkey = 0x0312;
    private HwndSource? _source;
    private nint _handle;
    public event Action<int>? Pressed;

    private Key _startKey;
    private int _startId = 1;
    private int _pressCount = 1;
    private int _generation;
    private nint _keyboardHook;
    private KeyboardProc? _keyboardProc;
    private nint _mouseHook;
    private KeyboardProc? _mouseProc;
    private long _firstPress;
    private bool _targetDown;

    public void Attach(Window window, Key startKey, int pressCount)
    {
        _handle = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_handle);
        _source.AddHook(Hook);
        Register(3, Key.F10);
        Register(2, Key.F7);
        ChangeStartKey(startKey, pressCount);
    }
    public void ChangeStartKey(Key key, int pressCount)
    {
        if (pressCount is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(pressCount));
        if (key is Key.F7 or Key.F10 or Key.F12 or Key.None)
            throw new InvalidOperationException("Эта клавиша зарезервирована.");
        if (key == _startKey && pressCount == _pressCount) return;
        var newId = _startId == 4 ? 1 : 4;
        bool needsKeyboardHook = pressCount == 2;
        bool needsMouseHook = IsMouse(key) || pressCount == 2;
        bool hadKeyboardHook = _keyboardHook != 0;
        bool hadMouseHook = _mouseHook != 0;
        try
        {
            if (needsKeyboardHook && !hadKeyboardHook)
            {
                _keyboardProc = ObserveKeyboard;
                _keyboardHook = SetWindowsHookEx(13, _keyboardProc, GetModuleHandle(null), 0);
                if (_keyboardHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось включить двойное нажатие.");
            }
            if (needsMouseHook && !hadMouseHook)
            {
                _mouseProc = ObserveMouse;
                _mouseHook = SetWindowsHookEx(14, _mouseProc, GetModuleHandle(null), 0);
                if (_mouseHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось подключить кнопки мыши.");
            }
            if (pressCount == 1 && !IsMouse(key)) Register(newId, key);
        }
        catch
        {
            if (!hadKeyboardHook) RemoveKeyboardHook();
            if (!hadMouseHook) RemoveMouseHook();
            throw;
        }
        if (_startKey != Key.None && _pressCount == 1 && !IsMouse(_startKey)) UnregisterHotKey(_handle, _startId);
        if (!needsKeyboardHook) RemoveKeyboardHook();
        if (!needsMouseHook) RemoveMouseHook();
        _startKey = key;
        _startId = newId;
        _pressCount = pressCount;
        _generation++;
        _firstPress = 0;
        int virtualKey = key switch
        {
            MouseLeft => 1, MouseRight => 2, MouseMiddle => 4, MouseBack => 5, MouseForward => 6,
            _ => KeyInterop.VirtualKeyFromKey(key)
        };
        _targetDown = (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    private nint ObserveKeyboard(int code, nint message, nint data)
    {
        if (code >= 0 && _pressCount == 2 && (Marshal.ReadInt32(data, 8) & 0x10) == 0)
        {
            int key = Marshal.ReadInt32(data);
            int kind = message.ToInt32();
            bool target = !IsMouse(_startKey) && key == KeyInterop.VirtualKeyFromKey(_startKey);
            if (kind is 0x100 or 0x104) ObservePress(target, true);
            else if (kind is 0x101 or 0x105) ObservePress(target, false);
        }
        return CallNextHookEx(0, code, message, data);
    }

    private nint ObserveMouse(int code, nint message, nint data)
    {
        if (code >= 0 && (Marshal.ReadInt32(data, 12) & 1) == 0)
        {
            int kind = message.ToInt32();
            var key = kind switch
            {
                0x201 or 0x202 => MouseLeft,
                0x204 or 0x205 => MouseRight,
                0x207 or 0x208 => MouseMiddle,
                0x20B or 0x20C => (Marshal.ReadInt32(data, 8) >> 16) == 1 ? MouseBack : MouseForward,
                _ => Key.None
            };
            if (key != Key.None)
                ObservePress(key == _startKey, kind is 0x201 or 0x204 or 0x207 or 0x20B);
        }
        return CallNextHookEx(0, code, message, data);
    }

    private void ObservePress(bool target, bool down)
    {
        if (!target)
        {
            if (down) _firstPress = 0;
            return;
        }
        if (!down) { _targetDown = false; return; }
        if (_targetDown) return;
        _targetDown = true;
        long now = Stopwatch.GetTimestamp();
        if (_pressCount == 2 && (_firstPress == 0 || Stopwatch.GetElapsedTime(_firstPress, now) > TimeSpan.FromMilliseconds(300)))
        {
            _firstPress = now;
            return;
        }
        _firstPress = 0;
        int generation = _generation;
        _source?.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (generation == _generation) Pressed?.Invoke(1);
        }));
    }

    private void RemoveMouseHook()
    {
        if (_mouseHook != 0) UnhookWindowsHookEx(_mouseHook);
        _mouseHook = 0;
        _mouseProc = null;
    }

    private void RemoveKeyboardHook()
    {
        if (_keyboardHook == 0) return;
        UnhookWindowsHookEx(_keyboardHook);
        _keyboardHook = 0;
        _keyboardProc = null;
    }
    private void Register(int id, Key key)
    {
        var vk = KeyInterop.VirtualKeyFromKey(key);
        if (!RegisterHotKey(_handle, id, 0x4000, (uint)vk)) throw new Win32Exception(Marshal.GetLastWin32Error(), $"Не удалось зарегистрировать {key}.");
    }
    private nint Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmHotkey)
        {
            handled = true;
            int id = wParam.ToInt32();
            if (id is 2 or 3) Pressed?.Invoke(id);
            else if (_pressCount == 1 && id == _startId) Pressed?.Invoke(1);
        }
        return 0;
    }
    public void Dispose()
    {
        _generation++;
        RemoveKeyboardHook();
        RemoveMouseHook();
        if (_handle != 0) for (var i = 1; i <= 4; i++) UnregisterHotKey(_handle, i);
        if (_source is not null) _source.RemoveHook(Hook);
    }
    private delegate nint KeyboardProc(int code, nint message, nint data);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int id, KeyboardProc callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? module);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(nint hwnd, int id);
}
