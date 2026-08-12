# Notepad Pro v1.3.0

## Version 1.3.0 — July 29, 2026

### Critical Fixes

**Saving reliability overhaul**

- Fixed a long-standing issue where the application could save empty files in multiple scenarios.
- Save, Save As, Save All, Close prompts, and auto-save on focus/window change now correctly fetch the latest editor content before writing.
- Added multiple safety checks that prevent writing an empty buffer when the document reports unsaved changes.
- New documents using "Save As" are now properly protected; the save will only proceed when real content is available.
- Increased the timeout for retrieving editor content from the webview to improve reliability on slower systems.

These changes eliminate the risk of accidentally overwriting a file with nothing or creating empty new files.

### Release Assets

- Installer (InstallMe.Lite): `NotepadPro-v1.3.0-20260729-074922.exe`
- App package (framework-dependent, win-x64): `NotepadPro-v1.3.0-20260729-073952.zip`

---

**Full changelog and technical details:** See `CHANGELOG.md` in the repository.
