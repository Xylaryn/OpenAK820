using System.Text.Json;
using OpenAK820.Device;
using OpenAK820.Model;
using OpenAK820.Protocol;
using OpenAK820.Services;
using OpenAK820.Ui;
using static OpenAK820.Ui.Loc;

namespace OpenAK820.Pages;

public sealed class HomePage : Page
{
    private readonly AppState _state;
    private readonly Label _device = Make.Label("", Theme.Title);
    private readonly Label _detail = Make.Label("", null, Theme.Muted);
    private readonly ListBox _profiles = Make.List(300, 190);

    public HomePage(AppState state)
    {
        _state = state;
        var view = new KeyboardView { Dock = DockStyle.Fill };
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 290, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(16, 8, 16, 8), BackColor = Theme.Panel };

        var left = Make.Column(460, new Padding(0, 0, 24, 0));
        left.AutoSize = true;
        left.Controls.Add(_device);
        left.Controls.Add(_detail);
        left.Controls.Add(Make.Note(T("This app has no network code and sends nothing anywhere. Your profiles are stored on this PC."), 420));
        left.Controls.Add(Make.Button(T("Copy diagnostic info"), CopyDiagnostics, false, 200));
        left.Controls.Add(Make.Note(T("For bug reports. Lists only what the keyboard reports about itself, with nothing about you or your PC."), 420));
        bottom.Controls.Add(left);

        var right = Make.Column();
        right.Controls.Add(Make.Heading(T("Profiles")));
        _profiles.SelectedIndexChanged += (_, _) =>
        {
            if (_profiles.SelectedIndex >= 0 && _profiles.SelectedIndex != _state.Saved.ActiveProfile)
            {
                _state.Saved.ActiveProfile = _profiles.SelectedIndex;
                _state.Save();
                _state.RaiseProfileChanged();
            }
        };
        right.Controls.Add(_profiles);
        bottom.Controls.Add(right);

        var buttons = Make.Column(0, new Padding(10, 26, 0, 0));
        buttons.Controls.Add(Make.Button(T("Write to keyboard"), () => _ = _state.ApplyProfileAsync(), true, 170));
        buttons.Controls.Add(Make.Button(T("New"), New, false, 170));
        buttons.Controls.Add(Make.Button(T("Rename"), Rename, false, 170));
        buttons.Controls.Add(Make.Button(T("Delete"), Delete, false, 170));
        buttons.Controls.Add(Make.Button(T("Import"), Import, false, 170));
        buttons.Controls.Add(Make.Button(T("Export"), Export, false, 170));
        bottom.Controls.Add(buttons);

