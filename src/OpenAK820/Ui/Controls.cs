using System.ComponentModel;
using System.Drawing.Drawing2D;
using OpenAK820.Model;

namespace OpenAK820.Ui;

public static class Theme
{
    public static readonly Color Back = Color.FromArgb(0x14, 0x16, 0x1A);
    public static readonly Color Panel = Color.FromArgb(0x1B, 0x1E, 0x24);
    public static readonly Color Card = Color.FromArgb(0x23, 0x27, 0x2F);
    public static readonly Color Line = Color.FromArgb(0x33, 0x38, 0x42);
    public static readonly Color Text = Color.FromArgb(0xE8, 0xEA, 0xED);
    public static readonly Color Muted = Color.FromArgb(0x9A, 0xA0, 0xA8);
    public static readonly Color Accent = Color.FromArgb(0x4D, 0xA3, 0xFF);
    public static readonly Color Warn = Color.FromArgb(0xF2, 0xB1, 0x3C);
    public static readonly Color Bad = Color.FromArgb(0xFF, 0x6B, 0x6B);
    public static readonly Color Good = Color.FromArgb(0x4C, 0xD1, 0x7F);
    public static readonly Color KeyFace = Color.FromArgb(0x2E, 0x33, 0x3C);

    public static readonly Font Body = new("Segoe UI", 9.5f);
    public static readonly Font Small = new("Segoe UI", 8.5f);
    public static readonly Font Heading = new("Segoe UI Semibold", 11f);
    public static readonly Font Title = new("Segoe UI Semibold", 16f);

    public static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        var d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static Color ToColor(this Rgb c) => Color.FromArgb(c.R, c.G, c.B);
    public static Rgb ToRgb(this Color c) => new(c.R, c.G, c.B);
}

/// <summary>Small factories, so page code reads as a description of the page.</summary>
public static class Make
{
    public static Label Label(string text, Font? font = null, Color? color = null, int width = 0) => new()
    {
        Text = text, AutoSize = width == 0, Width = width == 0 ? 100 : width, Font = font ?? Theme.Body,
        ForeColor = color ?? Theme.Text, BackColor = Color.Transparent, Margin = new Padding(0, 6, 0, 2),
        MaximumSize = width == 0 ? default : new Size(width, 0),
    };

    public static Label Heading(string text) => Label(text, Theme.Heading);

    public static Label Note(string text, int width = 300)
    {
        var l = Label(text, Theme.Small, Theme.Muted, width);
        l.AutoSize = true;
        return l;
    }

    public static Button Button(string text, Action onClick, bool primary = false, int width = 0)
    {
        var b = new Button
        {
            Text = text, AutoSize = width == 0, Width = width == 0 ? 75 : width, Height = 30, Font = Theme.Body,
            FlatStyle = FlatStyle.Flat, ForeColor = primary ? Color.White : Theme.Text,
            BackColor = primary ? Color.FromArgb(0x2F, 0x7F, 0xE0) : Theme.Card, Margin = new Padding(0, 4, 6, 4),
            UseVisualStyleBackColor = false, Cursor = Cursors.Hand,
        };
        b.AutoSizeMode = AutoSizeMode.GrowOnly;
        b.MinimumSize = new Size(0, 30);
        b.FlatAppearance.BorderColor = primary ? Color.FromArgb(0x2F, 0x7F, 0xE0) : Theme.Line;
        b.Click += (_, _) => onClick();
        return b;
    }

    public static FlowLayoutPanel Column(int width = 0, Padding? padding = null) => new()
    {
        FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = width == 0,
        AutoSizeMode = AutoSizeMode.GrowAndShrink, Width = width == 0 ? 100 : width, Padding = padding ?? Padding.Empty,
        Margin = Padding.Empty, BackColor = Color.Transparent, AutoScroll = false,
    };

