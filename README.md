# OpenAK820

[![CI](../../actions/workflows/ci.yml/badge.svg)](../../actions/workflows/ci.yml)

A small, open-source replacement for the AJAZZ AK820 Pro configuration software, for the triple-mode model with the 0.85-inch colour screen and RGB lighting. Windows only.

> **Early alpha.** It's been tested on one keyboard. It works well there, but not every feature has been tried in every connection mode. See [Testing](docs/TESTING.md).
>
> Not affiliated with or endorsed by AJAZZ.

## Quick start

1. Download `OpenAK820-...-win-x64.exe` from [Releases](../../releases). There's nothing to install.
2. Close the SignalRGB app, OpenRGB and the AJAZZ software if any of them are running.
3. Connect the keyboard by USB and open the EXE.

**Windows will probably warn you** with "Windows protected your PC", because the EXE isn't code-signed. Choose **More info**, then **Run anyway**. Each release lists a SHA-256 checksum and is built by GitHub Actions from the public source, so you can check where it came from.

**Before you change anything:** the app can't read what's already stored on a keyboard. Writing keys, macros or settings replaces what's there, and your old values can't be recovered. New to the app? Start with Lighting Mode, which is easy to set again.

If the app shows **No keyboard found**, see [Troubleshooting](docs/USER-GUIDE.md#troubleshooting).

## Features

| Area | What you can do |
|---|---|
| **Keys** | Remap keys on the top and Fn layers: other keys and combos, mouse actions, media and web keys, Windows shortcuts, macros, or opening programs, websites and text |
| **Macros** | Record keyboard and mouse input, edit timing, and store macros on the keyboard |
| **Lighting** | 20 built-in modes with colour, brightness, speed and direction |
| **User lighting** | Set a colour per key, stored on the keyboard |
| **Real-time lighting** | Give each key its own effect, animated by your PC |
| **Music** | Lighting that reacts to what your PC is playing |
| **Screen** | Import or draw animations of up to 140 frames, upload them, and set the clock |
| **Settings** | Key response, sleep time, Fn behaviour, and Win, Alt+F4 and Alt+Tab locks |
| **App** | Profiles, tray and autostart, and English, Chinese and Ukrainian (partly translated) |

The [User Guide](docs/USER-GUIDE.md) covers each page. [Feature parity](docs/PARITY.md) compares everything with the AJAZZ software, including what's missing.

### What it deliberately doesn't do

- **No firmware flashing.** A failed flash can leave a keyboard unusable.
- **No automatic updates.** You choose when to download a new version.
- **No vendor files.** AJAZZ's images, fonts and animations aren't included.

## Supported keyboards

| Connection | USB ID | Requirement |
|---|---|---|
| USB | `0C45:8009` or `0C45:800A` | Product name `AK820` and a control interface on usage page `FF13` |
| 2.4 GHz dongle | `0C45:FDFD` | Product name `2.4G Dongle` |

Many unrelated products share this vendor ID, and some AK820 Pro boards with the same IDs run a different firmware. The app won't talk to anything that doesn't match exactly, so those boards show **No keyboard found**. If that's you, please [report it](docs/USER-GUIDE.md#reporting-a-problem).

Bluetooth can't configure the keyboard, and this is also true of the AJAZZ software.

## Privacy

- **No network code.** The app has no code that connects to the internet. It sends no telemetry and doesn't check for updates.
- **Everything stays on your PC.** Settings are in `%APPDATA%\OpenAK820`. Files only leave that folder if you export them.
- **No accounts.**
- **Music** reads your PC's audio output as it plays. It's analysed in memory and never recorded or saved.
- **Macro recording** watches all keyboard and mouse input, but only while you're recording. It captures whatever you type, including passwords, so check a macro before sharing it.
- **Exported files** can contain personal details. Profiles can include program paths, which may contain your Windows username, along with websites and text you've assigned to keys. The app warns you about this when you export.
- **Bug reports:** Copy diagnostic info lists only what the keyboard reports about itself.

## Building from source

Requires Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). There are no third-party runtime dependencies.

```bash
dotnet test
```

```bash
dotnet publish src/OpenAK820 -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o out
```

| Folder | Contents |
|---|---|
| `src/OpenAK820.Core` | HID access, protocol, byte encoders and data model, with no UI |
| `src/OpenAK820` | The Windows app |
| `src/ak820ctl` | Command line tool |
| `tests` | Automated tests |
| `docs` | [User guide](docs/USER-GUIDE.md), [protocol](docs/PROTOCOL.md), [parity](docs/PARITY.md), [testing](docs/TESTING.md) |

Want to help? See [CONTRIBUTING.md](CONTRIBUTING.md). Security issues go through [SECURITY.md](SECURITY.md).

## Credits and legal

The protocol was worked out by examining the vendor software, to let this software work with hardware its users own. Findings were cross-checked against community work, especially [wsclx/ak820pro-modder](https://github.com/wsclx/ak820pro-modder). No code from that project or the vendor is included.

This repository contains no vendor code, images, fonts or translations. Key positions and indices are functional data, redrawn in code. AJAZZ and AK820 are used only to say which hardware this works with, and belong to their owners.

Rules on reverse engineering vary by country. This software comes with no warranty, and you use it at your own risk. Writing to a keyboard can change or erase its stored settings.

[MIT licensed](LICENSE).
