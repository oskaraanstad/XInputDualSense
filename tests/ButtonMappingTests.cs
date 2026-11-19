// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;

internal static class ButtonMappingTests
{
    private static void Main()
    {
        var defaults = ButtonProfile.Default();
        var mapper = new ButtonMapper(defaults);
        var report = new byte[33];
        mapper.Encode(new XInputMapping.Gamepad { Buttons = 0x30 }, report);
        Expect(BitConverter.ToUInt32(report, 4) == 0x10000, "Default PS chord");
        mapper.Encode(new XInputMapping.Gamepad { Buttons = 0xa0 }, report);
        Expect(BitConverter.ToUInt32(report, 4) == 0x20000, "Default touchpad chord");
        mapper.Encode(new XInputMapping.Gamepad { LeftTrigger = 20, RightTrigger = 192 }, report);
        Expect(report[9] == 20 && report[10] == 192, "Default analog triggers");

        var profile = defaults.Copy();
        profile.Rules.Insert(0, new ButtonRule(0x1000 | 0x2000, "Touchpad click"));
        profile.Rules.RemoveAll(rule => rule.Input == 0x1000);
        profile.Rules.Add(new ButtonRule(0x1000, "PS"));
        mapper = new ButtonMapper(profile);
        mapper.Encode(new XInputMapping.Gamepad { Buttons = 0x3000 }, report);
        Expect(BitConverter.ToUInt32(report, 4) == 0x20000, "Chord consumes both single mappings");
        mapper.Encode(new XInputMapping.Gamepad { Buttons = 0x1000 }, report);
        Expect(BitConverter.ToUInt32(report, 4) == 0x10000, "Single-button remap");
        mapper.Encode(new XInputMapping.Gamepad(), report);
        Expect(BitConverter.ToUInt32(report, 4) == 0 && report[8] == 0 && report[9] == 0, "Released output clears");

        // The active mapper must not observe edits to an unsaved dialog profile.
        profile.Rules.Clear();
        mapper.Encode(new XInputMapping.Gamepad { Buttons = 0x1000 }, report);
        Expect(BitConverter.ToUInt32(report, 4) == 0x10000, "Immutable active mappings");

        var overlap = new ButtonProfile();
        overlap.Rules.Add(new ButtonRule(0x3000, "Cross"));
        overlap.Rules.Add(new ButtonRule(0x6000, "Circle"));
        mapper = new ButtonMapper(overlap);
        mapper.Encode(new XInputMapping.Gamepad { Buttons = 0x7000 }, report);
        Expect(BitConverter.ToUInt32(report, 4) == 0x20, "Equal-sized rules use list order");

        var triggers = new ButtonProfile();
        triggers.Rules.Add(new ButtonRule(65536, "PS"));
        triggers.Rules.Add(new ButtonRule(0x1000, "L2"));
        mapper = new ButtonMapper(triggers);
        mapper.Encode(new XInputMapping.Gamepad { LeftTrigger = 31, Buttons = 0x1000 }, report);
        Expect(BitConverter.ToUInt32(report, 4) == 0x10400 && report[9] == 255, "Trigger remap and digital L2");
        mapper.Encode(new XInputMapping.Gamepad { LeftTrigger = 30 }, report);
        Expect(BitConverter.ToUInt32(report, 4) == 0 && report[9] == 0, "Trigger threshold");

        var path = Path.Combine(Path.GetTempPath(), "XInputDualSense-profile-" + Guid.NewGuid().ToString("N"), "profile.xml");
        try
        {
            triggers.Save(path);
            var loaded = ButtonProfile.Load(path);
            Expect(loaded.Rules.Count == 2 && loaded.Rules[0].Output == "PS", "Saved profile round trip");
            defaults.Save(path);
            Expect(ButtonProfile.Load(path).Rules.Count == defaults.Rules.Count, "Atomic replacement / restore defaults");
            var invalid = defaults.Copy();
            invalid.Rules.Add(new ButtonRule(0x1000, "Unknown"));
            bool rejected = false;
            try { invalid.Save(path); } catch (InvalidDataException) { rejected = true; }
            Expect(rejected && ButtonProfile.Load(path).Rules.Count == defaults.Rules.Count, "Invalid save preserves existing profile");
            File.WriteAllText(path, "<ButtonProfile><Version>9</Version></ButtonProfile>");
            rejected = false;
            try { ButtonProfile.Load(path); } catch (InvalidDataException) { rejected = true; }
            Expect(rejected, "Future version rejected");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            Directory.Delete(Path.GetDirectoryName(path));
        }
        Console.WriteLine("Button mapping and profile tests passed.");
    }

    private static void Expect(bool result, string label)
    {
        if (!result) throw new InvalidOperationException(label);
    }
}
