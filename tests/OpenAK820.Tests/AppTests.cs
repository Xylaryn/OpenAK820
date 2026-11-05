using System.Drawing.Imaging;
using System.Numerics;
using System.Reflection;
using OpenAK820.Device;
using OpenAK820.Hid;
using OpenAK820.Model;
using OpenAK820.Pages;
using OpenAK820.Protocol;
using OpenAK820.Services;
using OpenAK820.Ui;
using Xunit;

namespace OpenAK820.Tests;

public class GifTests
{
    private static Bitmap Solid(Color c)
    {
        var b = new Bitmap(GifCodec.Size, GifCodec.Size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(b);
        g.Clear(c);
        return b;
    }

    /// <summary>Random pixels defeat LZW compression, so this exercises code growth and table resets.</summary>
    private static Bitmap Noise(int seed)
    {
        var r = new Random(seed);
        var b = new Bitmap(GifCodec.Size, GifCodec.Size, PixelFormat.Format32bppArgb);
        for (int y = 0; y < GifCodec.Size; y++)
            for (int x = 0; x < GifCodec.Size; x++)
                b.SetPixel(x, y, Color.FromArgb(r.Next(256), r.Next(256), r.Next(256)));
        return b;
    }

    private static string Temp() => Path.Combine(Path.GetTempPath(), $"ak820-test-{Guid.NewGuid():N}.gif");

    [Fact]
    public void Round_trip_keeps_frames_timing_and_colours()
    {
        var path = Temp();
        try
        {
            GifCodec.Save(path, new[] { new GifFrame(Solid(Color.Red), 100), new GifFrame(Solid(Color.Blue), 250), new GifFrame(Solid(Color.Lime), 480) });
            var loaded = GifCodec.Load(path);
            Assert.Equal(3, loaded.Count);
            Assert.Equal(new[] { 100, 250, 480 }, loaded.Select(f => f.DelayMs));
            Assert.Equal(Color.FromArgb(255, 0, 0).ToArgb(), loaded[0].Image.GetPixel(64, 64).ToArgb());
            Assert.Equal(Color.FromArgb(0, 0, 255).ToArgb(), loaded[1].Image.GetPixel(64, 64).ToArgb());
            Assert.Equal(Color.FromArgb(0, 255, 0).ToArgb(), loaded[2].Image.GetPixel(64, 64).ToArgb());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Incompressible_frames_decode_exactly()
    {
        var path = Temp();
        try
        {
            using var source = Noise(1);
            GifCodec.Save(path, new[] { new GifFrame(source, 100), new GifFrame(Noise(2), 100) });
            var loaded = GifCodec.Load(path);
            Assert.Equal(2, loaded.Count);
            // Colours are quantised, so compare with the tolerance of the 6-7-6 cube.
            for (int y = 0; y < GifCodec.Size; y += 7)
                for (int x = 0; x < GifCodec.Size; x += 7)
                {
                    Color a = source.GetPixel(x, y), b = loaded[0].Image.GetPixel(x, y);
                    Assert.InRange(Math.Abs(a.R - b.R), 0, 28);
                    Assert.InRange(Math.Abs(a.G - b.G), 0, 24);
                    Assert.InRange(Math.Abs(a.B - b.B), 0, 28);
                }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Non_square_images_fill_the_screen()
    {
        using var wide = new Bitmap(320, 64);
        using (var g = Graphics.FromImage(wide)) g.Clear(Color.Red);
        using var fitted = GifCodec.Fit(wide);
        Assert.Equal(128, fitted.Width);
        Assert.Equal(128, fitted.Height);
        Assert.Equal(255, fitted.GetPixel(0, 0).R);
        Assert.Equal(255, fitted.GetPixel(127, 127).R);
    }

    [Fact]
    public void Red_converts_to_rgb565_red()
    {
        using var red = Solid(Color.Red);
        Assert.All(GifCodec.ToRgb565(red), p => Assert.Equal(0xF800, p));
    }
}

public class DiagnosticsTests
{
    private static HidInterfaceInfo Device(string path) =>
        new(path, 0x0C45, 0x800A, 0x110, 0xFF13, 1, 65, 65, 65, "SONiX", "AK820");

    [Fact]
    public void Report_describes_the_board()
    {
        var text = Diagnostics.Report(new[] { Device("x") }, "1.2.3");
        Assert.Contains("OpenAK820 1.2.3", text);
        Assert.Contains("0C45:800A", text);
        Assert.Contains("page FF13", text);
        Assert.Contains("AK820 Pro (USB)", text);
    }

    [Fact]
    public void Report_leaves_out_the_pc_specific_device_path()
    {
        const string instance = "a&16386c71&0&0000";
        var text = Diagnostics.Report(new[] { Device($@"\\?\hid#vid_0c45&pid_800a&mi_03#{instance}#{{4d1e55b2}}") }, "1.2.3");
        Assert.DoesNotContain(instance, text);
        Assert.DoesNotContain("hid#", text);
    }

    [Fact]
    public void Report_contains_no_user_or_machine_names()
    {
        var text = Diagnostics.Report(new[] { Device("x") }, "1.2.3");
        Assert.DoesNotContain(Environment.UserName, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.MachineName, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Report_explains_when_nothing_is_found()
    {
        var text = Diagnostics.Report(Array.Empty<HidInterfaceInfo>(), "1.2.3");
        Assert.Contains("Recognised: none", text);
        Assert.Contains("No HID devices", text);
    }
}

public class InputTests
{
    [Theory]
    [InlineData(0x41, 0x04)] // A
    [InlineData(0x5A, 0x1D)] // Z
    [InlineData(0x31, 0x1E)] // 1
    [InlineData(0x30, 0x27)] // 0
    [InlineData(0x0D, 0x28)] // Enter
    [InlineData(0x70, 0x3A)] // F1
    [InlineData(0x7B, 0x45)] // F12
    [InlineData(0x7C, 0x68)] // F13
    [InlineData(0xA2, 0xE0)] // Left Ctrl
    [InlineData(0x5C, 0xE7)] // Right Win
    [InlineData(0x26, 0x52)] // Up
    public void Virtual_keys_map_to_hid(int vk, int hid) => Assert.Equal(hid, HidKeys.FromVirtualKey(vk));

    [Fact]
    public void Every_layout_key_has_a_unique_index_inside_the_table()
    {
        Assert.Equal(81, KeyLayout.Keys.Count);
        Assert.Equal(81, KeyLayout.Keys.Select(k => k.Index).Distinct().Count());
        Assert.All(KeyLayout.Keys, k => Assert.InRange(k.Index * 4 + 3, 0, Encoders.TableSize - 3));
    }
}

public class EffectTests
{
    [Fact]
    public void Fft_finds_a_pure_tone()
    {
        var a = new Complex[1024];
        for (int i = 0; i < a.Length; i++) a[i] = new Complex(Math.Sin(2 * Math.PI * 50 * i / a.Length), 0);
        AudioLoopback.Transform(a);
        var peak = Enumerable.Range(1, 511).MaxBy(i => a[i].Magnitude);
        Assert.Equal(50, peak);
    }

    [Theory]
    [InlineData(RealtimeEffect.StaticOn)] [InlineData(RealtimeEffect.Starlight)] [InlineData(RealtimeEffect.Fluttering)] [InlineData(RealtimeEffect.DynamicBreathing)]
    [InlineData(RealtimeEffect.ColorfulFountain)] [InlineData(RealtimeEffect.RainbowWave)] [InlineData(RealtimeEffect.FollowingCurrents)] [InlineData(RealtimeEffect.PeakRevolving)]
    public void Realtime_effects_cover_every_key(RealtimeEffect effect)
    {
        var scheme = new RealtimeScheme();
        foreach (var k in KeyLayout.Keys) scheme.Keys[k.Index] = new RealtimeKey { Effect = effect, Color = "#FF0000" };
        Assert.Equal(KeyLayout.Keys.Count, Effects.RenderRealtime(scheme, 1.234).Count);
    }

    [Fact]
    public void Static_effect_uses_the_chosen_colour_at_full_brightness()
    {
        var scheme = new RealtimeScheme();
        scheme.Keys[1] = new RealtimeKey { Effect = RealtimeEffect.StaticOn, Color = "#102030" };
        var frame = Effects.RenderRealtime(scheme, 0);
        Assert.Equal(new Rgb(0x10, 0x20, 0x30), frame[1]);
        Assert.Equal(Rgb.Black, frame[2]);
    }

    [Fact]
    public void Silence_with_no_background_is_dark()
    {
        var layer = new MusicLayer { Rhythm = 2, Background = MusicColorMode.Off };
        var frame = Effects.RenderMusic(layer, new double[16], 0, Rgb.Black);
        Assert.All(frame.Values, c => Assert.Equal(Rgb.Black, c));
    }

    [Fact]
    public void Loud_signal_lights_the_board_for_every_rhythm()
    {
        for (int rhythm = 0; rhythm < Effects.Rhythms.Length; rhythm++)
        {
            var bands = Enumerable.Repeat(0.9, 16).ToArray();
            // Sparkle depends on time, so sample several moments.
            var any = Enumerable.Range(0, 40).Any(t => Effects.RenderMusic(new MusicLayer { Rhythm = rhythm, Foreground = MusicColorMode.White }, bands, t / 7.0, Rgb.Black).Values.Any(c => c != Rgb.Black));
            Assert.True(any, $"Rhythm {rhythm} stayed dark");
        }
    }
}

/// <summary>
/// Builds the real pages against the simulator. WinForms needs an STA thread, and these use reflection
/// to press buttons, because the point is to run the same code paths a click would.
/// </summary>
public class UiSmokeTests
{
    private static void Sta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception e) { failure = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    private static AppState NewState(out SimulatedPort control)
    {
        Environment.SetEnvironmentVariable("OPENAK820_DATA", Path.Combine(Path.GetTempPath(), "ak820-tests-" + Guid.NewGuid().ToString("N")));
        var state = new AppState();
        control = new SimulatedPort();
        var info = new HidInterfaceInfo("sim", 0x0C45, 0x800A, 0x110, 0xFF13, 1, 65, 65, 65, "SONiX", "AK820");
        var attached = new ManualResetEventSlim();
        state.Keyboard.ConnectionChanged += attached.Set;
        state.Keyboard.Attach(new WiredProtocol(control, new SimulatedPort(), 0, _ => { }), new KeyboardLink(LinkKind.Usb, info, info, null));
        attached.Wait(2000);
        return state;
    }

    private static T Field<T>(object o, string name) =>
        (T)o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(o)!;

    private static object? Call(object o, string name, params object[] args) =>
        o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(o, args);

    [Fact]
    public void Every_page_builds_loads_and_paints_in_every_language()
    {
        Sta(() =>
        {
            foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "uk" })
            {
                Loc.Set(language);
                var state = NewState(out _);
                using var live = new LiveEngine(state);
                var pages = new Page[]
                {
                    new HomePage(state), new KeysPage(state), new MacroPage(state), new LightingPage(state), new PerKeyPage(state),
                    new RealtimePage(state, live), new MusicPage(state, live), new ScreenPage(state), new SettingsPage(state, () => { }),
                };
                foreach (var page in pages)
                {
                    using var host = new Form { ClientSize = new Size(1000, 760) };
                    host.Controls.Add(page);
                    host.CreateControl();
                    page.Reload();
                    using var bitmap = new Bitmap(1000, 760);
                    host.DrawToBitmap(bitmap, host.ClientRectangle);
                    page.Dispose();
                }
                state.Keyboard.Dispose();
            }
            Loc.Set("en");
        });
    }

    [Fact]
    public void Every_key_function_can_be_assigned_and_written()
    {
        Sta(() =>
        {
            var state = NewState(out var control);
            state.Saved.Macros.Add(new Macro { Name = "M", Events = { new() { Kind = MacroEventKind.KeyDown, Code = 4 } } });
            using var page = new KeysPage(state);
            using var host = new Form();
            host.Controls.Add(page);
            host.CreateControl();
            page.Reload();
            var view = Field<KeyboardView>(page, "_view");
            var kind = Field<ComboBox>(page, "_kind");
            var keys = KeyLayout.Keys.Where(k => k.Name != "Fn").ToList();
            for (int i = 0; i < kind.Items.Count; i++)
            {
                view.Selected.Clear();
                view.Selected.Add(keys[i].Index);
                kind.SelectedIndex = i;
                Call(page, "ApplyToKey");
                var chosen = ((Choice<KeyActionKind>)kind.Items[i]!).Value;
                if (chosen == KeyActionKind.Default) Assert.False(state.Profile.Layers[0].ContainsKey(keys[i].Index));
                else Assert.Equal(chosen, state.Profile.Layers[0][keys[i].Index].Kind);
            }
            // Twelve non-default functions, four of them handled by the PC with distinct IDs.
            Assert.Equal(12, state.Profile.Layers[0].Count);
            var ids = state.Profile.Layers[0].Values.Where(a => a.IsHostSide).Select(a => a.HostId).ToList();
            Assert.Equal(4, ids.Count);
            Assert.Equal(4, ids.Distinct().Count());
            Assert.DoesNotContain((byte)0, ids);

            var table = Encoders.KeyTable(state.Profile.Layers[0], 0);
            Assert.True(state.Keyboard.RunAsync(k => k.SetKeyTable(0, table)).GetAwaiter().GetResult());
            Assert.Contains(control.Features, f => f[1] == 0x04 && f[2] == 0x11);
            state.Keyboard.Dispose();
        });
    }

    [Fact]
    public void Fn_key_cannot_be_selected_for_remapping()
    {
        Sta(() =>
        {
            var state = NewState(out _);
            using var page = new KeysPage(state);
            var view = Field<KeyboardView>(page, "_view");
            Assert.False(view.KeyEnabled!(KeyLayout.Keys.First(k => k.Name == "Fn")));
            Assert.True(view.KeyEnabled!(KeyLayout.Keys.First(k => k.Name == "A")));
            state.Keyboard.Dispose();
        });
    }

    [Fact]
    public void Profile_survives_save_and_load()
    {
        var state = NewState(out _);
        state.Profile.Name = "Gaming";
        state.Profile.Layers[1][56] = new KeyAction { Kind = KeyActionKind.OpenWebsite, Text = "example.com", HostId = 3 };
        state.Profile.Lighting.Mode = 7;
        state.Saved.Macros.Add(new Macro { Name = "M1", Events = { new() { Kind = MacroEventKind.MouseDown, Code = 3, DelayMs = 42 } } });
        state.Profile.PerKeySchemes.Add(new PerKeyScheme { Name = "S", Colors = { [5] = "#010203" } });
        state.Save();

        var again = new AppState();
        again.Load();
        Assert.Equal("Gaming", again.Profile.Name);
        Assert.Equal(KeyActionKind.OpenWebsite, again.Profile.Layers[1][56].Kind);
        Assert.Equal(3, again.Profile.FindHostAction(3)!.HostId);
        Assert.Equal(7, again.Profile.Lighting.Mode);
        Assert.Equal(42, again.Saved.Macros[0].Events[0].DelayMs);
        Assert.Equal("#010203", again.Profile.PerKeySchemes[0].Colors[5]);
        state.Keyboard.Dispose();
        again.Keyboard.Dispose();
    }

    [Fact]
    public void Writing_a_profile_touches_every_area()
    {
        var state = NewState(out var control);
        Assert.True(state.ApplyProfileAsync().GetAwaiter().GetResult());
        var commands = control.Features.Where(f => f[1] == 0x04).Select(f => f[2]).ToHashSet();
        foreach (var expected in new byte[] { 0x19, 0x15, 0x11, 0x27, 0x17, 0x13, 0x02, 0xF0 }) Assert.Contains(expected, commands);
        state.Keyboard.Dispose();
    }

    /// <summary>A real keyboard freezes on the last live frame until told otherwise.</summary>
    [Fact]
    public void Stopping_or_exiting_during_an_effect_restores_stored_lighting()
    {
        Sta(() =>
        {
            foreach (var viaDispose in new[] { false, true })
            {
                var state = NewState(out var control);
                var live = new LiveEngine(state);
                live.StartRealtime(new RealtimeScheme());
                Assert.True(live.IsRunning);
                if (viaDispose) live.Dispose();
                else live.Stop();
                Assert.False(live.IsRunning);
                state.Keyboard.Dispose(); // Finishes queued work, exactly as closing the app does.
                Assert.Contains(control.Features, f => f[1] == 0x04 && f[2] == 0x13);
            }
        });
    }

    [Fact] // 0x4348D3
    public void Usb_connection_sets_the_clock()
    {
        var state = NewState(out var control);
        state.SyncClockOnConnectAsync().GetAwaiter().GetResult();
        Assert.Contains(control.Features, f => f[1] == 0x04 && f[2] == 0x28);
        state.Keyboard.Dispose();
    }

    [Fact]
    public void Failed_clock_sync_on_connect_stays_quiet()
    {
        var state = NewState(out var control);
        control.EchoOverride = new byte[65]; // Another program's traffic.
        string? error = null;
        state.Status += (message, isError) => { if (isError) error = message; };
        state.SyncClockOnConnectAsync().GetAwaiter().GetResult();
        Assert.Null(error);
        state.Keyboard.Dispose();
    }

    [Fact]
    public void Corrupt_settings_file_is_kept_and_defaults_are_used()
    {
        var state = NewState(out _);
        Directory.CreateDirectory(AppState.DataFolder);
        var path = Path.Combine(AppState.DataFolder, "state.json");
        File.WriteAllText(path, "{ this is not json");
        state.Load();
        Assert.Single(state.Saved.Profiles);
        Assert.True(File.Exists(path + ".bad"));
        state.Keyboard.Dispose();
    }

    [Fact]
    public void Reports_failure_instead_of_success_when_another_program_interferes()
    {
        var state = NewState(out var control);
        control.EchoOverride = new byte[65];
        control.EchoOverride[1] = 0x04;
        control.EchoOverride[2] = 0x20; // Another program's frame, exactly what SignalRGB's traffic looks like.
        string? error = null;
        state.Status += (message, isError) => { if (isError) error = message; };
        Assert.False(state.SendAsync("done", k => k.SetLighting(new LightingState())).GetAwaiter().GetResult());
        Assert.Contains("Another program", error);
        state.Keyboard.Dispose();
    }
}
