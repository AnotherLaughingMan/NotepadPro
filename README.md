# Notepad Pro

A modern, Monaco-powered text and code editor for Windows — built with Avalonia and WebView2.

Notepad Pro brings VS Code-quality editing to a lightweight native desktop app. It is designed for developers, writers, and power users who want a fast, themeable editor without the overhead of a full IDE.

---

## Features

### Editor
- Full [Monaco Editor](https://microsoft.github.io/monaco-editor/) (the same engine that powers VS Code) with syntax highlighting for 60+ languages
- Auto indentation, bracket matching, word wrap, whitespace rendering, and minimap with configurable fade behavior
- Language-aware tab icons so files are recognizable at a glance
- Unsaved-change indicators on tabs with `Save / Save As / Don't Save / Cancel` close prompts

### Navigation
- **Goto Anything** (`Ctrl+P`) — search open tabs, recent files, workspace files, bookmarks, `:line:column` jumps, `@symbols`, and `#text` matches
- **Command Palette** (`Ctrl+Shift+P`) — all editor, settings, theme, encoding, and formatting actions in one picker
- **Bookmarks** — add, remove, navigate, search, and filter bookmarks; import/export per workspace; resilient to file renames via content fingerprinting

### Explorer & Workspace
- File Explorer with flat VS Code-style folder listing, pinned tabs, reveal-in-explorer, tab context menus
- Workspace and folder bookmark scopes that auto-swap when context changes
- Fold All / Unfold All from the Explorer workspace-title context menu

### Themes
| Name | Variant |
|---|---|
| Dark+ | Dark |
| Dark Modern | Dark |
| Dark High Contrast | Dark |
| One Dark Pro | Dark |
| Monokai Pro | Dark |
| Solarized Dark | Dark |
| Goth | Dark |
| Vampire | Dark |
| Sand | Dark |
| Peach Sunset Soft | Light |
| Peach Sunset Light | Light |
| Light+ | Light |

### Markdown
- Live Markdown preview panel
- Floating or pinned Markdown toolbar with modifier shortcuts
- Toolbar shortcuts reference under **Help > Markdown Toolbar Shortcuts**

### Settings & Appearance
- Activity Bar position: Left, Right, or Hidden
- Primary panel position: Left or Right
- Configurable font size, zoom, word wrap, minimap, minimap fade speed, scrollbar opacity, line numbers, render whitespace, auto indent, auto close brackets
- Settings persist across sessions

---

## Requirements

- **Windows 10 or 11** (x64)
- **[Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)** — already installed on Windows 11 and most up-to-date Windows 10 machines
- **.NET 9 Runtime** (framework-dependent build) — download from [dot.net](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

> **Self-contained builds** (bundling the .NET runtime) are not currently published. If you do not have .NET 9 installed, get it from the link above.

---

## Installation

1. Download the latest installer (`NotepadPro-v*.exe`) from the [Releases](https://github.com/AnotherLaughingMan/NotepadPro/releases) page.
2. Run the installer.

> ### ⚠️ Windows SmartScreen Warning
>
> Because this installer is not code-signed, **Windows SmartScreen will block it** with a "Windows protected your PC" message.
>
> To proceed:
> 1. Click **More info** in the SmartScreen dialog.
> 2. Click **Run anyway**.
>
> This warning appears for any unsigned executable downloaded from the internet and does **not** indicate malware. You can inspect the source code in this repository to verify what is installed.

3. The installer places Notepad Pro in `C:\Apps\NotepadPro` by default (you can choose a different folder).
4. Desktop and Start Menu shortcuts are created automatically.
5. An entry is added to **Add or Remove Programs** so you can uninstall cleanly at any time.

---

## Keyboard Shortcuts

| Action | Shortcut |
|---|---|
| Goto Anything | `Ctrl+P` |
| Command Palette | `Ctrl+Shift+P` |
| New Tab | `Ctrl+N` |
| Open File | `Ctrl+O` |
| Save | `Ctrl+S` |
| Save As | `Ctrl+Shift+S` |
| Close Tab | `Ctrl+W` |
| Word Wrap | `Alt+Z` |
| Toggle Explorer | `Ctrl+Shift+E` |
| Toggle Search | `Ctrl+Shift+F` |
| Find / Replace | `Ctrl+H` |
| Go to Line | `Ctrl+G` |
| Next Tab | `Ctrl+Tab` |
| Previous Tab | `Ctrl+Shift+Tab` |

---

## Building from Source

**Prerequisites:** .NET 9 SDK, Node.js 20+, Git

```powershell
git clone https://github.com/AnotherLaughingMan/NotepadPro.git
cd NotepadPro

# Build the webview (Monaco editor bundle)
cd webview
npm install
npm run build          # outputs to NotepadPro/wwwroot/
cd ..

# Build the desktop app
dotnet build "Notepad Pro.sln" -c Debug
```

To publish a release build:

```powershell
dotnet publish NotepadPro/NotepadPro.csproj -c Release -r win-x64 --no-self-contained -o artifacts/publish/win-x64-framework-dependent
```

See [InstallMe.Lite/README.md](InstallMe.Lite/README.md) for instructions on building the installer.

---

## Contributing

Contributions, bug reports, and feature suggestions are welcome.

Please read the [Code of Conduct](CODE_OF_CONDUCT.md) before participating.

1. Fork the repository.
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Commit your changes with a clear message.
4. Open a pull request against `main`.

---

## License

[MIT](LICENSE) © 2026 AnotherLaughingMan