        Controls.Add(view);
        Controls.Add(bottom);
    }

    public override void Reload()
    {
        var link = _state.Keyboard.Link;
        _device.Text = link?.DisplayName ?? T("No keyboard found");
        _device.ForeColor = link != null ? Theme.Text : Theme.Muted;
        _detail.Text = link == null
            ? T("Connect the keyboard by USB or its 2.4 GHz dongle. Bluetooth cannot configure the keyboard.")
            : _state.Battery is { } b ? string.Format(T("Battery {0}%"), b) : link.Kind == LinkKind.Usb ? T("Connected by USB") : T("Connected by 2.4 GHz");
        _profiles.BeginUpdate();
        _profiles.Items.Clear();
        foreach (var p in _state.Saved.Profiles) _profiles.Items.Add(p.Name);
        _profiles.SelectedIndex = Math.Clamp(_state.Saved.ActiveProfile, 0, _profiles.Items.Count - 1);
        _profiles.EndUpdate();
    }

    private void New()
    {
        var name = Prompt(T("New"), T("New Profile"));
        if (string.IsNullOrEmpty(name)) return;
        _state.Saved.Profiles.Add(new Profile { Name = name });
        _state.Saved.ActiveProfile = _state.Saved.Profiles.Count - 1;
        _state.Save();
        _state.RaiseProfileChanged();
    }

    private void Rename()
    {
        var name = Prompt(T("Rename"), _state.Profile.Name);
        if (string.IsNullOrEmpty(name)) return;
        _state.Profile.Name = name;
        _state.Save();
        Reload();
    }

    private void Delete()
    {
        if (_state.Saved.Profiles.Count <= 1) { _state.Report(T("At least one profile is needed."), true); return; }
        if (!Confirm(T("Delete"), string.Format(T("Delete profile \"{0}\"?"), _state.Profile.Name))) return;
        _state.Saved.Profiles.RemoveAt(_state.Saved.ActiveProfile);
        _state.Saved.ActiveProfile = 0;
        _state.Save();
        _state.RaiseProfileChanged();
    }

    private void CopyDiagnostics()
    {
        try
        {
            var version = Application.ProductVersion.Split('+')[0];
            Clipboard.SetText(Diagnostics.Report(OpenAK820.Hid.HidEnumerator.List(KeyboardFinder.Vendor), version));
            _state.Report(T("Diagnostic info copied. Paste it into your bug report."));
        }
        catch (System.Runtime.InteropServices.ExternalException) { _state.Report(T("Could not use the clipboard. Please try again."), true); }
    }

    private void Export()
    {
        if (!ConfirmExportContents(_state.Profile.Layers.SelectMany(l => l.Values))) return;
        var path = PickSave("Profile (*.json)|*.json", _state.Profile.Name + ".json");
        if (path == null) return;
        File.WriteAllText(path, JsonSerializer.Serialize(_state.Profile, AppState.Json));
    }

    private void Import()
    {
        var path = PickOpen("Profile (*.json)|*.json");
        if (path == null) return;
        try
        {
            var p = JsonSerializer.Deserialize<Profile>(File.ReadAllText(path), AppState.Json) ?? throw new InvalidDataException();
            if (!ConfirmHostActions(p.Layers.SelectMany(l => l.Values))) return;
            _state.Saved.Profiles.Add(p);
            _state.Saved.ActiveProfile = _state.Saved.Profiles.Count - 1;
            _state.Save();
            _state.RaiseProfileChanged();
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or IOException)
        {
            _state.Report(T("That file is not a valid profile."), true);
        }
    }
}

