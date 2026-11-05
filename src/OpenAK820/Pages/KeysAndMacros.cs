using System.Text.Json;
using OpenAK820.Model;
using OpenAK820.Protocol;
using OpenAK820.Services;
using OpenAK820.Ui;
using static OpenAK820.Ui.Loc;

namespace OpenAK820.Pages;

public sealed class KeysPage : Page
{
    private readonly AppState _state;
    private readonly KeyboardView _view = new() { Dock = DockStyle.Fill };
    private readonly RadioButton _top, _fn;
    private readonly ComboBox _kind = Make.Combo(240);
    private readonly FlowLayoutPanel _editor = Make.Column();
    private readonly Label _keyName = Make.Heading("");
    private Func<KeyAction>? _build;
    private int _layer;

    private static readonly (string Name, KeyActionKind Kind)[] Kinds =
    {
        ("Default Key", KeyActionKind.Default), ("Disable Key", KeyActionKind.Disabled), ("Custom Function", KeyActionKind.KeyCombo),
        ("Multi-Key", KeyActionKind.MultiKey), ("Macro Definition", KeyActionKind.Macro), ("Mouse Function", KeyActionKind.Mouse),
        ("Multimedia", KeyActionKind.Media), ("System and Web Keys", KeyActionKind.Consumer), ("Windows Shortcut", KeyActionKind.WindowsShortcut),
        ("Open Program", KeyActionKind.OpenProgram), ("Open Website", KeyActionKind.OpenWebsite), ("Send Text", KeyActionKind.SendText),
        ("Switch Profile", KeyActionKind.SwitchProfile),
    };

    public KeysPage(AppState state)
    {
        _state = state;
        // Fn stays locked on both layers: remapping it could leave no way to reach the Fn layer.
        _view.KeyEnabled = k => k.Name != "Fn" && (_layer == 0 || !k.FnLayerLocked);
        _view.KeyColor = k => Current(k.Index) != null ? Color.FromArgb(0x1D, 0x4E, 0x89) : null;
        _view.KeyText = k => Current(k.Index) is { } a ? Summary(a) : KeyboardView.Label(k);
        _view.SelectionChanged += LoadKey;

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 290, WrapContents = false, Padding = new Padding(16, 8, 16, 8), BackColor = Theme.Panel };

        var layer = Make.Column();
        layer.Controls.Add(Make.Heading(T("Key Layer")));
        _top = Make.Radio(T("Top Layer"), true, () => SetLayer(0));
        _fn = Make.Radio(T("Fn Layer"), false, () => SetLayer(1));
        layer.Controls.Add(_top);
        layer.Controls.Add(_fn);
        layer.Controls.Add(Make.Note(T("Assignments are stored on the keyboard once written, except for programs, websites and text, which need this app running."), 220));
        layer.Controls.Add(Make.Button(T("Write to keyboard"), Upload, true, 200));
        layer.Controls.Add(Make.Button(T("Reset this layer"), ResetLayer, false, 200));
        bottom.Controls.Add(layer);

        var edit = Make.Column(0, new Padding(28, 0, 0, 0));
        edit.Controls.Add(_keyName);
        edit.Controls.Add(Make.Label(T("Key Function")));
        _kind.Fill(Kinds.Select(k => (T(k.Name), k.Kind)));
        _kind.SelectedIndexChanged += (_, _) => BuildEditor(null);
        edit.Controls.Add(_kind);
        edit.Controls.Add(_editor);
        bottom.Controls.Add(edit);

        var apply = Make.Column(0, new Padding(24, 26, 0, 0));
        apply.Controls.Add(Make.Button(T("Apply"), ApplyToKey, true, 120));
        apply.Controls.Add(Make.Button(T("Import"), Import, false, 120));
        apply.Controls.Add(Make.Button(T("Export"), Export, false, 120));
        bottom.Controls.Add(apply);

