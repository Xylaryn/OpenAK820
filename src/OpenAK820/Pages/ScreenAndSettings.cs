using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using OpenAK820.Model;
using OpenAK820.Protocol;
using OpenAK820.Services;
using OpenAK820.Ui;
using static OpenAK820.Ui.Loc;

namespace OpenAK820.Pages;

/// <summary>One screen frame being edited. <see cref="Original"/> is what the eraser and Clear Edit restore.</summary>
public sealed class EditFrame : IDisposable
{
    public Bitmap Image;
    public Bitmap Original;
    public int DelayMs;
    public readonly Stack<Bitmap> Undo = new();

    public EditFrame(Bitmap image, int delayMs)
    {
        Image = image;
        Original = new Bitmap(image);
        DelayMs = delayMs;
    }

    public void PushUndo()
    {
        Undo.Push(new Bitmap(Image));
        if (Undo.Count > 20)
        {
            var keep = Undo.Take(20).Reverse().ToList();
            foreach (var extra in Undo.Skip(20)) extra.Dispose();
            Undo.Clear();
            foreach (var b in keep) Undo.Push(b);
        }
    }

    public void Dispose()
    {
        Image.Dispose();
        Original.Dispose();
        foreach (var b in Undo) b.Dispose();
    }
}

public enum ScreenTool { Brush, Eraser, Text }

/// <summary>Shows a frame at 3x with crisp pixels, and handles drawing on it.</summary>
[DesignerCategory("Code")]
public sealed class FrameCanvas : Control
{
    public const int Zoom = 3;
    private bool _drawing;
    private Point _last;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public EditFrame? Frame { get; set; }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public ScreenTool Tool { get; set; }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public Color Color { get; set; } = Color.White;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int PenSize { get; set; } = 2;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool Grid { get; set; }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public Func<(string Text, int Size)>? TextToPlace { get; set; }
    public event Action? Edited;

    public FrameCanvas()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Width = Height = GifCodec.Size * Zoom;
        Margin = new Padding(0, 4, 0, 4);
        BackColor = Color.Black;
        Cursor = Cursors.Cross;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.Black);
        if (Frame == null) return;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(Frame.Image, 0, 0, Width, Height);
        if (!Grid) return;
        using var pen = new Pen(Color.FromArgb(50, Color.White));
        g.PixelOffsetMode = PixelOffsetMode.None;
        for (int i = 8; i < GifCodec.Size; i += 8)
        {
            g.DrawLine(pen, i * Zoom, 0, i * Zoom, Height);
            g.DrawLine(pen, 0, i * Zoom, Width, i * Zoom);
        }
    }

    private static Point ToImage(Point p) => new(p.X / Zoom, p.Y / Zoom);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (Frame == null || e.Button != MouseButtons.Left) return;
        Frame.PushUndo();
        var at = ToImage(e.Location);
        if (Tool == ScreenTool.Text)
        {
            var (text, size) = TextToPlace?.Invoke() ?? ("", 12);
            if (string.IsNullOrEmpty(text)) { Frame.Undo.Pop().Dispose(); return; }
            using var g = Graphics.FromImage(Frame.Image);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            using var font = new Font("Segoe UI", size, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(Color);
            g.DrawString(text, font, brush, at);
            Invalidate();
            Edited?.Invoke();
            return;
        }
        _drawing = true;
        _last = at;
        Stroke(at);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_drawing) Stroke(ToImage(e.Location));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!_drawing) return;
        _drawing = false;
        Edited?.Invoke();
    }

    private void Stroke(Point to)
    {
        if (Frame == null) return;
        using var g = Graphics.FromImage(Frame.Image);
        if (Tool == ScreenTool.Eraser)
        {
            // Erasing brings back the imported picture rather than painting black.
            using var brush = new TextureBrush(Frame.Original);
            using var pen = new Pen(brush, PenSize * 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, _last, to);
            g.FillEllipse(brush, to.X - PenSize, to.Y - PenSize, PenSize * 2, PenSize * 2);
        }
        else
        {
            using var pen = new Pen(Color, PenSize) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, _last, to);
            if (_last == to) g.FillRectangle(pen.Brush, to.X, to.Y, Math.Max(1, PenSize), Math.Max(1, PenSize));
        }
        _last = to;
        Invalidate();
    }
}

