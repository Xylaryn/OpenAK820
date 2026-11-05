# User guide

## How the app works

Some changes are stored on the keyboard and keep working without the app. Others need the app to stay open.

| Keeps working with the app closed | Needs the app running |
|---|---|
| Lighting modes and user lighting | Real-time lighting |
| Key remaps to keys, mouse actions, media keys and shortcuts | Music lighting |
| Macros | Keys that open programs or websites, send text or switch profile |
| Keyboard settings | Setting the clock when a USB keyboard connects |
| Screen animation | |

**The app can't read back what's on the keyboard.** Pages show the settings saved in the app, which may not match the keyboard until you write them. Writing replaces what was there.

Lighting changes are sent about half a second after you stop adjusting. Everything else waits for you to press **Write to keyboard**, or the upload button on the screen page, to limit wear on the keyboard's flash memory.

Only one program should drive the keyboard at a time. A yellow banner appears if the app sees SignalRGB, OpenRGB or the AJAZZ software running.

## Pages

### Home

Shows the connection and, over 2.4 GHz, the battery level. Manages profiles, which are complete sets of keys, lighting and settings.

- Selecting a profile doesn't change the keyboard. **Write to keyboard** sends its macros, both key layers, settings and lighting, replacing what's stored.
- Macros are shared between all profiles.
- **Copy diagnostic info** is for [bug reports](#reporting-a-problem).

### Keys

Click a key, choose a function and press **Apply**. Changed keys turn blue. When you're done, press **Write to keyboard**. Each layer is written separately.

| Function | What it does |
|---|---|
| Default Key | Returns the key to normal |
| Disable Key | The key does nothing |
| Custom Function | Another key, with Ctrl, Shift, Alt or Win if you like |
| Multi-Key | Sends up to three keys at once |
| Macro Definition | Plays a macro. Choose how it plays and how many times |
| Mouse Function | Clicks, scroll and side buttons |
| Multimedia | Play, stop, track and volume keys |
| System and Web Keys | Calculator, mail, browser keys and similar |
| Windows Shortcut | Show desktop, lock, copy, paste and others |
| Open Program | Opens a program or file. Needs the app running |
| Open Website | Opens an `http` or `https` address. Needs the app running |
| Send Text | Types your text. Needs the app running |
| Switch Profile | Switches profile and writes it to the keyboard. Needs the app running |

The Fn layer works while Fn is held. Fn can't be remapped on either layer, because that could leave you unable to reach the layer. Esc, Backspace, Home, Backslash and Left Win can't be changed on the Fn layer.

**Reset this layer** clears the layer in the app. Press **Write to keyboard** afterwards to apply it.

### Macros

1. Press **New**, then choose how to record timing: real timing, none, or a fixed interval.
2. Press **Start Recording**, do the actions, then press **Stop Recording**. Clicks on the app's own window aren't recorded.
3. Edit if needed. Double-click a row to change its delay.
4. Press **Write to keyboard**.
5. Assign the macro to a key on the Keys page, and write that too.

All macros share about 3 KB on the keyboard, and the counter shows how much you've used. **Recording captures everything you type, including passwords.**

### Lighting Mode

Pick a mode and adjust it. Controls grey out when a mode doesn't use them.

### User Lighting

Select keys by clicking them, or by dragging from an empty area across the board. Choose a colour and press **Paint selected keys**. **Write to keyboard** stores the colours and switches the keyboard to show them. To go back, pick a mode on Lighting Mode.

### Real-time Lighting

Select keys, choose an effect and colour, press **Apply to selected keys**, then **Start**. Needs USB. Your PC draws each frame, so:

- The effect stops when you press **Stop** or exit, and your stored lighting comes back.
- If the app crashes or Windows shuts down mid-effect, the keys can stay frozen. To fix that, open the app and change the lighting mode.

The system timer runs at a higher resolution during effects to keep them smooth, which uses a little more power.

### Music Rhythm

Play something, then press **Start**. Choose a rhythm, colours, amplitude and background. Ambilight follows the colours on your screen. The app reads audio output in memory and never records it.

### TFT Screen

Import a GIF or image, or create frames and draw on them. Images are scaled to fill the 128×128 screen and cropped if needed. Press **Upload to keyboard** when you're done. Needs USB.

- Up to 140 frames, each lasting 30 to 500 ms.
- Allow about 1 second per frame to upload. Don't unplug during an upload. If one fails, the screen can look wrong until you upload again.
- The old animation is replaced and can't be read back, so keep your own copy.
- **Time Sync** sets the clock. The app also does this when a USB keyboard connects.
- After restarting on USB, the screen can show the stored animation until the clock is set.

### System Settings

Key response, sleep time, Fn behaviour and key locks are all written together, so check every value before you press **Write to keyboard**. If keys double-type, raise the key response level.

**Reset Keyboard** returns keys, macros, settings and lighting on the keyboard to defaults, and keeps your profiles in the app.

**Factory Reset** does the same and also deletes the profiles and macros in the app. Neither one changes the screen animation.

## Troubleshooting

| Problem | Try this |
|---|---|
| **No keyboard found** | Check the cable or dongle, and the keyboard's mode switch. Bluetooth isn't supported. If it still isn't found, your board may use a different firmware, so please [report it](#reporting-a-problem) |
| **Red message about another program** | Close SignalRGB, OpenRGB or the AJAZZ software |
| **Keys frozen on one colour** | Pick any mode on Lighting Mode |
| **A key misbehaves** | On Keys: **Reset this layer**, then **Write to keyboard**, for both layers |
| **Macro won't stop** | Unplug the keyboard, and turn it off if it's on battery |
| **Screen shows an old animation at restart** | Open the app, or press **Time Sync**. To replace it, upload a new one |
| **"Needs the USB cable"** | Real-time lighting and screen uploads don't work over 2.4 GHz |
| **Windows blocks the EXE** | Choose **More info**, then **Run anyway**. The EXE isn't code-signed |

## Reporting a problem

Press **Copy diagnostic info** on Home and paste the result into a [bug report](../../../issues/new/choose). It lists the app version and what the keyboard reports about itself: its IDs, interfaces and product name. It contains nothing about you or your PC.

Please don't include your name, email, or file paths that contain your username.

## Removing the app

1. In System Settings, untick **Automatic Start** if it's on.
2. Exit from the tray icon and delete the EXE.
3. Delete `%APPDATA%\OpenAK820` to remove your saved settings.

Anything already written to the keyboard stays there. Use **Reset Keyboard** first if you want to clear it.

## Command line options

| Option | Effect |
|---|---|
| `--hide` | Start hidden in the tray |
| `--simulate` | Use a fake keyboard and temporary settings, to try the app safely |
| `--lang en`, `zh-Hans`, `zh-Hant` or `uk` | Override the language |

`ak820ctl` is a separate command line tool, available in the releases, for scripting and diagnosing. It requires the .NET 10 Desktop Runtime. Run it with no arguments to see its commands.
