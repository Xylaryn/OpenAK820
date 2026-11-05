using OpenAK820.Device;
using OpenAK820.Hid;
using OpenAK820.Model;
using OpenAK820.Protocol;
using Xunit;

namespace OpenAK820.Tests;

/// <summary>
/// Golden layouts. Each expectation was read from the vendor binary's disassembly; the address in the
/// comment is where to look. If one of these fails, the code changed, not the keyboard.
/// </summary>
public class EncoderTests
{
    private static string Hex(IEnumerable<byte> b) => Convert.ToHexString(b.ToArray());

    [Fact] // 0x42B18E
    public void Command_places_value_at_bytes_8_and_9()
    {
        var f = Encoders.Command(0x72, 1, 0x0141);
        Assert.Equal(64, f.Length);
        Assert.Equal("0472010000000000" + "4101", Hex(f.Take(10)));
    }

    [Fact] // 0x42B1B4
    public void Lighting_layout()
    {
        var f = Encoders.LightingData(new LightingState { Mode = 10, Color = "#112233", Colorful = true, Brightness = 5, Speed = 3, Direction = 2 });
        Assert.Equal("0A112233" + "00000000" + "01050302" + "0000" + "AA55", Hex(f.Take(16)));
    }

    [Fact] // 0x42B1C7: mode 0 skips every other field
    public void Lighting_off_only_sets_the_mode()
    {
        var f = Encoders.LightingData(new LightingState { Mode = 0, Color = "#FFFFFF", Brightness = 5 });
        Assert.All(f.Take(14), b => Assert.Equal(0, b));
        Assert.Equal(0xAA, f[14]);
    }

    [Fact] // 0x44B977
    public void User_lighting_mode_layout()
    {
        var f = Encoders.UserLightingMode(4);
        Assert.Equal(0x80, f[0]);
        Assert.Equal(4, f[9]);
        Assert.Equal("AA55", Hex(f.Skip(14).Take(2)));
    }

    [Fact] // 0x4140F8
    public void Config_layout()
    {
        var f = Encoders.ConfigData(new DeviceConfig { DisableWinKey = true, DisableAltF4 = false, DisableAltTab = true, FnSwitch = 1, SleepTime = 2, KeyResponse = 3 });
        Assert.Equal("00" + "01" + "01" + "00" + "01" + "01" + "02" + "00" + "03", Hex(f.Take(9)));
        Assert.Equal("AA55", Hex(f.Skip(62)));
    }

    [Fact] // 0x4239CB
    public void Clock_layout()
    {
        var t = new DateTime(2026, 11, 5, 14, 51, 9); // A Thursday
        var f = Encoders.ClockData(t);
        Assert.Equal("00" + "01" + "5A" + "1A" + "0B" + "05" + "0E" + "33" + "09" + "00" + "04", Hex(f.Take(11)));
        Assert.Equal("AA55", Hex(f.Skip(62)));
    }

    public static TheoryData<KeyAction, string> KeyEntries => new()
    {
        { new KeyAction { Kind = KeyActionKind.Default }, "00000000" },
        { new KeyAction { Kind = KeyActionKind.Disabled }, "05030000" },
        { new KeyAction { Kind = KeyActionKind.KeyCombo, Key = 0x04 }, "02000400" },
        { new KeyAction { Kind = KeyActionKind.KeyCombo, Key = 0x04, Mods = KeyMods.LCtrl | KeyMods.LShift }, "02030400" },
        { new KeyAction { Kind = KeyActionKind.KeyCombo, Key = 0xE3 }, "02080000" }, // 0x418630: lone modifier
        { new KeyAction { Kind = KeyActionKind.MultiKey, Key = 4, Key2 = 5, Key3 = 6 }, "07040506" },
        { new KeyAction { Kind = KeyActionKind.Macro, MacroIndex = 2, PlayMode = MacroPlayMode.PlayMultiple, Loops = 7 }, "06020107" },
        { new KeyAction { Kind = KeyActionKind.Mouse, Value = (int)MouseAction.MiddleClick }, "01010400" },
        { new KeyAction { Kind = KeyActionKind.Mouse, Value = (int)MouseAction.DoubleClick }, "01010300" },
        { new KeyAction { Kind = KeyActionKind.Mouse, Value = (int)MouseAction.ScrollDown }, "0103FF00" },
        { new KeyAction { Kind = KeyActionKind.Media, Value = (int)MediaAction.Mute }, "03E20000" },
        { new KeyAction { Kind = KeyActionKind.Consumer, Value = (int)ConsumerAction.Calculator }, "03920100" },
        { new KeyAction { Kind = KeyActionKind.Consumer, Value = (int)ConsumerAction.WebSearch }, "03210200" },
        { new KeyAction { Kind = KeyActionKind.WindowsShortcut, Value = (int)WindowsShortcut.LockComputer }, "02080F00" },
        { new KeyAction { Kind = KeyActionKind.WindowsShortcut, Value = (int)WindowsShortcut.SwitchWindows }, "02042B00" },
        { new KeyAction { Kind = KeyActionKind.OpenWebsite, HostId = 9 }, "05020900" },
    };

