using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenAK820.Model;

namespace OpenAK820.Services;

/// <summary>USB HID usage IDs, their names, and conversion from Windows virtual keys.</summary>
public static class HidKeys
{
    public static readonly IReadOnlyList<(byte Code, string Name)> All = Build();

    private static List<(byte, string)> Build()
    {
        var list = new List<(byte, string)>();
        for (int i = 0; i < 26; i++) list.Add(((byte)(0x04 + i), ((char)('A' + i)).ToString()));
        for (int i = 1; i <= 9; i++) list.Add(((byte)(0x1D + i), i.ToString()));
        list.Add((0x27, "0"));
        list.AddRange(new (byte, string)[]
        {
            (0x28, "Enter"), (0x29, "Esc"), (0x2A, "Backspace"), (0x2B, "Tab"), (0x2C, "Space"), (0x2D, "-"), (0x2E, "="),
            (0x2F, "["), (0x30, "]"), (0x31, "\\"), (0x33, ";"), (0x34, "'"), (0x35, "`"), (0x36, ","), (0x37, "."), (0x38, "/"),
            (0x39, "Caps Lock"), (0x46, "Print Screen"), (0x47, "Scroll Lock"), (0x48, "Pause"), (0x49, "Insert"), (0x4A, "Home"),
            (0x4B, "Page Up"), (0x4C, "Delete"), (0x4D, "End"), (0x4E, "Page Down"), (0x4F, "Right"), (0x50, "Left"), (0x51, "Down"), (0x52, "Up"),
            (0x53, "Num Lock"), (0x54, "Num /"), (0x55, "Num *"), (0x56, "Num -"), (0x57, "Num +"), (0x58, "Num Enter"),
            (0x59, "Num 1"), (0x5A, "Num 2"), (0x5B, "Num 3"), (0x5C, "Num 4"), (0x5D, "Num 5"), (0x5E, "Num 6"), (0x5F, "Num 7"), (0x60, "Num 8"), (0x61, "Num 9"), (0x62, "Num 0"), (0x63, "Num ."),
            (0x65, "Menu"),
        });
        for (int i = 1; i <= 12; i++) list.Add(((byte)(0x39 + i), $"F{i}"));
        for (int i = 13; i <= 24; i++) list.Add(((byte)(0x68 + i - 13), $"F{i}"));
        list.AddRange(new (byte, string)[]
        {
            (0xE0, "Left Ctrl"), (0xE1, "Left Shift"), (0xE2, "Left Alt"), (0xE3, "Left Win"),
            (0xE4, "Right Ctrl"), (0xE5, "Right Shift"), (0xE6, "Right Alt"), (0xE7, "Right Win"),
        });
        return list;
    }

    public static string Name(int code)
    {
        foreach (var (c, n) in All) if (c == code) return n;
        return $"0x{code:X2}";
    }

    /// <summary>Converts a Windows virtual key to a HID usage. Returns 0 if the key has no equivalent.</summary>
    public static byte FromVirtualKey(int vk, bool extended = false)
    {
        switch (vk)
        {
            case >= 0x41 and <= 0x5A: return (byte)(vk - 0x41 + 0x04);
            case >= 0x31 and <= 0x39: return (byte)(vk - 0x31 + 0x1E);
            case 0x30: return 0x27;
            case >= 0x70 and <= 0x7B: return (byte)(vk - 0x70 + 0x3A);
            case >= 0x7C and <= 0x87: return (byte)(vk - 0x7C + 0x68);
            case >= 0x61 and <= 0x69: return (byte)(vk - 0x61 + 0x59);
            case 0x60: return 0x62;
        }
        return vk switch
        {
            0x0D => (byte)(extended ? 0x58 : 0x28), 0x1B => 0x29, 0x08 => 0x2A, 0x09 => 0x2B, 0x20 => 0x2C,
            0xBD => 0x2D, 0xBB => 0x2E, 0xDB => 0x2F, 0xDD => 0x30, 0xDC => 0x31, 0xBA => 0x33, 0xDE => 0x34, 0xC0 => 0x35,
            0xBC => 0x36, 0xBE => 0x37, 0xBF => 0x38, 0x14 => 0x39, 0x2C => 0x46, 0x91 => 0x47, 0x13 => 0x48,
            0x2D => 0x49, 0x24 => 0x4A, 0x21 => 0x4B, 0x2E => 0x4C, 0x23 => 0x4D, 0x22 => 0x4E,
            0x27 => 0x4F, 0x25 => 0x50, 0x28 => 0x51, 0x26 => 0x52, 0x90 => 0x53,
            0x6F => 0x54, 0x6A => 0x55, 0x6D => 0x56, 0x6B => 0x57, 0x6E => 0x63, 0x5D => 0x65,
            0xA2 or 0x11 => 0xE0, 0xA0 or 0x10 => 0xE1, 0xA4 or 0x12 => 0xE2, 0x5B => 0xE3,
            0xA3 => 0xE4, 0xA1 => 0xE5, 0xA5 => 0xE6, 0x5C => 0xE7,
            _ => 0,
        };
    }
}

