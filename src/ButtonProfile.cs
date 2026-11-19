// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

public sealed class ButtonRule
{
    public int Input { get; set; }
    public string Output { get; set; }

    public ButtonRule() { }
    public ButtonRule(int input, string output) { Input = input; Output = output; }
}

public sealed class ButtonProfile
{
    public int Version { get; set; }
    public List<ButtonRule> Rules { get; set; }

    public ButtonProfile() { Version = 1; Rules = new List<ButtonRule>(); }

    internal static readonly string[] Inputs =
    {
        "D-pad Up", "D-pad Down", "D-pad Left", "D-pad Right", "Start", "Select",
        "L3", "R3", "LB", "RB", "A", "B", "X", "Y", "LT", "RT"
    };
    internal static readonly int[] Masks =
    {
        1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 4096, 8192, 16384, 32768, 65536, 131072
    };
    internal static readonly string[] Outputs =
    {
        "D-pad Up", "D-pad Down", "D-pad Left", "D-pad Right", "Options", "Create",
        "L3", "R3", "L1", "R1", "Cross", "Circle", "Square", "Triangle", "L2", "R2",
        "PS", "Touchpad click", "Disabled"
    };
    internal const int AllowedInputs = 0x3f3ff;

    internal static ButtonProfile Default()
    {
        var profile = new ButtonProfile();
        profile.Rules.Add(new ButtonRule(32 | 16, "PS"));
        profile.Rules.Add(new ButtonRule(32 | 128, "Touchpad click"));
        for (int i = 0; i < Masks.Length; i++)
            profile.Rules.Add(new ButtonRule(Masks[i], Outputs[i]));
        return profile;
    }

    internal ButtonProfile Copy()
    {
        return new ButtonProfile { Version = Version,
            Rules = Rules.Select(rule => new ButtonRule(rule.Input, rule.Output)).ToList() };
    }

    internal void Validate()
    {
        if (Version != 1 || Rules == null || Rules.Count > 64)
            throw new InvalidDataException("Unsupported profile version or rule count (maximum 64).");
        var seen = new HashSet<int>();
        foreach (var rule in Rules)
        {
            if (rule == null || rule.Input == 0 || (rule.Input & ~AllowedInputs) != 0 ||
                !seen.Add(rule.Input) || Array.IndexOf(Outputs, rule.Output) < 0)
                throw new InvalidDataException("The profile contains an invalid or duplicate mapping.");
        }
    }

    internal static string InputName(int mask)
    {
        return string.Join(" + ", Inputs.Where((name, i) => (mask & Masks[i]) != 0));
    }

    internal void Save(string path)
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = File.Create(temporary))
            {
                new XmlSerializer(typeof(ButtonProfile)).Serialize(file, this);
                file.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal static ButtonProfile Load(string path)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = 65536 };
        using (var reader = XmlReader.Create(path, settings))
        {
            var profile = (ButtonProfile)new XmlSerializer(typeof(ButtonProfile)).Deserialize(reader);
            profile.Validate();
            return profile;
        }
    }
}

internal sealed class ButtonMapper
{
    private struct Mapping
    {
        internal int Input;
        internal int Output;
    }
    private readonly Mapping[] rules;

    internal ButtonMapper(ButtonProfile profile)
    {
        profile.Validate();
        // Longer combinations consume their inputs before single-button rules.
        rules = profile.Rules.OrderByDescending(rule => CountBits(rule.Input))
            .Select(rule => new Mapping { Input = rule.Input, Output = Array.IndexOf(ButtonProfile.Outputs, rule.Output) }).ToArray();
    }

    private static int CountBits(int bits)
    {
        int count = 0;
        while (bits != 0) { bits &= bits - 1; count++; }
        return count;
    }

    internal void Encode(XInputMapping.Gamepad pad, byte[] report)
    {
        int remaining = pad.Buttons & ButtonProfile.AllowedInputs;
        if (pad.LeftTrigger > XInputMapping.TriggerPressThreshold) remaining |= 65536;
        if (pad.RightTrigger > XInputMapping.TriggerPressThreshold) remaining |= 131072;
        uint buttons = 0;
        byte dpad = 0, l2 = 0, r2 = 0;
        foreach (var rule in rules)
        {
            // Preserve analog trigger travel below the digital activation threshold.
            if (rule.Input == 65536 && rule.Output == 14 && pad.LeftTrigger <= XInputMapping.TriggerPressThreshold)
                l2 = Math.Max(l2, pad.LeftTrigger);
            if (rule.Input == 131072 && rule.Output == 15 && pad.RightTrigger <= XInputMapping.TriggerPressThreshold)
                r2 = Math.Max(r2, pad.RightTrigger);
            if ((remaining & rule.Input) != rule.Input) continue;
            remaining &= ~rule.Input;
            int output = rule.Output;
            if (output < 4) dpad |= (byte)(1 << output);
            else if (output == 14)
                l2 = Math.Max(l2, rule.Input == 65536 ? pad.LeftTrigger : (byte)255);
            else if (output == 15)
                r2 = Math.Max(r2, rule.Input == 131072 ? pad.RightTrigger : (byte)255);
            else buttons |= ButtonBits(output);
        }
        if (l2 > XInputMapping.TriggerPressThreshold) buttons |= 0x400;
        if (r2 > XInputMapping.TriggerPressThreshold) buttons |= 0x800;
        report[4] = (byte)buttons;
        report[5] = (byte)(buttons >> 8);
        report[6] = (byte)(buttons >> 16);
        report[7] = 0;
        report[8] = dpad;
        report[9] = l2;
        report[10] = r2;
        report[32] = 0xe0;
    }

    private static uint ButtonBits(int output)
    {
        switch (output)
        {
            case 4: return 0x2000;
            case 5: return 0x1000;
            case 6: return 0x4000;
            case 7: return 0x8000;
            case 8: return 0x100;
            case 9: return 0x200;
            case 10: return 0x20;
            case 11: return 0x40;
            case 12: return 0x10;
            case 13: return 0x80;
            case 16: return 0x10000;
            case 17: return 0x20000;
            default: return 0;
        }
    }
}