    [Theory, MemberData(nameof(KeyEntries))]
    public void Key_entry_encoding(KeyAction action, string expected)
    {
        var (a, b, c, d) = Encoders.KeyEntry(action);
        Assert.Equal(expected, Hex(new[] { a, b, c, d }));
    }

    [Fact]
    public void Key_table_is_indexed_and_tailed()
    {
        var table = Encoders.KeyTable(new Dictionary<int, KeyAction> { [56] = new() { Kind = KeyActionKind.Disabled } }, 0);
        Assert.Equal(576, table.Length);
        Assert.Equal("05030000", Hex(table.Skip(56 * 4).Take(4)));
        Assert.Equal("AA55", Hex(table.Skip(574)));
    }

    [Fact] // 0x4185C0: Esc is locked in the Fn layer
    public void Fn_layer_skips_locked_keys()
    {
        var actions = new Dictionary<int, KeyAction> { [1] = new() { Kind = KeyActionKind.Disabled } };
        Assert.Equal("05030000", Hex(Encoders.KeyTable(actions, 0).Skip(4).Take(4)));
        Assert.Equal("00000000", Hex(Encoders.KeyTable(actions, 1).Skip(4).Take(4)));
    }

    [Fact] // 0x44BBD0
    public void Per_key_entries_carry_their_index()
    {
        var t = Encoders.PerKeyTable(new Dictionary<int, Rgb> { [39] = new(1, 2, 3) });
        Assert.Equal("27010203", Hex(t.Skip(39 * 4).Take(4)));
        Assert.Equal("AA55", Hex(t.Skip(574)));
    }

    [Fact] // 0x432B40: packed, not positional
    public void Realtime_frame_is_packed_in_layout_order()
    {
        var f = Encoders.RealtimeFrame(new Dictionary<int, Rgb> { [2] = new(9, 8, 7) });
        Assert.Equal(512, f.Length);
        Assert.Equal("01000000" + "02090807", Hex(f.Take(8)));
    }

    [Fact] // 0x42D8AA .. 0x42DBD9
    public void Macro_image_layout()
    {
        var macros = new List<Macro>
        {
            new() { Events = {
                new() { Kind = MacroEventKind.KeyDown, Code = 0x04, DelayMs = 300 },
                new() { Kind = MacroEventKind.KeyUp, Code = 0x04, DelayMs = 1 },
                new() { Kind = MacroEventKind.MouseDown, Code = (int)MacroMouseButton.Middle, DelayMs = 20 },
                new() { Kind = MacroEventKind.MouseUp, Code = (int)MacroMouseButton.Right, DelayMs = 20 },
            } },
            new(),
            new() { Events = { new() { Kind = MacroEventKind.KeyDown, Code = 0x05, DelayMs = 10 } } },
        };
        var image = Encoders.MacroImage(macros, out var pages);
        Assert.Equal("90010000", Hex(image.Take(4)));            // macro 0 at 0x190
        Assert.Equal("FFFFFFFF", Hex(image.Skip(4).Take(4)));    // macro 1 is empty
        Assert.Equal("B8010000", Hex(image.Skip(8).Take(4)));    // macro 2 at 0x190 + 8 + 8 records * 4
        Assert.Equal("0800", Hex(image.Skip(0x190).Take(2)));    // eight records
        Assert.Equal(
            "000004B0" + "2C010050" + "00000430" + "0A000050" +  // short delays are raised to 10 ms
            "00000490" + "14000050" + "00000210" + "14000050",
            Hex(image.Skip(0x190 + 8).Take(32)));
        // Used 0x1B8 + 8 + 8 = 456 bytes: 7 whole pages plus a remainder, so 9 pages.
        Assert.Equal(9, pages);
        Assert.Equal(9 * 64, image.Length);
        Assert.Equal("AA55", Hex(image.Skip(image.Length - 2)));
    }

