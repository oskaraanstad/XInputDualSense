// SPDX-License-Identifier: GPL-3.0-or-later
internal sealed class ButtonCapture
{
    private bool released;
    private int captured;

    internal int? Update(bool connected, XInputMapping.Gamepad pad)
    {
        if (!connected)
        {
            released = false;
            captured = 0;
            return null;
        }

        int mask = pad.Buttons & ButtonProfile.AllowedInputs;
        if (pad.LeftTrigger > XInputMapping.TriggerPressThreshold) mask |= 65536;
        if (pad.RightTrigger > XInputMapping.TriggerPressThreshold) mask |= 131072;

        // A held button at the start must be released before capturing a new press.
        if (!released)
        {
            released = mask == 0;
            return null;
        }
        if (mask != 0) captured |= mask;
        return mask == 0 && captured != 0 ? (int?)captured : null;
    }
}