public sealed class LightingPage : Page
{
    private readonly AppState _state;
    private readonly ListBox _modes = Make.List(190, 560);
    private readonly ColorPicker _color = new();
    private readonly CheckBox _colorful;
    private readonly Slider _brightness = new(), _speed = new();
    private readonly RadioButton _dirA, _dirB;
    private readonly Label _dirLabel = Make.Label(T("Lighting Direction"));
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 450 };
    private bool _loading;

    public LightingPage(AppState state)
    {
        _state = state;
        var split = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(16), BackColor = Theme.Back };

        var left = Make.Column();
        left.Controls.Add(Make.Heading(T("Lighting Mode")));
        foreach (var m in LightingModes.All) _modes.Items.Add(new Choice<int>(T(m.Name), m.Id));
        _modes.SelectedIndexChanged += (_, _) => { if (!_loading) { Changed(); UpdateEnabled(); } };
        left.Controls.Add(_modes);
        split.Controls.Add(left);

        var right = Make.Column(0, new Padding(24, 0, 0, 0));
        right.Controls.Add(Make.Label(T("Lighting Color")));
        right.Controls.Add(_color);
        _colorful = Make.Check(T("Colorful"), false, _ => { if (!_loading) Changed(); });
        _color.ColorChanged += () => { _colorful.Checked = false; Changed(); };
        right.Controls.Add(_colorful);
        right.Controls.Add(Make.Label(T("Lighting Brightness")));
        _brightness.Maximum = 5;
        _brightness.ValueChanged += () => { if (!_loading) Changed(); };
        right.Controls.Add(_brightness);
        right.Controls.Add(Make.Label(T("Lighting Speed")));
        _speed.Maximum = 5;
        _speed.ValueChanged += () => { if (!_loading) Changed(); };
        right.Controls.Add(_speed);
        right.Controls.Add(_dirLabel);
        _dirA = Make.Radio("", true, () => { if (!_loading) Changed(); });
        _dirB = Make.Radio("", false, () => { if (!_loading) Changed(); });
        right.Controls.Add(Make.Row(_dirA, _dirB));
        right.Controls.Add(Make.Note(T("Changes are sent shortly after you stop adjusting, to spare the keyboard's flash memory."), 300));
        split.Controls.Add(right);
        Controls.Add(split);

        _debounce.Tick += (_, _) => { _debounce.Stop(); Send(); };
    }

    private LightingModeInfo Current => LightingModes.Get(_modes.SelectedItem is Choice<int> c ? c.Value : 1);

    public override void Reload()
    {
        _loading = true;
        var s = _state.Profile.Lighting;
        for (int i = 0; i < _modes.Items.Count; i++)
            if (_modes.Items[i] is Choice<int> c && c.Value == s.Mode) _modes.SelectedIndex = i;
        _color.Hex = s.Color;
        _colorful.Checked = s.Colorful;
        _brightness.Value = s.Brightness;
        _speed.Value = s.Speed;
        _dirA.Checked = s.Direction == 0;
        _dirB.Checked = s.Direction != 0;
        _loading = false;
        UpdateEnabled();
    }

    private void UpdateEnabled()
    {
        var caps = Current.Caps;
        _color.Enabled = caps.HasFlag(ModeCaps.Palette);
        _colorful.Enabled = caps.HasFlag(ModeCaps.Colorful);
        _brightness.Enabled = caps.HasFlag(ModeCaps.Brightness);
        _speed.Enabled = caps.HasFlag(ModeCaps.Speed);
        var horizontal = caps.HasFlag(ModeCaps.DirectionHorizontal);
        var vertical = caps.HasFlag(ModeCaps.DirectionVertical);
        _dirA.Enabled = _dirB.Enabled = _dirLabel.Enabled = horizontal || vertical;
        _dirA.Text = vertical ? T("Up") : T("Right");
        _dirB.Text = vertical ? T("Down") : T("Left");
    }

    private void Changed()
    {
        var s = _state.Profile.Lighting;
        var caps = Current.Caps;
        s.Mode = Current.Id;
        s.Color = _color.Hex;
        s.Colorful = _colorful.Checked;
        s.Brightness = _brightness.Value;
        s.Speed = _speed.Value;
        // Direction 1 is the opposite horizontal direction, 2 is the opposite vertical one.
        s.Direction = _dirA.Checked ? 0 : caps.HasFlag(ModeCaps.DirectionVertical) ? 2 : 1;
        _state.Save();
        _debounce.Stop();
        _debounce.Start();
    }

    private void Send()
    {
        var s = _state.Profile.Lighting;
        _ = _state.SendAsync(T("Lighting sent to keyboard."), k => k.SetLighting(s));
    }
}

public sealed class PerKeyPage : Page
{
    private readonly AppState _state;
    private readonly KeyboardView _view = new() { Dock = DockStyle.Fill, MultiSelect = true };
    private readonly ListBox _schemes = Make.List(200, 130);
    private readonly ColorPicker _color = new();
    private readonly Slider _brightness = new();

    public PerKeyPage(AppState state)
    {
        _state = state;
        _view.KeyColor = k => Scheme != null && Scheme.Colors.TryGetValue(k.Index, out var hex) ? Rgb.Parse(hex).ToColor() : null;
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 290, WrapContents = false, Padding = new Padding(16, 8, 16, 8), BackColor = Theme.Panel };

        var list = Make.Column();
        list.Controls.Add(Make.Heading(T("User Lighting")));
        _schemes.SelectedIndexChanged += (_, _) => { _view.SelectNone(); LoadScheme(); };
        list.Controls.Add(_schemes);
        list.Controls.Add(Make.Row(Make.Button(T("New"), New), Make.Button(T("Rename"), Rename), Make.Button(T("Delete"), Delete)));
        list.Controls.Add(Make.Row(Make.Button(T("Import"), Import), Make.Button(T("Export"), Export)));
        bottom.Controls.Add(list);

