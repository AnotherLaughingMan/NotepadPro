using System;
using System.Collections.Generic;
using Avalonia.Controls;
using NotepadPro.ViewModels;

namespace NotepadPro.Models;

public sealed class AppSettings
{
    public SettingsData Settings { get; set; } = new();

    public WindowStateData Window { get; set; } = new();

    public List<RecentEditorData> RecentEditors { get; set; } = new();

    public List<string> RecentFiles { get; set; } = new();

    public List<string> RecentProjects { get; set; } = new();

    public string LastWorkspacePath { get; set; } = string.Empty;

    public string LastFolderPath { get; set; } = string.Empty;

    public List<string> ExpandedExplorerPaths { get; set; } = new();

    public List<string> OpenDocuments { get; set; } = new();

    public List<OpenDocumentStateData> OpenDocumentStates { get; set; } = new();

    public int ActiveOpenDocumentIndex { get; set; } = 0;

    public Dictionary<string, List<BookmarkItemData>> BookmarkScopes { get; set; } = new();
}

public sealed class OpenDocumentStateData
{
    public string Path { get; set; } = string.Empty;

    public int CaretIndex { get; set; }
}

public sealed class RecentEditorData
{
    public string Title { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;
}

public sealed class BookmarkItemData
{
    public string FilePath { get; set; } = string.Empty;

    public int LineNumber { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string AnchorFingerprint { get; set; } = string.Empty;

    public string ContextBefore { get; set; } = string.Empty;

    public string ContextAfter { get; set; } = string.Empty;

    public bool IsGlobal { get; set; }

    public bool IsStale { get; set; }
}

public sealed class SettingsData
{
    public bool WordWrap { get; set; } = true;

    public bool ShowLineNumbers { get; set; } = true;

    public bool IsMinimapVisible { get; set; } = true;

    public bool AutoIndentation { get; set; } = true;

    public bool AutoBracketing { get; set; } = true;

    public bool CleanCopyEnabled { get; set; } = true;

    public bool DetectJsonFromContent { get; set; } = true;

    public bool RenderWhitespace { get; set; } = false;

    public bool IsActivityBarVisible { get; set; } = true;

    public string ActivityBarPosition { get; set; } = "Left";

    public string PrimaryPanelPosition { get; set; } = "Left";

    public AutoSaveMode AutoSaveMode { get; set; } = AutoSaveMode.AfterDelay;

    public string Encoding { get; set; } = "UTF-8";

    public string Indentation { get; set; } = "Spaces: 4";

    public string Eol { get; set; } = "LF";

    public string Theme { get; set; } = "Dark+";

    public bool IsStatusBarVisible { get; set; } = true;

    public double EditorFontSize { get; set; } = 11;

    public double UiFontSize { get; set; } = 11;

    public double MenuFontSize { get; set; } = 11;

    public double TabFontSize { get; set; } = 11;

    public double StatusBarFontSize { get; set; } = 10;

    public double RailIconSize { get; set; } = 32;

    public double PanelHeaderFontSize { get; set; } = 11;

    public double ScrollSpeed { get; set; } = 3;

    public double TiltScrollSpeed { get; set; } = 3;

    public double ScrollbarOpacity { get; set; } = 0.5;

    public double MinimapFadeSpeedMs { get; set; } = 140;

    public bool IsExplorerVisible { get; set; } = false;

    public bool IsSearchVisible { get; set; } = false;

    public bool IsBookmarksVisible { get; set; } = false;

    public double ToolPanelWidth { get; set; } = 320;

    public bool IsMarkdownToolbarVisible { get; set; } = true;

    public bool RestoreOpenDocumentsOnStartup { get; set; } = true;

    public bool AutoOpenDetectedWorkspaces { get; set; } = true;

    public bool ExplorerOpenEditorsExpanded { get; set; } = true;

    public bool ExplorerRecentEditorsExpanded { get; set; } = true;

    public bool ExplorerFilesExpanded { get; set; } = true;

    public bool ExplorerOpenEditorsVisible { get; set; } = true;

    public bool ExplorerRecentEditorsVisible { get; set; } = true;

    public bool ExplorerFilesVisible { get; set; } = true;

    public string DefaultNewFileType { get; set; } = "Text";

    public bool IsMarkdownToolbarPinned { get; set; } = false;

    public double MarkdownToolbarOpacity { get; set; } = 0.9;

    public double MarkdownToolbarX { get; set; } = 220;

    public double MarkdownToolbarY { get; set; } = 70;
}

public sealed class WindowStateData
{
    public int? X { get; set; }

    public int? Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public WindowState WindowState { get; set; } = WindowState.Normal;
}