    public static FlowLayoutPanel Row(params Control[] controls)
    {
        var p = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = Padding.Empty, BackColor = Color.Transparent,
        };
        p.Controls.AddRange(controls);
        return p;
    }

    public static ComboBox Combo(int width = 220)
    {
        return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width, Font = Theme.Body, Margin = new Padding(0, 2, 0, 6), FlatStyle = FlatStyle.Flat };
    }

    public static TextBox Text(int width = 220, bool multiline = false) => new()
    {
        Width = width, Font = Theme.Body, BorderStyle = BorderStyle.FixedSingle, BackColor = Theme.Card, ForeColor = Theme.Text,
        Margin = new Padding(0, 2, 0, 6), Multiline = multiline, Height = multiline ? 90 : 26,
        ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
    };

    public static ListBox List(int width = 200, int height = 300) => new()
    {
        Width = width, Height = height, Font = Theme.Body, BorderStyle = BorderStyle.None, BackColor = Theme.Card, ForeColor = Theme.Text,
        IntegralHeight = false, Margin = new Padding(0, 4, 0, 4), ItemHeight = 22,
    };

    public static CheckBox Check(string text, bool value, Action<bool>? changed = null)
    {
        var c = new CheckBox { Text = text, Checked = value, AutoSize = true, Font = Theme.Body, ForeColor = Theme.Text, BackColor = Color.Transparent, Margin = new Padding(0, 4, 12, 4) };
        if (changed != null) c.CheckedChanged += (_, _) => changed(c.Checked);
        return c;
    }

    public static RadioButton Radio(string text, bool value, Action? selected = null)
    {
        var r = new RadioButton { Text = text, Checked = value, AutoSize = true, Font = Theme.Body, ForeColor = Theme.Text, BackColor = Color.Transparent, Margin = new Padding(0, 4, 12, 4) };
        if (selected != null) r.CheckedChanged += (_, _) => { if (r.Checked) selected(); };
        return r;
    }

    public static NumericUpDown Number(int min, int max, int value, int width = 80) => new()
    {
        Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), Width = width, Font = Theme.Body,
        BorderStyle = BorderStyle.FixedSingle, BackColor = Theme.Card, ForeColor = Theme.Text, Margin = new Padding(0, 2, 6, 6),
    };
}

/// <summary>A combo box holding typed values alongside their display text.</summary>
public sealed record Choice<T>(string Text, T Value)
{
    public override string ToString() => Text;
}

public static class ComboExtensions
{
    public static void Fill<T>(this ComboBox box, IEnumerable<(string Text, T Value)> items, T? selected = default)
    {
        box.Items.Clear();
        foreach (var (text, value) in items) box.Items.Add(new Choice<T>(text, value));
        box.Select(selected);
    }

    public static void Select<T>(this ComboBox box, T? value)
    {
        for (int i = 0; i < box.Items.Count; i++)
            if (box.Items[i] is Choice<T> c && EqualityComparer<T>.Default.Equals(c.Value, value)) { box.SelectedIndex = i; return; }
        if (box.Items.Count > 0) box.SelectedIndex = 0;
    }

    public static T? Value<T>(this ComboBox box) => box.SelectedItem is Choice<T> c ? c.Value : default;
}