        Controls.Add(_view);
        Controls.Add(bottom);
    }

    private KeyAction? Current(int index) => _state.Profile.Layers[_layer].TryGetValue(index, out var a) && a.Kind != KeyActionKind.Default ? a : null;

    private static string Summary(KeyAction a) => a.Kind switch
    {
        KeyActionKind.Disabled => "-",
        KeyActionKind.KeyCombo => (a.Mods != KeyMods.None ? "+" : "") + HidKeys.Name(a.Key),
        KeyActionKind.MultiKey => "x3",
        KeyActionKind.Macro => "M" + (a.MacroIndex + 1),
        KeyActionKind.Mouse => "Ms",
        KeyActionKind.Media or KeyActionKind.Consumer => "Fx",
        KeyActionKind.WindowsShortcut => "Win",
        KeyActionKind.OpenProgram => "App",
        KeyActionKind.OpenWebsite => "Web",
        KeyActionKind.SendText => "Txt",
        KeyActionKind.SwitchProfile => "P",
        _ => "",
    };

    public override void Reload()
    {
        _view.Invalidate();
        LoadKey();
    }

    private void SetLayer(int layer)
    {
        _layer = layer;
        _view.SelectNone();
        _view.Invalidate();
    }

    private void LoadKey()
    {
        var index = _view.Selected.Count == 1 ? _view.Selected.First() : -1;
        var key = KeyLayout.ByIndex(index);
        _keyName.Text = key != null ? string.Format(T("Key: {0}"), KeyboardView.Label(key)) : T("Click a key to change it.");
        var action = index >= 0 && _state.Profile.Layers[_layer].TryGetValue(index, out var a) ? a : new KeyAction();
        _kind.Select(action.Kind);
        BuildEditor(action);
    }

    private static IEnumerable<(string, byte)> KeyChoices() => HidKeys.All.Select(k => (k.Name, k.Code));

    private void BuildEditor(KeyAction? existing)
    {
        var kind = _kind.Value<KeyActionKind>();
        existing ??= new KeyAction { Kind = kind };
        if (existing.Kind != kind) existing = new KeyAction { Kind = kind };
        _editor.Controls.Clear();
        _build = () => new KeyAction { Kind = kind };

        switch (kind)
        {
            case KeyActionKind.KeyCombo:
            {
                var (ctrl, shift, alt, win) = (Make.Check("Ctrl", existing.Mods.HasFlag(KeyMods.LCtrl)), Make.Check("Shift", existing.Mods.HasFlag(KeyMods.LShift)),
                    Make.Check("Alt", existing.Mods.HasFlag(KeyMods.LAlt)), Make.Check("Win", existing.Mods.HasFlag(KeyMods.LWin)));
                var key = Make.Combo();
                key.Fill(KeyChoices(), existing.Key);
                _editor.Controls.Add(Make.Row(ctrl, shift, alt, win));
                _editor.Controls.Add(key);
                _build = () => new KeyAction
                {
                    Kind = kind, Key = key.Value<byte>(),
                    Mods = (ctrl.Checked ? KeyMods.LCtrl : 0) | (shift.Checked ? KeyMods.LShift : 0) | (alt.Checked ? KeyMods.LAlt : 0) | (win.Checked ? KeyMods.LWin : 0),
                };
                break;
            }
            case KeyActionKind.MultiKey:
            {
                ComboBox Pick(byte value)
                {
                    var c = Make.Combo(160);
                    c.Fill(new[] { (T("None"), (byte)0) }.Concat(KeyChoices()), value);
                    return c;
                }
                var (a, b, c) = (Pick(existing.Key), Pick(existing.Key2), Pick(existing.Key3));
                _editor.Controls.Add(a);
                _editor.Controls.Add(b);
                _editor.Controls.Add(c);
                _build = () => new KeyAction { Kind = kind, Key = a.Value<byte>(), Key2 = b.Value<byte>(), Key3 = c.Value<byte>() };
                break;
            }
            case KeyActionKind.Macro:
            {
                var macro = Make.Combo();
                macro.Fill(_state.Saved.Macros.Select((m, i) => (m.Name, i)), existing.MacroIndex);
                var mode = Make.Combo();
                mode.Fill(new[]
                {
                    (T("Play Once"), MacroPlayMode.PlayOnce), (T("Play Multiple Times"), MacroPlayMode.PlayMultiple),
                    (T("Stop when key is pressed again"), MacroPlayMode.StopOnNextPress), (T("Release to stop"), MacroPlayMode.ReleaseToStop),
                    (T("Click to play"), MacroPlayMode.ClickToPlay),
                }, existing.PlayMode);
                var loops = Make.Number(1, 255, existing.Loops);
                _editor.Controls.Add(macro);
                _editor.Controls.Add(mode);
                _editor.Controls.Add(Make.Row(Make.Label(T("Loop Count (1 ~ 255)") + "  "), loops));
                if (_state.Saved.Macros.Count == 0) _editor.Controls.Add(Make.Note(T("No macros yet. Create one on the Macro page."), 240));
                _build = () => new KeyAction { Kind = kind, MacroIndex = macro.Value<int>(), PlayMode = mode.Value<MacroPlayMode>(), Loops = (byte)loops.Value };
                break;
            }
            case KeyActionKind.Mouse:
                ValuePicker(kind, existing, new (string, int)[]
                {
                    (T("Left Click"), 1), (T("Middle Click"), 2), (T("Right Click"), 3), (T("Double Click"), 4),
                    (T("Scroll Up"), 5), (T("Scroll Down"), 6), (T("Mouse Button 4"), 7), (T("Mouse Button 5"), 8),
                });
                break;
            case KeyActionKind.Media:
                ValuePicker(kind, existing, new (string, int)[]
                {
                    (T("Play/Pause"), 1), (T("Stop"), 2), (T("Previous Track"), 3), (T("Next Track"), 4),
                    (T("Volume +"), 5), (T("Volume -"), 6), (T("Mute"), 7),
                });
                break;
            case KeyActionKind.Consumer:
                ValuePicker(kind, existing, new (string, int)[]
                {
                    (T("My Computer"), 500), (T("Calculator"), 501), (T("Mail"), 502), (T("Multimedia"), 503), (T("Homepage"), 511),
                    (T("Web Page - Refresh"), 512), (T("Web Page - Stop"), 513), (T("Web Page - Back"), 514),
                    (T("Web Page - Forward"), 515), (T("Web Page - Favorites"), 516), (T("Web Page - Search"), 517),
                });
                break;
            case KeyActionKind.WindowsShortcut:
                ValuePicker(kind, existing, new (string, int)[]
                {
                    (T("Show Desktop"), 1), (T("My Computer"), 2), (T("Lock Computer"), 3), (T("Close Window"), 4),
                    (T("Switch Windows"), 5), (T("Copy"), 6), (T("Paste"), 7), (T("Cut"), 8),
                });
                break;
            case KeyActionKind.OpenProgram:
            {
                var path = Make.Text(240);
                path.Text = existing.Text;
                _editor.Controls.Add(path);
                _editor.Controls.Add(Make.Button(T("Browse..."), () =>
                {
                    var file = PickOpen("All files (*.*)|*.*");
                    if (file != null) path.Text = file;
                }));
                _build = () => new KeyAction { Kind = kind, Text = path.Text };
                break;
            }
            case KeyActionKind.OpenWebsite:
            {
                var url = Make.Text(240);
                url.Text = existing.Text;
                _editor.Controls.Add(url);
                _build = () => new KeyAction { Kind = kind, Text = url.Text };
                break;
            }
            case KeyActionKind.SendText:
            {
                var text = Make.Text(240, true);
                text.Text = existing.Text;
                _editor.Controls.Add(text);
                _build = () => new KeyAction { Kind = kind, Text = text.Text };
                break;
            }
            case KeyActionKind.SwitchProfile:
            {
                var profile = Make.Combo();
                profile.Fill(_state.Saved.Profiles.Select((p, i) => (p.Name, i)), existing.Value);
                _editor.Controls.Add(profile);
                _build = () => new KeyAction { Kind = kind, Value = profile.Value<int>() };
                break;
            }
        }
    }

    private void ValuePicker(KeyActionKind kind, KeyAction existing, IEnumerable<(string, int)> items)
    {
        var box = Make.Combo();
        box.Fill(items, existing.Value);
        _editor.Controls.Add(box);
        _build = () => new KeyAction { Kind = kind, Value = box.Value<int>() };
    }

    private void ApplyToKey()
    {
        if (_view.Selected.Count != 1 || _build == null) { _state.Report(T("Click a key to change it."), true); return; }
        var index = _view.Selected.First();
        var action = _build();
        var layer = _state.Profile.Layers[_layer];
        if (action.Kind == KeyActionKind.Default) layer.Remove(index);
        else
        {
            if (action.IsHostSide)
            {
                layer.Remove(index); // Release this key's old ID before asking for one.
                action.HostId = _state.Profile.AllocateHostId();
                if (action.HostId == 0) { _state.Report(T("Too many program, website and text keys."), true); return; }
            }
            layer[index] = action;
        }
        _state.Save();
        _view.Invalidate();
        _state.Report(T("Saved. Use Write to keyboard when you're done editing."));
    }

    private void Upload()
    {
        var layer = _layer;
        var table = Encoders.KeyTable(_state.Profile.Layers[layer], layer);
        _ = _state.SendAsync(T("Key assignments written to keyboard."), k => k.SetKeyTable(layer, table));
    }

    private void ResetLayer()
    {
        if (!Confirm(T("Reset this layer"), T("Return every key on this layer to its default?"))) return;
        _state.Profile.Layers[_layer].Clear();
        _state.Save();
        _view.Invalidate();
        LoadKey();
    }

    private void Export()
    {
        if (!ConfirmExportContents(_state.Profile.Layers.SelectMany(l => l.Values))) return;
        var path = PickSave("Custom Key (*.json)|*.json", "keys.json");
        if (path != null) File.WriteAllText(path, JsonSerializer.Serialize(_state.Profile.Layers, AppState.Json));
    }

    private void Import()
    {
        var path = PickOpen("Custom Key (*.json)|*.json");
        if (path == null) return;
        try
        {
            var layers = JsonSerializer.Deserialize<Dictionary<int, KeyAction>[]>(File.ReadAllText(path), AppState.Json);
            if (layers is not { Length: 2 }) throw new InvalidDataException();
            if (!ConfirmHostActions(layers.SelectMany(l => l.Values))) return;
            _state.Profile.Layers = layers;
            _state.Save();
            Reload();
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or IOException)
        {
            _state.Report(T("That file is not a valid key layout."), true);
        }
    }
}

