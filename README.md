# Nuvo

**A screenshot tool straight from Mac, only on Windows.**

[Русская версия](README.ru.md)

Press `PrtScr`, select what you want, mark it up, and get a clean screenshot on a beautiful background with rounded corners and a soft shadow, already in your clipboard.

## Features

- **Global `PrtScr`** — Nuvo takes the key over from Snipping Tool.
- **Three selection modes** — area, window (hover to highlight, click to pick) or the whole desktop, including multiple monitors.
- **Markup** — arrow, rectangle and line, with a color picker and `Ctrl+Z` to undo.
- **Beautiful background** — 8 macOS-style gradients or your own color; the screenshot gets rounded corners and a shadow.
- **Instantly in the clipboard** — the result is copied automatically after every change, ready to paste. A PNG is placed there, so transparency survives in browsers and messengers.
- **Save as PNG** — the save button or `Ctrl+S`, files go to `Pictures\Nuvo`.
- **Dark UI**, tray icon, settings window (click the tray icon) and optional autostart.

## Shortcuts

| Key | Action |
| --- | --- |
| `PrtScr` | Start a screenshot |
| `Ctrl+Z` | Undo the last mark |
| `Ctrl+S` | Save as PNG and close |
| `Esc`, `Enter` or right click | Close the overlay |

## Build

Requires Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet build -c Release
```

Run `bin\Release\net8.0-windows\Nuvo.exe`. To publish a single self-contained file:

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Font

The UI uses Google Sans when available. Drop the `.ttf` files into the `Fonts` folder before building; otherwise Nuvo falls back to Segoe UI. Font files are not part of the repository.

## Notes

- Transparent corners around the card survive in PNG-aware apps. Apps that ignore alpha, such as Paint, get a copy flattened on white.
- Settings are stored in `%AppData%\Nuvo\settings.json`.
