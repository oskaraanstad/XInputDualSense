// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.InteropServices;
using System.Text;

internal static class WindowsDevices
{
    private const uint PresentDevices = 0x100;
    private const uint DriverServiceProperty = 5;
    private const uint DeviceStarted = 0x00000008;
    private const uint DeviceHasProblem = 0x00000400;

    [DllImport("cfgmgr32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_ID_List_SizeW(out uint length, string filter, uint flags);

    [DllImport("cfgmgr32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_ID_ListW(string filter, [Out] char[] buffer, uint length, uint flags);

    [DllImport("cfgmgr32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern uint CM_Locate_DevNodeW(out uint device, string instanceId, uint flags);

    [DllImport("cfgmgr32.dll", ExactSpelling = true)]
    private static extern uint CM_Get_DevNode_Status(out uint status, out uint problem, uint device, uint flags);

    [DllImport("cfgmgr32.dll", ExactSpelling = true)]
    private static extern uint CM_Get_Parent(out uint parent, uint device, uint flags);

    [DllImport("cfgmgr32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_DevNode_Registry_PropertyW(uint device, uint property, IntPtr type,
        StringBuilder buffer, ref uint length, uint flags);

    internal static bool VirtualDualSenseReady()
    {
        uint length;
        const string filter = @"HID\VID_054C&PID_0CE6&MI_03";
        const uint flags = PresentDevices | 1; // Present devices, filtered by enumerator and device ID.
        if (CM_Get_Device_ID_List_SizeW(out length, filter, flags) != 0) return false;
        var ids = new char[length];
        if (CM_Get_Device_ID_ListW(filter, ids, length, flags) != 0) return false;

        foreach (string id in new string(ids).Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries))
        {
            uint device, status, problem, parent;
            if (CM_Locate_DevNodeW(out device, id, 0) != 0 ||
                CM_Get_DevNode_Status(out status, out problem, device, 0) != 0 ||
                (status & (DeviceStarted | DeviceHasProblem)) != DeviceStarted)
                continue;

            while (CM_Get_Parent(out parent, device, 0) == 0)
            {
                device = parent;
                var service = new StringBuilder(128);
                uint bytes = (uint)(service.Capacity * sizeof(char));
                if (CM_Get_DevNode_Registry_PropertyW(device, DriverServiceProperty, IntPtr.Zero, service, ref bytes, 0) == 0 &&
                    string.Equals(service.ToString(), "usbip2_ude", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }
}
