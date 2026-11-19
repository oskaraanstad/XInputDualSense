// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

internal sealed class MappingDialog : Form
{
    private ButtonProfile profile;
    private readonly ListBox rules = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true };
    private readonly CheckedListBox inputs = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
    private readonly ComboBox output = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly string path;
    internal ButtonProfile SavedProfile { get; private set; }

    internal MappingDialog(ButtonProfile current, string path)
    {
        this.path = path;
        profile = current.Copy();
        Text = "Button mappings";
        Font = new Font("Segoe UI", 10);
        ClientSize = new Size(740, 460);
        MinimumSize = new Size(740, 460);
        StartPosition = FormStartPosition.CenterParent;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var help = new Label { AutoSize = true, MaximumSize = new Size(700, 0),
            Text = "Select a rule to edit, or choose input buttons and an output to add one. Longer combinations take priority. Equal-length overlaps use list order." };
        layout.Controls.Add(help, 0, 0); layout.SetColumnSpan(help, 2);
        layout.Controls.Add(rules, 0, 1);
        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.Controls.Add(new Label { Text = "XInput buttons (hold together)", AutoSize = true }, 0, 0);
        inputs.Items.AddRange(ButtonProfile.Inputs);
        editor.Controls.Add(inputs, 0, 1);
        editor.Controls.Add(new Label { Text = "DualSense output", AutoSize = true }, 0, 2);
        output.Items.AddRange(ButtonProfile.Outputs); output.SelectedIndex = 0;
        editor.Controls.Add(output, 0, 3);
        layout.Controls.Add(editor, 1, 1);
        var editActions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        AddButton(editActions, "Add / update", AddRule);
        AddButton(editActions, "Remove selected", (sender, args) =>
        {
            if (rules.SelectedIndex < 0) return;
            profile.Rules.RemoveAt(rules.SelectedIndex);
            RefreshRules();
        });
        layout.Controls.Add(editActions, 0, 2); layout.SetColumnSpan(editActions, 2);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        AddButton(actions, "Restore defaults", (sender, args) => { profile = ButtonProfile.Default(); RefreshRules(); });
        AddButton(actions, "Save profile", (sender, args) =>
        {
            try
            {
                profile.Save(path);
                SavedProfile = profile.Copy();
                DialogResult = DialogResult.OK;
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Could not save profile"); }
        });
        AddButton(actions, "Cancel", (sender, args) => { DialogResult = DialogResult.Cancel; });
        layout.Controls.Add(actions, 0, 3); layout.SetColumnSpan(actions, 2);
        Controls.Add(layout);
        rules.SelectedIndexChanged += (sender, args) =>
        {
            if (rules.SelectedIndex < 0) return;
            var rule = profile.Rules[rules.SelectedIndex];
            for (int i = 0; i < ButtonProfile.Masks.Length; i++)
                inputs.SetItemChecked(i, (rule.Input & ButtonProfile.Masks[i]) != 0);
            output.SelectedItem = rule.Output;
        };
        RefreshRules();
    }

    private static void AddButton(FlowLayoutPanel panel, string text, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += click;
        panel.Controls.Add(button);
    }

    private void RefreshRules()
    {
        rules.Items.Clear();
        foreach (var rule in profile.Rules)
            rules.Items.Add(ButtonProfile.InputName(rule.Input) + " → " + rule.Output);
    }

    private void AddRule(object sender, EventArgs args)
    {
        int mask = 0;
        foreach (int i in inputs.CheckedIndices) mask |= ButtonProfile.Masks[i];
        if (mask == 0) { MessageBox.Show(this, "Choose at least one input button."); return; }
        string target = (string)output.SelectedItem;
        int existing = profile.Rules.FindIndex(rule => rule.Input == mask);
        if (existing >= 0) profile.Rules[existing] = new ButtonRule(mask, target);
        else if (profile.Rules.Count < 64) profile.Rules.Add(new ButtonRule(mask, target));
        else { MessageBox.Show(this, "A profile supports up to 64 rules."); return; }
        RefreshRules();
    }
}