public sealed class ScreenPage : Page
{
    private readonly AppState _state;
    private readonly List<EditFrame> _frames = new();
    private readonly ListBox _list = Make.List(150, 400);
    private readonly FrameCanvas _canvas = new();
    private readonly NumericUpDown _delay = Make.Number(30, 500, 100);
    private readonly ColorPicker _color = new();
    private readonly TextBox _text = Make.Text(240);
    private readonly NumericUpDown _fontSize = Make.Number(8, 64, 16);
    private readonly ProgressBar _progress = new() { Width = 240, Height = 8, Margin = new Padding(0, 6, 0, 2), Visible = false };
    private readonly Label _count = Make.Label("", Theme.Small, Theme.Muted);
    private readonly Button _play;
    private readonly System.Windows.Forms.Timer _preview = new();
    private int _previewAt;
    private bool _loaded, _loading;

    public ScreenPage(AppState state)
    {
        _state = state;
        var split = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(16), BackColor = Theme.Back, AutoScroll = true };

        var left = Make.Column();
        left.Controls.Add(Make.Heading(T("Frames")));
        _list.SelectedIndexChanged += (_, _) => ShowFrame();
        left.Controls.Add(_list);
        left.Controls.Add(_count);
        left.Controls.Add(Make.Button(T("Import GIF or image"), Import, true, 150));
        left.Controls.Add(Make.Button(T("Create a new frame"), AddBlank, false, 150));
        left.Controls.Add(Make.Button(T("Copy frame"), CopyFrame, false, 150));
        left.Controls.Add(Make.Button(T("Delete frame"), DeleteFrame, false, 150));
        split.Controls.Add(left);

        var middle = Make.Column(0, new Padding(20, 0, 0, 0));
        middle.Controls.Add(Make.Heading(T("Editing")));
        middle.Controls.Add(_canvas);
        _canvas.Edited += SaveFrames;
        _canvas.TextToPlace = () => (_text.Text, (int)_fontSize.Value);
        _play = Make.Button(T("Preview"), TogglePreview, false, 110);
        middle.Controls.Add(Make.Row(_play,
            Make.Button(T("Undo modification"), UndoEdit),
            Make.Button(T("Clear edit"), ClearEdit),
            Make.Check(T("Grid"), false, v => { _canvas.Grid = v; _canvas.Invalidate(); })));
        split.Controls.Add(middle);

        var right = Make.Column(0, new Padding(20, 0, 0, 0));
        right.Controls.Add(Make.Heading(T("Tools")));
        right.Controls.Add(Make.Row(
            Make.Radio(T("Brush"), true, () => _canvas.Tool = ScreenTool.Brush),
            Make.Radio(T("Eraser"), false, () => _canvas.Tool = ScreenTool.Eraser),
            Make.Radio(T("Add text"), false, () => _canvas.Tool = ScreenTool.Text)));
        var size = Make.Combo(240);
        size.Fill(new[] { (T("Brush-fine"), 1), (T("Brush - medium"), 2), (T("Brush - Thick"), 4) }, 2);
        size.SelectedIndexChanged += (_, _) => _canvas.PenSize = size.Value<int>();
        right.Controls.Add(size);
        right.Controls.Add(Make.Label(T("Color settings:")));
        right.Controls.Add(_color);
        _color.ColorChanged += () => _canvas.Color = _color.Color;
        right.Controls.Add(Make.Label(T("Text input:")));
        right.Controls.Add(_text);
        right.Controls.Add(Make.Row(Make.Label(T("FontSize") + "  "), _fontSize));
        right.Controls.Add(Make.Note(T("Choose Add text, type above, then click the picture to place it."), 240));

        right.Controls.Add(Make.Heading(T("Frame timing")));
        right.Controls.Add(Make.Row(Make.Label(T("Current frame delay:") + "  "), _delay, Make.Label(T("ms"))));
        _delay.ValueChanged += (_, _) =>
        {
            if (_loading || Current == null) return;
            Current.DelayMs = (int)_delay.Value;
            SaveFrames();
            RefreshList();
        };
        right.Controls.Add(Make.Button(T("Use this delay for all frames"), () =>
        {
            foreach (var f in _frames) f.DelayMs = (int)_delay.Value;
            SaveFrames();
            RefreshList();
        }, false, 240));

