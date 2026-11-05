using System.Runtime.InteropServices;
using OpenAK820.Model;

namespace OpenAK820.Services;

/// <summary>Colour calculations for the effects the PC animates. No I/O, so previews can reuse them.</summary>
public static class Effects
{
    private const double Width = KeyLayout.DesignRight - KeyLayout.DesignLeft;
    private const double Height = KeyLayout.DesignBottom - KeyLayout.DesignTop;

    private static (double X, double Y) Position(KeyInfo k) =>
        ((k.X + k.W / 2.0 - KeyLayout.DesignLeft) / Width, (k.Y + k.H / 2.0 - KeyLayout.DesignTop) / Height);

    /// <summary>Stable per-key pseudo-random phase, so sparkles differ between keys but not between frames.</summary>
    private static double Phase(int index) => (index * 2654435761u % 1000) / 1000.0;

    public static Dictionary<int, Rgb> RenderRealtime(RealtimeScheme scheme, double seconds)
    {
        var result = new Dictionary<int, Rgb>();
        var speed = 0.25 + scheme.Speed * 0.35;
        var brightness = scheme.Brightness / 5.0;
        foreach (var key in KeyLayout.Keys)
        {
            if (!scheme.Keys.TryGetValue(key.Index, out var setting) || setting.Effect == RealtimeEffect.Off)
            {
                result[key.Index] = Rgb.Black;
                continue;
            }
            var (x, y) = Position(key);
            var t = seconds * speed;
            var color = Rgb.Parse(setting.Color);
            var phase = Phase(key.Index);
            result[key.Index] = (setting.Effect switch
            {
                RealtimeEffect.StaticOn => color,
                RealtimeEffect.Starlight => color.Scale(Math.Pow(Math.Max(0, Math.Sin((t + phase * 7) * 3)), 6)),
                RealtimeEffect.Fluttering => color.Scale(0.5 + 0.5 * Math.Sin((t * 6) + phase * 40)),
                RealtimeEffect.DynamicBreathing => color.Scale(0.5 - 0.5 * Math.Cos(t * 3)),
                RealtimeEffect.ColorfulFountain => Rgb.FromHsv(360 * (Distance(x, y) - t * 0.5), 1, 1),
                RealtimeEffect.RainbowWave => Rgb.FromHsv(360 * (x - t * 0.5), 1, 1),
                RealtimeEffect.FollowingCurrents => color.Scale(Math.Pow(0.5 + 0.5 * Math.Sin((x * 2 - t) * Math.PI * 2), 3)),
                RealtimeEffect.PeakRevolving => Rgb.FromHsv(360 * (Math.Atan2(y - 0.5, x - 0.5) / (2 * Math.PI) + t * 0.5), 1, 1),
                _ => Rgb.Black,
            }).Scale(brightness);
        }
        return result;
    }

    private static double Distance(double x, double y) => Math.Sqrt((x - 0.5) * (x - 0.5) + (y - 0.5) * (y - 0.5));

    public static readonly string[] Rhythms = { "Rising Bars", "Centre Bars", "Whole-Board Pulse", "Rolling Wave", "Sparkle", "Ripple" };

    /// <summary>Colour for a position (0-1) and level (0-1) in one of the music palettes.</summary>
    public static Rgb MusicColor(MusicColorMode mode, double x, double level, double seconds, Rgb ambient) => mode switch
    {
        MusicColorMode.GreenYellowRed => level < 0.5 ? new Rgb(0, 255, 0) : level < 0.8 ? new Rgb(255, 200, 0) : new Rgb(255, 0, 0),
        MusicColorMode.Rainbow => Rgb.FromHsv(360 * x, 1, 1),
        MusicColorMode.ReverseRainbow => Rgb.FromHsv(360 * (1 - x), 1, 1),
        MusicColorMode.Gradient => Rgb.FromHsv(200 + 120 * x, 1, 1),
        MusicColorMode.SpectrumCycle => Rgb.FromHsv(seconds * 40, 1, 1),
        MusicColorMode.White => Rgb.White,
        MusicColorMode.Red => new Rgb(255, 0, 0),
        MusicColorMode.Orange => new Rgb(255, 110, 0),
        MusicColorMode.Yellow => new Rgb(255, 220, 0),
        MusicColorMode.Green => new Rgb(0, 255, 0),
        MusicColorMode.Cyan => new Rgb(0, 255, 255),
        MusicColorMode.Blue => new Rgb(0, 0, 255),
        MusicColorMode.Purple => new Rgb(170, 0, 255),
        MusicColorMode.Ambilight => ambient,
        _ => Rgb.Black,
    };

    public static Dictionary<int, Rgb> RenderMusic(MusicLayer layer, double[] bands, double seconds, Rgb ambient)
    {
        var result = new Dictionary<int, Rgb>();
        var gain = 0.5 + layer.Amplitude * 0.2;
        var average = bands.Length == 0 ? 0 : Math.Clamp(bands.Average() * gain, 0, 1);
        var shape = Math.Clamp(layer.Rhythm, 0, Rhythms.Length - 1);
        foreach (var key in KeyLayout.Keys)
        {
            var (x, y) = Position(key);
            var band = bands.Length == 0 ? 0 : Math.Clamp(bands[Math.Min((int)(x * bands.Length), bands.Length - 1)] * gain, 0, 1);
            var fromBottom = 1 - y;
            var lit = shape switch
            {
                0 => fromBottom <= band ? 1.0 : 0.0,
                1 => Math.Abs(y - 0.5) * 2 <= band ? 1.0 : 0.0,
                2 => average,
                3 => Math.Clamp(average * 2 * (0.5 + 0.5 * Math.Sin((x * 2 - seconds) * Math.PI * 2)), 0, 1),
                4 => band > 0.25 && Math.Sin((seconds * 9) + Phase(key.Index) * 40) > 1.2 - band * 1.6 ? 1.0 : 0.0,
                _ => Math.Abs(Distance(x, y) * 1.6 - average) < 0.14 ? 1.0 : 0.0,
            };
            var back = layer.Background == MusicColorMode.Off
                ? Rgb.Black
                : MusicColor(layer.Background, x, band, seconds, ambient).Scale(layer.BackgroundBrightness / 5.0);
            result[key.Index] = lit > 0
                ? MusicColor(layer.Foreground, x, Math.Max(fromBottom, band), seconds, ambient).Scale(lit)
                : back;
        }
        return result;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(IntPtr dc, int x, int y);

    /// <summary>Samples a coarse grid of the screen for the Ambilight palette.</summary>
    public static Rgb SampleScreen()
    {
        var bounds = SystemInformation.VirtualScreen;
        var dc = GetDC(IntPtr.Zero);
        try
        {
            long r = 0, g = 0, b = 0; int n = 0;
            for (int ix = 1; ix < 6; ix++)
            for (int iy = 1; iy < 4; iy++)
            {
                var c = GetPixel(dc, bounds.Left + bounds.Width * ix / 6, bounds.Top + bounds.Height * iy / 4);
                if (c == 0xFFFFFFFF) continue;
                r += c & 0xFF; g += (c >> 8) & 0xFF; b += (c >> 16) & 0xFF; n++;
            }
            return n == 0 ? Rgb.Black : new Rgb((byte)(r / n), (byte)(g / n), (byte)(b / n));
        }
        finally { ReleaseDC(IntPtr.Zero, dc); }
    }
}
