// SPDX-License-Identifier: GPL-3.0-or-later
using System;

internal static class ControllerSelectionTests
{
    private static readonly bool[] Connected = new bool[4];

    private static void Main()
    {
        uint lastAutoSlot = XInputMapping.NoSlot;
        Connected[1] = true;
        ExpectRead(XInputMapping.AutomaticSlot, ref lastAutoSlot, 1, 2);

        Connected[0] = true;
        ExpectRead(XInputMapping.AutomaticSlot, ref lastAutoSlot, 1, 2);
        ExpectRead(0, ref lastAutoSlot, 0, 1);
        ExpectRead(3, ref lastAutoSlot, XInputMapping.NoSlot, 0);
        ExpectRead(XInputMapping.AutomaticSlot, ref lastAutoSlot, 1, 2);

        Connected[1] = false;
        ExpectRead(XInputMapping.AutomaticSlot, ref lastAutoSlot, 0, 1);
        ExpectRead(1, ref lastAutoSlot, XInputMapping.NoSlot, 0);

        Connected[1] = true;
        ExpectRead(1, ref lastAutoSlot, 1, 2);
        ExpectRead(XInputMapping.AutomaticSlot, ref lastAutoSlot, 0, 1);

        Connected[0] = false;
        Connected[1] = false;
        ExpectRead(XInputMapping.AutomaticSlot, ref lastAutoSlot, XInputMapping.NoSlot, 0);

        Console.WriteLine("Controller selection tests passed.");
    }

    private static bool ReadSlot(uint slot, out XInputMapping.Gamepad pad)
    {
        pad = new XInputMapping.Gamepad { Buttons = (ushort)(slot + 1) };
        return Connected[slot];
    }

    private static void ExpectRead(int selection, ref uint lastAutoSlot, uint expectedSlot, ushort expectedButtons)
    {
        uint slot;
        XInputMapping.Gamepad pad;
        bool connected = XInputMapping.TryReadController(selection, ref lastAutoSlot, ReadSlot, out slot, out pad);
        if (connected != (expectedSlot != XInputMapping.NoSlot) || slot != expectedSlot || pad.Buttons != expectedButtons)
            throw new InvalidOperationException("Unexpected XInput source or gamepad state for selection " + selection + ".");
    }
}