public sealed class MacroPage : Page
{
    private readonly AppState _state;
    private readonly ListBox _macros = Make.List(190, 420);
    private readonly ListView _events = new()
    {
        View = View.Details, FullRowSelect = true, HideSelection = false, GridLines = false, BackColor = Theme.Card, ForeColor = Theme.Text,
        BorderStyle = BorderStyle.None, Font = Theme.Body, Width = 390, Height = 470, HeaderStyle = ColumnHeaderStyle.Nonclickable, Margin = new Padding(0, 4, 0, 4),
    };
    private readonly Label _capacity = Make.Label("", Theme.Small, Theme.Muted);
    private readonly Button _record;
    private readonly RadioButton _realTiming, _noTiming, _fixedTiming;
    private readonly NumericUpDown _fixedDelay = Make.Number(10, 65535, 50);
    private readonly ComboBox _insertKey = Make.Combo(150), _insertMouse = Make.Combo(150);
    private InputRecorder? _recorder;

    public MacroPage(AppState state)
    {
        _state = state;
        var split = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(16), BackColor = Theme.Back, AutoScroll = true };

        var left = Make.Column();
        left.Controls.Add(Make.Heading(T("Macro Manager")));
        _macros.SelectedIndexChanged += (_, _) => LoadEvents();
        left.Controls.Add(_macros);
        left.Controls.Add(Make.Row(Make.Button(T("New"), New), Make.Button(T("Rename"), Rename), Make.Button(T("Delete"), Delete)));
        left.Controls.Add(Make.Row(Make.Button(T("Import"), Import), Make.Button(T("Export"), Export)));
        split.Controls.Add(left);

