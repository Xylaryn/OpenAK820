using OpenAK820.Model;

namespace OpenAK820.Protocol;

/// <summary>Pure byte-layout code. Nothing here touches hardware, so it is all covered by golden tests.</summary>
public static class Encoders
{
    public const int FrameSize = 64;
    public const int TableSize = 576;
    public const int RealtimeSize = 512;
    public const int MacroImageSize = 0xE00;
    public const int MacroTableSize = 0x190;
    public const int MaxMacros = MacroTableSize / 4;
    public const int ScreenHeaderSize = 256;
    public const int ScreenPageSize = 4096;
    public const int ScreenWidth = 128, ScreenHeight = 128, ScreenMaxFrames = 140;

    private static void Tail(byte[] buffer)
    {
        buffer[^2] = 0xAA;
        buffer[^1] = 0x55;
    }

    /// <summary><c>04 cmd sub .. [8]=value lo [9]=value hi</c></summary>
    public static byte[] Command(byte cmd, byte sub = 0, int value = 0)
    {
        var f = new byte[FrameSize];
        f[0] = 0x04; f[1] = cmd; f[2] = sub;
        f[8] = (byte)value; f[9] = (byte)(value >> 8);
        return f;
    }

    public static byte[] LightingData(LightingState s)
    {
        var f = new byte[FrameSize];
        WriteLighting(f, 0, s);
        f[14] = 0xAA; f[15] = 0x55;
        return f;
    }

    private static void WriteLighting(byte[] f, int at, LightingState s)
    {
        f[at] = (byte)s.Mode;
        if (s.Mode == 0) return;
        var c = Rgb.Parse(s.Color);
        f[at + 1] = c.R; f[at + 2] = c.G; f[at + 3] = c.B;
        f[at + 8] = (byte)(s.Colorful ? 1 : 0);
        f[at + 9] = (byte)s.Brightness;
        f[at + 10] = (byte)s.Speed;
        f[at + 11] = (byte)s.Direction;
    }

    /// <summary>Selects the firmware's stored per-key colour mode at a given brightness.</summary>
    public static byte[] UserLightingMode(int brightness)
    {
        var f = new byte[FrameSize];
        f[0] = LightingModes.UserDefined;
        f[9] = (byte)brightness;
        f[14] = 0xAA; f[15] = 0x55;
        return f;
    }

    public static byte[] ConfigData(DeviceConfig c)
    {
        var f = new byte[FrameSize];
        WriteConfig(f, 0, c);
        f[62] = 0xAA; f[63] = 0x55;
        return f;
    }

    private static void WriteConfig(byte[] f, int at, DeviceConfig c)
    {
        f[at + 1] = 1;
        f[at + 2] = (byte)(c.DisableWinKey ? 1 : 0);
        f[at + 3] = (byte)(c.DisableAltF4 ? 1 : 0);
        f[at + 4] = (byte)(c.DisableAltTab ? 1 : 0);
        f[at + 5] = (byte)c.FnSwitch;
        f[at + 6] = (byte)c.SleepTime;
        f[at + 8] = (byte)c.KeyResponse;
    }

    public static byte[] ClockData(DateTime t, int slot = 1)
    {
        var f = new byte[FrameSize];
        WriteClock(f, 0, t, slot);
        f[62] = 0xAA; f[63] = 0x55;
        return f;
    }

    private static void WriteClock(byte[] f, int at, DateTime t, int slot)
    {
        f[at + 1] = (byte)slot;
        f[at + 2] = 0x5A;
        f[at + 3] = (byte)(t.Year % 2000);
        f[at + 4] = (byte)t.Month;
        f[at + 5] = (byte)t.Day;
        f[at + 6] = (byte)t.Hour;
        f[at + 7] = (byte)t.Minute;
        f[at + 8] = (byte)t.Second;
        f[at + 10] = (byte)t.DayOfWeek;
    }

