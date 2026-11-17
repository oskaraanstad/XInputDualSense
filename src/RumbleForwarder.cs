// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Threading;

internal sealed class RumbleForwarder : IDisposable
{
    private const byte LegacyRumble = 0x01;
    private const byte RumbleEmulation = 0x02;
    private const byte ImprovedRumble = 0x04;
    private const int NoRequest = -1;

    internal delegate bool SetMotors(uint slot, ushort left, ushort right);

    private readonly SetMotors setMotors;
    private int pending = NoRequest;
    private uint? slot;
    private int sent;

    internal RumbleForwarder(uint initialSlot, SetMotors setMotors)
    {
        slot = initialSlot;
        this.setMotors = setMotors;
    }

    // The native callback can run on a different thread. XInput is called only by the worker.
    internal void Queue(byte flags1, byte flags3, byte smallMotor, byte largeMotor)
    {
        if ((flags1 & (LegacyRumble | RumbleEmulation)) == 0 && (flags3 & ImprovedRumble) == 0)
            return; // LED and other unrelated reports do not replace the current motor state.

        Interlocked.Exchange(ref pending, largeMotor | (smallMotor << 8));
    }

    internal void Update(uint? activeSlot)
    {
        if (slot != activeSlot)
        {
            StopMotors();
            Interlocked.Exchange(ref pending, NoRequest);
            slot = activeSlot;
        }

        int requested = Interlocked.Exchange(ref pending, NoRequest);
        if (!slot.HasValue || requested == NoRequest || requested == sent) return;

        ushort left = (ushort)((requested & 0xff) * 257);
        ushort right = (ushort)((requested >> 8) * 257);
        if (setMotors(slot.Value, left, right)) sent = requested;
    }

    private void StopMotors()
    {
        if (slot.HasValue && sent != 0) setMotors(slot.Value, 0, 0);
        sent = 0;
    }

    public void Dispose()
    {
        StopMotors();
        slot = null;
        Interlocked.Exchange(ref pending, NoRequest);
    }
}
