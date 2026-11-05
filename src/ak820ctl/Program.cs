using OpenAK820.Device;
using OpenAK820.Hid;
using OpenAK820.Model;
using OpenAK820.Protocol;

// A small scriptable front end, and the tool used to check the protocol against real hardware.
// "list" and "read-live" change nothing. The other commands write to the keyboard's memory.

const string Usage = """
    ak820ctl - command line control for the AJAZZ AK820 Pro

      list                                 Show matching keyboards and their HID collections
      read-live                            Read the colours currently shown on the keys
      battery                              Read battery level (2.4 GHz only)
      light <mode 0-19> [#rrggbb] [brightness 0-5] [speed 0-5] [direction]
      sync-clock                           Set the screen clock to this PC's time
      perkey-test                          Store Q red, W green, E blue, other keys dark
      screen-test                          Upload an 8-frame test animation to the screen
      stream-test [hires]                  Stream temporary frames, measure the rate, then stop
      screen-upload <file> [--check <png>] Upload a GIF or image to the screen. With --check, only
                                           decode it and save a sheet of sample frames
    """;

if (args.Length == 0) { Console.WriteLine(Usage); return 0; }

// A check touches no hardware, so it runs before looking for a keyboard.
if (args[0] == "screen-upload" && args.Length == 4 && args[2] == "--check")
{
    var decoded = OpenAK820.Services.GifCodec.Load(args[1]);
    const int Cell = 132, Across = 4;
    var picks = Enumerable.Range(0, 12).Select(i => i * (decoded.Count - 1) / 11).Distinct().ToList();
    using var sheet = new Bitmap(Across * Cell, (picks.Count + Across - 1) / Across * Cell);
    using (var g = Graphics.FromImage(sheet))
    {
        g.Clear(Color.FromArgb(40, 40, 40));
        for (int i = 0; i < picks.Count; i++) g.DrawImage(decoded[picks[i]].Image, i % Across * Cell + 2, i / Across * Cell + 2, 128, 128);
    }
    sheet.Save(args[3], System.Drawing.Imaging.ImageFormat.Png);
    var pagesNeeded = (Encoders.ScreenHeaderSize + Math.Min(decoded.Count, Encoders.ScreenMaxFrames) * 128 * 128 * 2 + Encoders.ScreenPageSize - 1) / Encoders.ScreenPageSize;
    Console.WriteLine($"{decoded.Count} frames, delays {decoded.Min(f => f.DelayMs)}-{decoded.Max(f => f.DelayMs)} ms, {pagesNeeded} pages. Frames shown: {string.Join(", ", picks)}");
    return 0;
}

var links = KeyboardFinder.Find();

if (args[0] == "list")
{
    foreach (var i in HidEnumerator.List(KeyboardFinder.Vendor))
        Console.WriteLine($"{i.VendorId:X4}:{i.ProductId:X4} rel {i.Release:X4} page {i.UsagePage:X4} usage {i.Usage:X4} in {i.InputLength,4} out {i.OutputLength,4} feat {i.FeatureLength,3}  {i.Product}");
    Console.WriteLine();
    if (links.Count == 0) Console.WriteLine("No supported keyboard recognised.");
    foreach (var l in links)
        Console.WriteLine($"Recognised: {l.DisplayName}  screen={(l.Screen != null ? "yes" : "no")}  notifications={(l.Notify != null ? "yes" : "no")}");
    return 0;
}

var link = links.FirstOrDefault(l => l.Kind == LinkKind.Usb) ?? links.FirstOrDefault();
if (link == null) { Console.Error.WriteLine("No supported keyboard found."); return 1; }

using var control = new WinHidPort(link.Control.Path);
using var screen = link.Screen != null ? new WinHidPort(link.Screen.Path) : null;
IKeyboardProtocol protocol = link.Kind == LinkKind.Usb ? new WiredProtocol(control, screen) : new WirelessProtocol(control);