    [Fact] // 0x42DBAE: an exact multiple of 64 gets one spare page; otherwise two
    public void Macro_pages_round_up_with_a_spare()
    {
        var exact = new Macro();
        for (int i = 0; i < 13; i++) exact.Events.Add(new() { Kind = MacroEventKind.KeyDown, Code = 4 });
        Encoders.MacroImage(new[] { exact }, out var pages); // 400 + 8 + 13*8 = 512, exactly 8 pages
        Assert.Equal(9, pages);

        var inexact = new Macro();
        for (int i = 0; i < 12; i++) inexact.Events.Add(new() { Kind = MacroEventKind.KeyDown, Code = 4 });
        Encoders.MacroImage(new[] { inexact }, out pages); // 400 + 8 + 12*8 = 504, 7.875 pages
        Assert.Equal(9, pages);
    }

    [Fact]
    public void Macro_overflow_is_refused()
    {
        var m = new Macro();
        for (int i = 0; i < 500; i++) m.Events.Add(new() { Kind = MacroEventKind.KeyDown, Code = 4 });
        Assert.Throws<InvalidOperationException>(() => Encoders.MacroImage(new[] { m }, out _));
    }

    [Fact] // 0x422C86, 0x422CD3
    public void Screen_image_layout()
    {
        var pixels = new ushort[128 * 128];
        pixels[0] = Encoders.ToRgb565(0xFF, 0, 0);
        var image = Encoders.ScreenImage(new[] { (pixels, 160), (pixels, 1), (pixels, 5000) }, out var pages);
        Assert.Equal(3, image[0]);
        Assert.Equal(80, image[1]);
        Assert.Equal(1, image[2]);   // Minimum delay byte.
        Assert.Equal(255, image[3]); // Saturates.
        Assert.Equal(0xFF, image[4]);
        Assert.Equal(0x00, image[256]); // RGB565 little-endian: red is F800.
        Assert.Equal(0xF8, image[257]);
        Assert.Equal((256 + 3 * 32768 + 4095) / 4096, pages);
        Assert.Equal(pages * 4096, image.Length);
        Assert.Equal(0xFF, image[^1]);
    }

    [Fact]
    public void Screen_image_caps_at_140_frames()
    {
        var pixels = new ushort[128 * 128];
        var frames = Enumerable.Repeat((pixels, 100), 200).ToList();
        var image = Encoders.ScreenImage(frames, out _);
        Assert.Equal(140, image[0]);
    }

    [Fact] // 0x44F660
    public void Wireless_checksum_is_a_byte_sum()
    {
        var f = Encoders.Seal(Encoders.Wireless(0x20, 0x01));
        Assert.Equal(33, f.Length);
        Assert.Equal(0x21, f[32]);
    }

    [Fact] // 0x42B318
    public void Wireless_lighting_layout()
    {
        var f = Encoders.WirelessLighting(new LightingState { Mode = 7, Color = "#0A0B0C", Colorful = false, Brightness = 2, Speed = 4, Direction = 1 });
        Assert.Equal("00" + "0510" + "00" + "07" + "0A0B0C", Hex(f.Take(8)));
        Assert.Equal("00" + "02" + "04" + "01", Hex(f.Skip(12).Take(4)));
        Assert.Equal("AA55", Hex(f.Skip(18).Take(2)));
    }

    [Fact] // 0x44C113
    public void Wireless_table_chunks()
    {
        var chunks = Encoders.WirelessChunks(0x14, Encoders.PerKeyTable(new Dictionary<int, Rgb>())).ToList();
        Assert.Equal(21, chunks.Count);
        Assert.Equal("00141C00", Hex(chunks[0].Take(4)));
        Assert.Equal("00141014", Hex(chunks[20].Take(4)));
    }

