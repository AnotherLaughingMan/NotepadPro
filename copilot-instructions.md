# Notepad Pro: Development Instructions

## Overview

Notepad Pro is a modern C# text editor application inspired by the Modern Notepad in Windows 11 Pro, but without any integration or features related to Microsoft Copilot or AI-assisted editing. The app focuses on core text editing functionalities with enhanced UI elements borrowed from Visual Studio Code (VS Code) for a professional, developer-friendly experience. Key emphases include readability in Dark Mode, customizable themes, and advanced editing tools.

This document serves as a guide for implementing the app's features, UI, and behaviors. All features from Windows 11's Modern Notepad should be included except for Copilot integration.

## Core Features (Inherited from Windows 11 Modern Notepad)

- **Text Editing Basics**: Support for creating, opening, saving, and editing plain text files (.txt) and other common formats (e.g., .md, .json, .xml).
- **Find and Replace**: Standard search functionality with options for case sensitivity, whole word matching, and replace all.
- **Word Wrap**: Toggleable word wrap for long lines.
- **Zoom**: Adjustable zoom levels for text size.
- **Undo/Redo**: Multi-level undo and redo history.
- **Auto-Save**: Automatic saving of unsaved changes at configurable intervals.
- **Print Support**: Basic printing with page setup options.
- **File Associations**: Associate with common text file types for opening via double-click.
- **Menu Bar**: Identical to Windows 11 Notepad's menu bar, including:
  - File: New, Open, Save, Save As, Page Setup, Print, Exit.
  - Edit: Undo, Redo, Cut, Copy, Paste, Delete, Find, Find Next, Replace, Go To, Select All, Time/Date.
  - View: Zoom In, Zoom Out, Restore Default Zoom, Status Bar (toggleable), Word Wrap.
  - Help: About Notepad Pro.
- **Tab Management**: Multi-tab interface for editing multiple files simultaneously. Tabs can be closed via:
  - Middle-click on the tab.
  - Clicking the 'x' button on individual tabs.
- **No Copilot Integration**: Explicitly exclude any AI-powered features, suggestions, or integrations like code completion, refactoring, or natural language queries.

## UI Style (Inspired by VS Code)

The overall UI should mimic VS Code's clean, minimalistic design for a professional feel:

- **Layout**:
  - Central editor pane.
  - Line number gutter on the left (showing line numbers, foldable regions if syntax supports it).
  - Minimap (code map) on the right side of the editor, providing a high-level overview of the document structure. The minimap should be toggleable via settings or View menu.
  - Left-side rail includes Explorer and Search buttons, using the same Fluent icon behavior to keep them readable.
- **Fonts and Styling**: Use monospace fonts by default (e.g., Consolas or Source Code Pro). Allow font family and size customization in settings.
- **Syntax Highlighting**: Automatic syntax detection and highlighting for common languages (e.g., Python, JavaScript, HTML, CSS, Markdown, JSON). Use a lightweight syntax parser similar to VS Code's TextMate grammars.
- **Status Bar**: Located at the bottom of the window, displaying:
  - Current line and column numbers (abbreviated as "Ln X, Col Y").
  - Encoding selection (e.g., UTF-8, ANSI).
  - Indentation configuration (e.g., Spaces: 4 or Tabs: 8).
  - Other important info: File type/language, EOL sequence (LF/CRLF), word count, selection info.
  - The status bar should be toggleable via the View menu.
- **Preference Pane**: Accessible via a gear icon or File > Preferences > Settings. Allow users to configure:
  - Encoding selection (e.g., UTF-8, UTF-16, ANSI) with auto-detection on file open.
  - Indentation: Tabs vs. Spaces, indent size (2, 4, 8), auto-indent on new lines.
  - Auto-indentation and auto-bracketing: Supported and configurable.
  - Auto-save: Configurable like VS Code (off, after delay, on focus change, on window change).
  - Other VS Code-like options: Bracket matching, auto-save delay, theme selection.
  - All settings and options must be accessed under Settings (hotkey Ctrl+,) from a gear icon in the left-side rail.
  - The Preferences/Settings gear icon must be positioned at the bottom of the left rail.
  - The Settings menu uses silhouette icons plus names; tooltips should be brief explanations.
  - All app settings live in the Preference Pane under the Settings option, styled like VS Code.

## Dark Mode and Themes

- **Dark Mode Requirement**: Implement a Dark Mode that ensures high readability. Text contrast must meet WCAG AA standards (minimum 4.5:1 contrast ratio). Avoid low-contrast combinations that make text unreadable (e.g., light gray on dark gray).
- **Default Palette**: Base the primary Dark Mode on VS Code's "Dark+" theme, including:
  - Background: #1E1E1E
  - Foreground/Text: #D4D4D4
  - Syntax colors: Strings in green (#CE9178), keywords in purple (#C586C0), comments in gray (#6A9955), etc.
  - Selection: Semi-transparent blue highlight (#264F78).
  - Line numbers: Subtle gray (#858585).
- **Theme Variants**: Provide a selection of Dark Mode variants, all ensuring readability:
  - Dark+ (default).
  - Dark High Contrast: Increased contrast for accessibility (e.g., brighter text on blacker backgrounds).
  - One Dark Pro: Inspired by Atom's theme, with deeper blues and greens.ot
  - Monokai Pro: Dimmed background with vibrant syntax colors.
  - Solarized Dark: Balanced, low-contrast dark theme for eye comfort.
  - Allow users to switch themes via a dropdown in the settings or View menu.
- **Light Mode**: Include a default Light Mode based on VS Code's "Light+" for users who prefer it, with similar variants.
- **System Integration**: Auto-detect and switch to Dark/Light based on Windows system settings, with manual override.

## Additional Features

- **Encoding Handling**: Support viewing and changing file encoding. Display warnings for encoding mismatches that could cause data loss.
- **Indentation Tools**: Commands for indent/outdent selection, convert tabs to spaces (and vice versa).
- **Folding**: Support code folding for syntax-aware languages (e.g., collapse functions or sections).
- **Go To Line**: Quick navigation to specific lines via Ctrl+G.
- **Performance**: Ensure the app remains lightweight and responsive, even with large files (up to 100MB).
- **Accessibility**: Keyboard shortcuts for all major actions (e.g., Ctrl+S for Save, Ctrl+F for Find). Screen reader compatibility (e.g., ARIA labels for UI elements).
- **Platform**: Target Windows 11/10, with potential for cross-platform (e.g., via Electron if expanding beyond native Win32).
- **Icons**: Use silhouette icons for all buttons and tabs; do not use emojis. Prefer Fluent UI System Icons (Regular/Outlined variant) as the standard set. Use Regular (outlined) for most toolbar and menu icons to keep them light and readable in Dark+ mode; switch to Filled for active or selected states.

## Implementation Guidelines

- **Framework**: Use Avalonia for the UI, and mimic Windows 10/11 UI where appropriate.
- **Code Style**: Keep code lines under 400 characters; split long lines when necessary.
- **Testing**:
  - Verify Dark Mode readability across all themes using tools like WAVE or manual contrast checks.
  - Test tab closing behaviors on various input devices (mouse, touch).
  - Ensure no Copilot-related code or dependencies are included.
- **Versioning**: Start with v1.0, focusing on core features.
- **Licensing**: Open-source under MIT, or proprietary as per team decision.

This document should be updated as features evolve. For questions, contact the development lead.
