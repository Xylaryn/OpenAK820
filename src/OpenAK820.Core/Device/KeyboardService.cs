using System.Collections.Concurrent;
using OpenAK820.Hid;
using OpenAK820.Model;
using OpenAK820.Protocol;

namespace OpenAK820.Device;

public enum LinkKind { Usb, Dongle }

/// <summary>The HID collections that together make up one recognised keyboard.</summary>
public sealed record KeyboardLink(LinkKind Kind, HidInterfaceInfo Control, HidInterfaceInfo? Screen, HidInterfaceInfo? Notify)
{
    public string DisplayName => Kind == LinkKind.Usb ? "AK820 Pro (USB)" : "AK820 Pro (2.4 GHz)";
}

public static class KeyboardFinder
{
    public const ushort Vendor = 0x0C45;

    /// <summary>
    /// Sonix's vendor ID is shared by many unrelated products, so a board must match on product ID,
    /// product name and the exact shape of its control collection before we will talk to it.
    /// Bootloader IDs never match, so the app cannot address a keyboard in firmware-update mode.
    /// </summary>
    public static List<KeyboardLink> Find(IEnumerable<HidInterfaceInfo>? interfaces = null)
    {
        var all = (interfaces ?? HidEnumerator.List(Vendor)).ToList();
        var links = new List<KeyboardLink>();

        var usb = all.Where(i => i.ProductId is 0x8009 or 0x800A && i.Product.Contains("AK820", StringComparison.OrdinalIgnoreCase)).ToList();
        var control = usb.FirstOrDefault(i => i.UsagePage == 0xFF13 && i.FeatureLength == 65);
        if (control != null)
            links.Add(new KeyboardLink(LinkKind.Usb, control,
                usb.FirstOrDefault(i => i.UsagePage == 0xFF68 && i.OutputLength == 4097),
                usb.FirstOrDefault(i => i.UsagePage == 0xFFFF && i.InputLength == 4)));

        var dongle = all.Where(i => i.ProductId == 0xFDFD && i.Product.Contains("2.4G", StringComparison.OrdinalIgnoreCase)).ToList();
        var wireless = dongle.FirstOrDefault(i => i.UsagePage == 0xFF60 && i.OutputLength == 33);
        if (wireless != null)
            links.Add(new KeyboardLink(LinkKind.Dongle, wireless, null,
                dongle.FirstOrDefault(i => i.UsagePage == 0xFFFF && i.InputLength == 4)));

        return links;
    }
}