    [Fact] // 0x42E100
    public void Wireless_macro_declares_used_length_in_last_chunk()
    {
        var m = new Macro { Events = { new() { Kind = MacroEventKind.KeyDown, Code = 4 } } };
        var image = Encoders.WirelessMacroImage(new[] { m }, out var used);
        Assert.Equal(416, used);
        var chunks = Encoders.WirelessChunks(0x09, image, used).ToList();
        Assert.Equal(15, chunks.Count);                   // 416 / 28 rounded up
        Assert.Equal(416 - 14 * 28, chunks[14][2]);       // Length declared for the last chunk
        Assert.Equal("AA55", Hex(chunks[14].Skip(30).Take(2)));
    }
}

public class ProtocolSequenceTests
{
    private static List<string> Cmds(SimulatedPort p) => p.Features.Select(f => Convert.ToHexString(f, 1, 2)).ToList();

    private static (WiredProtocol, SimulatedPort, SimulatedPort) Wired()
    {
        var control = new SimulatedPort();
        var screen = new SimulatedPort();
        return (new WiredProtocol(control, screen, 0, _ => { }), control, screen);
    }

    [Fact] // 0x42B100
    public void Lighting_sequence()
    {
        var (w, c, _) = Wired();
        w.SetLighting(new LightingState { Mode = 1 });
        var cmds = Cmds(c);
        Assert.Equal(5, cmds.Count);
        Assert.Equal("0418", cmds[0]);
        Assert.Equal("0413", cmds[1]);
        Assert.Equal(1, c.Features[2][1]); // Third frame is the data frame, starting with the mode.
        Assert.Equal("0402", cmds[3]);
        Assert.Equal("04F0", cmds[4]);
    }

    [Fact] // 0x44BA10 + 0x44B900
    public void Per_key_runs_two_sessions()
    {
        var (w, c, _) = Wired();
        w.SetPerKey(new Dictionary<int, Rgb> { [1] = Rgb.White }, 5);
        var cmds = Cmds(c);
        Assert.Equal("0418", cmds[0]);
        Assert.Equal("0413", cmds[1]);
        Assert.Equal(0x80, c.Features[2][1]);
        Assert.Equal("0402", cmds[3]);
        Assert.Equal("04F0", cmds[4]);
        Assert.Equal("0418", cmds[5]);
        Assert.Equal("0423", cmds[6]);
        Assert.Equal(9, c.Features[6][9]);
        Assert.Equal(5 + 2 + 9 + 2, c.Features.Count);
        Assert.Equal("04F0", cmds[^1]);
    }

    [Theory] // 0x41848E: 0x11 top layer, 0x27 Fn layer
    [InlineData(0, "0411")]
    [InlineData(1, "0427")]
    public void Key_table_command_by_layer(int layer, string expected)
    {
        var (w, c, _) = Wired();
        w.SetKeyTable(layer, Encoders.KeyTable(new Dictionary<int, KeyAction>(), layer));
        Assert.Equal(expected, Cmds(c)[1]);
    }

    [Fact] // 0x42D75B
    public void Macros_use_their_own_start_and_end_with_commit()
    {
        var (w, c, _) = Wired();
        w.SetMacros(new[] { new Macro { Events = { new() { Kind = MacroEventKind.KeyDown, Code = 4 } } } });
        var cmds = Cmds(c);
        Assert.Equal("0419", cmds[0]);
        Assert.Equal("0415", cmds[1]);
        Assert.Equal("0402", cmds[^1]);
    }

    [Fact] // 0x422F2D
    public void Screen_upload_sequence()
    {
        var (w, c, s) = Wired();
        var image = Encoders.ScreenImage(new[] { (new ushort[128 * 128], 100) }, out var pages);
        var progress = new List<int>();
        w.UploadScreen(image, pages, 1, progress.Add);
        var cmds = Cmds(c);
        Assert.Equal(new[] { "0418", "0472", "0402" }, cmds);
        Assert.Equal(1, c.Features[1][3]);
        Assert.Equal(pages, c.Features[1][9] | c.Features[1][10] << 8);
        Assert.Equal(pages, s.Writes.Count);
        Assert.All(s.Writes, r => Assert.Equal(4097, r.Length));
        Assert.Equal(0, s.Writes[0][0]);
        Assert.Equal(100, progress[^1]);
    }

