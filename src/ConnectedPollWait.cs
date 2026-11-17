// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

internal sealed class ConnectedPollWait : IDisposable
{
    private const uint CreateWaitableTimerHighResolution = 0x00000002;
    private const uint TimerModifyState = 0x00000002;
    private const uint Synchronize = 0x00100000;
    private const int PollIntervalMs = 4;

    private readonly WaitHandle stop;
    private readonly WaitableTimer timer;
    private readonly WaitHandle[] waits;

    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern SafeWaitHandle CreateWaitableTimerExW(IntPtr attributes, string name, uint flags, uint access);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWaitableTimer(SafeWaitHandle timer, ref long due, int period,
        IntPtr completionRoutine, IntPtr argument, [MarshalAs(UnmanagedType.Bool)] bool resume);

    internal ConnectedPollWait(WaitHandle stop)
    {
        this.stop = stop;
        SafeWaitHandle handle = CreateWaitableTimerExW(IntPtr.Zero, null, CreateWaitableTimerHighResolution,
            TimerModifyState | Synchronize);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return;
        }

        timer = new WaitableTimer(handle);
        waits = new WaitHandle[] { stop, timer };
    }

    internal void Wait()
    {
        long due = -PollIntervalMs * TimeSpan.TicksPerMillisecond;
        if (timer != null && SetWaitableTimer(timer.SafeWaitHandle, ref due, 0, IntPtr.Zero, IntPtr.Zero, false))
            WaitHandle.WaitAny(waits);
        else
            stop.WaitOne(PollIntervalMs);
    }

    public void Dispose()
    {
        if (timer != null) timer.Dispose();
    }

    private sealed class WaitableTimer : WaitHandle
    {
        internal WaitableTimer(SafeWaitHandle handle)
        {
            SafeWaitHandle = handle;
        }
    }
}