    /// <summary>Encodes one key action as its four-byte table entry.</summary>
    public static (byte, byte, byte, byte) KeyEntry(KeyAction a)
    {
        switch (a.Kind)
        {
            case KeyActionKind.Disabled: return (0x05, 0x03, 0, 0);
            case KeyActionKind.KeyCombo:
                // A lone modifier key is sent as a modifier bit, not as a key code.
                if (a.Mods == KeyMods.None && a.Key is >= 0xE0 and <= 0xE7) return (0x02, (byte)(1 << (a.Key - 0xE0)), 0, 0);
                return (0x02, (byte)a.Mods, a.Key, 0);
            case KeyActionKind.MultiKey: return (0x07, a.Key, a.Key2, a.Key3);
            case KeyActionKind.Macro: return (0x06, (byte)a.MacroIndex, (byte)a.PlayMode, a.Loops);
            case KeyActionKind.Mouse:
                return (MouseAction)a.Value switch
                {
                    MouseAction.LeftClick => (0x01, 0x01, 0x01, 0),
                    MouseAction.MiddleClick => (0x01, 0x01, 0x04, 0),
                    MouseAction.RightClick => (0x01, 0x01, 0x02, 0),
                    MouseAction.DoubleClick => (0x01, 0x01, 0x03, 0),
                    MouseAction.ScrollUp => (0x01, 0x03, 0x01, 0),
                    MouseAction.ScrollDown => (0x01, 0x03, 0xFF, 0),
                    MouseAction.Button4 => (0x01, 0x01, 0x08, 0),
                    MouseAction.Button5 => (0x01, 0x01, 0x10, 0),
                    _ => (0x01, 0, 0, 0),
                };
            case KeyActionKind.Media:
                return (MediaAction)a.Value switch
                {
                    MediaAction.PlayPause => (0x03, 0xCD, 0, 0),
                    MediaAction.Stop => (0x03, 0xB7, 0, 0),
                    MediaAction.Previous => (0x03, 0xB6, 0, 0),
                    MediaAction.Next => (0x03, 0xB5, 0, 0),
                    MediaAction.VolumeUp => (0x03, 0xE9, 0, 0),
                    MediaAction.VolumeDown => (0x03, 0xEA, 0, 0),
                    MediaAction.Mute => (0x03, 0xE2, 0, 0),
                    _ => (0x03, 0, 0, 0),
                };
            case KeyActionKind.Consumer:
                var (lo, hi) = (ConsumerAction)a.Value switch
                {
                    ConsumerAction.MyComputer => (0x94, 0x01),
                    ConsumerAction.Calculator => (0x92, 0x01),
                    ConsumerAction.Mail => (0x8A, 0x01),
                    ConsumerAction.MediaPlayer => (0x83, 0x01),
                    ConsumerAction.Homepage => (0x23, 0x02),
                    ConsumerAction.WebRefresh => (0x27, 0x02),
                    ConsumerAction.WebStop => (0x26, 0x02),
                    ConsumerAction.WebBack => (0x24, 0x02),
                    ConsumerAction.WebForward => (0x25, 0x02),
                    ConsumerAction.WebFavorites => (0x2A, 0x02),
                    ConsumerAction.WebSearch => (0x21, 0x02),
                    _ => (0, 0),
                };
                return (0x03, (byte)lo, (byte)hi, 0);
            case KeyActionKind.WindowsShortcut:
                return (WindowsShortcut)a.Value switch
                {
                    WindowsShortcut.ShowDesktop => (0x02, 0x08, 0x07, 0),
                    WindowsShortcut.MyComputer => (0x02, 0x08, 0x08, 0),
                    WindowsShortcut.LockComputer => (0x02, 0x08, 0x0F, 0),
                    WindowsShortcut.CloseWindow => (0x02, 0x01, 0x1A, 0),
                    WindowsShortcut.SwitchWindows => (0x02, 0x04, 0x2B, 0),
                    WindowsShortcut.Copy => (0x02, 0x01, 0x06, 0),
                    WindowsShortcut.Paste => (0x02, 0x01, 0x19, 0),
                    WindowsShortcut.Cut => (0x02, 0x01, 0x1B, 0),
                    _ => (0x02, 0, 0, 0),
                };
            case KeyActionKind.OpenProgram:
            case KeyActionKind.OpenWebsite:
            case KeyActionKind.SendText:
            case KeyActionKind.SwitchProfile:
                return (0x05, 0x02, a.HostId, 0);
            default: return (0, 0, 0, 0);
        }
    }

    /// <summary>Builds the 576-byte table for one layer. Entries left at zero keep the firmware default.</summary>
    public static byte[] KeyTable(IReadOnlyDictionary<int, KeyAction> actions, int layer)
    {
        var t = new byte[TableSize];
        foreach (var (index, action) in actions)
        {
            if (index < 0 || index * 4 + 3 >= TableSize - 2) continue;
            if (layer == 1 && KeyLayout.ByIndex(index)?.FnLayerLocked == true) continue;
            var (a, b, c, d) = KeyEntry(action);
            t[index * 4] = a; t[index * 4 + 1] = b; t[index * 4 + 2] = c; t[index * 4 + 3] = d;
        }
        Tail(t);
        return t;
    }

