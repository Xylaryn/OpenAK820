using OpenAK820.Hid;
using OpenAK820.Model;

namespace OpenAK820.Protocol;

public class ProtocolException(string message) : Exception(message);

/// <summary>Another program is sending to the keyboard at the same time, so this exchange cannot be trusted.</summary>
public sealed class KeyboardBusyException() : ProtocolException(
    "Another program is talking to the keyboard (often SignalRGB or the vendor driver). Close it and try again.");

/// <summary>Every operation the app can ask of the keyboard. Calls block; run them on the service worker.</summary>
public interface IKeyboardProtocol
{
    bool SupportsScreen { get; }
    bool SupportsRealtime { get; }
    void SetLighting(LightingState state);
    void SetPerKey(IReadOnlyDictionary<int, Rgb> colors, int brightness);
    void SetKeyTable(int layer, byte[] table);
    void SetMacros(IReadOnlyList<Macro> macros);
    void SetConfig(DeviceConfig config);
    void SyncClock(DateTime now, int slot = 1);
    void UploadScreen(byte[] image, int pages, int slot, Action<int>? progress);
    /// <summary>Shows one temporary frame. Returns false if the keyboard dropped it.</summary>
    bool StreamFrame(IReadOnlyDictionary<int, Rgb> colors);
    /// <summary>Reads the 576-byte table of currently displayed key colours. Null if unavailable.</summary>
    byte[]? ReadLiveColors();
    /// <summary>Battery percentage, or null when the connection does not report one.</summary>
    int? ReadBattery();
}

/// <summary>
/// USB: 65-byte feature reports on the FF13 collection, plus 4097-byte output reports on FF68 for the screen.
/// Timings and sequences mirror the vendor application.
/// </summary>
public sealed class WiredProtocol : IKeyboardProtocol
{
    private readonly IHidPort _control;
    private readonly IHidPort? _screen;
    private readonly int _delayMs;
    private readonly Action<int> _sleep;

    public WiredProtocol(IHidPort control, IHidPort? screen, int commandDelayMs = 35, Action<int>? sleep = null)
    {
        _control = control;
        _screen = screen;
        _delayMs = commandDelayMs;
        _sleep = sleep ?? Thread.Sleep;
    }

    public bool SupportsScreen => _screen != null;
    public bool SupportsRealtime => true;

    private static byte[] Report(byte[] frame)
    {
        var r = new byte[65];
        Array.Copy(frame, 0, r, 1, Math.Min(frame.Length, 64));
        return r;
    }

    /// <summary>Send a command and check the status byte of the echoed reply.</summary>
    private void Acked(byte[] frame)
    {
        _sleep(_delayMs);
        var report = Report(frame);
        _control.SetFeature(report);
        _sleep(_delayMs);
        var reply = new byte[65];
        _control.GetFeature(reply);
        // The reply channel echoes whichever frame the keyboard received last, from any program.
        // Without this check, another app's traffic could be mistaken for our own success.
        if (reply[1] != frame[0] || reply[2] != frame[1])
            throw new KeyboardBusyException();
        if (reply[4] != 1)
            throw new ProtocolException($"Keyboard refused command {frame[1]:X2} (status {reply[4]:X2}).");
    }

    /// <summary>Data frames get a reply too, but only commands carry a meaningful status.</summary>
    private void DataWithReply(byte[] frame)
    {
        _sleep(_delayMs);
        _control.SetFeature(Report(frame));
        _sleep(_delayMs);
        _control.GetFeature(new byte[65]);
    }