        right.Controls.Add(Make.Heading(T("Keyboard")));
        right.Controls.Add(Make.Button(T("Upload to keyboard"), Upload, true, 240));
        right.Controls.Add(_progress);
        right.Controls.Add(Make.Button(T("Time Sync"), SyncTime, false, 240));
        right.Controls.Add(Make.Button(T("Export GIF"), ExportGif, false, 240));
        split.Controls.Add(right);
        Controls.Add(split);

        _preview.Tick += (_, _) => AdvancePreview();
    }

    private EditFrame? Current => _list.SelectedIndex >= 0 && _list.SelectedIndex < _frames.Count ? _frames[_list.SelectedIndex] : null;

    public override void Reload()
    {
        if (_loaded) return;
        _loaded = true;
        foreach (var saved in _state.Saved.ScreenFrames)
        {
            try
            {
                var path = Path.Combine(AppState.ScreenFolder, Path.GetFileName(saved.File));
                using var image = Image.FromFile(path);
                _frames.Add(new EditFrame(GifCodec.Fit(image), saved.DelayMs));
            }
            catch (Exception e) when (e is IOException or OutOfMemoryException or ArgumentException) { /* A missing frame file should not block the page. */ }
        }
        RefreshList();
        if (_frames.Count > 0) _list.SelectedIndex = 0;
    }

    private void RefreshList()
    {
        var selected = _list.SelectedIndex;
        _loading = true;
        _list.BeginUpdate();
        _list.Items.Clear();
        for (int i = 0; i < _frames.Count; i++) _list.Items.Add($"{i + 1}.   {_frames[i].DelayMs} ms");
        if (_list.Items.Count > 0) _list.SelectedIndex = Math.Clamp(selected, 0, _list.Items.Count - 1);
        _list.EndUpdate();
        _loading = false;
        _count.Text = string.Format(T("{0} of {1} frames"), _frames.Count, Encoders.ScreenMaxFrames);
        ShowFrame();
    }

    private void ShowFrame()
    {
        if (_loading) return;
        _canvas.Frame = Current;
        _canvas.Invalidate();
        if (Current != null)
        {
            _loading = true;
            _delay.Value = Math.Clamp(Current.DelayMs, 30, 500);
            _loading = false;
        }
    }

    private void SaveFrames()
    {
        try
        {
            Directory.CreateDirectory(AppState.ScreenFolder);
            var saved = new List<ScreenFrame>();
            for (int i = 0; i < _frames.Count; i++)
            {
                var name = $"frame{i:D3}.png";
                _frames[i].Image.Save(Path.Combine(AppState.ScreenFolder, name), ImageFormat.Png);
                saved.Add(new ScreenFrame { File = name, DelayMs = _frames[i].DelayMs });
            }
            _state.Saved.ScreenFrames = saved;
            _state.Save();
        }
        catch (Exception e) when (e is IOException or System.Runtime.InteropServices.ExternalException)
        {
            _state.Report(e.Message, true);
        }
    }

    private void Import()
    {
        var path = PickOpen("Images (*.gif;*.png;*.jpg;*.jpeg;*.bmp)|*.gif;*.png;*.jpg;*.jpeg;*.bmp");
        if (path == null) return;
        try
        {
            var loaded = GifCodec.Load(path);
            var room = Encoders.ScreenMaxFrames - _frames.Count;
            if (loaded.Count > room)
                _state.Report(string.Format(T("The keyboard allows setting a maximum of 140 frames. The GIF has {0}; the extra frames were left out."), loaded.Count), true);
            foreach (var f in loaded.Take(Math.Max(room, 0))) _frames.Add(new EditFrame(f.Image, f.DelayMs));
            foreach (var f in loaded.Skip(Math.Max(room, 0))) f.Image.Dispose();
            SaveFrames();
            RefreshList();
        }
        catch (Exception e) when (e is IOException or OutOfMemoryException or ArgumentException)
        {
            _state.Report(T("That image could not be opened."), true);
        }
    }

    private bool HasRoom()
    {
        if (_frames.Count < Encoders.ScreenMaxFrames) return true;
        _state.Report(T("Maximum allowable 140 frames"), true);
        return false;
    }

    private void AddBlank()
    {
        if (!HasRoom()) return;
        var bitmap = new Bitmap(GifCodec.Size, GifCodec.Size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap)) g.Clear(Color.Black);
        _frames.Add(new EditFrame(bitmap, 100));
        SaveFrames();
        RefreshList();
        _list.SelectedIndex = _frames.Count - 1;
    }

    private void CopyFrame()
    {
        if (Current == null) { _state.Report(T("Please add pictures before copying"), true); return; }
        if (!HasRoom()) return;
        var at = _list.SelectedIndex + 1;
        _frames.Insert(at, new EditFrame(new Bitmap(Current.Image), Current.DelayMs));
        SaveFrames();
        RefreshList();
        _list.SelectedIndex = at;
    }

    private void DeleteFrame()
    {
        if (Current == null) { _state.Report(T("Please select frame"), true); return; }
        var frame = Current;
        _frames.Remove(frame);
        _canvas.Frame = null;
        frame.Dispose();
        SaveFrames();
        RefreshList();
    }

    private void UndoEdit()
    {
        if (Current is not { } f || f.Undo.Count == 0) return;
        f.Image.Dispose();
        f.Image = f.Undo.Pop();
        _canvas.Invalidate();
        SaveFrames();
    }

    private void ClearEdit()
    {
        if (Current is not { } f) return;
        f.PushUndo();
        f.Image.Dispose();
        f.Image = new Bitmap(f.Original);
        _canvas.Invalidate();
        SaveFrames();
    }

    private void TogglePreview()
    {
        if (_preview.Enabled)
        {
            _preview.Stop();
            _play.Text = T("Preview");
            ShowFrame();
            return;
        }
        if (_frames.Count == 0) return;
        _previewAt = 0;
        _play.Text = T("Stop");
        AdvancePreview();
        _preview.Start();
    }

    private void AdvancePreview()
    {
        if (_frames.Count == 0) { TogglePreview(); return; }
        var frame = _frames[_previewAt++ % _frames.Count];
        _canvas.Frame = frame;
        _canvas.Invalidate();
        _preview.Interval = Math.Max(frame.DelayMs, 30);
    }

    private async void Upload()
    {
        if (_frames.Count == 0) { _state.Report(T("The number of frames is 0, please add it and try again!"), true); return; }
        var pixels = _frames.Select(f => (GifCodec.ToRgb565(f.Image), f.DelayMs)).ToList();
        var image = Encoders.ScreenImage(pixels, out var pages);
        _progress.Value = 0;
        _progress.Visible = true;
        await _state.SendAsync(T("Animation uploaded."), k => k.UploadScreen(image, pages, 1, percent => BeginInvoke(() => _progress.Value = percent)));
        _progress.Visible = false;
    }

    private void SyncTime() => _ = _state.SendAsync(T("Clock set to this PC's time."), k => k.SyncClock(DateTime.Now));

    private void ExportGif()
    {
        if (_frames.Count == 0) return;
        var path = PickSave("LED File (*.gif)|*.gif", "animation.gif");
        if (path == null) return;
        try
        {
            GifCodec.Save(path, _frames.Select(f => new GifFrame(f.Image, f.DelayMs)).ToList());
            _state.Report(T("GIF saved."));
        }
        catch (IOException e) { _state.Report(e.Message, true); }
    }
}