        var middle = Make.Column(0, new Padding(20, 0, 0, 0));
        middle.Controls.Add(Make.Heading(T("Action Record List")));
        _events.Columns.Add(T("Key"), 170);
        _events.Columns.Add(T("Action"), 110);
        _events.Columns.Add(T("Delay(ms)"), 90);
        _events.DoubleClick += (_, _) => EditDelay();
        middle.Controls.Add(_events);
        middle.Controls.Add(_capacity);
        split.Controls.Add(middle);

        var right = Make.Column(0, new Padding(20, 0, 0, 0));
        right.Controls.Add(Make.Heading(T("Record Input")));
        _realTiming = Make.Radio(T("Record Input Interval Time"), true);
        _noTiming = Make.Radio(T("Do Not Record Input Interval Time"), false);
        _fixedTiming = Make.Radio(T("Default Interval"), false);
        right.Controls.Add(_realTiming);
        right.Controls.Add(_noTiming);
        right.Controls.Add(Make.Row(_fixedTiming, _fixedDelay, Make.Label(T("ms"))));
        _record = Make.Button(T("Start Recording"), ToggleRecord, true, 240);
        right.Controls.Add(_record);
        right.Controls.Add(Make.Note(T("Recording captures everything you type, including passwords. Exported macros contain those keys."), 240));

