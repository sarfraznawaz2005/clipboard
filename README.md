# Clipboard

A simple, clean, offline clipboard history manager for Windows, built with WPF on .NET 8.

## Features

- Watches the clipboard and keeps a history of what you copy - text, HTML, rich text, and
  images. Default 300 items, configurable in Settings.
- Auto-clear old items by age (e.g. delete anything older than 30 days). Off by default,
  pinned items are never auto-cleared.
- Ctrl+Shift+V opens the history popup from anywhere.
- Click an entry (or select it with arrow keys + Enter) to copy it back. Auto-paste into the
  window you were just in is available as an opt-in setting - off by default.
- Search box to filter history.
- Pin entries so they're never trimmed or cleared.
- Skips copies tagged "exclude from clipboard monitoring" - the convention 1Password,
  Bitwarden, KeePass, and Windows' own credential UI use to keep secrets out of clipboard
  history tools. On by default, can be turned off in Settings.
- Runs from the system tray. Start with Windows, close to tray, minimize to tray, start
  minimized - all optional, all in Settings.
- Sharp at any display scale (tested at 250%).

## What this app does not do

- No network access of any kind. Nothing is sent anywhere, ever.
- No telemetry, analytics, or update checks.

## Data and privacy

All data stays on your machine, under your own user account:

- History and settings: `%APPDATA%\Clipboard\history.json` and `settings.json`, plain JSON,
  human-readable. Copied images are cached as `.png` files under `%APPDATA%\Clipboard\images\`.
- "Start with Windows" writes one value under
  `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`. No admin rights are used
  or required, and nothing is written to `HKEY_LOCAL_MACHINE`.

## Building

Requires the .NET 8 SDK (`dotnet --version` should report 8.x).

- `build.bat` (or `build.ps1`) - cleans, then publishes a slim, framework-dependent,
  single-file production exe to `publish\Clipboard.exe`. "Framework-dependent" means it needs
  the .NET 8 Desktop Runtime already on the machine that runs it - that's what keeps it small
  instead of bundling the whole runtime.
- `run.bat` (or `run.ps1`) - stops any already-running instance, cleans, then runs the app
  straight from source in dev mode (`dotnet run`, Debug config).

Debug builds never register the app for Windows startup on their own, even if that setting is
on - only an explicit Save in Settings applies that registry change.

## Project layout

```
src/Clipboard/       the app (WPF, .NET 8)
  Models/             ClipboardEntry, AppSettings - what's persisted
  Services/           storage, clipboard monitoring, global hotkey, auto-paste, tray, startup
  Views/              MainWindow (the history popup), Settings
```
