**This is an early test release.** It's been tested on one keyboard, and not every feature has been tried in every connection mode.

## Getting started

1. Download **`OpenAK820-...-win-x64.exe`**. There's nothing to install, and .NET is included.
2. Close the SignalRGB app, OpenRGB and the AJAZZ software if they're running.
3. Connect your AK820 Pro by USB and open the EXE.

**Windows will probably warn you**, because the EXE isn't code-signed. Choose **More info**, then **Run anyway**.

**Before you change anything:** the app can't read what's already stored on your keyboard. Writing keys, macros or settings replaces what's there, and your old values can't be recovered.

If the app says **No keyboard found**, your board may use a different firmware. Press **Copy diagnostic info** on Home and paste it into a [bug report]({{REPO}}/issues/new/choose).

See the [User Guide]({{REPO}}/blob/main/docs/USER-GUIDE.md) and the [changelog]({{REPO}}/blob/main/CHANGELOG.md).

## Downloads

| File | Use it if |
|---|---|
| `OpenAK820-...-win-x64.exe` | **You want the normal download** (about 47 MB) |
| `OpenAK820-...-win-x64-needs-dotnet.exe` | You already have the .NET 10 Desktop Runtime (under 1 MB) |
| `ak820ctl-...-needs-dotnet.exe` | You want the command line tool |