/// <summary>Text a user can paste into a bug report. It must stay free of anything that identifies them or their PC.</summary>
public static class Diagnostics
{
    /// <summary>
    /// Describes only what the keyboard itself reports. Device paths are left out on purpose:
    /// they contain instance IDs specific to one PC, and nobody needs them to diagnose a board.
    /// </summary>
    public static string Report(IEnumerable<HidInterfaceInfo> interfaces, string appVersion)
    {
        var list = interfaces.ToList();
        var lines = new List<string> { $"OpenAK820 {appVersion}" };
        var links = KeyboardFinder.Find(list);
        lines.Add(links.Count == 0
            ? "Recognised: none"
            : "Recognised: " + string.Join(", ", links.Select(l => l.DisplayName)));
        if (list.Count == 0) lines.Add($"No HID devices with vendor ID {KeyboardFinder.Vendor:X4} were found.");
        foreach (var i in list.OrderBy(i => i.ProductId).ThenBy(i => i.UsagePage))
            lines.Add($"{i.VendorId:X4}:{i.ProductId:X4} rel {i.Release:X4} page {i.UsagePage:X4} usage {i.Usage:X4} in {i.InputLength} out {i.OutputLength} feat {i.FeatureLength} [{i.Manufacturer}] [{i.Product}]");
        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>Owns the open device. All I/O happens on one worker thread, so UI code never blocks on the keyboard.</summary>
public sealed class KeyboardService : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _worker;
    private readonly List<IDisposable> _ports = new();
    private CancellationTokenSource? _notifyCts;
    private IReadOnlyDictionary<int, Rgb>? _pendingFrame;
    private int _frameQueued;

    public IKeyboardProtocol? Protocol { get; private set; }
    public KeyboardLink? Link { get; private set; }
    public bool IsConnected => Protocol != null;

    public event Action? ConnectionChanged;
    /// <summary>Raised on a background thread when the keyboard reports a host-side key press.</summary>
    public event Action<byte>? HostKeyPressed;
    public event Action<Exception>? Error;

    public KeyboardService()
    {
        _worker = new Thread(Run) { IsBackground = true, Name = "keyboard-io" };
        _worker.Start();
    }

    private void Run()
    {
        foreach (var work in _queue.GetConsumingEnumerable())
        {
            try { work(); }
            catch (Exception e) { Error?.Invoke(e); }
        }
    }

    /// <summary>Queue work against the connected keyboard. Completes with false if none is connected.</summary>
    public Task<bool> RunAsync(Action<IKeyboardProtocol> work)
    {
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (_queue.IsAddingCompleted) { done.SetResult(false); return done.Task; }
        _queue.Add(() =>
        {
            var protocol = Protocol;
            if (protocol == null) { done.SetResult(false); return; }
            try { work(protocol); done.SetResult(true); }
            catch (Exception e) { done.SetException(e); }
        });
        return done.Task;
    }

    /// <summary>Queue a live frame. A newer frame replaces one still waiting, so a slow link never builds a backlog.</summary>
    public void SubmitFrame(IReadOnlyDictionary<int, Rgb> colors)
    {
        Volatile.Write(ref _pendingFrame, colors);
        if (Interlocked.Exchange(ref _frameQueued, 1) == 1 || _queue.IsAddingCompleted) return;
        _queue.Add(() =>
        {
            Interlocked.Exchange(ref _frameQueued, 0);
            var frame = Interlocked.Exchange(ref _pendingFrame, null);
            if (frame != null && Protocol is { SupportsRealtime: true } p) p.StreamFrame(frame);
        });
    }

    /// <summary>Re-scan for the keyboard. Safe to call when nothing has changed.</summary>
    public void Refresh()
    {
        if (_queue.IsAddingCompleted) return;
        _queue.Add(() =>
        {
            var links = KeyboardFinder.Find();
            // Prefer the cable. A dongle stays enumerated even while its keyboard is off or wired.
            var best = links.FirstOrDefault(l => l.Kind == LinkKind.Usb) ?? links.FirstOrDefault();
            if (best?.Control.Path == Link?.Control.Path) return;
            Close();
            if (best != null) Open(best);
            ConnectionChanged?.Invoke();
        });
    }

    /// <summary>Use an already-built protocol. This is how tests and <c>--simulate</c> attach.</summary>
    public void Attach(IKeyboardProtocol protocol, KeyboardLink link)
    {
        _queue.Add(() =>
        {
            Protocol = protocol;
            Link = link;
            ConnectionChanged?.Invoke();
        });
    }

    private void Open(KeyboardLink link)
    {
        var control = new WinHidPort(link.Control.Path);
        _ports.Add(control);
        IHidPort? screen = null;
        if (link.Screen != null)
        {
            try { screen = new WinHidPort(link.Screen.Path); _ports.Add(screen); }
            catch (Exception e) { Error?.Invoke(e); }
        }
        Protocol = link.Kind == LinkKind.Usb ? new WiredProtocol(control, screen) : new WirelessProtocol(control);
        Link = link;
        if (link.Notify != null)
        {
            try { StartNotifications(new WinHidPort(link.Notify.Path)); }
            catch (Exception e) { Error?.Invoke(e); }
        }
    }

    private void StartNotifications(WinHidPort port)
    {
        _ports.Add(port);
        var cts = _notifyCts = new CancellationTokenSource();
        var thread = new Thread(() =>
        {
            var buffer = new byte[4];
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    if (port.Read(buffer, 1000) > 0 && TryParseHostKey(buffer, out var id))
                        HostKeyPressed?.Invoke(id);
                }
                catch (Exception) { return; } // Unplugged. The next device scan cleans up.
            }
        }) { IsBackground = true, Name = "keyboard-notify" };
        thread.Start();
    }

    /// <summary><c>05 xx id ..</c> is a host-side key press. Second byte A6 marks dongle link status instead.</summary>
    public static bool TryParseHostKey(ReadOnlySpan<byte> report, out byte id)
    {
        id = 0;
        if (report.Length < 3 || report[0] != 5 || report[1] == 0xA6) return false;
        id = report[2];
        return id != 0;
    }

    private void Close()
    {
        _notifyCts?.Cancel();
        _notifyCts = null;
        foreach (var p in _ports) { try { p.Dispose(); } catch (Exception) { } }
        _ports.Clear();
        Protocol = null;
        Link = null;
    }

    public void Dispose()
    {
        _queue.Add(Close);
        _queue.CompleteAdding();
        _worker.Join(2000);
    }
}
