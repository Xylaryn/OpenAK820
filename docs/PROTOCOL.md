# AK820 Pro protocol

Applies to the board identified as `0C45:800A` with product name `AK820`, and the `0C45:FDFD` "2.4G Dongle". Worked out from the vendor application version 1.0.0.5. Addresses such as `0x42B100` refer to its `DeviceDriver.exe` and can be used to check any claim here.

Sonix's vendor ID is shared by unrelated products, and the same IDs are reported for a different firmware family that uses other HID collections. `KeyboardFinder` requires the ID, the product name and the collection shape below before it will talk to a device. Bootloader IDs never match.

## HID collections

| Connection | Usage page | Input | Output | Feature | Purpose |
|---|---|---|---|---|---|
| USB | `FF13` | 65 | 65 | 65 | Commands |
| USB | `FF68` | 65 | 4097 | 0 | Screen data |
| USB | `FFFF` | 4 | 0 | 0 | Key notifications |
| Dongle | `FF60` | 33 | 33 | 0 | Commands |
| Dongle | `FF59` | 65 | 65 | 0 | Purpose unknown; the vendor app's wireless screen path does not appear to need it |
| Dongle | `FFFF` | 4 | 0 | 0 | Key notifications and link status |

Lengths include the report ID, which is always `00` on the command and screen collections.

## USB commands

A frame is 64 bytes, sent as a 65-byte feature report. A command frame is:

```
04 cmd sub 00 00 00 00 00 lo hi 00 ...
```

`lo hi` is a little-endian value at bytes 8 and 9, usually a count of data frames.

**The reply channel is shared.** A feature read returns the last frame the keyboard received, from any program, with the status in byte 3. `01` means accepted, `FF` busy and `00` refused. A sender must check the echoed `04 cmd` to know the reply is its own. On a real board with SignalRGB running, reads returned SignalRGB's `04 20` and `04 02` frames. Data frames also get an echo, but their status is not meaningful.

The vendor app waits 35 ms (`cmd_delaytime` in its layout file) before each send and before each read. Multi-frame data is sent as consecutive feature reports with that delay between them, and without reading replies.

| Cmd | Purpose | Value | Data |
|---|---|---|---|
| `18` | Start a write session | | |
| `02` | Commit | | |
| `F0` | End session; no reply is read | | |
| `13` | Lighting mode | 1 | 1 frame |
| `17`, sub `01` | Keyboard settings | 1 | 1 frame |
| `11` | Key table, top layer | 9 | 576 bytes |
| `27` | Key table, Fn layer | 9 | 576 bytes |
| `19` | Start macro write | | |
| `15` | Macro data | pages | pages x 64 bytes |
| `23` | Stored per-key colours | 9 | 576 bytes |
| `20` | Live colour frame | 8 | 512 bytes |
| `F5` | Read live colours | 9 | 9 frames to read |
| `28` | Set clock | 1 | 1 frame |
| `72`, sub slot | Screen animation | 4 KB pages (u16) | pages on `FF68` |

### Sequences

| Operation | Frames |
|---|---|
| Lighting | `18`, `13`, data (no reply read), `02`, `F0` |
| Per-key colours | Lighting sequence with mode `80`, then `18`, `23`, table, `02`, `F0` |
| Key table | `18`, `11` or `27`, table, `02`, `F0` |
| Macros | `19`, `15`, wait 30 ms, image, wait 30 ms, `02` |
| Settings | `18`, `17`, data (no reply read), `02` |
| Clock | `18`, `28`, data, `02` |
| Screen | `18`, `72`, pages, `02` |
| Live frame | `20`, frame, `02`, using 5 ms and 2 ms delays |
| Read live | `F5`, status read, 9 reads, `02` (answers `00`, because no session is open) |

Without a commit, the keyboard answers `FF` (busy) for a while. On a real board, an `F5` exchange left unclosed still answered `FF` 500 ms later, and independent community testing reports about two seconds. Each write goes to flash, so avoid sending one on every small UI change.

### Lighting data

```
0  mode        (0 = off; the remaining fields are then left zero)
1  R   2  G   3  B
8  colourful   9  brightness 0-5   10  speed 0-5   11  direction
14 AA  15 55
```

