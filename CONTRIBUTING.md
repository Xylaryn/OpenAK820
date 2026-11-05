# Contributing

Bug reports and test results from real keyboards are the most useful help right now, especially over 2.4 GHz and on other firmware. See the [test plan](docs/TESTING.md).

## Rules for this repository

**No vendor material.** Don't commit AJAZZ's installers, binaries, images, fonts, animations or translations, even small ones. Facts about how the hardware behaves are fine. Say where a finding came from in [docs/PROTOCOL.md](docs/PROTOCOL.md).

**No code copied from other projects** unless its license allows it and it's credited.

**No network code, telemetry or auto-updating.** The privacy promises in the [README](README.md#privacy) are part of the project.

**No firmware flashing.** The risk of leaving a keyboard unusable isn't worth it.

**Protect your own privacy.** Commits are public and permanent. Before committing, consider using your GitHub no-reply address for this repository, and check that logs or screenshots you share don't show your username or file paths:

```bash
git config user.email "ID+USERNAME@users.noreply.github.com"
```

Git also records your time zone in each commit. To stamp a commit in UTC instead, set `TZ=UTC` when committing from Git Bash.

## Making changes

1. Run the tests:
   ```bash
   dotnet test
   ```
2. If you change a byte layout or command sequence, update its golden test and [docs/PROTOCOL.md](docs/PROTOCOL.md), and say how you checked it on hardware.
3. If you add a feature, update [docs/USER-GUIDE.md](docs/USER-GUIDE.md), [docs/PARITY.md](docs/PARITY.md), [CHANGELOG.md](CHANGELOG.md) and the test plan.
4. Don't claim something works on a keyboard unless it was tried on one. Mark anything inferred as an open question.

## Safety when testing

Anything that writes to a keyboard can overwrite settings that can't be read back. Use `--simulate` where you can. Never send a command whose purpose you haven't worked out, because an unknown command could do anything, including entering firmware-update mode.

## Style

- Keep dependencies at zero at runtime.
- Keep protocol code in `OpenAK820.Core`, free of UI.
- Write user-facing text in plain language, and pass it through `Loc.T` so it can be translated.
