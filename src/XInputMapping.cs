// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.InteropServices;

internal static class XInputMapping
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Gamepad
    {
        internal ushort Buttons;
        internal byte LeftTrigger, RightTrigger;
        internal short LeftX, LeftY, RightX, RightY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        internal uint PacketNumber;
        internal Gamepad Pad;
    }

    [DllImport("xinput1_4.dll", ExactSpelling = true)]
    private static extern uint XInputGetState(uint index, out XInputState state);

    [StructLayout(LayoutKind.Sequential)]
    private struct Vibration
    {
        internal ushort LeftMotorSpeed, RightMotorSpeed;
    }

    [DllImport("xinput1_4.dll", ExactSpelling = true)]
    private static extern uint XInputSetState(uint index, ref Vibration vibration);

    internal static bool TrySetVibration(uint slot, ushort left, ushort right)
    {
        var vibration = new Vibration { LeftMotorSpeed = left, RightMotorSpeed = right };
        return XInputSetState(slot, ref vibration) == 0;
    }

    internal const byte TriggerPressThreshold = 30;
    internal const int ReportLength = 33;

    internal static volatile ButtonMapper Mapper = new ButtonMapper(ButtonProfile.Default());

    internal static bool TryReadGamepad(uint slot, out Gamepad pad)
    {
        XInputState state;
        bool connected = XInputGetState(slot, out state) == 0;
        pad = connected ? state.Pad : new Gamepad();
        return connected;
    }

    internal static void Encode(Gamepad pad, byte[] report)
    {
        report[0] = ToStick(pad.LeftX, false);
        report[1] = ToStick(pad.LeftY, true);
        report[2] = ToStick(pad.RightX, false);
        report[3] = ToStick(pad.RightY, true);

        Mapper.Encode(pad, report);
    }

    private static byte ToStick(short value, bool invert)
    {
        // Signed 8-bit, center 0 and full range preserved on both axes.
        int oriented = invert ? -(int)value : value;
        int scaled = (int)Math.Round(oriented / 256.0);
        return unchecked((byte)Math.Min(127, scaled));
    }
}
