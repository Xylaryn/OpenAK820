# Feature parity with the vendor software (1.0.0.5)

The feature list comes from the vendor's language file, layout file and binary. "Done" means implemented and passing tests against the simulator. See [TESTING.md](TESTING.md) for what has been tested.

## Same or equivalent

| Vendor feature | OpenAK820 |
|---|---|
| Device detection, USB and 2.4 GHz | Done, and event-driven instead of polling |
| Battery level over 2.4 GHz | Done |
| Profiles: new, rename, delete, switch, import, export | Done. Files are JSON |
| Key functions: default, disable, custom key, multi-key, macro, mouse, multimedia, Windows shortcuts, open program, open file, open website, send text | Done |
| Top layer and Fn layer | Done |
| Fn switch (momentary or toggle) | Done |
| Switch profile from a key | Done |
| Macro manager: record, insert key or mouse, edit delay, reorder, delete, import, export | Done |
| Interval options: recorded, none, fixed | Done |
| Macro play modes and loop count | Done. Mode values need hardware confirmation |
| Macro space warnings | Done, with a live byte count |
| 20 lighting modes with colour, colourful, brightness, speed and direction | Done, with controls enabled per mode as in the vendor's table |
| User lighting: per-key colours, schemes, import, export | Done |
| Screen: import GIF, up to 140 frames, per-frame and all-frame delay | Done |
| Screen editor: brush in three sizes, eraser, text with colour and size, grid, new, copy and delete frame, undo, clear, preview | Done |
| Export GIF, upload to keyboard, time sync | Done. Like the vendor app, it also sets the clock whenever a USB keyboard connects, which needs the app running |
| Key response level, sleep time, disable Win, Alt+F4 and Alt+Tab | Done |
| Autostart, minimize to tray | Done |
| Reset keyboard | Done, by writing defaults over USB as the vendor does, plus the wireless clear command |
| Languages: English, Simplified Chinese, Traditional Chinese, Ukrainian | Done. Text is newly written, not copied. Chinese and Ukrainian cover the main labels only, fall back to English elsewhere, and need native-speaker review |

## Different on purpose

| Vendor feature | OpenAK820 | Why |
|---|---|---|
| Firmware update | **Removed** | The package contains no firmware, and a failed flash can leave the keyboard unusable |
| Driver auto-update | **Removed** | The vendor's updater downloads and runs an EXE. New versions should be a deliberate choice |
| Send Text via clipboard and simulated Ctrl+V | Types the text directly | Leaves the clipboard alone |
| Encrypted local database | Plain JSON | Your settings stay readable and portable |
| Writes lighting on every change | Waits until you stop adjusting | Less flash wear |
| Text is dragged into place | Click to place | Simpler |
| Animated background (22.5 MB GIF), vendor skins | Plain dark UI drawn in code | Size, and those assets are not ours to redistribute |

## Added

- Warns when SignalRGB, OpenRGB or the vendor driver is running, and reports failure if another program's reply is received.
- Fn is locked on both layers, so you cannot remap your way out of the Fn layer.
- Importing a file that binds keys to programs or typed text shows what it contains and asks first.
- Website keys only accept `http` and `https`.
- Command line tool and a keyboard-free simulator mode.
- A Copy diagnostic info button for bug reports, which lists what the keyboard reports about itself and nothing about you or your PC.
- Exporting warns you when a file would include program paths, website addresses or typed text.

## Gaps

| Vendor feature | State |
|---|---|
| **Music rhythm effects** | The vendor lists many named effects, drawn by the PC with math that was not recovered. OpenAK820 has six of its own shapes. All foreground and background palettes are present, including Ambilight, but the result will not look the same |
| **Real-time effects** | All eight effects exist and each key can have its own, but they are reinterpretations of the names, not exact copies |
| **On-screen preview of firmware lighting** | The vendor animates its on-screen keyboard using the `F5` read. That read now works on real hardware, but so far it is only in `ak820ctl`, not the UI |
| **Importing vendor profiles and macros** | Not supported. Vendor exports are XML in a format that was not traced, and its database is encrypted, so settings need to be set up again |
| **Other layouts** | Only the ANSI layout from the vendor package is drawn |
| **Default screen animation** | The vendor package ships a 132-frame default animation. It isn't ours to redistribute, so it isn't included, and reset doesn't restore it. You can import it from your own copy of the vendor package |

## Not applicable to this model

Side lighting has strings in the vendor's language file and code, but the layout file for this board turns it off (`sidelight="0"`). The screen and real-time pages are turned off for wireless the same way. Bluetooth has no configuration path in the vendor app either. "Log Off", "Sleep", "Shutdown" and "Restart" exist as strings, but the key encoder has no entries for them.

## Notes on the vendor package

Observations from static analysis, not a security audit. `mui.dll` was only inspected through its exports, and nothing was executed.

- No hard-coded URLs were found in `DeviceDriver.exe`. Update URLs come from `config.xml`, and they are blank in this package, so its updater appears dormant unless that file changes.
- The updater code can download a ZIP, extract it, and launch `Keyboard-Software-Setup.exe` or `FirmwareUpdateTool.exe`. No signature check was seen in the parts reviewed.
- It starts with Windows through `HKCU\...\Run` when enabled, and links a SQLite build with encryption support.
- Installing global keyboard and mouse hooks is expected here, because macro recording needs them. OpenAK820 uses the same technique, but only while you are recording.