public sealed class SettingsPage : Page
{
    private readonly AppState _state;
    private readonly Action _languageChanged;
    private readonly ComboBox _sleep = Make.Combo(), _language = Make.Combo(), _fn = Make.Combo();
    private readonly Slider _response = new();
    private readonly CheckBox _win, _altF4, _altTab;
    private readonly CheckBox _autostart, _tray;
    private bool _loading;

    public SettingsPage(AppState state, Action languageChanged)
    {
        _state = state;
        _languageChanged = languageChanged;
        var split = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(16), BackColor = Theme.Back, AutoScroll = true };

        var keyboard = Make.Column(330);
        keyboard.AutoSize = true;
        keyboard.Controls.Add(Make.Heading(T("Keyboard")));
        keyboard.Controls.Add(Make.Label(T("Key Response Time")));
        _response.Minimum = 1;
        _response.Maximum = 5;
        keyboard.Controls.Add(_response);
        keyboard.Controls.Add(Make.Note(T("Level 1 is the fastest. If keys start to chatter, raise the level."), 300));
        keyboard.Controls.Add(Make.Label(T("Sleep Time")));
        _sleep.Fill(new[] { (T("No Sleep"), 0), (T("1min"), 1), (T("5min"), 2), (T("30min"), 3) });
        keyboard.Controls.Add(_sleep);
        keyboard.Controls.Add(Make.Label(T("Fn Switch Function")));
        _fn.Fill(new[] { (T("Momentary"), 0), (T("Toggle"), 1) });
        keyboard.Controls.Add(_fn);
        _win = Make.Check(T("Disable Windows Key"), false);
        _altF4 = Make.Check(T("Disable ALT + F4"), false);
        _altTab = Make.Check(T("Disable ALT + TAB"), false);
        keyboard.Controls.Add(_win);
        keyboard.Controls.Add(_altF4);
        keyboard.Controls.Add(_altTab);
        keyboard.Controls.Add(Make.Button(T("Write to keyboard"), SendConfig, true, 220));
        keyboard.Controls.Add(Make.Heading(T("Reset")));
        keyboard.Controls.Add(Make.Button(T("Reset Keyboard"), Reset, false, 220));
        keyboard.Controls.Add(Make.Button(T("Factory Reset"), FactoryReset, false, 220));
        split.Controls.Add(keyboard);