/// <summary>A flat slider with an inline value, because the stock TrackBar ignores dark colours.</summary>
[DesignerCategory("Code")]
public sealed class Slider : Control
{
    private int _value;
    public int Minimum { get; set; }
    public int Maximum { get; set; } = 5;
    public event Action? ValueChanged;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            var v = Math.Clamp(value, Minimum, Maximum);
            if (v == _value) return;
            _value = v;
            Invalidate();
            ValueChanged?.Invoke();
        }
    }

    public Slider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
        Height = 26; Width = 220; Margin = new Padding(0, 2, 0, 6); BackColor = Color.Transparent;
    }

    private float Track => Width - 34;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var f = Maximum == Minimum ? 0 : (Value - Minimum) / (float)(Maximum - Minimum);
        var y = Height / 2f;
        var color = Enabled ? Theme.Accent : Theme.Line;
        using (var line = new Pen(Theme.Line, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(line, 8, y, Track, y);
        using (var line = new Pen(color, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(line, 8, y, 8 + (Track - 8) * f, y);
        using (var knob = new SolidBrush(Enabled ? Color.White : Theme.Muted)) g.FillEllipse(knob, 8 + (Track - 8) * f - 7, y - 7, 14, 14);
        TextRenderer.DrawText(g, Value.ToString(), Theme.Small, new Rectangle((int)Track + 6, 0, 28, Height), Enabled ? Theme.Text : Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
    }

    private void Drag(int x)
    {
        var f = Math.Clamp((x - 8) / (Track - 8), 0, 1);
        Value = Minimum + (int)Math.Round(f * (Maximum - Minimum));
    }

    protected override void OnMouseDown(MouseEventArgs e) { Focus(); Drag(e.X); }
    protected override void OnMouseMove(MouseEventArgs e) { if (e.Button == MouseButtons.Left) Drag(e.X); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    protected override bool IsInputKey(Keys k) => k is Keys.Left or Keys.Right || base.IsInputKey(k);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Left) Value--;
        if (e.KeyCode == Keys.Right) Value++;
    }
}

/// <summary>A colour swatch that opens the colour picker and offers quick presets.</summary>
[DesignerCategory("Code")]
public sealed class ColorPicker : FlowLayoutPanel
{
    private static readonly string[] Presets = { "#FF0000", "#FF7A00", "#FFD400", "#00E05A", "#00D5FF", "#0066FF", "#A000FF", "#FF2FA8", "#FFFFFF" };
    private readonly Panel _swatch;
    private Color _color = Color.White;
    public event Action? ColorChanged;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Color
    {
        get => _color;
        set { _color = value; _swatch.BackColor = value; }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Hex
    {
        get => _color.ToRgb().ToHex();
        set => Color = Rgb.Parse(value).ToColor();
    }

    public ColorPicker()
    {
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; WrapContents = false; Margin = new Padding(0, 2, 0, 6); BackColor = Color.Transparent;
        _swatch = new Panel { Width = 38, Height = 24, BackColor = _color, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 0), BorderStyle = BorderStyle.FixedSingle };
        _swatch.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { Color = _color, FullOpen = true };
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK) Pick(dialog.Color);
        };
        Controls.Add(_swatch);
        foreach (var hex in Presets)
        {
            var c = Rgb.Parse(hex).ToColor();
            var chip = new Panel { Width = 18, Height = 24, BackColor = c, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 3, 0) };
            chip.Click += (_, _) => Pick(c);
            Controls.Add(chip);
        }
    }

    private void Pick(Color c) { Color = c; ColorChanged?.Invoke(); }
}

/// <summary>Draws the AK820 Pro from its key table. Single or multi-select; dragging selects a group of keys.</summary>
[DesignerCategory("Code")]
public sealed class KeyboardView : Control
{
    private static readonly Dictionary<string, string> Short = new()
    {
        ["~`"] = "`", ["!1"] = "1", ["@2"] = "2", ["#3"] = "3", ["$4"] = "4", ["%5"] = "5", ["^6"] = "6", ["&7"] = "7", ["*8"] = "8",
        ["(9"] = "9", [")0"] = "0", ["_-"] = "-", ["+="] = "=", ["{["] = "[", ["}}"] = "]", ["|\\"] = "\\", [":;"] = ";", ["\"'"] = "'",
        [",<"] = ",", [".>"] = ".", ["/?"] = "/", ["Shift_L"] = "Shift", ["Shift_R"] = "Shift", ["Ctrl_L"] = "Ctrl", ["Ctrl_R"] = "Ctrl",
        ["Alt_L"] = "Alt", ["Alt_R"] = "Alt", ["Win_L"] = "Win", ["home"] = "Home", ["CapsLock"] = "Caps", ["Backspace"] = "Bksp", ["Delete"] = "Del",
    };

    public HashSet<int> Selected { get; } = new();
    /// <summary>When true, clicks toggle keys and dragging selects a group.</summary>
    public bool MultiSelect { get; set; }
    public Func<KeyInfo, Color?>? KeyColor { get; set; }
    public Func<KeyInfo, string?>? KeyText { get; set; }
    public Func<KeyInfo, bool>? KeyEnabled { get; set; }
    public event Action? SelectionChanged;

    private int _hover = -1;
    private Point? _dragStart;
    private Rectangle _dragRect;