    /// <summary>Opening a session can be refused for a moment while the keyboard finishes earlier work.</summary>
    private void Open(byte cmd)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { Acked(Encoders.Command(cmd)); return; }
            catch (ProtocolException) when (attempt < 30) { _sleep(100); }
        }
    }

    private void Unacked(byte[] frame)
    {
        _sleep(_delayMs);
        _control.SetFeature(Report(frame));
    }

    private void Bulk(byte[] data)
    {
        _sleep(_delayMs);
        for (int at = 0; at < data.Length; at += Encoders.FrameSize)
        {
            var report = new byte[65];
            Array.Copy(data, at, report, 1, Math.Min(Encoders.FrameSize, data.Length - at));
            for (int attempt = 0; ; attempt++)
            {
                try { _control.SetFeature(report); break; }
                catch (System.ComponentModel.Win32Exception) when (attempt < 10) { _sleep(_delayMs); }
            }
            _sleep(_delayMs);
        }
    }

    private const byte CmdStart = 0x18, CmdCommit = 0x02, CmdEnd = 0xF0, CmdLighting = 0x13, CmdConfig = 0x17,
        CmdMacroStart = 0x19, CmdMacroData = 0x15, CmdPerKey = 0x23, CmdLive = 0x20, CmdClock = 0x28, CmdScreen = 0x72, CmdReadLive = 0xF5;

    public static byte KeyTableCommand(int layer) => (byte)(layer == 0 ? 0x11 : 0x27);

    public void SetLighting(LightingState s) => Session(() =>
    {
        Acked(Encoders.Command(CmdLighting, 0, 1));
        Unacked(Encoders.LightingData(s));
    });

    public void SetPerKey(IReadOnlyDictionary<int, Rgb> colors, int brightness)
    {
        Session(() =>
        {
            Acked(Encoders.Command(CmdLighting, 0, 1));
            Unacked(Encoders.UserLightingMode(brightness));
        });
        Session(() =>
        {
            Acked(Encoders.Command(CmdPerKey, 0, Encoders.TableSize / Encoders.FrameSize));
            Bulk(Encoders.PerKeyTable(colors));
        });
    }

    public void SetKeyTable(int layer, byte[] table) => Session(() =>
    {
        Acked(Encoders.Command(KeyTableCommand(layer), 0, Encoders.TableSize / Encoders.FrameSize));
        Bulk(table);
    });

    /// <summary>Wraps a write in START ... COMMIT ... END. END is sent even if something failed.</summary>
    private void Session(Action body)
    {
        Open(CmdStart);
        try
        {
            body();
            Acked(Encoders.Command(CmdCommit));
        }
        finally
        {
            try { Unacked(Encoders.Command(CmdEnd)); } catch (Exception) { /* the original failure matters more */ }
        }
    }

    public void SetMacros(IReadOnlyList<Macro> macros)
    {
        var image = Encoders.MacroImage(macros, out var pages);
        Open(CmdMacroStart);
        Acked(Encoders.Command(CmdMacroData, 0, pages));
        _sleep(30);
        Bulk(image);
        _sleep(30);
        Acked(Encoders.Command(CmdCommit));
    }

    public void SetConfig(DeviceConfig c)
    {
        Open(CmdStart);
        Acked(Encoders.Command(CmdConfig, 1, 1));
        Unacked(Encoders.ConfigData(c));
        Acked(Encoders.Command(CmdCommit));
    }

    public void SyncClock(DateTime now, int slot = 1)
    {
        Open(CmdStart);
        Acked(Encoders.Command(CmdClock, 0, 1));
        DataWithReply(Encoders.ClockData(now, slot));
        Acked(Encoders.Command(CmdCommit));
    }

    public void UploadScreen(byte[] image, int pages, int slot, Action<int>? progress)
    {
        if (_screen == null) throw new ProtocolException("The screen interface is not available.");
        Open(CmdStart);
        Acked(Encoders.Command(CmdScreen, (byte)slot, pages));
        var page = new byte[Encoders.ScreenPageSize + 1];
        var reply = new byte[65];
        for (int i = 0; i < pages; i++)
        {
            Array.Copy(image, i * Encoders.ScreenPageSize, page, 1, Encoders.ScreenPageSize);
            _screen.Write(page);
            // The keyboard acknowledges each page. Stop rather than send data it may not have room for.
            if (_screen.Read(reply, 300) <= 0 && _screen.Read(reply, 1000) <= 0)
                throw new ProtocolException($"Keyboard stopped acknowledging the upload at page {i + 1} of {pages}.");
            progress?.Invoke((i + 1) * 100 / pages);
        }
        Acked(Encoders.Command(CmdCommit));
    }

    public bool StreamFrame(IReadOnlyDictionary<int, Rgb> colors)
    {
        // Fast path used by the vendor app for animation: short delays, one retry on status FF.
        _sleep(5);
        var start = Report(Encoders.Command(CmdLive, 0, Encoders.RealtimeSize / Encoders.FrameSize));
        _control.SetFeature(start);
        _sleep(5);
        var reply = new byte[65];
        _control.GetFeature(reply);
        if (reply[4] == 0xFF)
        {
            _control.SetFeature(start);
            _sleep(5);
            _control.GetFeature(reply);
        }
        if (reply[4] != 1) return false; // Dropping a frame is fine; the next one is moments away.
        var frame = Encoders.RealtimeFrame(colors);
        for (int at = 0; at < frame.Length; at += Encoders.FrameSize)
        {
            var report = new byte[65];
            Array.Copy(frame, at, report, 1, Encoders.FrameSize);
            _control.SetFeature(report);
            _sleep(2);
        }
        _control.SetFeature(Report(Encoders.Command(CmdCommit)));
        _sleep(5);
        _control.GetFeature(reply);
        return reply[1] == 0x04 && reply[2] == CmdCommit && reply[4] == 1;
    }

    public byte[]? ReadLiveColors()
    {
        const int Frames = 9;
        var request = Report(Encoders.Command(CmdReadLive, 0, Frames));
        _sleep(3);
        _control.SetFeature(request);
        _sleep(5);
        var reply = new byte[65];
        _control.GetFeature(reply);
        if (reply[1] != 0x04 || reply[2] != CmdReadLive) throw new KeyboardBusyException();
        if (reply[4] != 1) return null;
        var data = new byte[Frames * Encoders.FrameSize];
        for (int i = 0; i < Frames; i++)
        {
            _sleep(3);
            _control.GetFeature(reply);
            // Frames that echo a command, or records whose index is out of place, are someone else's traffic.
            if (reply[1] == 0x04 && reply[2] is CmdLive or CmdCommit or CmdStart) throw new KeyboardBusyException();
            Array.Copy(reply, 1, data, i * Encoders.FrameSize, Encoders.FrameSize);
        }
        // The vendor app closes this exchange the same way. No write session is open,
        // so the keyboard answers with status 00 (seen on real hardware); that is not an error.
        DataWithReply(Encoders.Command(CmdCommit));
        return data;
    }

    public int? ReadBattery() => null;
}