    /// <summary>Builds the 576-byte stored per-key colour table: <c>[index R G B]</c> at index*4.</summary>
    public static byte[] PerKeyTable(IReadOnlyDictionary<int, Rgb> colors)
    {
        var t = new byte[TableSize];
        foreach (var (index, c) in colors)
        {
            if (index < 0 || index * 4 + 3 >= TableSize - 2) continue;
            t[index * 4] = (byte)index; t[index * 4 + 1] = c.R; t[index * 4 + 2] = c.G; t[index * 4 + 3] = c.B;
        }
        Tail(t);
        return t;
    }

    /// <summary>Builds a 512-byte live frame: <c>[index R G B]</c> records packed in layout order.</summary>
    public static byte[] RealtimeFrame(IReadOnlyDictionary<int, Rgb> colors)
    {
        var t = new byte[RealtimeSize];
        int at = 0;
        foreach (var key in KeyLayout.Keys)
        {
            colors.TryGetValue(key.Index, out var c);
            t[at] = (byte)key.Index; t[at + 1] = c.R; t[at + 2] = c.G; t[at + 3] = c.B;
            at += 4;
        }
        return t;
    }

    /// <summary>Bytes a macro needs on the device, so the UI can warn before the image overflows.</summary>
    public static int MacroBytes(Macro m) => m.Events.Count == 0 ? 0 : 8 + m.Events.Count * 8;

    /// <summary>
    /// Macro image: a table of u32 slots at the start (u16 offset, then padding; FF FF FF FF if empty),
    /// then for each macro a u16 record count, six zero bytes, and four-byte records:
    /// key <c>00 00 code B0/30</c>, mouse <c>00 00 btn 90/10</c>, delay <c>lo hi 00 50</c>.
    /// </summary>
    public static byte[] MacroImage(IReadOnlyList<Macro> macros, out int pages) => MacroImage(macros, out pages, out _);

    private static byte[] MacroBody(IReadOnlyList<Macro> macros, out int used)
    {
        var image = new byte[MacroImageSize + FrameSize];
        int offset = MacroTableSize;
        for (int i = 0; i < macros.Count && i < MaxMacros; i++)
        {
            var events = macros[i].Events;
            if (events.Count == 0)
            {
                image[i * 4] = image[i * 4 + 1] = image[i * 4 + 2] = image[i * 4 + 3] = 0xFF;
                continue;
            }
            if (offset + 8 + events.Count * 8 > MacroImageSize)
                throw new InvalidOperationException("Macro space is full.");
            image[i * 4] = (byte)offset;
            image[i * 4 + 1] = (byte)(offset >> 8);
            int at = offset + 8, records = 0;
            foreach (var e in events)
            {
                if (e.Kind is MacroEventKind.KeyDown or MacroEventKind.KeyUp)
                {
                    image[at + 2] = (byte)e.Code;
                    image[at + 3] = (byte)(e.Kind == MacroEventKind.KeyDown ? 0xB0 : 0x30);
                }
                else
                {
                    image[at + 2] = (byte)((MacroMouseButton)e.Code switch { MacroMouseButton.Left => 1, MacroMouseButton.Middle => 4, MacroMouseButton.Right => 2, _ => 0 });
                    image[at + 3] = (byte)(e.Kind == MacroEventKind.MouseDown ? 0x90 : 0x10);
                }
                at += 4; records++;
                var delay = Math.Max(e.DelayMs, 10);
                image[at] = (byte)delay;
                image[at + 1] = (byte)(delay >> 8);
                image[at + 3] = 0x50;
                at += 4; records++;
            }
            image[offset] = (byte)records;
            image[offset + 1] = (byte)(records >> 8);
            offset += records * 4 + 8;
        }
        used = offset;
        return image;
    }

    private static byte[] MacroImage(IReadOnlyList<Macro> macros, out int pages, out int used)
    {
        var image = MacroBody(macros, out used);
        pages = used / FrameSize + (used % FrameSize == 0 ? 1 : 2);
        Array.Resize(ref image, pages * FrameSize);
        Tail(image);
        return image;
    }