switch (args[0])
{
    case "read-live":
        var data = protocol.ReadLiveColors();
        if (data == null) { Console.Error.WriteLine("The keyboard did not provide live colours on this connection."); return 1; }
        int lit = 0;
        foreach (var key in KeyLayout.Keys)
        {
            var at = key.Index * 4;
            if (data[at + 1] != 0 || data[at + 2] != 0 || data[at + 3] != 0) lit++;
        }
        Console.WriteLine($"Read {data.Length} bytes. {lit} of {KeyLayout.Keys.Count} keys are lit.");
        for (int row = 0; row < 8 * 4; row += 16)
            Console.WriteLine($"{row:X3}: {Convert.ToHexString(data, row, 16)}");
        foreach (var name in args.Length > 1 ? args.Skip(1) : new[] { "Esc", "Q", "A", "Space" })
        {
            var key = KeyLayout.Keys.First(k => k.Name == name);
            var at = key.Index * 4;
            Console.WriteLine($"{name,-6} index {key.Index,3}: {Convert.ToHexString(data, at, 4)}");
        }
        return 0;

    case "trace-live":
        // Diagnostic: shows the raw reply stream for the read-only live colour request.
        var settle = args.Length > 1 ? int.Parse(args[1]) : 5;
        var between = args.Length > 2 ? int.Parse(args[2]) : 3;
        var request = new byte[65];
        request[1] = 0x04; request[2] = 0xF5; request[9] = 9;
        control.SetFeature(request);
        Thread.Sleep(settle);
        for (int i = 0; i < 12; i++)
        {
            var reply = new byte[65];
            control.GetFeature(reply);
            Console.WriteLine($"get {i,2}: {Convert.ToHexString(reply, 0, 21)}");
            Thread.Sleep(between);
        }
        return 0;

    case "battery":
        var pct = protocol.ReadBattery();
        Console.WriteLine(pct is null ? "Not reported on this connection." : $"{pct}%");
        return 0;

    case "light":
        var state = new LightingState
        {
            Mode = int.Parse(args[1]),
            Color = args.Length > 2 ? args[2] : "#FFFFFF",
            Colorful = args.Length <= 2,
            Brightness = args.Length > 3 ? int.Parse(args[3]) : 5,
            Speed = args.Length > 4 ? int.Parse(args[4]) : 3,
            Direction = args.Length > 5 ? int.Parse(args[5]) : 0,
        };
        protocol.SetLighting(state);
        Console.WriteLine($"Set {LightingModes.Get(state.Mode).Name}.");
        return 0;

    case "sync-clock":
        protocol.SyncClock(DateTime.Now);
        Console.WriteLine($"Clock set to {DateTime.Now:yyyy-MM-dd HH:mm:ss}.");
        return 0;

    case "perkey-test":
        int Index(string name) => KeyLayout.Keys.First(k => k.Name == name).Index;
        protocol.SetPerKey(new Dictionary<int, Rgb>
        {
            [Index("Q")] = new(255, 0, 0),
            [Index("W")] = new(0, 255, 0),
            [Index("E")] = new(0, 0, 255),
        }, 5);
        Console.WriteLine("Per-key test pattern stored.");
        return 0;

    case "screen-upload":
    {
        var loaded = OpenAK820.Services.GifCodec.Load(args[1]);
        if (loaded.Count > Encoders.ScreenMaxFrames) Console.WriteLine($"Using the first {Encoders.ScreenMaxFrames} of {loaded.Count} frames.");
        var frames = loaded.Take(Encoders.ScreenMaxFrames).Select(f => (OpenAK820.Services.GifCodec.ToRgb565(f.Image), f.DelayMs)).ToList();
        var image = Encoders.ScreenImage(frames, out var pages);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var lastShown = -1;
        Console.WriteLine($"Uploading {frames.Count} frames, {pages} pages.");
        protocol.UploadScreen(image, pages, 1, percent =>
        {
            if (percent / 10 == lastShown) return;
            lastShown = percent / 10;
            Console.WriteLine($"  {percent}%  ({clock.Elapsed.TotalSeconds:F0} s)");
        });
        Console.WriteLine($"Uploaded in {clock.Elapsed.TotalSeconds:F0} s. Every page was acknowledged.");
        return 0;
    }

    case "stream-test":
    {
        var hires = args.Length > 1 && args[1] == "hires";
        if (hires) TimeBeginPeriod(1);

        // Part 1: a frame with a different colour per key, to prove each record lands on the right key.
        var pattern = new Dictionary<int, Rgb>();
        foreach (var key in KeyLayout.Keys) pattern[key.Index] = new Rgb(0, (byte)(key.Index * 2), 200);
        pattern[KeyLayout.Keys.First(k => k.Name == "Q").Index] = new Rgb(255, 0, 0);
        for (int i = 0; i < 20; i++) protocol.StreamFrame(pattern);
        var held = protocol.ReadLiveColors();
        if (held == null) { Console.Error.WriteLine("No read-back."); return 1; }
        int matching = 0, wrongIndex = 0;
        foreach (var key in KeyLayout.Keys)
        {
            var at = key.Index * 4;
            if (held[at] != key.Index) wrongIndex++;
            var want = pattern[key.Index];
            // Red is scaled to about 65% by the firmware, and green and blue to about 98%.
            bool Near(int got, int sent, double scale) => Math.Abs(got - sent * scale) <= 6;
            if (Near(held[at + 1], want.R, 167 / 255.0) && Near(held[at + 2], want.G, 251 / 255.0) && Near(held[at + 3], want.B, 251 / 255.0)) matching++;
        }
        Console.WriteLine($"Per-key frame: {matching} of {KeyLayout.Keys.Count} keys match, {wrongIndex} wrong indices.");
        foreach (var name in new[] { "Esc", "Q", "A", "Space", "Fn" })
        {
            var key = KeyLayout.Keys.First(k => k.Name == name);
            Console.WriteLine($"  {name,-6} sent {pattern[key.Index].ToHex()}  read {Convert.ToHexString(held, key.Index * 4, 4)}");
        }

        // Part 2: an animation for five seconds, to measure the frame rate and drops.
        var clock = System.Diagnostics.Stopwatch.StartNew();
        int sent = 0, dropped = 0;
        var frame = new Dictionary<int, Rgb>();
        while (clock.Elapsed.TotalSeconds < 5)
        {
            var t = clock.Elapsed.TotalSeconds;
            foreach (var key in KeyLayout.Keys) frame[key.Index] = Rgb.FromHsv(360 * ((key.X - 95) / 610.0 - t * 0.5), 1, 1);
            if (!protocol.StreamFrame(frame)) dropped++;
            sent++;
        }
        Console.WriteLine($"Animation ({(hires ? "1 ms timer" : "default timer")}): {sent} frames in {clock.Elapsed.TotalSeconds:F1} s = {sent / clock.Elapsed.TotalSeconds:F1} fps, {dropped} dropped.");
        if (hires) TimeEndPeriod(1);

        // Part 3: stop, and watch whether the keys hold the last frame or go back to the stored mode.
        foreach (var wait in new[] { 100, 900, 2000, 3000 })
        {
            Thread.Sleep(wait);
            var after = protocol.ReadLiveColors();
            var q = KeyLayout.Keys.First(k => k.Name == "Q").Index * 4;
            var space = KeyLayout.Keys.First(k => k.Name == "Space").Index * 4;
            Console.WriteLine(after == null ? "  no read-back" : $"  +{wait,4} ms  Q {Convert.ToHexString(after, q + 1, 3)}  Space {Convert.ToHexString(after, space + 1, 3)}");
        }
        return 0;
    }

    case "screen-test":
    {
        // Each frame: red top-left, green top-right, blue bottom-left, white bottom-right,
        // and a white bar that steps left to right. That shows orientation, colour byte order and frame timing.
        const int Size = Encoders.ScreenWidth, FrameCount = 8;
        var frames = new List<(ushort[], int)>();
        for (int f = 0; f < FrameCount; f++)
        {
            var pixels = new ushort[Size * Size];
            var barAt = f * Size / FrameCount;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var (r, g, b) = (x < Size / 2, y < Size / 2) switch
                {
                    (true, true) => (255, 0, 0),
                    (false, true) => (0, 255, 0),
                    (true, false) => (0, 0, 255),
                    _ => (90, 90, 90),
                };
                if (x >= barAt && x < barAt + 6) (r, g, b) = (255, 255, 255);
                pixels[y * Size + x] = Encoders.ToRgb565((byte)r, (byte)g, (byte)b);
            }
            frames.Add((pixels, 200));
        }
        var image = Encoders.ScreenImage(frames, out var pages);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var lastShown = -1;
        protocol.UploadScreen(image, pages, 1, percent =>
        {
            if (percent / 25 == lastShown) return;
            lastShown = percent / 25;
            Console.WriteLine($"  {percent}%  ({clock.ElapsedMilliseconds} ms)");
        });
        Console.WriteLine($"Uploaded {FrameCount} frames, {pages} pages, in {clock.ElapsedMilliseconds} ms. Every page was acknowledged.");
        return 0;
    }

    default:
        Console.WriteLine(Usage);
        return 2;
}

[System.Runtime.InteropServices.DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")] static extern uint TimeBeginPeriod(uint ms);
[System.Runtime.InteropServices.DllImport("winmm.dll", EntryPoint = "timeEndPeriod")] static extern uint TimeEndPeriod(uint ms);
