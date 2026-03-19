# Changelog

All notable changes to Notepad Pro will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## Release Checklist

Complete all items before cutting a new version:

- [ ] Confirm all intended changes are listed under Unreleased and grouped under Added, Changed, Fixed, Removed, Security as applicable.
- [ ] Update version in app metadata and any About dialog text.
- [ ] Run Debug build from solution root and confirm zero errors.
- [ ] Run Release build from solution root and confirm zero errors.
- [ ] Perform smoke test of core flows: open/save files, tab close prompts, search, bookmarks, and settings persistence.
- [ ] Update docs impacted by the release (README, roadmap, setup notes, shortcuts help).
- [ ] Move Unreleased entries into a new version section with release date.
- [ ] Create release artifacts and verify installer/startup behavior on a clean machine.

---

## [Unreleased]

### Added
- Placeholder for upcoming additions.

### Changed
- Placeholder for upcoming behavior changes.

### Fixed
- Placeholder for upcoming bug fixes.

## [1.1.9574] - 2026-03-19

### Added
- Added command palette quick picker for file, navigation, bookmark, recent reopen, theme, encoding, EOL, indentation, and editor-setting actions.
- Added Goto Anything quick-open flow covering open tabs, recent files, workspace or folder files, bookmarks, :line[:column] jumps, @symbols, and #text matches.
- Added bookmark list, clear-in-file, and clear-all actions alongside toggle, next, and previous navigation with visible in-editor bookmark markers.
- Added better open-editor affordances including pinned tabs, duplicate active-tab actions, reveal-in-explorer, tab context menu actions, and pinned-state visibility in the explorer open-editors list.
- Added workspace- and folder-scoped bookmark persistence so bookmark sets swap automatically when the active project context changes.
- Added explicit bookmark import and export for the current workspace or folder scope using a portable JSON format.
- Added decision prompts for bookmark exchange so import can merge or replace, warn about out-of-scope paths, and export can target either the current file or the current scope.
- Added a dedicated Bookmarks tool panel with separate scoped and global bookmark sections, plus quick actions for adding and removing either kind.
- Added bookmark panel search, filter, and sort controls for quickly isolating current-file, stale, scoped, or global bookmarks.
- Added bookmark path recovery across the active workspace or folder so bookmarks can survive file renames and moves when the content fingerprint still matches.
- Added View menu toggles for Line Numbers, Render Whitespace, Auto Indent, Auto Close Brackets, Explorer, Search, and Pin Markdown Toolbar.
- Added Word Wrap shortcut labeling (Alt+Z) and grouped related View menu editor toggles to match VS Code structure.
- Added persistence for RenderWhitespace in app settings and live sync to Monaco through the web bridge.
- Added startup diagnostics in the webview harness so missing Monaco language registrations fail visibly during development.
- Added bookmark indicators in the Monaco gutter so bookmarked lines are visible beside line numbers.
- Added Markdown toolbar recovery actions to re-pin to the title bar or reset floating position to a default visible location.
- Added a Minimap Fade Speed setting in Settings > Editor to control minimap fade transitions.
- Added Fold All and Unfold All actions to the Explorer workspace-title right-click context menu.
- Added Help > Markdown Toolbar Shortcuts menu with quick reference entries for markdown toolbar modifier behaviors.

### Changed
- Expanded the roadmap with a concrete forward plan for power-editor features, grouped into quick wins, medium-complexity work, and major architectural investments inspired by Sublime Text.
- Changed Ctrl+P to open Goto Anything, while Print remains available from the File menu.
- Moved bookmark management into the Edit menu and Command Palette instead of keyboard-only access.
- Updated bookmark storage from one global in-memory list to active workspace/folder scope storage with global bookmark support.
- Improved bookmark relocation logic using line fingerprints and surrounding context, with stale tracking when text drifts.
- Improved bookmark navigation path repair by searching active scope for filename/content-fingerprint matches before marking stale.
- Expanded command palette coverage for remaining power-user preference actions that previously required menu or settings navigation.
- Updated Goto Anything query-prefix routing to dedicated symbol and text-search modes.
- Improved bookmark import/export conflict handling with newest-timestamp duplicate resolution and interactive scope selection.
- Reorganized the View menu into logical sections: Zoom, Editor Display, Editing Behavior, Panels, Markdown, and Folding.
- Switched Explorer folder rendering to a flat VS Code-style list model instead of Avalonia TreeView nesting, fixing persisted expansion restoration and deep-node realization issues.
- Updated AXAML language detection to resolve AXAML files consistently instead of XML fallback.
- Updated Monaco cursor movement handling so status bar line and column stay in sync with active editor position.
- Updated Monaco minimap behavior so it fades out when idle and quickly fades in while scrolling or hovering the minimap area.
- Updated close prompts for dirty tabs to Save / Save As / Don't Save / Cancel, including closes initiated from Explorer open-editor actions.
- Synced Monaco dirty-state notifications into editor models so unsaved tab indicators stay accurate.
- Updated Help > About with author attribution (AnotherLaughingMan) and framework/API stack details.

### Fixed
- Fixed bookmark navigation so opening a bookmark moves the visible editor to the bookmarked line instead of only activating the file tab.
- Fixed line-number gutter jitter by reserving bookmark marker width so text no longer shifts when markers appear.
- Fixed the floating Markdown toolbar layering by moving the unpinned toolbar into a popup-backed overlay rendered above the editor host surface.
- Fixed floating Markdown toolbar popup placement and drag bounds so it no longer appears offset and can extend partially outside the window.
- Fixed floating Markdown toolbar position persistence so off-window coordinates are preserved and hidden-state resize or tab changes do not overwrite saved position.
- Fixed the View menu so the Markdown toolbar pin toggle remains available while pinned.
- Fixed the unlabeled scrollbar opacity control in Settings by adding a visible label and accessibility name.
- Fixed panel scrollbar opacity handling so Explorer and Search scrollbars follow the ScrollbarOpacity setting.
- Fixed Monaco scrollbar opacity sync so editor scrollbars respond to live ScrollbarOpacity changes.
- Fixed activity bar appearance menu wiring for Left/Right positions while preserving Hidden behavior.
- Fixed primary panel position menu wiring so the primary panel can be mounted on left or right side.
- Fixed new untitled tab dirty-state initialization so tabs start clean and only become dirty after actual edits.
- Fixed close-prompt dialog width so right-side action buttons no longer crowd Cancel.
- Restored Monaco syntax highlighting in the webview editor by loading required language contribution modules in the Vite ESM bundle.
- Switched the desktop app to the WebView2 Core projection path to avoid an unused WPF reference and eliminate the WindowsBase assembly conflict warning during builds.
- Fixed status bar file type labeling so it follows the active tab and normalizes common file types like Markdown, C#, XML, XAML, AXAML, and C++.
- Replaced tab header text badges with language-aware Fluent icons so common code and markup files no longer collapse to a generic TXT tag.