        right.Controls.Add(Make.Heading(T("Edit")));
        _insertKey.Fill(HidKeys.All.Select(k => (k.Name, k.Code)));
        _insertMouse.Fill(new[] { (T("Left Mouse Button"), MacroMouseButton.Left), (T("Middle Mouse Button"), MacroMouseButton.Middle), (T("Right Mouse Button"), MacroMouseButton.Right) });
        right.Controls.Add(Make.Row(_insertKey, Make.Button(T("Insert"), () => InsertPair(MacroEventKind.KeyDown, MacroEventKind.KeyUp, _insertKey.Value<byte>()))));
        right.Controls.Add(Make.Row(_insertMouse, Make.Button(T("Insert"), () => InsertPair(MacroEventKind.MouseDown, MacroEventKind.MouseUp, (int)_insertMouse.Value<MacroMouseButton>()))));
        right.Controls.Add(Make.Row(Make.Button(T("Move Up"), () => MoveEvent(-1)), Make.Button(T("Move Down"), () => MoveEvent(1))));
        right.Controls.Add(Make.Row(Make.Button(T("Modify Delay Time"), EditDelay), Make.Button(T("Delete"), DeleteEvents)));
        right.Controls.Add(Make.Button(T("Write to keyboard"), Upload, true, 240));
        right.Controls.Add(Make.Note(T("All macros are stored together on the keyboard, so they run without this app."), 240));
        split.Controls.Add(right);

