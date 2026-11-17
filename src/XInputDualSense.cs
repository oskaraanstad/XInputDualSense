// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class XInputDualSense : Form
{
    private const uint NoSlot = 4;
    private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
    private readonly Label status;
    private readonly InputPreview input = new InputPreview();
    private readonly Button retry;
    private readonly System.Windows.Forms.Timer refresh;
    private const string Waiting = "Waiting for an XInput controller…";
    private volatile string currentStatus = Waiting;
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
        ClientSize = new Size(470, 272);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.White;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 4
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        status = new Label
        {
            Dock = DockStyle.Fill, Text = currentStatus,
            BackColor = Color.FromArgb(238, 244, 255), ForeColor = Color.FromArgb(22, 37, 64),
            Padding = new Padding(12), TextAlign = ContentAlignment.MiddleLeft
        };
        var buttons = new Label
        {
            Dock = DockStyle.Fill, Text = input.ReadText(Stopwatch.GetTimestamp()),
            BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(10, 4, 10, 4),
            TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(3, 8, 3, 0)
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
        layout.Controls.Add(status, 0, 0);
        layout.Controls.Add(buttons, 0, 1);
        layout.Controls.Add(mapping, 0, 2);
        layout.Controls.Add(actions, 0, 3);
        Controls.Add(layout);

        refresh = new System.Windows.Forms.Timer { Interval = 75 };
        refresh.Tick += (sender, args) =>
        {
            status.Text = currentStatus;
            buttons.Text = input.ReadText(Stopwatch.GetTimestamp());
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
        currentStatus = Waiting;
        status.Text = Waiting;
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
            uint slot = NoSlot;
            XInputMapping.Gamepad pad;
            while (!ReadController(ref slot, out pad))
            {
                if (stop.WaitHandle.WaitOne(150)) return;
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
                    ReadController(ref slot, out pad);
                    session.Send(pad);
                }

                registration.Restart();
                bool deviceMissing = false;
                uint? shownSlot = null;
                while (!stop.IsCancellationRequested)
                {
                    if (registration.ElapsedMilliseconds >= 1000)
                    {
                        registration.Restart();
                        bool present = WindowsDevices.VirtualDualSenseReady();
                        if (!present && deviceMissing)
                            throw new InvalidOperationException("The virtual DualSense disconnected. Select Retry to reconnect it.");
                        deviceMissing = !present;
                    }

                    bool connected = ReadController(ref slot, out pad);
                    uint activeSlot = connected ? slot : NoSlot;
                    if (activeSlot != shownSlot)
                    {
                        currentStatus = connected
                            ? "Virtual DualSense is ready.\r\nXInput slot " + (slot + 1) + " is connected."
                            : "Virtual DualSense is ready.\r\nWaiting for the XInput controller to reconnect…";
                        shownSlot = activeSlot;
                    }
                    session.Send(pad);
                    input.Update(connected, pad);
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
        finally
        {
            input.Update(false, new XInputMapping.Gamepad());
        }
    }

    private static bool ReadController(ref uint slot, out XInputMapping.Gamepad pad)
    {
        if (slot < NoSlot && XInputMapping.TryReadGamepad(slot, out pad)) return true;
        for (uint i = 0; i < NoSlot; i++)
        {
            if (!XInputMapping.TryReadGamepad(i, out pad)) continue;
            slot = i;
            return true;
        }
        pad = new XInputMapping.Gamepad();
        return false;
    }
}