        var paint = Make.Column(0, new Padding(28, 0, 0, 0));
        paint.Controls.Add(Make.Label(T("Lighting Color")));
        paint.Controls.Add(_color);
        paint.Controls.Add(Make.Row(
            Make.Button(T("Paint selected keys"), () => PaintKeys(true), true),
            Make.Button(T("Clear selected keys"), () => PaintKeys(false))));
        paint.Controls.Add(Make.Row(Make.Button(T("Select All"), _view.SelectAll), Make.Button(T("Select None"), _view.SelectNone)));
        paint.Controls.Add(Make.Note(T("Click keys, or drag across the board from an empty area to select a group."), 330));
        bottom.Controls.Add(paint);

        var send = Make.Column(0, new Padding(28, 0, 0, 0));
        send.Controls.Add(Make.Label(T("Lighting Brightness")));
        _brightness.Maximum = 5;
        _brightness.ValueChanged += () => { if (Scheme != null) { Scheme.Brightness = _brightness.Value; _state.Save(); } };
        send.Controls.Add(_brightness);
        send.Controls.Add(Make.Button(T("Write to keyboard"), Upload, true, 200));
        send.Controls.Add(Make.Note(T("Stored on the keyboard, so it keeps working without this app."), 220));
        bottom.Controls.Add(send);

        Controls.Add(_view);
        Controls.Add(bottom);
    }

    private PerKeyScheme? Scheme => _schemes.SelectedIndex >= 0 && _schemes.SelectedIndex < _state.Profile.PerKeySchemes.Count ? _state.Profile.PerKeySchemes[_schemes.SelectedIndex] : null;

    public override void Reload()
    {
        if (_state.Profile.PerKeySchemes.Count == 0) _state.Profile.PerKeySchemes.Add(new PerKeyScheme { Name = T("Scheme 1") });
        var selected = Math.Max(_schemes.SelectedIndex, 0);
        _schemes.Items.Clear();
        foreach (var s in _state.Profile.PerKeySchemes) _schemes.Items.Add(s.Name);
        _schemes.SelectedIndex = Math.Min(selected, _schemes.Items.Count - 1);
        LoadScheme();
    }

    private void LoadScheme()
    {
        if (Scheme != null) _brightness.Value = Scheme.Brightness;
        _view.Invalidate();
    }

    private void PaintKeys(bool apply)
    {
        if (Scheme == null) return;
        if (_view.Selected.Count == 0) { _state.Report(T("Select some keys first."), true); return; }
        foreach (var i in _view.Selected)
        {
            if (apply) Scheme.Colors[i] = _color.Hex;
            else Scheme.Colors.Remove(i);
        }
        _state.Save();
        _view.Invalidate();
    }

    private void New()
    {
        var name = Prompt(T("New"), string.Format(T("Scheme {0}"), _state.Profile.PerKeySchemes.Count + 1));
        if (string.IsNullOrEmpty(name)) return;
        _state.Profile.PerKeySchemes.Add(new PerKeyScheme { Name = name });
        _state.Save();
        Reload();
        _schemes.SelectedIndex = _schemes.Items.Count - 1;
    }

    private void Rename()
    {
        if (Scheme == null) return;
        var name = Prompt(T("Rename"), Scheme.Name);
        if (string.IsNullOrEmpty(name)) return;
        Scheme.Name = name;
        _state.Save();
        Reload();
    }

    private void Delete()
    {
        if (Scheme == null || !Confirm(T("Delete"), string.Format(T("Delete \"{0}\"?"), Scheme.Name))) return;
        _state.Profile.PerKeySchemes.Remove(Scheme);
        _schemes.SelectedIndex = -1;
        _state.Save();
        Reload();
    }

    private void Export()
    {
        if (Scheme == null) return;
        var path = PickSave("Lighting (*.json)|*.json", Scheme.Name + ".json");
        if (path != null) File.WriteAllText(path, JsonSerializer.Serialize(Scheme, AppState.Json));
    }

    private void Import()
    {
        var path = PickOpen("Lighting (*.json)|*.json");
        if (path == null) return;
        try
        {
            var s = JsonSerializer.Deserialize<PerKeyScheme>(File.ReadAllText(path), AppState.Json) ?? throw new InvalidDataException();
            foreach (var hex in s.Colors.Values) Rgb.Parse(hex);
            _state.Profile.PerKeySchemes.Add(s);
            _state.Save();
            Reload();
            _schemes.SelectedIndex = _schemes.Items.Count - 1;
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or IOException or FormatException or ArgumentException)
        {
            _state.Report(T("That file is not a valid lighting scheme."), true);
        }
    }

    private void Upload()
    {
        if (Scheme == null) return;
        var colors = AppState.ToColors(Scheme);
        var brightness = Scheme.Brightness;
        _ = _state.SendAsync(T("Key colours written to keyboard."), k => k.SetPerKey(colors, brightness));
    }
}