    public KeyboardView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Back;
        MinimumSize = new Size(300, 120);
        DoubleBuffered = true;
    }

    public static string Label(KeyInfo k) => Short.TryGetValue(k.Name, out var s) ? s : k.Name;

    private float ViewScale => Math.Min((Width - 24f) / (KeyLayout.DesignRight - KeyLayout.DesignLeft + 24), (Height - 24f) / (KeyLayout.DesignBottom - KeyLayout.DesignTop + 24));
    private PointF Origin => new(
        (Width - (KeyLayout.DesignRight - KeyLayout.DesignLeft) * ViewScale) / 2 - KeyLayout.DesignLeft * ViewScale,
        (Height - (KeyLayout.DesignBottom - KeyLayout.DesignTop) * ViewScale) / 2 - KeyLayout.DesignTop * ViewScale);

    private RectangleF Rect(KeyInfo k)
    {
        var (s, o) = (ViewScale, Origin);
        return new RectangleF(o.X + k.X * s, o.Y + k.Y * s, k.W * s, k.H * s);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        var (s, o) = (ViewScale, Origin);
        var body = new RectangleF(o.X + (KeyLayout.DesignLeft - 11) * s, o.Y + (KeyLayout.DesignTop - 11) * s,
            (KeyLayout.DesignRight - KeyLayout.DesignLeft + 22) * s, (KeyLayout.DesignBottom - KeyLayout.DesignTop + 22) * s);
        using (var path = Theme.Rounded(body, 12 * s))
        using (var fill = new SolidBrush(Theme.Panel))
        using (var edge = new Pen(Theme.Line))
        {
            g.FillPath(fill, path);
            g.DrawPath(edge, path);
        }
        // The colour screen, top right on the real board.
        var screen = new RectangleF(o.X + 664 * s, o.Y + 16 * s, 41 * s, 41 * s);
        using (var path = Theme.Rounded(screen, 4 * s))
        using (var fill = new SolidBrush(Color.FromArgb(0x0A, 0x0C, 0x10)))
        using (var edge = new Pen(Theme.Line))
        {
            g.FillPath(fill, path);
            g.DrawPath(edge, path);
        }
        using var font = new Font("Segoe UI", Math.Max(6.5f, 8.2f * s), FontStyle.Regular, GraphicsUnit.Point);
        foreach (var key in KeyLayout.Keys)
        {
            var r = Rect(key);
            var enabled = KeyEnabled?.Invoke(key) ?? true;
            var color = KeyColor?.Invoke(key);
            var face = color ?? Theme.KeyFace;
            if (!enabled) face = Color.FromArgb(0x20, 0x23, 0x29);
            using (var path = Theme.Rounded(r, 3.5f * s))
            using (var fill = new SolidBrush(face))
            {
                g.FillPath(fill, path);
                var selected = Selected.Contains(key.Index);
                if (selected || key.Index == _hover)
                {
                    using var pen = new Pen(selected ? Theme.Accent : Theme.Muted, selected ? 2f : 1f);
                    g.DrawPath(pen, path);
                }
            }
            var luminance = 0.299 * face.R + 0.587 * face.G + 0.114 * face.B;
            var text = KeyText?.Invoke(key) ?? Label(key);
            var textColor = !enabled ? Color.FromArgb(0x60, 0x66, 0x70) : luminance > 140 ? Color.Black : Theme.Text;
            TextRenderer.DrawText(g, text, font, Rectangle.Round(r), textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoClipping | TextFormatFlags.NoPadding);
        }
        if (_dragStart != null)
        {
            using var band = new SolidBrush(Color.FromArgb(50, Theme.Accent));
            using var pen = new Pen(Theme.Accent);
            g.FillRectangle(band, _dragRect);
            g.DrawRectangle(pen, _dragRect);
        }
    }

    private KeyInfo? Hit(Point p) => KeyLayout.Keys.FirstOrDefault(k => Rect(k).Contains(p) && (KeyEnabled?.Invoke(k) ?? true));

    protected override void OnMouseDown(MouseEventArgs e)
    {
        var key = Hit(e.Location);
        if (MultiSelect)
        {
            if (key != null)
            {
                if (!Selected.Remove(key.Index)) Selected.Add(key.Index);
                SelectionChanged?.Invoke();
            }
            else _dragStart = e.Location;
        }
        else if (key != null)
        {
            Selected.Clear();
            Selected.Add(key.Index);
            SelectionChanged?.Invoke();
        }
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragStart is { } start)
        {
            _dragRect = Rectangle.FromLTRB(Math.Min(start.X, e.X), Math.Min(start.Y, e.Y), Math.Max(start.X, e.X), Math.Max(start.Y, e.Y));
            Invalidate();
            return;
        }
        var index = Hit(e.Location)?.Index ?? -1;
        if (index != _hover) { _hover = index; Invalidate(); }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_dragStart == null) return;
        foreach (var key in KeyLayout.Keys)
            if ((KeyEnabled?.Invoke(key) ?? true) && Rect(key).IntersectsWith(_dragRect)) Selected.Add(key.Index);
        _dragStart = null;
        SelectionChanged?.Invoke();
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); }

    public void SelectAll()
    {
        foreach (var k in KeyLayout.Keys) if (KeyEnabled?.Invoke(k) ?? true) Selected.Add(k.Index);
        SelectionChanged?.Invoke();
        Invalidate();
    }

    public void SelectNone()
    {
        Selected.Clear();
        SelectionChanged?.Invoke();
        Invalidate();
    }
}

