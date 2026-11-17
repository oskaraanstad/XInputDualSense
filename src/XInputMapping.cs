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

    private const ushort Back = 0x0020;
    private const ushort Start = 0x0010;
    private const ushort LeftStick = 0x0040;
    private const ushort RightStick = 0x0080;
    internal const int ReportLength = 33;
    internal const int AutomaticSlot = -1;
    internal const uint NoSlot = 4;
    internal delegate bool SlotReader(uint slot, out Gamepad pad);

    private static readonly ushort[] XInputButtons =
        { 0x4000, 0x1000, 0x2000, 0x8000, 0x0100, 0x0200, Back, Start, LeftStick, RightStick };

    internal static bool TryReadGamepad(uint slot, out Gamepad pad)
    {
        XInputState state;
        bool connected = XInputGetState(slot, out state) == 0;
        pad = connected ? state.Pad : new Gamepad();
        return connected;
    }

    internal static bool TryReadController(int requestedSlot, ref uint lastAutoSlot,
        SlotReader readSlot, out uint activeSlot, out Gamepad pad)
    {
        if (requestedSlot != AutomaticSlot)
        {
            if (readSlot((uint)requestedSlot, out pad))
            {
                activeSlot = (uint)requestedSlot;
                return true;
            }
        }
        else
        {
            if (lastAutoSlot < NoSlot && readSlot(lastAutoSlot, out pad))
            {
                activeSlot = lastAutoSlot;
                return true;
            }
            for (uint slot = 0; slot < NoSlot; slot++)
            {
                if (slot == lastAutoSlot || !readSlot(slot, out pad)) continue;
                lastAutoSlot = activeSlot = slot;
                return true;
            }
        }

        activeSlot = NoSlot;
        pad = new Gamepad();
        return false;
    }

    internal static void Encode(Gamepad pad, byte[] report)
    {
        report[0] = ToStick(pad.LeftX, false);
        report[1] = ToStick(pad.LeftY, true);
        report[2] = ToStick(pad.RightX, false);
        report[3] = ToStick(pad.RightY, true);

        int buttons = pad.Buttons;
        uint mapped = 0;
        if ((buttons & (Back | Start)) == (Back | Start))
        {
            mapped |= 0x10000; // PS
            buttons &= ~(Back | Start);
        }
        else if ((buttons & (Back | RightStick)) == (Back | RightStick))
        {
            mapped |= 0x20000; // Touchpad click
            buttons &= ~(Back | RightStick);
        }

        for (int i = 0; i < XInputButtons.Length; i++)
        {
            // Face and bumper buttons are followed by the digital trigger bits.
            int bit = i < 6 ? i + 4 : i + 6;
            if ((buttons & XInputButtons[i]) != 0)
                mapped |= 1u << bit;
        }
        if (pad.LeftTrigger > 30) mapped |= 0x400;
        if (pad.RightTrigger > 30) mapped |= 0x800;

        report[4] = (byte)mapped;
        report[5] = (byte)(mapped >> 8);
        report[6] = (byte)(mapped >> 16);
        report[7] = 0;
        report[8] = (byte)(buttons & 0x0f); // XInput and VIIPER use the same direction bits.
        report[9] = pad.LeftTrigger;
        report[10] = pad.RightTrigger;
        // Touches inactive, gyro zero and a flat controller at rest (AccelZ = -8192).
        report[32] = 0xe0;
    }

    private static byte ToStick(short value, bool invert)
    {
        // Signed 8-bit, center 0 and full range preserved on both axes.
        int oriented = invert ? -(int)value : value;
        int scaled = (int)Math.Round(oriented / 256.0);
        return unchecked((byte)Math.Min(127, scaled));
    }
}