    [Fact]
    public void Refused_command_raises()
    {
        var control = new SimulatedPort { Status = 0 };
        var w = new WiredProtocol(control, null, 0, _ => { });
        Assert.Throws<ProtocolException>(() => w.SetLighting(new LightingState()));
    }

    [Fact]
    public void Refused_lighting_data_still_ends_the_session()
    {
        var control = new SimulatedPort();
        var w = new WiredProtocol(control, null, 0, _ =>
        {
            // Refuse everything once the data frame has gone out, so the commit fails.
            if (control.Features.Count >= 3) control.Status = 0;
        });
        Assert.Throws<ProtocolException>(() => w.SetLighting(new LightingState()));
        Assert.Equal("04F0", Cmds(control)[^1]);
    }

    [Fact]
    public void Wireless_battery_is_parsed()
    {
        var port = new SimulatedPort { Battery = 87 };
        var w = new WirelessProtocol(port, _ => { });
        Assert.Equal(87, w.ReadBattery());
    }

    [Theory]
    [InlineData(new byte[] { 5, 2, 9, 0 }, true, 9)]
    [InlineData(new byte[] { 5, 0xA6, 0xFF, 1 }, false, 0)] // Dongle link status, not a key.
    [InlineData(new byte[] { 1, 2, 9, 0 }, false, 0)]
    public void Host_key_parsing(byte[] report, bool expected, byte id)
    {
        Assert.Equal(expected, KeyboardService.TryParseHostKey(report, out var parsed));
        Assert.Equal(id, parsed);
    }
}

public class FinderTests
{
    private static HidInterfaceInfo I(ushort pid, ushort page, int input, int output, int feature, string product) =>
        new($"p{pid}{page}", 0x0C45, pid, 0x110, page, 1, input, output, feature, "SONiX", product);

    /// <summary>These are the collections reported by a real AK820 Pro and its dongle.</summary>
    private static readonly HidInterfaceInfo[] Real =
    {
        I(0x800A, 0xFF68, 65, 4097, 0, "AK820"),
        I(0x800A, 0xFF13, 65, 65, 65, "AK820"),
        I(0x800A, 0xFFFF, 4, 0, 0, "AK820"),
        I(0xFDFD, 0xFF60, 33, 33, 0, "2.4G Dongle"),
        I(0xFDFD, 0xFF59, 65, 65, 0, "2.4G Dongle"),
        I(0xFDFD, 0xFFFF, 4, 0, 0, "2.4G Dongle"),
    };

    [Fact]
    public void Finds_real_board_and_dongle()
    {
        var links = KeyboardFinder.Find(Real);
        Assert.Equal(2, links.Count);
        Assert.Equal(LinkKind.Usb, links[0].Kind);
        Assert.Equal(0xFF13, links[0].Control.UsagePage);
        Assert.Equal(0xFF68, links[0].Screen!.UsagePage);
        Assert.Equal(0xFFFF, links[0].Notify!.UsagePage);
        Assert.Equal(0xFF60, links[1].Control.UsagePage);
    }

    [Fact]
    public void Ignores_other_sonix_products()
    {
        Assert.Empty(KeyboardFinder.Find(new[] { I(0x800A, 0xFF13, 65, 65, 65, "Some Other Keyboard") }));
    }

    [Fact]
    public void Ignores_the_bootloader()
    {
        Assert.Empty(KeyboardFinder.Find(new[] { I(0x7140, 0xFF13, 65, 65, 65, "AK820") }));
    }

    [Fact]
    public void Ignores_a_different_firmware_family()
    {
        // Same IDs, but control on FF68 and display on FF67: a protocol this app does not speak.
        Assert.Empty(KeyboardFinder.Find(new[] { I(0x8009, 0xFF68, 65, 65, 0, "AK820"), I(0x8009, 0xFF67, 65, 4097, 0, "AK820") }));
    }
}
