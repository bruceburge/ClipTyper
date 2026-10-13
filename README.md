# ClipTyper

A Windows tray utility that **types your clipboard into any window**, for the
fields that block paste: VM console logon screens, installers, KVM and remote
sessions, anywhere Ctrl+V does nothing. Copy the text, click into the target
field, press the hotkey, and the text arrives as real keystrokes.

It is the successor to [ClipStrip](https://github.com/bruceburge/ClipStrip)
(2015), with the emphasis flipped: typing is the main feature now, and
ClipStrip's formatting stripper is included as an optional extra.

## Features

- **Type the clipboard** with `Ctrl+Alt+V`. Characters are sent as Unicode
  keystrokes (SendInput with KEYEVENTF_UNICODE), not physical key positions,
  so symbols survive keyboard layout differences between you and the target:
  exactly the case where VMConnect's "type clipboard text" garbles passwords.
- **Optional formatting stripper.** When enabled in the tray menu, anything
  copied with formatting (RTF or HTML) is immediately replaced with its plain
  text, so the next paste comes out clean. Off by default; turn it on only if
  you want it.
- **Tray menu** with a delayed type (3 seconds, so you can click the target
  field first) for times when a hotkey is not convenient.
- **Optional clear after typing.** Off by default: your clipboard is left
  exactly as it was. If you type passwords with it and want them gone
  afterwards, the toggle is in the tray menu.
- **Honest failures.** SendInput results are checked. If Windows refuses a
  keystroke, you get the real error in a notification instead of a success
  message, and your clipboard is never cleared after a failed type.

## Hotkeys

| Keys | Action |
| --- | --- |
| Ctrl+Alt+V | Type the clipboard into the focused window |
| Ctrl+Alt+Q | Quit |

## Settings

Two toggles, both in the tray right-click menu, both off by default:

- Strip formatting from copied text
- Clear clipboard after typing

They persist in `%AppData%\ClipTyper\settings.ini`. That file is the only
thing ClipTyper ever writes. Clipboard contents are never stored, logged, or
sent anywhere.

## Requirements

- Windows 10 or 11, x64
- .NET 8 Desktop Runtime

## Building

```text
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

The project sets `EnableWindowsTargeting`, so it also builds on a Linux or
macOS machine with the .NET 8 SDK for delivery to Windows.

Layout:

- `Program.cs`: entry point, single-instance guard
- `MainForm.cs`: hidden host form: tray icon, menu, hotkeys, clipboard listener
- `Typer.cs`: the SendInput typing engine
- `ClipboardStripper.cs`: the optional formatting stripper
- `Settings.cs`: the two persisted preferences
- `Properties\AssemblyInfo.cs`: hand-maintained assembly attributes

## A note on the typing engine

SendInput's `INPUT` structure has to match the native layout exactly,
including the `MOUSEINPUT` member of its union. Get that wrong and the
structure is 32 bytes instead of 40 on x64, Windows rejects every call with
error 87, and, if you do not check the return value, your app cheerfully
reports success while typing nothing. Ask me how I know. `Typer.cs` checks
every call.

## History

- **ClipStrip** (2015): stripped formatting from copied text, using the legacy
  clipboard viewer chain.
- **Clipboard Typer** (2026): a single-file utility built for one bad morning
  with Entra-joined Hyper-V VMs whose logon screens would not take a paste.
- **ClipTyper** (2026): the two merged as one modern .NET 8 project, typing
  first, stripping optional.

## License

MIT. See [LICENSE](LICENSE).
