// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class MappingDialog : Form
{
    private ButtonProfile profile;
    private readonly ListBox rules = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true };
    private readonly TextBox capturedInput = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly Label captureHint = new Label { AutoSize = true, MaximumSize = new Size(260, 0) };
    private readonly Func<int> activeSlot;
    private CancellationTokenSource captureStop;
    private int inputMask;
    private bool closing;
    private volatile string captureStatus;

    private readonly ComboBox output = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly string path;
    internal ButtonProfile SavedProfile { get; private set; }

    internal MappingDialog(ButtonProfile current, string path, Func<int> activeSlot)
    {
        this.path = path;
        this.activeSlot = activeSlot;
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
            Text = "Choose New rule, press and release a controller button or combination, then choose its DualSense output. Longer combinations take priority; equal-length overlaps use list order." };
        layout.Controls.Add(help, 0, 0); layout.SetColumnSpan(help, 2);
        layout.Controls.Add(rules, 0, 1);
        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.Controls.Add(new Label { Text = "Controller input", AutoSize = true }, 0, 0);
        capturedInput.Multiline = true;
        capturedInput.Height = 64;
        editor.Controls.Add(capturedInput, 0, 1);
        var listen = new Button { Text = "Listen for input", AutoSize = true };
        editor.Controls.Add(listen, 0, 2);
        editor.Controls.Add(captureHint, 0, 3);
        editor.Controls.Add(new Label { Text = "DualSense output", AutoSize = true }, 0, 4);
        output.Items.AddRange(ButtonProfile.Outputs); output.SelectedItem = "Cross";
        editor.Controls.Add(output, 0, 5);
        layout.Controls.Add(editor, 1, 1);
        var editActions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var newRule = new Button { Text = "New rule", AutoSize = true };
        editActions.Controls.Add(newRule);
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
            SetInput(rule.Input);
            output.SelectedItem = rule.Output;
        };
        listen.Click += async (sender, args) =>
        {
            if (captureStop != null) { captureStop.Cancel(); return; }
            await Listen(listen, actions, editActions);
        };
        newRule.Click += async (sender, args) =>
        {
            rules.ClearSelected();
            SetInput(0);
            output.SelectedItem = "Cross";
            await Listen(listen, actions, editActions);
        };
        var captureTimer = new System.Windows.Forms.Timer { Interval = 50 };
        captureTimer.Tick += (sender, args) =>
        {
            if (captureStop != null) captureHint.Text = captureStatus;
        };
        captureTimer.Start();
        FormClosing += (sender, args) => { closing = true; if (captureStop != null) captureStop.Cancel(); };
        FormClosed += (sender, args) => captureTimer.Dispose();
        SetInput(0);
        RefreshRules();
    }

    private void SetInput(int mask)
    {
        inputMask = mask;
        capturedInput.Text = mask == 0 ? "No input selected" : ButtonProfile.InputName(mask);
    }

    private async Task Listen(Button listen, Control actions, Control editActions)
    {
        if (captureStop != null) return;
        using (var stop = new CancellationTokenSource())
        {
            captureStop = stop;
            captureStatus = "Release all buttons, then press and release the input to assign.";
            captureHint.Text = captureStatus;
            listen.Text = "Cancel listening";
            rules.Enabled = actions.Enabled = editActions.Enabled = output.Enabled = false;
            try
            {
                int mask = await Task.Run(() => CaptureInput(stop.Token));
                if (!closing && !IsDisposed && !stop.IsCancellationRequested)
                {
                    SetInput(mask);
                    captureHint.Text = "Input captured. Choose an output, then select Add / update.";
                    output.Focus();
                }
            }
            catch (OperationCanceledException)
            {
                if (!closing && !IsDisposed) captureHint.Text = "Listening cancelled.";
            }
            catch (Exception error)
            {
                if (!closing && !IsDisposed) captureHint.Text = error.Message;
            }
            finally
            {
                captureStop = null;
                if (!closing && !IsDisposed)
                {
                    listen.Text = "Listen for input";
                    rules.Enabled = actions.Enabled = editActions.Enabled = output.Enabled = true;
                }
            }
        }
    }

    private int CaptureInput(CancellationToken stop)
    {
        var capture = new ButtonCapture();
        int previousSlot = -1;
        using (var wait = new ConnectedPollWait(stop.WaitHandle))
        {
            while (true)
            {
                stop.ThrowIfCancellationRequested();
                int slot = activeSlot();
                XInputMapping.Gamepad pad = new XInputMapping.Gamepad();
                bool connected = slot >= 0 && slot < 4 && XInputMapping.TryReadGamepad((uint)slot, out pad);
                if (slot != previousSlot)
                {
                    capture.Update(false, pad);
                    previousSlot = slot;
                }
                int? mask = capture.Update(connected, pad);
                if (mask.HasValue) return mask.Value;
                captureStatus = connected
                    ? "XInput slot " + (slot + 1) + ". Release all buttons, then press and release your button or combination."
                    : "Waiting for the app's active XInput controller…";
                wait.Wait();
            }
        }
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
        int mask = inputMask;
        if (mask == 0) { MessageBox.Show(this, "Select New rule or Listen for input, then press and release a controller button."); return; }
        string target = (string)output.SelectedItem;
        int existing = profile.Rules.FindIndex(rule => rule.Input == mask);
        if (existing >= 0) profile.Rules[existing] = new ButtonRule(mask, target);
        else if (rules.SelectedIndex >= 0) profile.Rules[rules.SelectedIndex] = new ButtonRule(mask, target);
        else if (profile.Rules.Count < 64) profile.Rules.Add(new ButtonRule(mask, target));
        else { MessageBox.Show(this, "A profile supports up to 64 rules."); return; }
        RefreshRules();
    }
}