public sealed class RealtimePage : Page
{
    private readonly AppState _state;
    private readonly LiveEngine _engine;
    private readonly KeyboardView _view = new() { Dock = DockStyle.Fill, MultiSelect = true };
    private readonly ListBox _schemes = Make.List(200, 160);
    private readonly ListBox _effects = Make.List(190, 200);
    private readonly ColorPicker _color = new();
    private readonly Slider _speed = new(), _brightness = new();
    private readonly Button _run;
    private IReadOnlyDictionary<int, Rgb>? _still;

    private static readonly (string Name, RealtimeEffect Effect)[] Names =
    {
        ("Static On", RealtimeEffect.StaticOn), ("Starlight", RealtimeEffect.Starlight), ("Fluttering", RealtimeEffect.Fluttering),
        ("Dynamic Breathing", RealtimeEffect.DynamicBreathing), ("Colorful Fountain", RealtimeEffect.ColorfulFountain),
        ("Rainbow Wave", RealtimeEffect.RainbowWave), ("Following Currents", RealtimeEffect.FollowingCurrents),
        ("Peak Revolving", RealtimeEffect.PeakRevolving), ("Turn Off Lighting", RealtimeEffect.Off),
    };

    public RealtimePage(AppState state, LiveEngine engine)
    {
        _state = state;
        _engine = engine;
        _view.KeyColor = k =>
        {
            var frame = _engine.IsRealtime ? _engine.LastFrame : _still;
            return frame != null && frame.TryGetValue(k.Index, out var c) && c != Rgb.Black ? c.ToColor() : null;
        };
        _engine.Frame += () => { if (Visible && _engine.IsRealtime) _view.Invalidate(); };
        _engine.StateChanged += UpdateRun;

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 290, WrapContents = false, Padding = new Padding(16, 8, 16, 8), BackColor = Theme.Panel };
        var list = Make.Column();
        list.Controls.Add(Make.Heading(T("Real-time Lighting")));
        _schemes.SelectedIndexChanged += (_, _) => { _view.SelectNone(); LoadScheme(); };
        list.Controls.Add(_schemes);
        list.Controls.Add(Make.Row(Make.Button(T("New"), New), Make.Button(T("Delete"), Delete)));
        list.Controls.Add(Make.Row(Make.Button(T("Import"), Import), Make.Button(T("Export"), Export)));
        bottom.Controls.Add(list);

        var effect = Make.Column(0, new Padding(24, 0, 0, 0));
        effect.Controls.Add(Make.Label(T("Effect")));
        foreach (var (name, e) in Names) _effects.Items.Add(new Choice<RealtimeEffect>(T(name), e));
        _effects.SelectedIndex = 0;
        effect.Controls.Add(_effects);
        bottom.Controls.Add(effect);

        var paint = Make.Column(0, new Padding(24, 0, 0, 0));
        paint.Controls.Add(Make.Label(T("Lighting Color")));
        paint.Controls.Add(_color);
        paint.Controls.Add(Make.Button(T("Apply to selected keys"), Apply, true));
        paint.Controls.Add(Make.Row(Make.Button(T("Select All"), _view.SelectAll), Make.Button(T("Select None"), _view.SelectNone)));
        paint.Controls.Add(Make.Note(T("Runs only while this app is open, because the PC draws every frame."), 290));
        bottom.Controls.Add(paint);

