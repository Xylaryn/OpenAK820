# Security

## Reporting a problem

Please use GitHub's **Report a vulnerability** button on the [Security tab](../../security/advisories/new), so it stays private until it's fixed. Don't open a public issue for a security problem.

This is a small volunteer project in early alpha, so there's no guaranteed response time, and only the latest release is supported.

## What the app can do

| Capability | When | Why |
|---|---|---|
| Read and write the keyboard's HID interfaces | While a matching keyboard is connected | Its main job |
| Watch all keyboard and mouse input | Only while recording a macro | Macro recording |
| Read audio output | Only while music lighting runs | Music lighting, in memory only |
| Sample screen colours | Only while music lighting uses Ambilight | Colour matching, no images saved |
| Open programs and websites, and type text | When you press a key you assigned to do that | Key functions |
| Write to `HKCU\...\Run` | When you turn on Automatic Start | Starting with Windows |

It has no network code and doesn't need administrator rights.

## Imported files

Don't import profiles from people you don't trust. A profile can bind keys to programs or typed text, so the app shows those before importing and asks you to confirm. Website keys only accept `http` and `https`.

## Verifying a download

Release EXEs aren't code-signed, so Windows will warn you. Each release is built by GitHub Actions from the tagged source, and you can read the build log on the [Actions tab](../../actions). To compare your download with the checksum in the release notes:

```bash
certutil -hashfile OpenAK820-VERSION-win-x64.exe SHA256
```

## Keyboard safety

The app only talks to devices that match the exact IDs, product name and interface layout it expects, and never to a bootloader. It has no firmware-flashing code.
