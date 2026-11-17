// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Diagnostics;

internal sealed class InputPreview
{
    private const int LeftTrigger = 1 << 16;
    private const int RightTrigger = 1 << 17;
    private static readonly string[] ButtonNames =
    {
        "D-pad Up", "D-pad Down", "D-pad Left", "D-pad Right", "Start", "Select",
        "L3", "R3", "LB", "RB", null, null, "A", "B", "X", "Y", "LT", "RT"
    };

    private readonly object gate = new object();
    private int held;
    private int pendingPresses;
    private int recentPresses;
    private long showUntil;

    internal void Update(bool connected, XInputMapping.Gamepad pad)
    {
        lock (gate)
        {
            if (!connected)
            {
                held = pendingPresses = recentPresses = 0;
                showUntil = 0;
                return;
            }

            int buttons = pad.Buttons;
            if (pad.LeftTrigger > XInputMapping.TriggerPressThreshold) buttons |= LeftTrigger;
            if (pad.RightTrigger > XInputMapping.TriggerPressThreshold) buttons |= RightTrigger;
            pendingPresses |= buttons & ~held;
            held = buttons;
        }
    }

    internal string ReadText(long now)
    {
        int buttons;
        lock (gate)
        {
            // Keep a short tap visible even when it starts and ends between UI reads.
            if (pendingPresses != 0)
            {
                recentPresses = pendingPresses;
                showUntil = now + Stopwatch.Frequency / 5;
                pendingPresses = 0;
            }
            buttons = held | (now < showUntil ? recentPresses : 0);
        }

        var names = new List<string>();
        for (int i = 0; i < ButtonNames.Length; i++)
        {
            if (ButtonNames[i] != null && (buttons & (1 << i)) != 0)
                names.Add(ButtonNames[i]);
        }
        return names.Count == 0 ? "Pressed buttons: none" : "Pressed buttons: " + string.Join(", ", names);
    }
}