        var run = Make.Column(0, new Padding(24, 0, 0, 0));
        run.Controls.Add(Make.Label(T("Lighting Speed")));
        _speed.Maximum = 5;
        _speed.ValueChanged += () => { if (Scheme != null) { Scheme.Speed = _speed.Value; _state.Save(); } };
        run.Controls.Add(_speed);
        run.Controls.Add(Make.Label(T("Lighting Brightness")));
        _brightness.Maximum = 5;
        _brightness.ValueChanged += () => { if (Scheme != null) { Scheme.Brightness = _brightness.Value; _state.Save(); } };
        run.Controls.Add(_brightness);
        _run = Make.Button(T("Start"), Toggle, true, 200);
        run.Controls.Add(_run);
        bottom.Controls.Add(run);

        Controls.Add(_view);
        Controls.Add(bottom);
    }

    private RealtimeScheme? Scheme => _schemes.SelectedIndex >= 0 && _schemes.SelectedIndex < _state.Profile.RealtimeSchemes.Count ? _state.Profile.RealtimeSchemes[_schemes.SelectedIndex] : null;

    public override void Reload()
    {
        if (_state.Profile.RealtimeSchemes.Count == 0)
        {
            var first = new RealtimeScheme { Name = T("Scheme 1") };
            foreach (var k in KeyLayout.Keys) first.Keys[k.Index] = new RealtimeKey { Effect = RealtimeEffect.RainbowWave };
            _state.Profile.RealtimeSchemes.Add(first);
        }
        var selected = Math.Max(_schemes.SelectedIndex, 0);
        _schemes.Items.Clear();
        foreach (var s in _state.Profile.RealtimeSchemes) _schemes.Items.Add(s.Name);
        _schemes.SelectedIndex = Math.Min(selected, _schemes.Items.Count - 1);
        LoadScheme();
        UpdateRun();
    }

    private void LoadScheme()
    {
        if (Scheme != null)
        {
            _speed.Value = Scheme.Speed;
            _brightness.Value = Scheme.Brightness;
        }
        RefreshStill();
    }

    /// <summary>Stopped, the board shows one representative frame of the effects that are assigned.</summary>
    private void RefreshStill()
    {
        // Half a second in, the breathing and sparkle effects are visibly lit.
        _still = Scheme != null ? Effects.RenderRealtime(new RealtimeScheme { Keys = Scheme.Keys, Speed = 3, Brightness = 5 }, 0.5) : null;
        _view.Invalidate();
    }

    private void UpdateRun() => _run.Text = _engine.IsRealtime ? T("Stop") : T("Start");

    private void Toggle()
    {
        if (_engine.IsRealtime) { _engine.Stop(); return; }
        if (Scheme == null) return;
        if (_state.Keyboard.Protocol is { SupportsRealtime: false }) { _state.Report(T("Real-time lighting needs the USB cable."), true); return; }
        _engine.StartRealtime(Scheme);
    }

    private void Apply()
    {
        if (Scheme == null) return;
        if (_view.Selected.Count == 0) { _state.Report(T("Select some keys first."), true); return; }
        var effect = _effects.SelectedItem is Choice<RealtimeEffect> c ? c.Value : RealtimeEffect.StaticOn;
        foreach (var i in _view.Selected) Scheme.Keys[i] = new RealtimeKey { Effect = effect, Color = _color.Hex };
        _state.Save();
        RefreshStill();
    }

    private void New()
    {
        var name = Prompt(T("New"), string.Format(T("Scheme {0}"), _state.Profile.RealtimeSchemes.Count + 1));
        if (string.IsNullOrEmpty(name)) return;
        _state.Profile.RealtimeSchemes.Add(new RealtimeScheme { Name = name });
        _state.Save();
        Reload();
        _schemes.SelectedIndex = _schemes.Items.Count - 1;
    }

    private void Delete()
    {
        if (Scheme == null || !Confirm(T("Delete"), string.Format(T("Delete \"{0}\"?"), Scheme.Name))) return;
        if (_engine.IsRealtime) _engine.Stop();
        _state.Profile.RealtimeSchemes.Remove(Scheme);
        _schemes.SelectedIndex = -1;
        _state.Save();
        Reload();
    }

    private void Export()
    {
        if (Scheme == null) return;
        var path = PickSave("Lighting (*.json)|*.json", Scheme.Name + ".json");
        if (path != null) File.WriteAllText(path, JsonSerializer.Serialize(Scheme, AppState.Json));
    }

    private void Import()
    {
        var path = PickOpen("Lighting (*.json)|*.json");
        if (path == null) return;
        try
        {
            var s = JsonSerializer.Deserialize<RealtimeScheme>(File.ReadAllText(path), AppState.Json) ?? throw new InvalidDataException();
            foreach (var k in s.Keys.Values) Rgb.Parse(k.Color);
            _state.Profile.RealtimeSchemes.Add(s);
            _state.Save();
            Reload();
            _schemes.SelectedIndex = _schemes.Items.Count - 1;
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or IOException or FormatException or ArgumentException)
        {
            _state.Report(T("That file is not a valid lighting scheme."), true);
        }
    }
}

