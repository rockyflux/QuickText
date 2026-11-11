# QuickText

> A tray snippet manager and text expander for Windows

[简体中文](README.md) · **English**

Lives in the tray. Store email, signatures, templates, replies, prompts, Skills, images once — then in **any input box**, search or type an abbreviation and paste at the cursor.

- Single portable exe — **no account, offline by default** (update check and AI generate are opt-in)
- Data is local **JSON** (sync-drive friendly; plain text — use a password manager for secrets)
- Dark / light / follow Windows; **18 UI languages**; settings apply instantly

## Download

**[⬇ Get the latest release](http://172.16.68.178:8090/vb-coding/quicktext/-/releases)** — Windows x64, no installer.

| File | Needs | Notes |
| --- | --- | --- |
| `QuickText-<version>-win-x64.zip` | nothing | **take this if unsure** |
| `QuickText-<version>-win-x64-needs-dotnet10.zip` | [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) | much smaller |
| `QuickText.exe` | same as above | no zip — run or overwrite to update |

Unsigned: SmartScreen → **More info → Run anyway**.

## Quick start

1. Double-click `QuickText.exe` (stays in the tray; first run seeds a starter library)
2. `Ctrl+Shift+8` opens the panel → search → Enter pastes at the cursor
3. Tray → **Manager** to edit snippets; **Settings** for hotkey, theme, language, …

Same library also supports **text commands** (abbreviations), `{variable}` templates, optional **clipboard history** (default `Ctrl+Shift+V`), and **AI generate** in the Manager (bring your own key).

## Features

- **Search**: name / pinyin / abbreviation / body; browse by category; favorites and recent
- **Content**: text and images; per-snippet variable placeholders; optional code highlighting
- **Extras**: text commands, clipboard history (text, 3 days), Skills import, AI generate
- **Data**: local JSON, export/import backup, daily auto-backup, portable mode (data next to the exe)

## Build

Run `build.bat` from the repo root (menu: build / test / smoke / publish; or `build.bat 1`…`8` to pick directly). Or:

```bash
dotnet build QuickText.sln -c Debug
dotnet test  tests/QuickText.Core.Tests/QuickText.Core.Tests.csproj
dotnet run  --project src/QuickText.App
dotnet publish src/QuickText.App -c Release -p:PublishProfile=win-x64                 # runtime bundled
dotnet publish src/QuickText.App -c Release -p:PublishProfile=win-x64-needs-dotnet10  # needs .NET 10 Desktop Runtime
```

Requires the .NET 10 SDK. Windows only.
