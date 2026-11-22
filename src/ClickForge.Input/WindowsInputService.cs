using System.ComponentModel;
using System.Runtime.InteropServices;
using ClickForge.Core;

namespace ClickForge.Input;

public sealed class WindowsInputService : IInputService
{
    private readonly object _gate = new();
    private readonly HashSet<MouseButton> _held = [];

    public (int X, int Y) GetCursorPosition()
    {
        if (!GetCursorPos(out var p)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return (p.X, p.Y);
    }

    public void MoveCursor(int x, int y)
    {
        if (!SetCursorPos(x, y)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void MouseDown(MouseButton button)
    {
        Send(button, true);
        lock (_gate) _held.Add(button);
    }

    public void MouseUp(MouseButton button)
    {
        Send(button, false);
        lock (_gate) _held.Remove(button);
    }

    public async Task ClickAsync(MouseButton button, ClickType type, int holdDurationMs, CancellationToken token)
    {
        var count = type switch { ClickType.Double => 2, ClickType.Triple => 3, _ => 1 };
        if (type == ClickType.Hold) { MouseDown(button); return; }
        if (type == ClickType.Release) { MouseUp(button); return; }

        for (var i = 0; i < count; i++)
        {
            token.ThrowIfCancellationRequested();
            MouseDown(button);
            try { await Task.Delay(Math.Clamp(holdDurationMs, 1, 60_000), token).ConfigureAwait(false); }
            finally { MouseUp(button); }
            if (i + 1 < count) await Task.Delay(50, token).ConfigureAwait(false);
        }
    }

    public void ReleaseAll()
    {
        MouseButton[] buttons;
        lock (_gate) buttons = _held.ToArray();
        foreach (var button in buttons)
        {
            try { MouseUp(button); } catch { }
        }
    }

    private static void Send(MouseButton button, bool down)
    {
        var (flags, data) = button switch
        {
            MouseButton.Left => (down ? MouseEventFlags.LeftDown : MouseEventFlags.LeftUp, 0u),
            MouseButton.Right => (down ? MouseEventFlags.RightDown : MouseEventFlags.RightUp, 0u),
            MouseButton.Middle => (down ? MouseEventFlags.MiddleDown : MouseEventFlags.MiddleUp, 0u),
            MouseButton.X1 => (down ? MouseEventFlags.XDown : MouseEventFlags.XUp, 1u),
            MouseButton.X2 => (down ? MouseEventFlags.XDown : MouseEventFlags.XUp, 2u),
            _ => throw new ArgumentOutOfRangeException(nameof(button))
        };
        var input = new INPUT { type = 0, mi = new MOUSEINPUT { dwFlags = flags, mouseData = data } };
        if (SendInput(1, [input], Marshal.SizeOf<INPUT>()) != 1) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [Flags]
    private enum MouseEventFlags : uint { LeftDown = 0x0002, LeftUp = 0x0004, RightDown = 0x0008, RightUp = 0x0010, MiddleDown = 0x0020, MiddleUp = 0x0040, XDown = 0x0080, XUp = 0x0100 }
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT { public int dx, dy; public uint mouseData; public MouseEventFlags dwFlags; public uint time; public nint dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetCursorPos(out POINT point);
}
