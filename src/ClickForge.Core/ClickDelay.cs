using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ClickForge.Core;

public static class ClickDelay
{
    private const uint HighResolution = 0x00000002;
    private const uint SynchronizeAndModify = 0x00100002;

    public static Task WaitAsync(int milliseconds, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (milliseconds is <= 0 or >= 16 || !OperatingSystem.IsWindows())
            return Task.Delay(milliseconds, token);

        using var timer = CreateWaitableTimerExW(0, null, HighResolution, SynchronizeAndModify);
        if (timer.IsInvalid)
            return Task.Delay(milliseconds, token);

        long dueTime = -milliseconds * TimeSpan.TicksPerMillisecond;
        if (!SetWaitableTimer(timer, in dueTime, 0, 0, 0, false))
            return Task.Delay(milliseconds, token);

        // Short waits run on the existing automation worker and remain interruptible.
        using var signal = new TimerSignal(timer);
        WaitHandle.WaitAny([signal, token.WaitHandle]);
        token.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private sealed class TimerSignal : WaitHandle
    {
        public TimerSignal(SafeWaitHandle timer) => SafeWaitHandle = timer;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern SafeWaitHandle CreateWaitableTimerExW(nint attributes, string? name, uint flags, uint access);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWaitableTimer(SafeWaitHandle timer, in long dueTime, int period,
        nint completionRoutine, nint completionArgument, [MarshalAs(UnmanagedType.Bool)] bool resume);
}
