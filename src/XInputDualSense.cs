// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class XInputDualSense : Form
{
    private static readonly XInputMapping.SlotReader ReadSlot = XInputMapping.TryReadGamepad;
    private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
    private readonly Label status;
    private readonly Button retry;
    private readonly System.Windows.Forms.Timer refresh;
    private const string Waiting = "Waiting for an XInput controller…";
    private volatile string currentStatus = Waiting;
    private volatile int requestedSlot = XInputMapping.AutomaticSlot;
    private Task conversion;
    private bool canClose;

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (var instance = new Mutex(false, @"Local\XInputDualSense-libVIIPER"))
        {
            bool acquired;
            try { acquired = instance.WaitOne(0); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired)
            {
                MessageBox.Show("XInput to DualSense is already running.", "XInput to DualSense");
                return;
            }

            try
            {
                using (var window = new XInputDualSense())
                    Application.Run(window);
            }
            finally { instance.ReleaseMutex(); }
        }
    }

    private XInputDualSense()
    {
        Text = "XInput to DualSense";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Font;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(470, 254);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.White;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 4
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var selection = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill, WrapContents = false,
            Margin = new Padding(0, 0, 0, 8)
        };
        var sourceLabel = new Label
        {
            Text = "XInput source", AutoSize = true, Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 0, 8, 0)
        };
        var source = new ComboBox
        {
            AccessibleName = "XInput source", DropDownStyle = ComboBoxStyle.DropDownList, Width = 145
        };
        source.Items.Add("Automatic");
        for (int slot = 1; slot <= XInputMapping.NoSlot; slot++)
            source.Items.Add("Slot " + slot);
        source.SelectedIndex = 0;
        source.SelectedIndexChanged += (sender, args) => requestedSlot = source.SelectedIndex - 1;
        selection.Controls.Add(sourceLabel);
        selection.Controls.Add(source);
        status = new Label
        {
            Dock = DockStyle.Fill, Text = currentStatus,
            BackColor = Color.FromArgb(238, 244, 255), ForeColor = Color.FromArgb(22, 37, 64),
            Padding = new Padding(12), TextAlign = ContentAlignment.MiddleLeft
        };
        var mapping = new Label
        {
            AutoSize = true, Margin = new Padding(3, 10, 0, 0),
            Text = "Select + Start: PS button   |   Select + R3: touchpad click"
        };
        var actions = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, Margin = new Padding(0, 10, 0, 0)
        };
        var close = new Button { Text = "Exit", AutoSize = true };
        retry = new Button { Text = "Retry", AutoSize = true, Enabled = false };
        var test = new Button { Text = "Test in Windows", AutoSize = true };
        close.Click += (sender, args) => Close();
        retry.Click += (sender, args) => StartConversion();
        test.Click += (sender, args) =>
        {
            try
            {
                using (Process.Start(new ProcessStartInfo("control.exe", "joy.cpl"))) { }
            }
            catch (System.ComponentModel.Win32Exception error)
            {
                MessageBox.Show(this, error.Message, Text);
            }
        };
        actions.Controls.Add(close);
        actions.Controls.Add(retry);
        actions.Controls.Add(test);
        layout.Controls.Add(selection, 0, 0);
        layout.Controls.Add(status, 0, 1);
        layout.Controls.Add(mapping, 0, 2);
        layout.Controls.Add(actions, 0, 3);
        Controls.Add(layout);

        refresh = new System.Windows.Forms.Timer { Interval = 150 };
        refresh.Tick += (sender, args) =>
        {
            status.Text = currentStatus;
            retry.Enabled = conversion != null && conversion.IsCompleted;
        };
        Shown += (sender, args) =>
        {
            StartConversion();
            refresh.Start();
        };
        FormClosing += CloseConversion;
        FormClosed += (sender, args) =>
        {
            refresh.Dispose();
            shutdown.Dispose();
        };
    }

    private void StartConversion()
    {
        if (conversion != null && !conversion.IsCompleted) return;
        currentStatus = WaitingForController(requestedSlot);
        status.Text = currentStatus;
        retry.Enabled = false;
        conversion = Task.Run(() => ConvertInput(shutdown.Token));
    }

    private async void CloseConversion(object sender, FormClosingEventArgs args)
    {
        if (conversion == null || canClose) return;
        args.Cancel = true;
        shutdown.Cancel();
        refresh.Stop();
        status.Text = "Disconnecting the virtual controller…";
        await conversion;
        // Yield even if the worker had already stopped so the first close event returns.
        await Task.Yield();
        if (canClose) return;
        canClose = true;
        Close();
    }

    private void ConvertInput(CancellationToken stop)
    {
        try
        {
            uint lastAutoSlot = XInputMapping.NoSlot;
            uint activeSlot;
            int selection = requestedSlot;
            XInputMapping.Gamepad pad;
            while (!XInputMapping.TryReadController(selection, ref lastAutoSlot, ReadSlot, out activeSlot, out pad))
            {
                currentStatus = WaitingForController(selection);
                if (stop.WaitHandle.WaitOne(150)) return;
                selection = requestedSlot;
            }

            if (stop.IsCancellationRequested) return;
            if (WindowsDevices.VirtualDualSenseReady())
                throw new InvalidOperationException("A virtual DualSense is already connected. Close the app that created it, then select Retry.");

            currentStatus = "Connecting the virtual DualSense…";
            using (var session = new DualSenseSession())
            {
                session.Send(pad);
                var registration = Stopwatch.StartNew();
                while (!WindowsDevices.VirtualDualSenseReady())
                {
                    if (stop.WaitHandle.WaitOne(100)) return;
                    if (registration.ElapsedMilliseconds >= 12000)
                        throw new InvalidOperationException("Windows did not find the virtual DualSense. Check USBip-Win2 and select Retry.");
                    XInputMapping.TryReadController(requestedSlot, ref lastAutoSlot, ReadSlot, out activeSlot, out pad);
                    session.Send(pad);
                }

                uint? shownSlot = null;
                int? shownSelection = null;
                while (!stop.IsCancellationRequested)
                {
                    selection = requestedSlot;
                    bool connected = XInputMapping.TryReadController(selection, ref lastAutoSlot, ReadSlot, out activeSlot, out pad);
                    if (activeSlot != shownSlot || selection != shownSelection)
                    {
                        currentStatus = "Virtual DualSense is ready.\r\n" + (connected
                            ? "XInput slot " + (activeSlot + 1) + " is connected."
                            : WaitingForController(selection));
                        shownSlot = activeSlot;
                        shownSelection = selection;
                    }
                    session.Send(pad);
                    stop.WaitHandle.WaitOne(connected ? 4 : 150);
                }
            }
        }
        catch (DllNotFoundException)
        {
            currentStatus = "Could not load libVIIPER.dll. Keep the DLL next to the app.";
        }
        catch (Exception error)
        {
            currentStatus = error.Message;
        }
    }

    private static string WaitingForController(int selection)
    {
        return selection == XInputMapping.AutomaticSlot
            ? Waiting
            : "Waiting for an XInput controller in slot " + (selection + 1) + "…";
    }
}