| Mode | Name | Caps | | Mode | Name | Caps |
|---|---|---|---|---|---|---|
| 1 | Static | `31` | | 11 | Rolling | `37` |
| 2 | SingleOn | `33` | | 12 | Rotating | `37` |
| 3 | SingleOff | `33` | | 13 | Explode | `33` |
| 4 | Glittering | `33` | | 14 | Launch | `33` |
| 5 | Falling | `33` | | 15 | Ripples | `33` |
| 6 | Colourful | `03` | | 16 | Flowing | `37` |
| 7 | Breath | `33` | | 17 | Pulsating | `33` |
| 8 | Spectrum | `03` | | 18 | Tilt | `37` |
| 9 | Outward | `33` | | 19 | Shuttle | `33` |
| 10 | Scrolling | `3B` | | 0 | LED Off | `00` |

Mode `80` displays the stored per-key colours, with brightness at byte 9. Defaults are mode 11, brightness 5 and speed 3.

Checked on a real board by reading the keys back with `F5`. The firmware scales colours on the way to the LEDs, capping red at `A7` and green and blue at `FB`, which is probably white balance:

| Sent | Brightness | Read back |
|---|---|---|
| `FF 00 00` | 5 | `A7 00 00` |
| `FF 40 80` | 5 | `A7 3F 7E` |
| `FF 40 80` | 2 | `3E 17 2F` |

Per-key tables scale the same way: `FF 00 00`, `00 FF 00` and `00 00 FF` read back as `A7 00 00`, `00 FB 00` and `00 00 FB`, with unset keys dark. So `F5` reports output levels, not the values that were stored.

Caps bits: `01` brightness, `02` speed, `04` horizontal direction (values 0 and 1), `08` vertical direction (values 0 and 2), `10` colourful, `20` palette.

### Settings data

```
1 01   2 disable Win   3 disable Alt+F4   4 disable Alt+Tab
5 Fn switch   6 sleep (0 never, 1 = 1 min, 2 = 5 min, 3 = 30 min)   8 key response 1-5
62 AA  63 55
```

### Clock data

```
1 slot   2 5A   3 year-2000   4 month   5 day   6 hour   7 minute   8 second   10 day of week (Sunday = 0)
62 AA  63 55
```

### Key table

576 bytes: four per key at `key_index * 4`, ending `AA 55`. All zeros keeps the firmware's default.

| Function | Bytes |
|---|---|
| Disabled | `05 03 00 00` |
| Key with modifiers | `02 mods key 00` (`mods`: 1 LCtrl, 2 LShift, 4 LAlt, 8 LWin, 10 RCtrl, 20 RShift, 40 RAlt, 80 RWin) |
| Lone modifier | `02 modbit 00 00` |
| Up to three keys | `07 a b c` |
| Macro | `06 index playmode loops` |
| Mouse button | `01 01 button 00` (1 left, 2 right, 4 middle, 8 and 10 side, 3 double-click) |
| Mouse wheel | `01 03 01 00` up, `01 03 FF 00` down |
| Consumer key | `03 lo hi 00` |
| Handled by the PC | `05 02 id 00` |

Consumer usages: `CD` play/pause, `B7` stop, `B6` previous, `B5` next, `E9` volume up, `EA` volume down, `E2` mute, `0194` computer, `0192` calculator, `018A` mail, `0183` media player, `0223` home, `0227` refresh, `0226` stop, `0224` back, `0225` forward, `022A` favourites, `0221` search.

The keyboard's Fn key is index 96. The layout marks some keys as unavailable on the Fn layer: Esc, Backspace, Home, Backslash, Left Win and Fn itself.

### Key notifications

Pressing a `05 02 id` key sends a 4-byte input report `05 xx id ..` on the `FFFF` collection, and the PC performs the action. The vendor stores four such actions: open program or file (8), open website (9), send text (10) and switch profile (11).

When byte 1 is `A6` the report is dongle link status instead: `05 A6 FF 01` connected, `05 A6 FE 02` disconnected.

### Per-key and live colours

Stored table (`23`): `[index R G B]` at `index * 4`, 576 bytes, ending `AA 55`.

Live frame (`20`): `[index R G B]` records, 512 bytes, no tail. The vendor app packs records in key order, and this works: on a real board, packed frames set all 81 keys correctly with no mode change first. SignalRGB was observed sending them at `index * 4`.

Live frames only appear to scale red: `FF` reads back as `AA`, while green and blue read back as sent.

**The keyboard holds the last frame.** It keeps showing it after streaming stops, unchanged at least 6 seconds later, until a lighting command is sent. Software should send one when it stops or exits. Sending `13` then returns the keys to normal lighting.

