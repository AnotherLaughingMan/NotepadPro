# What's New in Notepad Pro

This file covers user-facing changes in plain language.
For the full technical changelog, see [CHANGELOG.md](CHANGELOG.md).

---

## Version 1.1.9574 — March 19, 2026

### New Features

**Goto Anything (`Ctrl+P`)**
Open anything from one keypress. Search open tabs and recent files by name, jump straight to a line with `:123`, search for symbols with `@`, or search inside files with `#`. Works across the active workspace or folder.

**Command Palette (`Ctrl+Shift+P`)**
Every editor action, theme switch, encoding change, and settings toggle is now reachable from the keyboard without digging through menus.

**Bookmarks — now much more powerful**
- Bookmark any line and navigate between them with keyboard shortcuts.
- Bookmarks are saved per workspace or folder and swap automatically when you switch projects.
- Import and export bookmark sets as portable JSON files.
- A dedicated Bookmarks panel lets you search, filter, and sort across all bookmarks.
- Bookmarks survive file renames by matching content fingerprints.

**Pinned tabs**
Pin a tab to keep it at the front of the strip. Pin state is visible in the Explorer open-editors list.

**Duplicate tab**
Duplicate the active file into a new tab from the tab context menu.

**Minimap fade**
The minimap fades out when you're not using it and snaps back in when you scroll or hover. Fade speed is adjustable in Settings > Editor.

**Activity Bar position**
Move the activity bar to the left, right, or hide it entirely from View > Appearance.

**Primary Panel position**
Dock the Explorer/Search/Bookmarks panel on the left or right side.

**Markdown Toolbar Shortcuts reference**
Quick reference for Markdown toolbar modifier behavior is now under Help > Markdown Toolbar Shortcuts.

**Fold All / Unfold All**
Right-click the folder title in the Explorer panel to collapse or expand everything at once.

---

### Improvements

- `Ctrl+P` now opens Goto Anything. Print is still available from File > Print.
- Bookmark management moved from keyboard-only to the Edit menu and Command Palette.
- Explorer folder listing switched to a flat VS Code-style model, fixing expansion state getting lost after restarts.
- Tab icons replaced with language-aware Fluent icons — code and markup files no longer fall back to a generic TXT badge.
- Status bar file type and cursor position stay in sync as you switch tabs.
- AXAML files are now recognized as AXAML, not mislabeled as XML.
- Close prompts now offer `Save`, `Save As...`, `Don't Save`, and `Cancel` for any unsaved file. The `Don't Save` button includes a tooltip warning that changes will be discarded.
- View menu reorganized into logical sections matching VS Code.
- Help > About now shows framework and API information plus author credit.

---

### Bug Fixes

- Opening a bookmark now scrolls the editor to the exact bookmarked line, not just the file.
- The line-number gutter no longer jumps or shifts text when bookmark markers appear.
- The floating Markdown toolbar no longer appears behind the editor surface.
- The floating Markdown toolbar popup position and drag bounds are now correct — it no longer appears offset on first open.
- Floating Markdown toolbar position is no longer overwritten when the window resizes or you switch tabs while it is hidden.
- The Markdown toolbar pin toggle in the View menu now stays enabled while the toolbar is already pinned.
- The scrollbar opacity setting in Settings was missing its label — it now shows a proper label.
- The Explorer and Search panel scrollbars now follow the Scrollbar Opacity setting, matching the editor scrollbar.
- Editor scrollbars now respond immediately when you change Scrollbar Opacity in Settings.
- New untitled tabs no longer show as unsaved (dirty) before you type anything.
- The close-prompt dialog is wider so the Cancel button is no longer crowded against the edge.
- Monaco syntax highlighting works correctly for all supported languages in the released build.
- The app no longer generates an assembly conflict warning during builds.