/// <summary>A page in the main window. Pages are built once and refreshed when shown.</summary>
[DesignerCategory("Code")]
public abstract class Page : UserControl
{
    protected Page()
    {
        BackColor = Theme.Back;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        Dock = DockStyle.Fill;
        DoubleBuffered = true;
    }

    /// <summary>Called when this page becomes visible, or the active profile or connection changes.</summary>
    public virtual void Reload() { }

    protected static string? PickSave(string filter, string name)
    {
        using var d = new SaveFileDialog { Filter = filter, FileName = name };
        return d.ShowDialog() == DialogResult.OK ? d.FileName : null;
    }

    protected static string? PickOpen(string filter)
    {
        using var d = new OpenFileDialog { Filter = filter };
        return d.ShowDialog() == DialogResult.OK ? d.FileName : null;
    }

    /// <summary>A simple one-line prompt. Returns null on cancel.</summary>
    protected static string? Prompt(string title, string value)
    {
        using var form = new Form
        {
            Text = title, Width = 380, Height = 150, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, BackColor = Theme.Panel, ForeColor = Theme.Text, ShowInTaskbar = false,
        };
        var box = Make.Text(340);
        box.Text = value;
        box.Location = new Point(12, 14);
        var ok = new Button { Text = Loc.T("OK"), DialogResult = DialogResult.OK, Location = new Point(190, 60), Width = 80, FlatStyle = FlatStyle.Flat };
        var cancel = new Button { Text = Loc.T("Cancel"), DialogResult = DialogResult.Cancel, Location = new Point(272, 60), Width = 80, FlatStyle = FlatStyle.Flat };
        form.Controls.AddRange(new Control[] { box, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return form.ShowDialog() == DialogResult.OK ? box.Text.Trim() : null;
    }

    /// <summary>
    /// A shared profile could bind a key to a program or a typed command, so importing one
    /// shows exactly what it would run and needs a clear yes.
    /// </summary>
    protected static bool ConfirmHostActions(IEnumerable<KeyAction> actions)
    {
        var risky = actions.Where(a => a.Kind is KeyActionKind.OpenProgram or KeyActionKind.SendText).Select(a => a.Text).Distinct().Take(12).ToList();
        return risky.Count == 0 || Confirm(Loc.T("Import"), Loc.T("This file binds keys to programs or typed text. Only continue if you trust it:") + "\n\n" + string.Join("\n", risky));
    }

    /// <summary>Exports are meant to be shared, and these fields can contain a Windows username or personal text.</summary>
    protected static bool ConfirmExportContents(IEnumerable<KeyAction> actions)
    {
        var personal = actions.Any(a => a.Kind is KeyActionKind.OpenProgram or KeyActionKind.OpenWebsite or KeyActionKind.SendText);
        return !personal || Confirm(Loc.T("Export"), Loc.T("This file will include the program paths, website addresses and text assigned to your keys. Paths can contain your Windows username. Only share it if you're happy with that."));
    }

    protected static bool Confirm(string title, string text) =>
        MessageBox.Show(text, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.OK;
}