        var app = Make.Column(0, new Padding(32, 0, 0, 0));
        app.Controls.Add(Make.Heading(T("This app")));
        app.Controls.Add(Make.Label(T("Language")));
        _language.Fill(Loc.Languages.Select(l => (l.Name, l.Code)));
        _language.SelectedIndexChanged += (_, _) =>
        {
            if (_loading) return;
            _state.Saved.Language = _language.Value<string>() ?? "";
            _state.Save();
            _languageChanged();
        };
        app.Controls.Add(_language);
        _autostart = Make.Check(T("Automatic Start"), Autostart.Enabled, v => { if (!_loading) Autostart.Enabled = v; });
        app.Controls.Add(_autostart);
        _tray = Make.Check(T("Minimize to Tray, Don't Exit"), _state.Saved.MinimizeToTray, v =>
        {
            if (_loading) return;
            _state.Saved.MinimizeToTray = v;
            _state.Save();
        });
        app.Controls.Add(_tray);

        app.Controls.Add(Make.Heading(T("Firmware and updates")));
        app.Controls.Add(Make.Note(T("This app never flashes firmware. The vendor package contains no firmware file, and a failed flash can leave the keyboard unusable, so that feature is deliberately left out."), 360));
        app.Controls.Add(Make.Note(T("It also never downloads or updates anything by itself. New versions are published on the project page."), 360));
        app.Controls.Add(Make.Heading(T("About")));
        app.Controls.Add(Make.Note($"OpenAK820 {Application.ProductVersion.Split('+')[0]}. {T("Not affiliated with or endorsed by AJAZZ.")}", 360));
        split.Controls.Add(app);
        Controls.Add(split);
    }

    public override void Reload()
    {
        _loading = true;
        var c = _state.Profile.Config;
        _response.Value = c.KeyResponse;
        _sleep.Select(c.SleepTime);
        _fn.Select(c.FnSwitch);
        _win.Checked = c.DisableWinKey;
        _altF4.Checked = c.DisableAltF4;
        _altTab.Checked = c.DisableAltTab;
        _language.Select(Loc.Current);
        _autostart.Checked = Autostart.Enabled;
        _tray.Checked = _state.Saved.MinimizeToTray;
        _loading = false;
    }

    private void SendConfig()
    {
        var c = _state.Profile.Config;
        c.KeyResponse = _response.Value;
        c.SleepTime = _sleep.Value<int>();
        c.FnSwitch = _fn.Value<int>();
        c.DisableWinKey = _win.Checked;
        c.DisableAltF4 = _altF4.Checked;
        c.DisableAltTab = _altTab.Checked;
        _state.Save();
        _ = _state.SendAsync(T("Settings written to keyboard."), k => k.SetConfig(c));
    }

    private void Reset()
    {
        if (Confirm(T("Reset Keyboard"), T("Resetting the keyboard will clear all keyboard data. Do you want to continue?")))
            _ = _state.ResetKeyboardAsync();
    }

    private void FactoryReset()
    {
        if (!Confirm(T("Factory Reset"), T("Factory reset will clear all data, including the profiles and macros saved on this PC. Do you want to continue?"))) return;
        _state.Saved.Profiles = new List<Profile> { new() { Name = "Default" } };
        _state.Saved.ActiveProfile = 0;
        _state.Saved.Macros.Clear();
        _state.Save();
        _state.RaiseProfileChanged();
        _ = _state.ResetKeyboardAsync();
    }
}