/// <summary>
/// 2.4 GHz dongle: 33-byte output reports on FF60. Each request is checked against an echo of its first three bytes.
/// </summary>
public sealed class WirelessProtocol : IKeyboardProtocol
{
    private readonly IHidPort _control;
    private readonly Action<int> _sleep;

    public WirelessProtocol(IHidPort control, Action<int>? sleep = null)
    {
        _control = control;
        _sleep = sleep ?? Thread.Sleep;
    }

    public bool SupportsScreen => false;
    public bool SupportsRealtime => false;

    private byte[] Exchange(byte[] frame)
    {
        try { _control.Write(frame); }
        catch (Exception) { _sleep(10); _control.Write(frame); }
        _sleep(5);
        var reply = new byte[Encoders.WirelessSize];
        for (int attempt = 0; attempt < 20; attempt++)
        {
            var n = _control.Read(reply, 25);
            if (n > 0)
            {
                // Windows puts the report ID first on some paths. Accept both shapes.
                int at = reply[0] == frame[1] && reply[1] == frame[2] ? 0 : 1;
                if (reply[at] == frame[1] && reply[at + 1] == frame[2] && reply[at + 2] == frame[3])
                {
                    if (at == 0) return reply;
                    var shifted = new byte[Encoders.WirelessSize];
                    Array.Copy(reply, 1, shifted, 0, Encoders.WirelessSize - 1);
                    return shifted;
                }
            }
            _sleep(10);
        }
        throw new ProtocolException($"No reply from keyboard for command {frame[1]:X2}. Is it awake and in 2.4 GHz mode?");
    }

    private void Chunks(byte cmd, byte[] data, int? declared = null)
    {
        foreach (var chunk in Encoders.WirelessChunks(cmd, data, declared))
        {
            Exchange(chunk);
            _sleep(2);
        }
    }

    public void SetLighting(LightingState s) => Exchange(Encoders.WirelessLighting(s));

    public void SetPerKey(IReadOnlyDictionary<int, Rgb> colors, int brightness)
    {
        Exchange(Encoders.WirelessUserMode(brightness));
        Chunks(0x14, Encoders.PerKeyTable(colors));
    }

    public void SetKeyTable(int layer, byte[] table) => Chunks((byte)(layer == 0 ? 0x10 : 0x12), table);

    public void SetMacros(IReadOnlyList<Macro> macros)
    {
        var image = Encoders.WirelessMacroImage(macros, out var used);
        Chunks(0x09, image, used);
    }

    public void SetConfig(DeviceConfig c) => Exchange(Encoders.WirelessConfig(c));
    public void SyncClock(DateTime now, int slot = 1) => Exchange(Encoders.WirelessClock(now, slot));

    public void UploadScreen(byte[] image, int pages, int slot, Action<int>? progress) =>
        throw new ProtocolException("Screen upload needs the USB cable.");

    public bool StreamFrame(IReadOnlyDictionary<int, Rgb> colors) =>
        throw new ProtocolException("Real-time lighting needs the USB cable.");

    public byte[]? ReadLiveColors() => null;

    public int? ReadBattery()
    {
        var reply = Exchange(Encoders.Seal(Encoders.Wireless(0x20, 0x01)));
        if (reply[0] != 0x20 || reply[3] == 0) return null;
        return Math.Min((int)reply[3], 100);
    }

    /// <summary>Asks the keyboard to clear its stored settings.</summary>
    public void FactoryReset() => Exchange(Encoders.Seal(Encoders.Wireless(0x1F)));

    public void SendMusic(int foreground, int background, int brightness, byte[] bands) =>
        Exchange(Encoders.WirelessMusic(foreground, background, brightness, bands));
}
