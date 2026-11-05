using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenAK820.Model;
using OpenAK820.Protocol;

namespace OpenAK820.Services;

/// <summary>
/// Runs the PC-animated lighting (real-time effects and music). These need the app to stay open, so the
/// engine lives with the main window and keeps going while it is hidden in the tray.
/// </summary>
public sealed class LiveEngine : IDisposable
{
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")] private static extern uint TimeBeginPeriod(uint ms);
    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")] private static extern uint TimeEndPeriod(uint ms);

    private readonly AppState _state;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 50 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly AudioLoopback _audio = new();
    private RealtimeScheme? _scheme;
    private MusicLayer? _music;
    private Rgb _ambient;
    private int _tick;

    public IReadOnlyDictionary<int, Rgb>? LastFrame { get; private set; }
    public double[] Bands => _audio.Bands;
    public bool IsRunning => _scheme != null || _music != null;
    public bool IsRealtime => _scheme != null;
    public bool IsMusic => _music != null;
    public event Action? Frame;
    public event Action? StateChanged;

    public LiveEngine(AppState state)
    {
        _state = state;
        _timer.Tick += (_, _) => Tick();
    }

    public void StartRealtime(RealtimeScheme scheme)
    {
        Stop();
        _scheme = scheme;
        Begin();
        StateChanged?.Invoke();
    }

    public void StartMusic(MusicLayer layer)
    {
        Stop();
        _music = layer;
        _audio.Start();
        Begin();
        StateChanged?.Invoke();
    }

    /// <summary>
    /// A frame involves several very short waits. At the default timer resolution each rounds up to about
    /// 15 ms: on a real board that gave 5.7 fps, against 16 fps at 1 ms. This costs some power system-wide,
    /// so it is held only while an effect runs.
    /// </summary>
    private void Begin()
    {
        TimeBeginPeriod(1);
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        if (_music != null) _audio.Stop();
        var wasRunning = IsRunning;
        if (wasRunning) TimeEndPeriod(1);
        _scheme = null;
        _music = null;
        LastFrame = null;
        if (wasRunning)
        {
            // The keyboard holds the last frame indefinitely, so the keys must be handed back explicitly.
            var lighting = _state.Profile.Lighting;
            _ = _state.SendAsync("", k => k.SetLighting(lighting));
            StateChanged?.Invoke();
        }
    }

    private void Tick()
    {
        var seconds = _clock.Elapsed.TotalSeconds;
        _tick++;
        if (_scheme != null)
        {
            LastFrame = Effects.RenderRealtime(_scheme, seconds);
            _state.Keyboard.SubmitFrame(LastFrame);
        }
        else if (_music != null)
        {
            if (_audio.Error is { } error)
            {
                _state.Report(error, true);
                Stop();
                return;
            }
            if (_music.Foreground == MusicColorMode.Ambilight || _music.Background == MusicColorMode.Ambilight)
                if (_tick % 5 == 0) _ambient = Effects.SampleScreen();
            var bands = _audio.Bands;
            LastFrame = Effects.RenderMusic(_music, bands, seconds, _ambient);
            if (_state.Keyboard.Protocol is WirelessProtocol)
            {
                // Over 2.4 GHz the firmware draws the effect; send it five levels at a slower rate.
                if (_tick % 3 == 0) SendWireless(bands);
            }
            else _state.Keyboard.SubmitFrame(LastFrame);
        }
        Frame?.Invoke();
    }

    private void SendWireless(double[] bands)
    {
        var layer = _music!;
        var five = new byte[5];
        for (int i = 0; i < 5; i++)
            five[i] = (byte)(255 * bands.Skip(i * bands.Length / 5).Take(bands.Length / 5).DefaultIfEmpty(0).Average());
        _ = _state.Keyboard.RunAsync(k => (k as WirelessProtocol)?.SendMusic((int)layer.Foreground - 106, (int)layer.Background - 106, layer.BackgroundBrightness, five));
    }

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
        _audio.Dispose();
    }
}
