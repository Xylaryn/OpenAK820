namespace OpenAK820.Model;

public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static readonly Rgb Black = new(0, 0, 0);
    public static readonly Rgb White = new(255, 255, 255);
    public Rgb Scale(double f) => new((byte)Math.Clamp(R * f, 0, 255), (byte)Math.Clamp(G * f, 0, 255), (byte)Math.Clamp(B * f, 0, 255));
    public static Rgb FromHsv(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        var c = v * s; var x = c * (1 - Math.Abs(h / 60 % 2 - 1)); var m = v - c;
        var (r, g, b) = (int)(h / 60) switch { 0 => (c, x, 0d), 1 => (x, c, 0d), 2 => (0d, c, x), 3 => (0d, x, c), 4 => (x, 0d, c), _ => (c, 0d, x) };
        return new((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";
    public static Rgb Parse(string hex)
    {
        hex = hex.TrimStart('#');
        return new(Convert.ToByte(hex[..2], 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16));
    }
}

/// <summary>What one key does. Values match the "macro_type" numbering in the vendor software.</summary>
public enum KeyActionKind
{
    Default = 0, Disabled = 1, KeyCombo = 2, Macro = 3, Mouse = 5, Media = 6, WindowsShortcut = 7,
    OpenProgram = 8, OpenWebsite = 9, SendText = 10, SwitchProfile = 11, Consumer = 12, MultiKey = 13,
}

public enum MouseAction { LeftClick = 1, MiddleClick = 2, RightClick = 3, DoubleClick = 4, ScrollUp = 5, ScrollDown = 6, Button4 = 7, Button5 = 8 }
public enum MediaAction { PlayPause = 1, Stop = 2, Previous = 3, Next = 4, VolumeUp = 5, VolumeDown = 6, Mute = 7 }
public enum WindowsShortcut { ShowDesktop = 1, MyComputer = 2, LockComputer = 3, CloseWindow = 4, SwitchWindows = 5, Copy = 6, Paste = 7, Cut = 8 }
public enum ConsumerAction
{
    MyComputer = 500, Calculator = 501, Mail = 502, MediaPlayer = 503, Homepage = 511, WebRefresh = 512,
    WebStop = 513, WebBack = 514, WebForward = 515, WebFavorites = 516, WebSearch = 517,
}
public enum MacroPlayMode { PlayOnce = 0, PlayMultiple = 1, StopOnNextPress = 2, ReleaseToStop = 3, ClickToPlay = 4 }

[Flags]
public enum KeyMods : byte { None = 0, LCtrl = 1, LShift = 2, LAlt = 4, LWin = 8, RCtrl = 16, RShift = 32, RAlt = 64, RWin = 128 }

public sealed class KeyAction
{
    public KeyActionKind Kind { get; set; }
    public byte Key { get; set; }
    public byte Key2 { get; set; }
    public byte Key3 { get; set; }
    public KeyMods Mods { get; set; }
    public int Value { get; set; }
    public int MacroIndex { get; set; }
    public MacroPlayMode PlayMode { get; set; }
    public byte Loops { get; set; } = 1;
    /// <summary>Program path, URL or text for the host-side actions.</summary>
    public string Text { get; set; } = "";
    /// <summary>Identifier the keyboard reports back when a host-side action is pressed (1-255).</summary>
    public byte HostId { get; set; }
    public bool IsHostSide => Kind is KeyActionKind.OpenProgram or KeyActionKind.OpenWebsite or KeyActionKind.SendText or KeyActionKind.SwitchProfile;
}

public enum MacroEventKind { KeyDown = 2, KeyUp = 3, MouseDown = 4, MouseUp = 5 }
public enum MacroMouseButton { Left = 1, Middle = 2, Right = 3 }

public sealed class MacroEvent
{
    public MacroEventKind Kind { get; set; }
    /// <summary>USB HID usage for key events; a <see cref="MacroMouseButton"/> for mouse events.</summary>
    public int Code { get; set; }
    /// <summary>Delay after this event in ms. The keyboard's minimum is 10 ms.</summary>
    public int DelayMs { get; set; } = 10;
}

public sealed class Macro
{
    public string Name { get; set; } = "New Macro";
    public List<MacroEvent> Events { get; set; } = new();
}

public sealed class LightingState
{
    /// <summary>Firmware mode ID; see <see cref="LightingModes"/>.</summary>
    public int Mode { get; set; } = 11;
    public string Color { get; set; } = "#00C8FF";
    public bool Colorful { get; set; } = true;
    public int Brightness { get; set; } = 5;
    public int Speed { get; set; } = 3;
    public int Direction { get; set; }
}

[Flags]
public enum ModeCaps { None = 0, Brightness = 1, Speed = 2, DirectionHorizontal = 4, DirectionVertical = 8, Colorful = 16, Palette = 32 }

public sealed record LightingModeInfo(int Id, string Name, ModeCaps Caps);

public static class LightingModes
{
    public const int Off = 0;
    /// <summary>Firmware mode that displays the per-key colours written with command 0x23.</summary>
    public const int UserDefined = 0x80;

    public static readonly IReadOnlyList<LightingModeInfo> All = new LightingModeInfo[]
    {
        new(1, "Static", (ModeCaps)0x31), new(2, "SingleOn", (ModeCaps)0x33), new(3, "SingleOff", (ModeCaps)0x33),
        new(4, "Glittering", (ModeCaps)0x33), new(5, "Falling", (ModeCaps)0x33), new(6, "Colourful", (ModeCaps)0x03),
        new(7, "Breath", (ModeCaps)0x33), new(8, "Spectrum", (ModeCaps)0x03), new(9, "Outward", (ModeCaps)0x33),
        new(10, "Scrolling", (ModeCaps)0x3B), new(11, "Rolling", (ModeCaps)0x37), new(12, "Rotating", (ModeCaps)0x37),
        new(13, "Explode", (ModeCaps)0x33), new(14, "Launch", (ModeCaps)0x33), new(15, "Ripples", (ModeCaps)0x33),
        new(16, "Flowing", (ModeCaps)0x37), new(17, "Pulsating", (ModeCaps)0x33), new(18, "Tilt", (ModeCaps)0x37),
        new(19, "Shuttle", (ModeCaps)0x33), new(0, "LED Off", ModeCaps.None),
    };

    public static LightingModeInfo Get(int id) => All.FirstOrDefault(m => m.Id == id) ?? All[0];
}

public sealed class DeviceConfig
{
    public bool DisableWinKey { get; set; }
    public bool DisableAltF4 { get; set; }
    public bool DisableAltTab { get; set; }
    /// <summary>0 = momentary, 1 = toggle.</summary>
    public int FnSwitch { get; set; }
    /// <summary>0 = never, 1 = 1 min, 2 = 5 min, 3 = 30 min.</summary>
    public int SleepTime { get; set; } = 1;
    /// <summary>1 (fastest) to 5.</summary>
    public int KeyResponse { get; set; } = 1;
}

/// <summary>Host-animated per-key effect IDs (the vendor's "Real-time Lighting").</summary>
public enum RealtimeEffect { Off = 0, StaticOn = 1, Starlight = 4, Fluttering = 5, DynamicBreathing = 7, ColorfulFountain = 9, RainbowWave = 10, FollowingCurrents = 11, PeakRevolving = 12 }

public sealed class RealtimeKey
{
    public RealtimeEffect Effect { get; set; }
    public string Color { get; set; } = "#FFFFFF";
}

public sealed class RealtimeScheme
{
    public string Name { get; set; } = "Scheme";
    public int Speed { get; set; } = 3;
    public int Brightness { get; set; } = 5;
    public Dictionary<int, RealtimeKey> Keys { get; set; } = new();
}

public sealed class PerKeyScheme
{
    public string Name { get; set; } = "Scheme";
    public int Brightness { get; set; } = 5;
    /// <summary>Hex colour by light index. Missing keys stay dark.</summary>
    public Dictionary<int, string> Colors { get; set; } = new();
}

public enum MusicColorMode { GreenYellowRed = 106, Rainbow = 107, ReverseRainbow = 108, Gradient = 109, SpectrumCycle = 110, White = 111, Red = 112, Orange = 113, Yellow = 114, Green = 115, Cyan = 116, Off = 117, Blue = 118, Purple = 119, Ambilight = 120 }

public sealed class MusicLayer
{
    public string Name { get; set; } = "New Music Effect";
    public int Rhythm { get; set; }
    public MusicColorMode Foreground { get; set; } = MusicColorMode.Rainbow;
    public int Amplitude { get; set; } = 5;
    public MusicColorMode Background { get; set; } = MusicColorMode.Off;
    public int BackgroundBrightness { get; set; } = 1;
}

public sealed class ScreenFrame
{
    public string File { get; set; } = "";
    public int DelayMs { get; set; } = 100;
}

public sealed class Profile
{
    public string Name { get; set; } = "Profile";
    /// <summary>[0] is the top layer and [1] is the Fn layer. Keyed by key index.</summary>
    public Dictionary<int, KeyAction>[] Layers { get; set; } = { new(), new() };
    public LightingState Lighting { get; set; } = new();
    public List<PerKeyScheme> PerKeySchemes { get; set; } = new();
    public List<RealtimeScheme> RealtimeSchemes { get; set; } = new();
    public List<MusicLayer> MusicLayers { get; set; } = new();
    public DeviceConfig Config { get; set; } = new();

    /// <summary>Host IDs are small, so they are handed out per profile. Returns 0 when all 255 are taken.</summary>
    public byte AllocateHostId()
    {
        var used = Layers.SelectMany(l => l.Values).Where(a => a.IsHostSide).Select(a => a.HostId).ToHashSet();
        for (int i = 1; i < 256; i++) if (!used.Contains((byte)i)) return (byte)i;
        return 0;
    }

    public KeyAction? FindHostAction(byte id) =>
        Layers.SelectMany(l => l.Values).FirstOrDefault(a => a.IsHostSide && a.HostId == id);
}

public sealed record KeyInfo(byte Code, string Name, int Index, int X, int Y, int W, int H, bool FnLayerLocked);

/// <summary>Key geometry and indices for the AK820 Pro ANSI board. Coordinates are in a 800x300 design space.</summary>
public static class KeyLayout
{
    public const int DesignLeft = 95, DesignTop = 22, DesignRight = 705, DesignBottom = 258;

    public static readonly IReadOnlyList<KeyInfo> Keys = new KeyInfo[]
    {
        new(0x29, "Esc", 1, 95, 22, 28, 28, true),
        new(0x3A, "F1", 2, 141, 22, 28, 28, false),
        new(0x3B, "F2", 3, 179, 22, 28, 28, false),
        new(0x3C, "F3", 4, 217, 22, 28, 28, false),
        new(0x3D, "F4", 5, 255, 22, 28, 28, false),
        new(0x3E, "F5", 6, 302, 22, 28, 28, false),
        new(0x3F, "F6", 7, 339, 22, 28, 28, false),
        new(0x40, "F7", 8, 377, 22, 28, 28, false),
        new(0x41, "F8", 9, 415, 22, 28, 28, false),
        new(0x42, "F9", 10, 462, 22, 28, 28, false),
        new(0x43, "F10", 11, 500, 22, 28, 28, false),
        new(0x44, "F11", 12, 538, 22, 28, 28, false),
        new(0x45, "F12", 13, 576, 22, 28, 28, false),
        new(0x4C, "Delete", 119, 623, 22, 28, 28, false),
        new(0x35, "~`", 19, 95, 69, 28, 28, false),
        new(0x1E, "!1", 20, 132, 69, 28, 28, false),
        new(0x1F, "@2", 21, 170, 69, 28, 28, false),
        new(0x20, "#3", 22, 208, 69, 28, 28, false),
        new(0x21, "$4", 23, 246, 69, 28, 28, false),
        new(0x22, "%5", 24, 283, 69, 28, 28, false),
        new(0x23, "^6", 25, 321, 69, 28, 28, false),
        new(0x24, "&7", 26, 359, 69, 28, 28, false),
        new(0x25, "*8", 27, 397, 69, 28, 28, false),
        new(0x26, "(9", 28, 434, 69, 28, 28, false),
        new(0x27, ")0", 29, 472, 69, 28, 28, false),
        new(0x2D, "_-", 30, 510, 69, 28, 28, false),
        new(0x2E, "+=", 31, 547, 69, 28, 28, false),
        new(0x2A, "Backspace", 103, 585, 69, 66, 28, true),
        new(0x4A, "home", 117, 677, 69, 28, 28, true),
        new(0x2B, "Tab", 37, 95, 108, 46, 28, false),
        new(0x14, "Q", 38, 151, 108, 28, 28, false),
        new(0x1A, "W", 39, 189, 108, 28, 28, false),
        new(0x08, "E", 40, 227, 108, 28, 28, false),
        new(0x15, "R", 41, 265, 108, 28, 28, false),
        new(0x17, "T", 42, 302, 108, 28, 28, false),
        new(0x1C, "Y", 43, 340, 108, 28, 28, false),
        new(0x18, "U", 44, 378, 108, 28, 28, false),
        new(0x0C, "I", 45, 416, 108, 28, 28, false),
        new(0x12, "O", 46, 453, 108, 28, 28, false),
        new(0x13, "P", 47, 491, 108, 28, 28, false),
        new(0x2F, "{[", 48, 529, 108, 28, 28, false),
        new(0x30, "}}", 49, 566, 108, 28, 28, false),
        new(0x31, "|\\", 67, 604, 108, 47, 28, true),
        new(0x4B, "PgUp", 118, 677, 108, 28, 28, false),
        new(0x39, "CapsLock", 55, 95, 145, 55, 28, false),
        new(0x04, "A", 56, 160, 145, 28, 28, false),
        new(0x16, "S", 57, 198, 145, 28, 28, false),
        new(0x07, "D", 58, 236, 145, 28, 28, false),
        new(0x09, "F", 59, 274, 145, 28, 28, false),
        new(0x0A, "G", 60, 312, 145, 28, 28, false),
        new(0x0B, "H", 61, 350, 145, 28, 28, false),
        new(0x0D, "J", 62, 387, 145, 28, 28, false),
        new(0x0E, "K", 63, 425, 145, 28, 28, false),
        new(0x0F, "L", 64, 462, 145, 28, 28, false),
        new(0x33, ":;", 65, 500, 145, 28, 28, false),
        new(0x34, "\"'", 66, 538, 145, 28, 28, false),
        new(0x28, "Enter", 85, 576, 145, 75, 28, false),
        new(0x4E, "PgDn", 121, 677, 145, 28, 28, false),
        new(0xE1, "Shift_L", 73, 95, 183, 75, 28, false),
        new(0x1D, "Z", 74, 179, 183, 28, 28, false),
        new(0x1B, "X", 75, 217, 183, 28, 28, false),
        new(0x06, "C", 76, 255, 183, 28, 28, false),
        new(0x19, "V", 77, 292, 183, 28, 28, false),
        new(0x05, "B", 78, 330, 183, 28, 28, false),
        new(0x11, "N", 79, 368, 183, 28, 28, false),
        new(0x10, "M", 80, 406, 183, 28, 28, false),
        new(0x36, ",<", 81, 443, 183, 28, 28, false),
        new(0x37, ".>", 82, 481, 183, 28, 28, false),
        new(0x38, "/?", 83, 519, 183, 28, 28, false),
        new(0xE5, "Shift_R", 84, 557, 183, 55, 28, false),
        new(0x52, "↑", 101, 631, 192, 28, 28, false),
        new(0xE0, "Ctrl_L", 91, 95, 221, 37, 28, false),
        new(0xE3, "Win_L", 92, 142, 221, 37, 28, true),
        new(0xE2, "Alt_L", 93, 189, 221, 37, 28, false),
        new(0x2C, "Space", 94, 236, 221, 225, 28, false),
        new(0xE6, "Alt_R", 95, 472, 221, 28, 28, false),
        new(0xAF, "Fn", 96, 510, 221, 28, 28, true),
        new(0xE4, "Ctrl_R", 98, 548, 221, 28, 28, false),
        new(0x50, "←", 99, 594, 230, 28, 28, false),
        new(0x51, "↓", 100, 631, 230, 28, 28, false),
        new(0x4F, "→", 102, 669, 230, 28, 28, false),
    };

    public static KeyInfo? ByCode(byte code) => Keys.FirstOrDefault(k => k.Code == code);
    public static KeyInfo? ByIndex(int index) => Keys.FirstOrDefault(k => k.Index == index);
}
