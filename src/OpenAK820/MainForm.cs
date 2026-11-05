using System.ComponentModel;
using System.Drawing.Drawing2D;
using OpenAK820.Device;
using OpenAK820.Pages;
using OpenAK820.Protocol;
using OpenAK820.Services;
using OpenAK820.Ui;
using static OpenAK820.Ui.Loc;

namespace OpenAK820;

/// <summary>The left-hand navigation list.</summary>
[DesignerCategory("Code")]
public sealed class NavBar : Control
{
    private const int ItemHeight = 44;
    private const int ItemsTop = 66;
    private readonly List<string> _items = new();
    private int _selected, _hover = -1;
    public event Action<int>? Selected;

    public NavBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Panel;
        Dock = DockStyle.Left;
        Width = 190;
    }

    public void SetItems(IEnumerable<string> items)
    {
        _items.Clear();
        _items.AddRange(items);
        Invalidate();
    }

    public int SelectedIndex
    {
        get => _selected;
        set { _selected = value; Invalidate(); Selected?.Invoke(value); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        TextRenderer.DrawText(g, "OpenAK820", Theme.Title, new Rectangle(18, 16, Width - 20, 36), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        for (int i = 0; i < _items.Count; i++)
        {
            var r = new RectangleF(10, ItemsTop + i * ItemHeight, Width - 20, ItemHeight - 6);
            if (i == _selected || i == _hover)
            {
                using var path = Theme.Rounded(r, 7);
                using var fill = new SolidBrush(i == _selected ? Color.FromArgb(0x24, 0x3A, 0x57) : Theme.Card);
                g.FillPath(fill, path);
            }
            TextRenderer.DrawText(g, _items[i], Theme.Body, Rectangle.Round(new RectangleF(r.X + 12, r.Y, r.Width - 16, r.Height)),
                i == _selected ? Color.White : Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    private int Hit(Point p)
    {
        var i = (p.Y - ItemsTop) / ItemHeight;
        return p.Y >= ItemsTop && i < _items.Count ? i : -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var i = Hit(e.Location);
        if (i == _hover) return;
        _hover = i;
        Cursor = i >= 0 ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        var i = Hit(e.Location);
        if (i >= 0) SelectedIndex = i;
    }
}

[DesignerCategory("Code")]
public sealed class MainForm : Form
{
    private readonly AppState _state;
    private readonly LiveEngine _live;
    private readonly HostActions _hostActions;
    private readonly NavBar _nav = new();
    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = Theme.Back };
    private readonly Label _banner = new() { Dock = DockStyle.Top, Height = 34, BackColor = Color.FromArgb(0x4A, 0x38, 0x12), ForeColor = Theme.Warn, Font = Theme.Body, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 0, 0), Visible = false };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 28, BackColor = Theme.Panel, ForeColor = Theme.Muted, Font = Theme.Small, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 0, 0) };
    private readonly NotifyIcon _tray = new();
    private readonly System.Windows.Forms.Timer _rescan = new() { Interval = 400 };
    private readonly System.Windows.Forms.Timer _battery = new() { Interval = 15000 };
    private readonly System.Windows.Forms.Timer _conflicts = new() { Interval = 10000 };
    private readonly bool _simulated;
    private List<(string Title, Page Page)> _pages = new();
    private bool _exiting;

    public MainForm(AppState state, bool startHidden, bool simulated)
    {
        _state = state;
        _simulated = simulated;
        _live = new LiveEngine(state);
        _hostActions = new HostActions(state);
        Text = "OpenAK820";
        Icon = LoadIcon();
        BackColor = Theme.Back;
        ForeColor = Theme.Text;
        ClientSize = new Size(1180, 780);
        MinimumSize = new Size(1080, 720);
        StartPosition = FormStartPosition.CenterScreen;
        Font = Theme.Body;

        Controls.Add(_content);
        Controls.Add(_banner);
        Controls.Add(_status);
        Controls.Add(_nav);

        _nav.Selected += ShowPage;
        BuildPages();

        _state.Status += (message, isError) => OnUi(() =>
        {
            if (string.IsNullOrEmpty(message)) return;
            _status.Text = message;
            _status.ForeColor = isError ? Theme.Bad : Theme.Muted;
        });
        _state.ProfileChanged += () => OnUi(ReloadCurrent);
        _state.Keyboard.ConnectionChanged += () => OnUi(ConnectionChanged);
        _state.Keyboard.Error += e => _state.Report(e.Message, true);
        _state.Keyboard.HostKeyPressed += id => OnUi(() => _hostActions.Run(id));

        _rescan.Tick += (_, _) => { _rescan.Stop(); _state.Keyboard.Refresh(); };
        _battery.Tick += (_, _) => ReadBattery();
        _battery.Start();
        _conflicts.Tick += (_, _) => CheckConflicts();
        _conflicts.Start();
        CheckConflicts();

        _tray.Icon = Icon;
        _tray.Text = "OpenAK820";
        _tray.Visible = true;
        var menu = new ContextMenuStrip();
        menu.Items.Add(T("Show"), null, (_, _) => ShowFromTray());
        menu.Items.Add(T("Exit Program"), null, (_, _) => { _exiting = true; Close(); });
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowFromTray();

        if (startHidden)
        {
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            Load += (_, _) => Hide();
        }
    }

    private static Icon LoadIcon()
    {
        using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("app.ico");
        return stream != null ? new Icon(stream) : SystemIcons.Application;
    }

    private void BuildPages()
    {
        var selected = _nav.SelectedIndex;
        foreach (var (_, page) in _pages) page.Dispose();
        _content.Controls.Clear();
        _pages = new List<(string, Page)>
        {
            (T("Home"), new HomePage(_state)),
            (T("Keys"), new KeysPage(_state)),
            (T("Macros"), new MacroPage(_state)),
            (T("Lighting Mode"), new LightingPage(_state)),
            (T("User Lighting"), new PerKeyPage(_state)),
            (T("Real-time Lighting"), new RealtimePage(_state, _live)),
            (T("Music Rhythm"), new MusicPage(_state, _live)),
            (T("TFT Screen"), new ScreenPage(_state)),
            (T("System Settings"), new SettingsPage(_state, LanguageChanged)),
        };
        _nav.SetItems(_pages.Select(p => p.Title));
        _nav.SelectedIndex = selected;
    }

    private void LanguageChanged()
    {
        Loc.Set(_state.Saved.Language);
        BeginInvoke(BuildPages);
    }

    private void ShowPage(int index)
    {
        if (index < 0 || index >= _pages.Count) return;
        var page = _pages[index].Page;
        _content.SuspendLayout();
        _content.Controls.Clear();
        _content.Controls.Add(page);
        _content.ResumeLayout();
        page.Reload();
    }

    /// <summary>Shows one page for the screenshot tool.</summary>
    public void SelectPage(int index) => _nav.SelectedIndex = index;
    public int PageCount => _pages.Count;

    private void ReloadCurrent()
    {
        if (_nav.SelectedIndex >= 0 && _nav.SelectedIndex < _pages.Count) _pages[_nav.SelectedIndex].Page.Reload();
    }

    private void ConnectionChanged()
    {
        _state.Battery = null;
        var link = _state.Keyboard.Link;
        Report(link != null ? string.Format(T("Found {0}."), link.DisplayName) : T("No keyboard found"));
        if (link?.Kind == LinkKind.Dongle) ReadBattery();
        _ = _state.SyncClockOnConnectAsync();
        if (_live.IsRunning && link == null) _live.Stop();
        ReloadCurrent();
    }

    private void Report(string message) => _state.Report(message);

    private async void ReadBattery()
    {
        if (_state.Keyboard.Protocol is not WirelessProtocol) return;
        try
        {
            int? level = null;
            await _state.Keyboard.RunAsync(k => level = k.ReadBattery());
            if (level != _state.Battery)
            {
                _state.Battery = level;
                ReloadCurrent();
            }
        }
        catch (Exception e) when (e is ProtocolException or IOException or Win32Exception) { /* The keyboard is asleep or was unplugged. The next read or device scan recovers. */ }
    }

    private void CheckConflicts()
    {
        var found = ConflictDetector.Find();
        _banner.Visible = found.Count > 0;
        if (found.Count > 0)
            _banner.Text = string.Format(T("{0} is running. Close it first: two programs driving the keyboard make writes fail."), string.Join(", ", found));
    }

    private void OnUi(Action action)
    {
        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke(action);
        else action();
    }

    private void ShowFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (!_simulated) _state.Keyboard.Refresh();
    }

    protected override void WndProc(ref Message m)
    {
        // WM_DEVICECHANGE with DBT_DEVNODES_CHANGED: a device arrived or left. Debounced, because one
        // plug-in raises many of these. This replaces polling, so an idle app uses no CPU.
        if (m.Msg == 0x0219 && m.WParam == 7 && !_simulated)
        {
            _rescan.Stop();
            _rescan.Start();
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exiting && e.CloseReason == CloseReason.UserClosing && _state.Saved.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        _live.Dispose();
        _tray.Visible = false;
        _state.Save();
        _state.Keyboard.Dispose();
        base.OnFormClosing(e);
    }
}