Frame rate depends on the PC's timer. The short waits round up to about 15 ms at the default resolution, which gave 5.7 fps. With a 1 ms timer, as the vendor app uses, it reached 16.1 fps with no dropped frames.

### Macro image

```
0x000  Table of up to 100 four-byte slots: u16 offset, 00 00. Empty macros are FF FF FF FF.
0x190  Macro bodies, one after another.
```

Each body starts with a u16 record count and six zero bytes, followed by four-byte records:

| Record | Bytes |
|---|---|
| Key down / up | `00 00 usage B0` / `00 00 usage 30` |
| Mouse down / up | `00 00 button 90` / `00 00 button 10` (1 left, 2 right, 4 middle) |
| Delay | `lo hi 00 50` in ms, minimum 10 |

Each input is followed by a delay record. The image ends with `AA 55` on a 64-byte boundary. Pages are `size / 64 + 1` when `size` is an exact multiple of 64, and `size / 64 + 2` otherwise. The vendor's buffer is `0xE00` bytes.

### Screen animation

128x128 pixels, up to 140 frames (`gif_maxframes`). The vendor layout allows 30 to 500 ms per frame.

```
0        frame count
1..n     per-frame delay in units of 2 ms, minimum 1
..255    FF
256..    frames: RGB565, little-endian
         FF padding to a multiple of 4096
```

On a real board restarted on USB, the screen shows the stored animation until the clock is set. The vendor app sets the clock every time a USB keyboard connects (`0x434810`), and so does OpenAK820. This suggests the clock command also changes what the screen shows, but that rests on one user's report, and the reason it was only seen on USB is unknown.

Each 4096-byte page goes out as a 4097-byte output report on `FF68` with report ID `00`, followed by a read with a 300 ms timeout. The vendor app ignores that read's result. OpenAK820 stops the upload if the keyboard stops acknowledging, rather than sending data it may not have room for. On a real board, an 8-frame upload of 65 pages took 8.3 seconds, about 125 ms per page, with every page acknowledged. The test animation displayed correctly, which confirms the frame order, the top-left pixel origin and little-endian RGB565. Slot 1 works on this board.

## 2.4 GHz commands

A frame is a 33-byte output report: `00 cmd sub third ...`. Byte 32 is the sum of the bytes before it. The reply echoes `cmd sub third`, and the vendor app retries the read up to 20 times, 10 ms apart. Replies may arrive with or without the leading report ID, depending on how they are read.

| Frame | Purpose |
|---|---|
| `05 10 00` + lighting data at byte 4, `AA 55` at 18 | Lighting |
| `07 10 00` + settings data at byte 4, `AA 55` at 18 | Settings |
| `0C 10 00` + clock data at byte 4, `AA 55` at 18 | Clock |
| `10 len idx` + 28 bytes | Key table, top layer |
| `12 len idx` + 28 bytes | Key table, Fn layer |
| `14 len idx` + 28 bytes | Per-key table (after selecting mode `80`) |
| `09 len idx` + 28 bytes | Macro image |
| `0B 1C 00 fore back bright`, five levels at byte 23 | Music levels |
| `20 01` | Battery: reply byte 3 is the percentage |
| `1F` | Clear stored settings |

Chunked data uses `len` = 28, except the last chunk, which gives the bytes remaining. The wireless macro image ends with `AA 55` on a 28-byte boundary, and its last `len` counts the bytes actually used, not the padding.

The vendor's layout file turns off the screen and real-time pages over 2.4 GHz, although its code has a `7F 03` / `80` screen path. OpenAK820 follows the layout file. Bluetooth has no configuration path.

## Open questions

These were worked out indirectly and need testing on hardware.

| Item | Why it is uncertain |
|---|---|
| Macro `playmode` values 0 to 4 | Inferred from the order of items in a combo box |
| Which direction is 0, 1 and 2 | Values are confirmed; which way each one moves is not |
| Wireless music `fore` and `back` values | The value set could not be fully traced |
| Whether `20` frames write to flash | Unknown. The vendor app and SignalRGB both send them continuously, which suggests not. Also unknown: whether a held frame survives a power cycle |
| Screen frame limit | Vendor layout says 140. A 132-frame upload (1,057 pages, 128 seconds) was fully acknowledged on a real board. 140 itself has not been tried. Another project limits uploads to 30 on a different firmware family |
| Sleep and key response positions | The binary puts them at bytes 6 and 8. One other project's notes place sleep at byte 8, untested |