        Controls.Add(split);
    }

    private Macro? Macro => _macros.SelectedIndex >= 0 && _macros.SelectedIndex < _state.Saved.Macros.Count ? _state.Saved.Macros[_macros.SelectedIndex] : null;

    public override void Reload()
    {
        var selected = Math.Max(_macros.SelectedIndex, 0);
        _macros.Items.Clear();
        foreach (var m in _state.Saved.Macros) _macros.Items.Add(m.Name);
        if (_macros.Items.Count > 0) _macros.SelectedIndex = Math.Min(selected, _macros.Items.Count - 1);
        LoadEvents();
    }

    private static string Describe(MacroEvent e) => e.Kind is MacroEventKind.KeyDown or MacroEventKind.KeyUp
        ? HidKeys.Name(e.Code)
        : (MacroMouseButton)e.Code switch { MacroMouseButton.Left => T("Left Mouse Button"), MacroMouseButton.Middle => T("Middle Mouse Button"), _ => T("Right Mouse Button") };

    private void LoadEvents()
    {
        _events.BeginUpdate();
        _events.Items.Clear();
        if (Macro != null)
            foreach (var e in Macro.Events)
                _events.Items.Add(new ListViewItem(new[] { Describe(e), T(e.Kind is MacroEventKind.KeyDown or MacroEventKind.MouseDown ? "Press" : "Release"), e.DelayMs.ToString() }));
        _events.EndUpdate();
        UpdateCapacity();
    }

    private void UpdateCapacity()
    {
        var used = Encoders.MacroTableSize + _state.Saved.Macros.Sum(Encoders.MacroBytes);
        _capacity.Text = string.Format(T("Macro memory: {0} of {1} bytes used"), used, Encoders.MacroImageSize);
        _capacity.ForeColor = used > Encoders.MacroImageSize ? Theme.Bad : used > Encoders.MacroImageSize * 0.9 ? Theme.Warn : Theme.Muted;
    }

    private void New()
    {
        if (_state.Saved.Macros.Count >= Encoders.MaxMacros) { _state.Report(T("The keyboard cannot hold any more macros."), true); return; }
        var name = Prompt(T("New Macro"), string.Format(T("Macro {0}"), _state.Saved.Macros.Count + 1));
        if (string.IsNullOrEmpty(name)) return;
        _state.Saved.Macros.Add(new Macro { Name = name });
        _state.Save();
        Reload();
        _macros.SelectedIndex = _macros.Items.Count - 1;
    }

    private void Rename()
    {
        if (Macro == null) return;
        var name = Prompt(T("Rename"), Macro.Name);
        if (string.IsNullOrEmpty(name)) { if (name != null) _state.Report(T("Renaming failed. Name cannot be empty."), true); return; }
        Macro.Name = name;
        _state.Save();
        Reload();
    }

    private void Delete()
    {
        if (Macro == null || !Confirm(T("Delete"), string.Format(T("Delete \"{0}\"?"), Macro.Name))) return;
        var index = _macros.SelectedIndex;
        _state.Saved.Macros.RemoveAt(index);
        // Keys point at macros by position, so later ones move down by one.
        foreach (var layer in _state.Saved.Profiles.SelectMany(p => p.Layers))
            foreach (var action in layer.Values.Where(a => a.Kind == KeyActionKind.Macro).ToList())
            {
                if (action.MacroIndex == index) { action.Kind = KeyActionKind.Default; }
                else if (action.MacroIndex > index) action.MacroIndex--;
            }
        _state.Save();
        Reload();
    }

    private void Export()
    {
        if (Macro == null) return;
        var path = PickSave("Macro (*.json)|*.json", Macro.Name + ".json");
        if (path != null) File.WriteAllText(path, JsonSerializer.Serialize(Macro, AppState.Json));
    }

    private void Import()
    {
        var path = PickOpen("Macro (*.json)|*.json");
        if (path == null) return;
        try
        {
            var m = JsonSerializer.Deserialize<Macro>(File.ReadAllText(path), AppState.Json) ?? throw new InvalidDataException();
            _state.Saved.Macros.Add(m);
            _state.Save();
            Reload();
            _macros.SelectedIndex = _macros.Items.Count - 1;
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or IOException)
        {
            _state.Report(T("That file is not a valid macro."), true);
        }
    }

    private void ToggleRecord()
    {
        if (_recorder != null) { StopRecording(); return; }
        if (Macro == null) { _state.Report(T("Please click the \"New\" button to start recording keyboard and mouse input actions."), true); return; }
        _recorder = new InputRecorder(p =>
        {
            var form = FindForm();
            return form != null && form.Bounds.Contains(p);
        });
        _recorder.FixedDelay = _noTiming.Checked ? 10 : _fixedTiming.Checked ? (int)_fixedDelay.Value : null;
        _recorder.Changed += () => BeginInvoke(() =>
        {
            if (_recorder == null) return;
            var e = _recorder.Events[^1];
            _events.Items.Add(new ListViewItem(new[] { Describe(e), T(e.Kind is MacroEventKind.KeyDown or MacroEventKind.MouseDown ? "Press" : "Release"), e.DelayMs.ToString() })).EnsureVisible();
        });
        _events.Items.Clear();
        _record.Text = T("Stop Recording");
        _record.BackColor = Theme.Bad;
        _recorder.Start();
    }

    private void StopRecording()
    {
        var recorder = _recorder!;
        recorder.Stop();
        _recorder = null;
        _record.Text = T("Start Recording");
        _record.BackColor = Color.FromArgb(0x2F, 0x7F, 0xE0);
        if (Macro != null)
        {
            Macro.Events = recorder.Events.ToList();
            _state.Save();
        }
        recorder.Dispose();
        LoadEvents();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible && _recorder != null) StopRecording();
    }

    private int InsertAt => _events.SelectedIndices.Count > 0 ? _events.SelectedIndices[^1] + 1 : Macro?.Events.Count ?? 0;

    private void InsertPair(MacroEventKind down, MacroEventKind up, int code)
    {
        if (Macro == null) return;
        var at = InsertAt;
        Macro.Events.Insert(at, new MacroEvent { Kind = down, Code = code, DelayMs = 50 });
        Macro.Events.Insert(at + 1, new MacroEvent { Kind = up, Code = code, DelayMs = 50 });
        _state.Save();
        LoadEvents();
    }

    private void MoveEvent(int by)
    {
        if (Macro == null || _events.SelectedIndices.Count != 1) return;
        var i = _events.SelectedIndices[0];
        var to = i + by;
        if (to < 0 || to >= Macro.Events.Count) return;
        (Macro.Events[i], Macro.Events[to]) = (Macro.Events[to], Macro.Events[i]);
        _state.Save();
        LoadEvents();
        _events.Items[to].Selected = true;
    }

    private void EditDelay()
    {
        if (Macro == null || _events.SelectedIndices.Count == 0) { _state.Report(T("Please select a macro list item"), true); return; }
        var text = Prompt(T("Modify Delay Time"), Macro.Events[_events.SelectedIndices[0]].DelayMs.ToString());
        if (text == null || !int.TryParse(text, out var delay)) return;
        foreach (int i in _events.SelectedIndices) Macro.Events[i].DelayMs = Math.Clamp(delay, 10, 65535);
        _state.Save();
        LoadEvents();
    }

    private void DeleteEvents()
    {
        if (Macro == null) return;
        foreach (var i in _events.SelectedIndices.Cast<int>().OrderDescending()) Macro.Events.RemoveAt(i);
        _state.Save();
        LoadEvents();
    }

    private void Upload()
    {
        var macros = _state.Saved.Macros.ToList();
        var used = Encoders.MacroTableSize + macros.Sum(Encoders.MacroBytes);
        if (used > Encoders.MacroImageSize) { _state.Report(T("Macro recording space has reached the maximum limit."), true); return; }
        _ = _state.SendAsync(T("Macros written to keyboard."), k => k.SetMacros(macros));
    }
}
