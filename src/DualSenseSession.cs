// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Runtime.InteropServices;

internal sealed class DualSenseSession : IDisposable
{
    private const string Library = "libVIIPER.dll";
    private readonly byte[] report = new byte[XInputMapping.ReportLength];
    private readonly LogCallback logCallback;
    private volatile string lastNativeError;
    private UIntPtr server;
    private UIntPtr device;

    [StructLayout(LayoutKind.Sequential)]
    private struct ServerConfig
    {
        internal IntPtr Address;
        internal ulong ConnectionTimeoutMs;
        internal ulong DeviceHandlerTimeoutMs;
        internal uint WriteBatchFlushIntervalMs;
    }

    // Native C layout includes padding. The previous TCP protocol uses a packed 33-byte report.
    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceState
    {
        internal sbyte LeftX, LeftY, RightX, RightY;
        internal uint Buttons;
        internal byte DPad, LeftTrigger, RightTrigger;
        internal ushort Touch1X, Touch1Y;
        internal byte Touch1Active;
        internal ushort Touch2X, Touch2Y;
        internal byte Touch2Active;
        internal short GyroX, GyroY, GyroZ, AccelX, AccelY, AccelZ;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void LogCallback(int level, IntPtr message);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern byte NewUSBServer(ref ServerConfig config, out UIntPtr handle, LogCallback log);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern byte CreateUSBBus(UIntPtr server, ref uint busId);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern byte CreateDualSenseFull(UIntPtr server, out UIntPtr device, uint busId,
        byte autoAttach, ushort vendorId, ushort productId, IntPtr metadata);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern byte SetDualSenseDeviceState(UIntPtr device, DeviceState state);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern byte RemoveDualSense(UIntPtr device);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern byte CloseUSBServer(UIntPtr server);

    internal DualSenseSession()
    {
        logCallback = (level, message) =>
        {
            if (level >= 8) lastNativeError = Marshal.PtrToStringAnsi(message);
        };

        try
        {
            IntPtr address = Marshal.StringToHGlobalAnsi("127.0.0.1:0");
            try
            {
                var config = new ServerConfig { Address = address };
                if (NewUSBServer(ref config, out server, logCallback) == 0)
                    throw Error("Could not start libVIIPER.");
            }
            finally { Marshal.FreeHGlobal(address); }

            uint busId = 0;
            if (CreateUSBBus(server, ref busId) == 0)
                throw Error("Could not create the USB/IP bus.");
            if (CreateDualSenseFull(server, out device, busId, 1, 0, 0, IntPtr.Zero) == 0)
                throw Error("Could not attach the virtual DualSense. Check USBip-Win2.");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal void Send(XInputMapping.Gamepad gamepad)
    {
        XInputMapping.Encode(gamepad, report);
        var state = new DeviceState
        {
            LeftX = unchecked((sbyte)report[0]), LeftY = unchecked((sbyte)report[1]),
            RightX = unchecked((sbyte)report[2]), RightY = unchecked((sbyte)report[3]),
            Buttons = BitConverter.ToUInt32(report, 4),
            DPad = report[8], LeftTrigger = report[9], RightTrigger = report[10],
            AccelZ = BitConverter.ToInt16(report, 31)
        };
        if (SetDualSenseDeviceState(device, state) == 0)
            throw Error("Could not update the virtual controller.");
    }

    public void Dispose()
    {
        if (device != UIntPtr.Zero)
        {
            RemoveDualSense(device);
            device = UIntPtr.Zero;
        }
        if (server != UIntPtr.Zero)
        {
            CloseUSBServer(server);
            server = UIntPtr.Zero;
        }
        GC.KeepAlive(logCallback);
    }

    private IOException Error(string message)
    {
        string details = lastNativeError;
        return new IOException(string.IsNullOrEmpty(details) ? message : message + "\r\n" + details);
    }
}
