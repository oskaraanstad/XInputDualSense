// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

internal static class RumbleTests
{
    private static void Main()
    {
        var calls = new List<Tuple<uint, ushort, ushort>>();
        using (var rumble = new RumbleForwarder(0, (slot, left, right) =>
        {
            calls.Add(Tuple.Create(slot, left, right));
            return true;
        }))
        {
            rumble.Queue(0x01, 0, 32, 255);
            if (calls.Count != 0) throw new InvalidOperationException("Callback wrote directly to XInput.");
            rumble.Update(0);
            Expect(calls, 0, 65535, 8224);
            rumble.Queue(0, 0, 0, 0); // Unrelated output report.
            rumble.Update(0);
            ExpectNoCalls(calls);
            rumble.Queue(0x02, 0, 32, 255); // Repeated rumble.
            rumble.Update(0);
            ExpectNoCalls(calls);

            rumble.Queue(0, 0x04, 255, 255);
            rumble.Update(0);
            Expect(calls, 0, 65535, 65535);
            rumble.Queue(0x01, 0, 0, 0);
            rumble.Update(0);
            Expect(calls, 0, 0, 0);

            rumble.Queue(0x01, 0, 1, 2);
            rumble.Update(0);
            Expect(calls, 0, 514, 257);
            rumble.Queue(0x01, 0, 255, 255);
            rumble.Update(1);
            Expect(calls, 0, 0, 0);
            rumble.Queue(0x01, 0, 2, 1);
            rumble.Update(1);
            Expect(calls, 1, 257, 514);

            rumble.Update(null);
            Expect(calls, 1, 0, 0);
            rumble.Queue(0x01, 0, 55, 66);
            rumble.Update(1);
            ExpectNoCalls(calls);
            rumble.Queue(0x01, 0, 2, 1);
            rumble.Update(1);
            Expect(calls, 1, 257, 514);
        }
        Expect(calls, 1, 0, 0);
        Console.WriteLine("Rumble tests passed: mapping, suppression, stop, slot changes and disposal.");
    }

    private static void Expect(List<Tuple<uint, ushort, ushort>> calls, uint slot, ushort left, ushort right)
    {
        if (calls.Count != 1 || !calls[0].Equals(Tuple.Create(slot, left, right)))
            throw new InvalidOperationException("Expected one motor update for slot " + slot + ": " + left + ", " + right);
        calls.Clear();
    }

    private static void ExpectNoCalls(List<Tuple<uint, ushort, ushort>> calls)
    {
        if (calls.Count != 0) throw new InvalidOperationException("Unexpected motor update.");
    }
}
