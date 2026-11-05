using OpenAK820.Device;
using OpenAK820.Hid;
using OpenAK820.Protocol;
using OpenAK820.Services;
using OpenAK820.Ui;

namespace OpenAK820;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var hide = args.Contains("--hide");
        var simulate = args.Contains("--simulate");
        var shotAt = Array.IndexOf(args, "--shot");
        var langAt = Array.IndexOf(args, "--lang");

        using var mutex = new Mutex(true, "OpenAK820.SingleInstance", out var first);
        if (!first && !simulate && shotAt < 0) return;

        // The simulator is for trying the app out, so it must never touch real saved settings.
        if (simulate || shotAt >= 0)
            Environment.SetEnvironmentVariable("OPENAK820_DATA", Path.Combine(Path.GetTempPath(), "OpenAK820-simulator"));

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => MessageBox.Show(e.Exception.Message, "OpenAK820", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Application.SetColorMode(SystemColorMode.Dark);

        var state = new AppState();
        if (shotAt < 0) state.Load();
        Loc.Set(langAt >= 0 ? args[langAt + 1] : state.Saved.Language);

        using var form = new MainForm(state, hide, simulate || shotAt >= 0);
        if (simulate || shotAt >= 0) AttachSimulator(state);

        if (shotAt >= 0)
        {
            form.Load += (_, _) => TakeShots(form, args[shotAt + 1]);
        }
        Application.Run(form);
    }

    private static void AttachSimulator(AppState state)
    {
        var info = new HidInterfaceInfo("simulated", KeyboardFinder.Vendor, 0x800A, 0x110, 0xFF13, 1, 65, 65, 65, "SONiX", "AK820");
        var protocol = new WiredProtocol(new SimulatedPort(), new SimulatedPort(), 1);
        state.Keyboard.Attach(protocol, new KeyboardLink(LinkKind.Usb, info, info, null));
    }

    /// <summary>Renders each page to a PNG, for checking the layout without clicking through by hand.</summary>
    private static void TakeShots(MainForm form, string folder)
    {
        Directory.CreateDirectory(folder);
        form.BeginInvoke(async () =>
        {
            for (int i = 0; i < form.PageCount; i++)
            {
                form.SelectPage(i);
                await Task.Delay(250);
                using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
                form.DrawToBitmap(bitmap, form.ClientRectangle);
                bitmap.Save(Path.Combine(folder, $"page{i}.png"));
            }
            Environment.Exit(0);
        });
    }
}
