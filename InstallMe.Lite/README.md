# InstallMe.Lite

InstallMe.Lite is a local GUI installer for Notepad Pro. It installs the published Notepad Pro files to a chosen folder (default C:\Apps\NotepadPro), creates Desktop and Start Menu shortcuts, and can uninstall the installation while preserving user data in %LOCALAPPDATA%\NotepadPro.

Features

- GUI-based install and uninstall
- Creates Desktop and Start Menu shortcuts using native Windows shortcuts
- Registers under Add/Remove Programs (HKCU) with an UninstallString
- Safer uninstall: attempts to stop running processes, retries deletions, schedules cleanup on reboot if needed

Usage

- Run the executable. The installer extracts the embedded Notepad Pro package zip and installs it to the selected target folder (default C:\\Apps\\NotepadPro).
- Click Install to copy files and register the app.
- Click Uninstall to remove installed files and registry keys.

Building a self-contained single EXE for testing

```powershell
# From repository root
cd InstallMe.Lite
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true -o ..\publish\InstallMe.Lite
```

Notes

- This project is intentionally kept local and excluded from git by default during development.
- The uninstaller schedules deletion on reboot if files are locked and cannot be removed immediately.

Portability / Retargeting

- Most app-specific installer settings are centralized in [InstallerProfile.cs](InstallerProfile.cs).
- To reuse this installer for another app, update the values in `InstallerProfile.cs` (display name, exe/process names, AppUserModelId, default path, uninstall key, shortcut names, package resource candidates).
- Project metadata and embedded package paths are also centralized in [InstallMe.Lite.csproj](InstallMe.Lite.csproj) via `InstallerTargetAppName`, `InstallerPublisher`, `InstallerVersion`, `PrimaryEmbeddedPackagePath`, and `FallbackEmbeddedPackagePath`.
- You can override those `.csproj` values without editing the project file: copy [Directory.Build.props.template](Directory.Build.props.template) to `Directory.Build.props` in `InstallMe.Lite/` and update the values.

Authorship: AnotherLaughingMan