public static class InputSender
{
    private const int InputKeyboard = 1;
    private const uint KeyUp = 0x2, Unicode = 0x4;

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput { public ushort Vk, Scan; public uint Flags, Time; public IntPtr Extra; public long Padding; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Input { public int Type; public int Pad; public KeyboardInput Key; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    /// <summary>Types text directly, so it works without touching the clipboard.</summary>
    public static void SendText(string text)
    {
        var inputs = new List<Input>(text.Length * 2);
        foreach (var c in text)
        {
            inputs.Add(new Input { Type = InputKeyboard, Key = new KeyboardInput { Scan = c, Flags = Unicode } });
            inputs.Add(new Input { Type = InputKeyboard, Key = new KeyboardInput { Scan = c, Flags = Unicode | KeyUp } });
        }
        if (inputs.Count > 0) SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Input>());
    }
}

/// <summary>Records keyboard and mouse input with low-level hooks. The hooks exist only while recording.</summary>
public sealed class InputRecorder : IDisposable
{
    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookExW(int id, HookProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string? name);

    [StructLayout(LayoutKind.Sequential)] private struct KbdHook { public int Vk, Scan, Flags, Time; public IntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseHook { public int X, Y, Data, Flags, Time; public IntPtr Extra; }

    private readonly HookProc _keyProc, _mouseProc;
    private IntPtr _keyHook, _mouseHook;
    private readonly Stopwatch _clock = new();
    private readonly HashSet<int> _down = new();
    private readonly Func<Point, bool> _ignoreClickAt;
    private MacroEvent? _previous;

    public List<MacroEvent> Events { get; } = new();
    /// <summary>When set, delays are replaced by this fixed value. Otherwise the real timing is kept.</summary>
    public int? FixedDelay { get; set; }
    public event Action? Changed;

    public InputRecorder(Func<Point, bool> ignoreClickAt)
    {
        _ignoreClickAt = ignoreClickAt;
        _keyProc = OnKey;
        _mouseProc = OnMouse;
    }

    public void Start()
    {
        Events.Clear();
        _down.Clear();
        _previous = null;
        _clock.Restart();
        var module = GetModuleHandleW(null);
        _keyHook = SetWindowsHookExW(13, _keyProc, module, 0);
        _mouseHook = SetWindowsHookExW(14, _mouseProc, module, 0);
    }

    public void Stop()
    {
        if (_keyHook != IntPtr.Zero) UnhookWindowsHookEx(_keyHook);
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        _keyHook = _mouseHook = IntPtr.Zero;
        _clock.Stop();
    }

    private void Add(MacroEventKind kind, int code)
    {
        var elapsed = (int)_clock.ElapsedMilliseconds;
        _clock.Restart();
        if (_previous != null) _previous.DelayMs = FixedDelay ?? Math.Clamp(elapsed, 10, 65535);
        _previous = new MacroEvent { Kind = kind, Code = code, DelayMs = FixedDelay ?? 10 };
        Events.Add(_previous);
        Changed?.Invoke();
    }

    private IntPtr OnKey(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var info = Marshal.PtrToStructure<KbdHook>(lParam);
            var hid = HidKeys.FromVirtualKey(info.Vk, (info.Flags & 1) != 0);
            if (hid != 0)
            {
                var message = (int)wParam;
                if (message is 0x100 or 0x104)
                {
                    if (_down.Add(hid)) Add(MacroEventKind.KeyDown, hid); // Ignore auto-repeat.
                }
                else if (_down.Remove(hid)) Add(MacroEventKind.KeyUp, hid);
            }
        }
        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    private IntPtr OnMouse(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var (kind, button) = (int)wParam switch
            {
                0x201 => (MacroEventKind.MouseDown, MacroMouseButton.Left),
                0x202 => (MacroEventKind.MouseUp, MacroMouseButton.Left),
                0x204 => (MacroEventKind.MouseDown, MacroMouseButton.Right),
                0x205 => (MacroEventKind.MouseUp, MacroMouseButton.Right),
                0x207 => (MacroEventKind.MouseDown, MacroMouseButton.Middle),
                0x208 => (MacroEventKind.MouseUp, MacroMouseButton.Middle),
                _ => ((MacroEventKind)0, (MacroMouseButton)0),
            };
            if (kind != 0)
            {
                var info = Marshal.PtrToStructure<MouseHook>(lParam);
                // Clicks on our own window (such as Stop) should not end up in the macro.
                if (!_ignoreClickAt(new Point(info.X, info.Y))) Add(kind, (int)button);
            }
        }
        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    public void Dispose() => Stop();
}