    /// <summary>The dongle sizes macro data in 28-byte chunks rather than 64-byte pages.</summary>
    public static byte[] WirelessMacroImage(IReadOnlyList<Macro> macros, out int used)
    {
        var image = MacroBody(macros, out used);
        var chunks = used / WirelessChunk + (used % WirelessChunk == 0 ? 0 : 1);
        Array.Resize(ref image, chunks * WirelessChunk);
        Tail(image);
        return image;
    }

    /// <summary>
    /// Screen image: frame count, one delay byte per frame (ms / 2, minimum 1), FF filler to 256 bytes,
    /// then little-endian RGB565 pixels, padded with FF to a whole number of 4 KB pages.
    /// </summary>
    public static byte[] ScreenImage(IReadOnlyList<(ushort[] Pixels, int DelayMs)> frames, out int pages)
    {
        var count = Math.Min(frames.Count, ScreenMaxFrames);
        var payload = ScreenHeaderSize + count * ScreenWidth * ScreenHeight * 2;
        pages = payload / ScreenPageSize + (payload % ScreenPageSize == 0 ? 0 : 1);
        var image = new byte[pages * ScreenPageSize];
        Array.Fill(image, (byte)0xFF);
        image[0] = (byte)count;
        for (int f = 0; f < count; f++)
        {
            image[f + 1] = (byte)Math.Clamp(frames[f].DelayMs / 2, 1, 255);
            var px = frames[f].Pixels;
            if (px.Length != ScreenWidth * ScreenHeight) throw new ArgumentException("Frames must be 128x128.");
            Buffer.BlockCopy(px, 0, image, ScreenHeaderSize + f * ScreenWidth * ScreenHeight * 2, px.Length * 2);
        }
        return image;
    }

    public static ushort ToRgb565(byte r, byte g, byte b) => (ushort)(((r & 0xF8) << 8) | ((g & 0xFC) << 3) | (b >> 3));

    // ---- 2.4 GHz frames: [00 cmd sub ...] 33 bytes, checksum in the last byte ----

    public const int WirelessSize = 33;
    public const int WirelessChunk = 28;

    public static byte[] Wireless(byte cmd, byte sub = 0, byte third = 0)
    {
        var f = new byte[WirelessSize];
        f[1] = cmd; f[2] = sub; f[3] = third;
        return f;
    }

    public static byte[] Seal(byte[] frame)
    {
        int sum = 0;
        for (int i = 0; i < WirelessSize - 1; i++) sum += frame[i];
        frame[WirelessSize - 1] = (byte)sum;
        return frame;
    }

    public static byte[] WirelessLighting(LightingState s)
    {
        var f = Wireless(0x05, 0x10);
        WriteLighting(f, 4, s);
        f[18] = 0xAA; f[19] = 0x55;
        return Seal(f);
    }

    public static byte[] WirelessUserMode(int brightness)
    {
        var f = Wireless(0x05, 0x10);
        f[4] = LightingModes.UserDefined; f[13] = (byte)brightness;
        f[18] = 0xAA; f[19] = 0x55;
        return Seal(f);
    }

    public static byte[] WirelessConfig(DeviceConfig c)
    {
        var f = Wireless(0x07, 0x10);
        WriteConfig(f, 4, c);
        f[18] = 0xAA; f[19] = 0x55;
        return Seal(f);
    }

    public static byte[] WirelessClock(DateTime t, int slot = 1)
    {
        var f = Wireless(0x0C, 0x10);
        WriteClock(f, 4, t, slot);
        f[18] = 0xAA; f[19] = 0x55;
        return Seal(f);
    }

    /// <summary>Splits a buffer into <c>[00 cmd len idx payload...]</c> chunks of 28 payload bytes.</summary>
    public static IEnumerable<byte[]> WirelessChunks(byte cmd, byte[] data, int? declaredLength = null)
    {
        var declared = declaredLength ?? data.Length;
        for (int i = 0, at = 0; at < data.Length; i++, at += WirelessChunk)
        {
            var f = Wireless(cmd, (byte)Math.Min(WirelessChunk, declared - at), (byte)i);
            Array.Copy(data, at, f, 4, Math.Min(WirelessChunk, data.Length - at));
            yield return Seal(f);
        }
    }

    public static byte[] WirelessMusic(int foreground, int background, int brightness, ReadOnlySpan<byte> bands)
    {
        var f = Wireless(0x0B, 0x1C);
        f[4] = (byte)foreground; f[5] = (byte)background; f[6] = (byte)brightness;
        for (int i = 0; i < 5 && i < bands.Length; i++) f[23 + i] = bands[i];
        return Seal(f);
    }
}
