using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;
using OpenAK820.Device;
using OpenAK820.Model;
using OpenAK820.Protocol;

namespace OpenAK820.Services;

public sealed class SavedState
{
    public List<Profile> Profiles { get; set; } = new() { new Profile { Name = "Default" } };
    public int ActiveProfile { get; set; }
    /// <summary>Macros are shared by all profiles, as in the vendor software.</summary>
    public List<Macro> Macros { get; set; } = new();
    public List<ScreenFrame> ScreenFrames { get; set; } = new();
    public string Language { get; set; } = "";
    public bool MinimizeToTray { get; set; } = true;
}

/// <summary>Everything the pages share: saved data, the keyboard connection, and common device actions.</summary>
public sealed class AppState
{
    /// <summary>OPENAK820_DATA redirects storage, which keeps tests and simulator runs away from real settings.</summary>
    public static string DataFolder => Environment.GetEnvironmentVariable("OPENAK820_DATA") is { Length: > 0 } custom
        ? custom
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenAK820");
    public static string ScreenFolder => Path.Combine(DataFolder, "screen");
    private static string StatePath => Path.Combine(DataFolder, "state.json");
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public SavedState Saved { get; private set; } = new();
    public KeyboardService Keyboard { get; } = new();
    public Profile Profile => Saved.Profiles[Math.Clamp(Saved.ActiveProfile, 0, Saved.Profiles.Count - 1)];
    public int? Battery { get; set; }

    public event Action? ProfileChanged;
    public event Action<string, bool>? Status;

    public void Report(string message, bool isError = false) => Status?.Invoke(message, isError);
    public void RaiseProfileChanged() => ProfileChanged?.Invoke();

    public void Load()
    {
        try
        {
            if (File.Exists(StatePath))
                Saved = JsonSerializer.Deserialize<SavedState>(File.ReadAllText(StatePath), Json) ?? new SavedState();
        }
        catch (Exception e)
        {
            // Keep the unreadable file so nothing is lost, and start fresh.
            try { File.Move(StatePath, StatePath + ".bad", true); } catch (IOException) { }
            Report("Saved settings could not be read: " + e.Message, true);
        }
        if (Saved.Profiles.Count == 0) Saved.Profiles.Add(new Profile { Name = "Default" });
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            var temp = StatePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Saved, Json));
            File.Move(temp, StatePath, true);
        }
        catch (Exception e) { Report("Could not save settings: " + e.Message, true); }
    }

    /// <summary>Runs a keyboard action, reporting the outcome. Returns true only if the keyboard accepted it.</summary>
    public async Task<bool> SendAsync(string doneMessage, Action<IKeyboardProtocol> work)
    {
        try
        {
            if (!await Keyboard.RunAsync(work))
            {
                Report(Ui.Loc.T("Saved. No keyboard is connected, so nothing was sent."));
                return false;
            }
            Report(doneMessage);
            return true;
        }
        catch (Exception e)
        {
            Report(e.Message, true);
            return false;
        }
    }

    /// <summary>
    /// Sets the screen clock when a USB keyboard connects, as the vendor app does (0x434810). On real
    /// hardware a keyboard restarted on USB shows its stored animation until the clock is set.
    /// A failure here isn't worth interrupting anyone: the next connection tries again.
    /// </summary>
    public async Task SyncClockOnConnectAsync()
    {
        if (Keyboard.Link?.Kind != LinkKind.Usb) return;
        try { await Keyboard.RunAsync(k => k.SyncClock(DateTime.Now)); }
        catch (Exception e) when (e is ProtocolException or IOException or System.ComponentModel.Win32Exception) { }
    }

    /// <summary>Writes the whole active profile to the keyboard. This rewrites its flash, so only do it on request.</summary>
    public Task<bool> ApplyProfileAsync()
    {
        var p = Profile;
        var macros = Saved.Macros.ToList();
        return SendAsync(Ui.Loc.T("Profile written to keyboard."), k =>
        {
            k.SetMacros(macros);
            k.SetKeyTable(0, Encoders.KeyTable(p.Layers[0], 0));
            k.SetKeyTable(1, Encoders.KeyTable(p.Layers[1], 1));
            k.SetConfig(p.Config);
            k.SetLighting(p.Lighting);
        });
    }

    /// <summary>Returns the keyboard to defaults: empty key tables and macros, default lighting and settings.</summary>
    public Task<bool> ResetKeyboardAsync()
    {
        return SendAsync(Ui.Loc.T("Keyboard reset."), k =>
        {
            if (k is WirelessProtocol wireless) wireless.FactoryReset();
            k.SetMacros(Array.Empty<Macro>());
            k.SetKeyTable(0, Encoders.KeyTable(new Dictionary<int, KeyAction>(), 0));
            k.SetKeyTable(1, Encoders.KeyTable(new Dictionary<int, KeyAction>(), 1));
            k.SetConfig(new DeviceConfig());
            k.SetLighting(new LightingState());
        });
    }

    public static IReadOnlyDictionary<int, Rgb> ToColors(PerKeyScheme scheme)
    {
        var d = new Dictionary<int, Rgb>();
        foreach (var (index, hex) in scheme.Colors) d[index] = Rgb.Parse(hex);
        return d;
    }
}

public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "OpenAK820";

    public static bool Enabled
    {
        get
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(Name) != null;
        }
        set
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) k.SetValue(Name, $"\"{Environment.ProcessPath}\" --hide");
            else k.DeleteValue(Name, false);
        }
    }
}

/// <summary>Programs known to drive this keyboard. Two at once corrupt each other's traffic.</summary>
public static class ConflictDetector
{
    private static readonly (string Process, string Name)[] Known =
    {
        ("SignalRgb", "SignalRGB"), ("DeviceDriver", "the AJAZZ driver"), ("OpenRGB", "OpenRGB"),
    };

    public static List<string> Find()
    {
        var found = new List<string>();
        foreach (var (process, name) in Known)
        {
            var list = Process.GetProcessesByName(process);
            if (list.Length > 0) found.Add(name);
            foreach (var p in list) p.Dispose();
        }
        return found;
    }
}

/// <summary>Performs the actions for keys the keyboard hands back to the PC.</summary>
public sealed class HostActions
{
    private readonly AppState _state;
    public HostActions(AppState state) => _state = state;

    public void Run(byte id)
    {
        var action = _state.Profile.FindHostAction(id);
        if (action == null) return;
        try
        {
            switch (action.Kind)
            {
                case KeyActionKind.OpenProgram:
                    Process.Start(new ProcessStartInfo(action.Text) { UseShellExecute = true });
                    break;
                case KeyActionKind.OpenWebsite:
                    var url = action.Text.Contains("://", StringComparison.Ordinal) ? action.Text : "https://" + action.Text;
                    // Only web addresses: a saved profile should not be able to launch arbitrary URL handlers.
                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                    break;
                case KeyActionKind.SendText:
                    InputSender.SendText(action.Text);
                    break;
                case KeyActionKind.SwitchProfile:
                    if (action.Value >= 0 && action.Value < _state.Saved.Profiles.Count)
                    {
                        _state.Saved.ActiveProfile = action.Value;
                        _state.Save();
                        _state.RaiseProfileChanged();
                        _ = _state.ApplyProfileAsync();
                    }
                    break;
            }
        }
        catch (Exception e) { _state.Report(e.Message, true); }
    }
}