public sealed class MusicPage : Page
{
    private readonly AppState _state;
    private readonly LiveEngine _engine;
    private readonly KeyboardView _view = new() { Dock = DockStyle.Fill };
    private readonly ListBox _layers = Make.List(200, 170);
    private readonly ComboBox _rhythm = Make.Combo(), _fore = Make.Combo(), _back = Make.Combo();
    private readonly Slider _amplitude = new(), _backBright = new();
    private readonly Button _run;
    private bool _loading;

    private static IEnumerable<(string, MusicColorMode)> Palettes(bool background)
    {
        foreach (var (name, mode) in new (string, MusicColorMode)[]
        {
            ("Green/Yellow/Red", MusicColorMode.GreenYellowRed), ("Rainbow", MusicColorMode.Rainbow), ("Reverse Rainbow", MusicColorMode.ReverseRainbow),
            ("Gradient", MusicColorMode.Gradient), ("Spectrum Cycle", MusicColorMode.SpectrumCycle), ("White", MusicColorMode.White), ("Red", MusicColorMode.Red),
            ("Orange", MusicColorMode.Orange), ("Yellow", MusicColorMode.Yellow), ("Green", MusicColorMode.Green), ("Cyan", MusicColorMode.Cyan),
            ("Blue", MusicColorMode.Blue), ("Purple", MusicColorMode.Purple), ("Ambilight", MusicColorMode.Ambilight),
        }) yield return (T(name), mode);
        if (background) yield return (T("Off"), MusicColorMode.Off);
    }

    public MusicPage(AppState state, LiveEngine engine)
    {
        _state = state;
        _engine = engine;
        _view.KeyColor = k => _engine.IsMusic && _engine.LastFrame != null && _engine.LastFrame.TryGetValue(k.Index, out var c) ? c.ToColor() : null;
        _view.KeyText = _ => "";
        _engine.Frame += () => { if (Visible && _engine.IsMusic) _view.Invalidate(); };
        _engine.StateChanged += () => { _run!.Text = _engine.IsMusic ? T("Stop") : T("Start"); _view.Invalidate(); };

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 290, WrapContents = false, Padding = new Padding(16, 8, 16, 8), BackColor = Theme.Panel };
        var list = Make.Column();
        list.Controls.Add(Make.Heading(T("Music Rhythm")));
        _layers.SelectedIndexChanged += (_, _) => LoadLayer();
        list.Controls.Add(_layers);
        list.Controls.Add(Make.Row(Make.Button(T("New"), New), Make.Button(T("Rename"), Rename), Make.Button(T("Delete"), Delete)));
        bottom.Controls.Add(list);

