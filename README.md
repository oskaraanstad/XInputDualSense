<img src="assets/icon.png" width="96" alt="XInput to DualSense icon">

# XInput to DualSense

[![Download for Windows x64](assets/download-windows.svg)](https://github.com/oskaraanstad/XInputDualSense/releases/latest/download/XInputDualSense-win-x64.zip)

[Release notes and previous versions](https://github.com/oskaraanstad/XInputDualSense/releases)

Turn an Xbox-compatible XInput controller into a virtual USB DualSense on Windows. The app detects the first available XInput controller, keeps the virtual device connected through controller reconnects, and removes it when you exit.

## Getting started

1. Install [USBip-Win2 0.9.8.1 for x64](https://github.com/vadimgrn/usbip-win2/releases/tag/v.0.9.8.1). This is a one-time setup.
2. Extract `XInputDualSense-win-x64.zip` and keep `libVIIPER.dll` next to the app.
3. Connect your controller in **XInput mode** and open `XInputDualSense.exe`.
4. Once the app says **Virtual DualSense is ready**, open your game or streaming app. Choose **Test in Windows** to open the Windows game controller panel.

Use **Exit** to disconnect the virtual controller. The app runs without administrator privileges once the USB/IP driver is installed.

## Live input preview (development builds)

The status panel shows the active XInput slot and whether Windows has registered the virtual DualSense. **Pressed buttons** shows the physical controller's XInput button names, including trigger presses. This helps confirm that the app sees input before you test the virtual controller in a game.

## Controller mapping

| XInput | DualSense |
| --- | --- |
| South, east, west, north face buttons | Cross, Circle, Square, Triangle |
| D-pad, sticks, stick clicks, bumpers and analog triggers | Corresponding DualSense controls |
| Start / Select | Options / Create |
| Select + Start | PS button |
| Select + right stick click | Touchpad click |

The physical controller remains visible in Windows. Some games can register input twice if they read both devices. The app maps one controller at a time. Touchpad gestures, motion, audio, vibration and adaptive trigger feedback are outside the current input mapping.

## Troubleshooting

- **Waiting for an XInput controller** — switch your controller to XInput mode or reconnect it.
- **A virtual DualSense is already connected** — close the other controller emulator, then choose **Retry**.
- **USB/IP attach fails** — check that USBip-Win2 is installed and that Windows has a single active USB/IP host controller. The official `usbip.exe port` command can report duplicate host controller interfaces.
- **PS Remote Play** — create the virtual controller before opening Sony's app. One Windows setup was tested with an 8BitDo in XInput mode and the bundled full DualSense backend. Compatibility can vary by client version and system setup.

## Build from source

Requires Windows 10 or 11 x64 and the Windows PowerShell/.NET Framework compiler included with Windows. From the repository root:

```powershell
.\scripts\build.ps1
```

The script produces `artifacts/bin/XInputDualSense.exe` and `artifacts/XInputDualSense-win-x64.zip`. GitHub Actions runs the same script and uploads the ZIP.

## License

The app source is provided under [GPL-3.0-or-later](LICENSE). See [third-party notices](THIRD_PARTY_NOTICES.md) for the pinned libVIIPER binary and its corresponding source.
