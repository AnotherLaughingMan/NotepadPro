# Notepad Pro Roadmap

Legend:

- [ ] Not started
- [I] In progress
- [x] Completed
- [P] Paused (requires a prerequisite step named in the item)

Estimates are rough and assume one developer working part-time.

## Phase 0 - Project Setup (Week 1)

- [x] Repository structure and Docs folder setup (1 day)
- [x] Avalonia app skeleton and build pipeline (2 days)
- [x] Basic window layout with left rail, editor pane, and status bar placeholders (2 days)

## Phase 1 - Core Editing (Weeks 2-3)

- [x] File operations: New, Open, Save, Save As (3 days)
- [x] Tab management with close buttons and middle-click close (2 days)
- [x] Undo/Redo stack and edit history (2 days)
- [x] Word wrap toggle and view state persistence (2 days)
- [x] Find and Replace with options (3 days)

## Phase 2 - UI and Styling (Weeks 4-5)

- [x] Dark+ theme baseline and contrast checks (3 days)
- [x] Theme switching: Dark+, Dark HC, One Dark Pro, Monokai Pro, Solarized Dark, Light+ (4 days)
- [x] Fluent UI System Icons integration and states (2 days)
- [x] Left rail: Explorer and Search buttons behavior (2 days)
- [x] Preference Pane UI modeled after VS Code (3 days)

## Phase 3 - Editor Enhancements (Weeks 6-7)

- [x] Syntax highlighting with language detection (4 days)
- [x] Line numbers, current line highlight, and selection visuals (2 days)
- [x] Go To Line (Ctrl+G) (1 day)
- [x] Zoom controls and default zoom restore (2 days)
- [x] Mini map (toggleable) (3 days)

## Phase 4 - Settings and Behaviors (Weeks 8-9)

- [x] Settings system and persistence (3 days)
- [x] Auto-save modes like VS Code (2 days)
- [x] Auto-indentation and auto-bracketing settings (2 days)
- [x] Encoding detection and manual override (3 days)
- [x] Indentation tools (tabs/spaces convert, indent/outdent) (2 days)

## Phase 5 - Quality and Accessibility (Weeks 10-11)

- [x] Keyboard shortcuts for all primary actions (3 days)
- [x] Status bar details: Ln/Col, encoding, indentation, EOL, file type (2 days)
- [ ] Accessibility review and screen reader labels (3 days)
- [ ] Performance tests with large files up to 100MB (3 days)

## Phase 6 - Finalization (Week 12)

- [x] Print support and page setup (3 days)
- [x] File associations for common text types (2 days)
- [x] About dialog and versioning polish (1 day)
- [ ] QA pass and release checklist (2 days)

## Forward Roadmap - Power Editor Features

This section captures the next feature tier for Notepad Pro after the original core milestone plan. The goal is to make the app feel closer to a fast power editor like Sublime Text while staying lighter than a full IDE.

### Quick Wins

- [x] Command Palette (1 week)
	Completed with a shared quick picker for file actions, recent reopen flows, navigation, bookmark workflows, settings toggles, theme switching, encoding, EOL, and indentation changes.
- [x] Goto Anything (1-2 weeks)
	Completed with unified quick-open support for files, bookmarks, `:line[:column]`, `@symbols`, and `#text` search across open tabs and indexed workspace or folder files.
- [x] Bookmarks (3-4 days)
	Completed with toggle, next, previous, bookmark listing, import, export, clear-in-file, clear-all, visible editor markers, automatic bookmark persistence per workspace or opened folder, runtime prompts for merge vs replace, out-of-scope imports, and file vs scope export, plus a dedicated bookmarks panel with separate scoped and global bookmark groups, search/filter/sort controls, automatic in-file relocation, and active-scope rename or move recovery when bookmark content fingerprints still match.
- [x] Better open-editor affordances (3-4 days)
	Completed with pinned tabs, active-tab duplication, reveal-in-explorer, tab context menu actions for close variants, and pinned-state visibility in the Explorer open editors section.
- [ ] Richer status/navigation surfaces (3-4 days)
	Breadcrumbs or a lightweight document outline fed by syntax/symbol extraction.

### Medium-Complexity Features

- [ ] Multi-caret and multi-selection editing (2-3 weeks)
	Add next occurrence, select all occurrences, box selection, and multi-caret paste/edit workflows.
- [ ] Split editor panes (2-3 weeks)
	Support side-by-side editing, moving tabs between groups, and keeping active-document state correct across splits.
- [ ] Local context-aware autocomplete (2 weeks)
	Word, symbol, and snippet completion sourced from the current file, open buffers, and project text without adding AI features.
- [ ] Symbol navigation (2 weeks)
	Document symbols, workspace symbols, and fast symbol jump integrated into Goto Anything and the Command Palette.
- [ ] Macro recording and replay (1-2 weeks)
	Record a sequence of edit commands and replay them within the current document.

### Major Architectural Features

- [ ] Workspace and project system (3-4 weeks)
	Persist folders, open tabs, split layout, pinned tabs, and project-level settings such as indentation or ignored paths.
- [ ] Large-file mode (2-3 weeks)
	Introduce a degraded-but-responsive path for very large files by disabling expensive features, reducing tokenization, and loading content incrementally where possible.
- [ ] Stronger syntax engine pipeline (3-5 weeks)
	Improve embedded-language handling, fold region extraction, symbol extraction, and language/file detection consistency across native and web editors.
- [ ] Diagnostics and editor decoration layer (2-3 weeks)
	Establish a common pipeline for search hits, bookmarks, diagnostics, modified-line markers, and minimap/overview ruler markers.
- [ ] Extension/plugin model investigation (research spike, 1-2 weeks)
	Only after core power-editor workflows are stable; evaluate a safe command/snippet/plugin surface without turning Notepad Pro into a full IDE.

### Recommended Delivery Order

- [x] Wave 1: Command Palette, Goto Anything, Bookmarks
- [ ] Wave 2: Multi-caret editing, Symbol navigation, Local autocomplete
- [ ] Wave 3: Split panes, Workspace persistence, Diagnostics/decorations
- [ ] Wave 4: Large-file mode, syntax pipeline upgrades, plugin-model investigation

### Product Guardrails

- [ ] Keep AI features out of scope until the privacy/trust model is defined.
- [ ] Prefer editor-speed and navigation wins over IDE-style heavyweight language services.
- [ ] Ensure native editor and webview editor stay behaviorally aligned for language detection, status bar data, and user-facing commands.