        var a = Make.Column(0, new Padding(28, 0, 0, 0));
        a.Controls.Add(Make.Label(T("Rhythm")));
        _rhythm.Fill(Effects.Rhythms.Select((n, i) => (T(n), i)));
        _rhythm.SelectedIndexChanged += (_, _) => Store();
        a.Controls.Add(_rhythm);
        a.Controls.Add(Make.Label(T("Color")));
        _fore.Fill(Palettes(false));
        _fore.SelectedIndexChanged += (_, _) => Store();
        a.Controls.Add(_fore);
        a.Controls.Add(Make.Label(T("Amplitude")));
        _amplitude.Maximum = 10;
        _amplitude.ValueChanged += Store;
        a.Controls.Add(_amplitude);
        bottom.Controls.Add(a);

        var b = Make.Column(0, new Padding(28, 0, 0, 0));
        b.Controls.Add(Make.Label(T("Background Mode")));
        _back.Fill(Palettes(true));
        _back.SelectedIndexChanged += (_, _) => Store();
        b.Controls.Add(_back);
        b.Controls.Add(Make.Label(T("Background Brightness")));
        _backBright.Maximum = 5;
        _backBright.ValueChanged += Store;
        b.Controls.Add(_backBright);
        _run = Make.Button(T("Start"), Toggle, true, 220);
        b.Controls.Add(_run);
        b.Controls.Add(Make.Note(T("Listens to what this PC is playing. Nothing is recorded or stored."), 230));
        bottom.Controls.Add(b);

        Controls.Add(_view);
        Controls.Add(bottom);
    }

    private MusicLayer? Layer => _layers.SelectedIndex >= 0 && _layers.SelectedIndex < _state.Profile.MusicLayers.Count ? _state.Profile.MusicLayers[_layers.SelectedIndex] : null;

    public override void Reload()
    {
        if (_state.Profile.MusicLayers.Count == 0) _state.Profile.MusicLayers.Add(new MusicLayer { Name = T("New Music Effect") });
        var selected = Math.Max(_layers.SelectedIndex, 0);
        _layers.Items.Clear();
        foreach (var l in _state.Profile.MusicLayers) _layers.Items.Add(l.Name);
        _layers.SelectedIndex = Math.Min(selected, _layers.Items.Count - 1);
    }

    private void LoadLayer()
    {
        if (Layer is not { } l) return;
        _loading = true;
        _rhythm.Select(l.Rhythm);
        _fore.Select(l.Foreground);
        _amplitude.Value = l.Amplitude;
        _back.Select(l.Background);
        _backBright.Value = l.BackgroundBrightness;
        _loading = false;
    }

    private void Store()
    {
        if (_loading || Layer is not { } l) return;
        l.Rhythm = _rhythm.Value<int>();
        l.Foreground = _fore.Value<MusicColorMode>();
        l.Amplitude = _amplitude.Value;
        l.Background = _back.Value<MusicColorMode>();
        l.BackgroundBrightness = _backBright.Value;
        _state.Save();
    }

    private void Toggle()
    {
        if (_engine.IsMusic) { _engine.Stop(); return; }
        if (Layer != null) _engine.StartMusic(Layer);
    }

    private void New()
    {
        var name = Prompt(T("New"), T("New Music Effect"));
        if (string.IsNullOrEmpty(name)) return;
        _state.Profile.MusicLayers.Add(new MusicLayer { Name = name });
        _state.Save();
        Reload();
        _layers.SelectedIndex = _layers.Items.Count - 1;
    }

    private void Rename()
    {
        if (Layer == null) return;
        var name = Prompt(T("Rename"), Layer.Name);
        if (string.IsNullOrEmpty(name)) return;
        Layer.Name = name;
        _state.Save();
        Reload();
    }

    private void Delete()
    {
        if (Layer == null || !Confirm(T("Delete"), string.Format(T("Delete \"{0}\"?"), Layer.Name))) return;
        if (_engine.IsMusic) _engine.Stop();
        _state.Profile.MusicLayers.Remove(Layer);
        _layers.SelectedIndex = -1;
        _state.Save();
        Reload();
    }
}
