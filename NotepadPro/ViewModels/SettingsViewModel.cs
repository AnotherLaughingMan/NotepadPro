using System.Collections.Generic;
using NotepadPro.Models;
using ReactiveUI;

namespace NotepadPro.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private bool _wordWrap = true;
    private bool _showLineNumbers = true;
    private bool _isMinimapVisible = true;
    private bool _autoIndentation = true;
    private bool _autoBracketing = true;
    private bool _renderWhitespace = false;
    private bool _isActivityBarVisible = true;
    private string _activityBarPosition = "Left";
    private string _primaryPanelPosition = "Left";
    private AutoSaveMode _autoSaveMode = AutoSaveMode.AfterDelay;
    private string _encoding = "UTF-8";
    private string _indentation = "Spaces: 4";
    private string _eol = "LF";
    private string _fileType = "Plain Text";
    private string _theme = "Dark+";
    private bool _isStatusBarVisible = true;
    private double _editorFontSize = 11;
    private double _uiFontSize = 11;
    private double _menuFontSize = 11;
    private double _tabFontSize = 11;
    private double _statusBarFontSize = 10;
    private double _railIconSize = 32;
    private double _panelHeaderFontSize = 11;
    private double _scrollSpeed = 3;
    private double _tiltScrollSpeed = 3;
    private double _scrollbarOpacity = 0.5;
    private double _minimapFadeSpeedMs = 140;
    private bool _isExplorerVisible;
    private bool _isSearchVisible;
    private bool _isBookmarksVisible;
    private double _toolPanelWidth = 320;
    private bool _isMarkdownToolbarVisible = true;
    private bool _restoreOpenDocumentsOnStartup = true;
    private bool _autoOpenDetectedWorkspaces = true;
    private bool _explorerOpenEditorsExpanded = true;
    private bool _explorerRecentEditorsExpanded = true;
    private bool _explorerFilesExpanded = true;
    private bool _explorerOpenEditorsVisible = true;
    private bool _explorerRecentEditorsVisible = true;
    private bool _explorerFilesVisible = true;
    private string _defaultNewFileType = "Text";
    private bool _isMarkdownToolbarPinned;
    private double _markdownToolbarOpacity = 0.9;
    private double _markdownToolbarX = 220;
    private double _markdownToolbarY = 70;

    public SettingsViewModel(SettingsData? data = null)
    {
        FontSizeOptions = new List<double> { 10, 11, 12, 13, 14 };
        ScrollSpeedOptions = new List<double> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        StatusBarFontSizeOptions = new List<double> { 10, 11, 12 };
        RailIconSizeOptions = new List<double> { 16, 20, 24, 28, 32, 36, 40 };

        AutoSaveModes = new List<AutoSaveMode>
        {
            AutoSaveMode.Off,
            AutoSaveMode.AfterDelay,
            AutoSaveMode.OnFocusChange,
            AutoSaveMode.OnWindowChange
        };

        EncodingOptions = new List<string> { "UTF-8", "UTF-16", "ANSI" };

        IndentationOptions = new List<string>
        {
            "Spaces: 2",
            "Spaces: 4",
            "Spaces: 8",
            "Tabs: 4",
            "Tabs: 8"
        };

        EolOptions = new List<string> { "LF", "CRLF" };

        ThemeOptions = new List<string>
        {
            "Dark+",
            "Dark Modern",
            "Dark High Contrast",
            "One Dark Pro",
            "Monokai Pro",
            "Solarized Dark",
            "Sand",
            "Goth",
            "Vampire",
            "Peach Sunset Light",
            "Peach Sunset Soft",
            "Light+"
        };

        if (data != null)
        {
            Apply(data);
        }
    }

    public IReadOnlyList<double> FontSizeOptions { get; }

    public IReadOnlyList<double> StatusBarFontSizeOptions { get; }

    public IReadOnlyList<double> RailIconSizeOptions { get; }

    public IReadOnlyList<double> ScrollSpeedOptions { get; }

    public IReadOnlyList<AutoSaveMode> AutoSaveModes { get; }

    public IReadOnlyList<string> EncodingOptions { get; }

    public IReadOnlyList<string> IndentationOptions { get; }

    public IReadOnlyList<string> EolOptions { get; }

    public IReadOnlyList<string> ThemeOptions { get; }

    public IReadOnlyList<string> DefaultNewFileTypeOptions { get; } = new List<string>
    {
        "Text",
        "Markdown",
        "JSON",
        "XML",
        "C#",
        "C",
        "C++",
        "XAML",
        "AXAML"
    };

    public bool WordWrap
    {
        get => _wordWrap;
        set => this.RaiseAndSetIfChanged(ref _wordWrap, value);
    }

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set => this.RaiseAndSetIfChanged(ref _showLineNumbers, value);
    }

    public bool IsMinimapVisible
    {
        get => _isMinimapVisible;
        set => this.RaiseAndSetIfChanged(ref _isMinimapVisible, value);
    }

    public bool AutoIndentation
    {
        get => _autoIndentation;
        set => this.RaiseAndSetIfChanged(ref _autoIndentation, value);
    }

    public bool AutoBracketing
    {
        get => _autoBracketing;
        set => this.RaiseAndSetIfChanged(ref _autoBracketing, value);
    }

    public bool RenderWhitespace
    {
        get => _renderWhitespace;
        set => this.RaiseAndSetIfChanged(ref _renderWhitespace, value);
    }

    public bool IsActivityBarVisible
    {
        get => _isActivityBarVisible;
        set
        {
            if (_isActivityBarVisible == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _isActivityBarVisible, value);
            this.RaisePropertyChanged(nameof(IsActivityBarLeft));
            this.RaisePropertyChanged(nameof(IsActivityBarRight));
        }
    }

    public string ActivityBarPosition
    {
        get => _activityBarPosition;
        set
        {
            var normalized = NormalizeSidePosition(value);
            if (string.Equals(_activityBarPosition, normalized, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _activityBarPosition, normalized);
            this.RaisePropertyChanged(nameof(IsActivityBarLeft));
            this.RaisePropertyChanged(nameof(IsActivityBarRight));
        }
    }

    public bool IsActivityBarLeft
    {
        get => IsActivityBarVisible && string.Equals(_activityBarPosition, "Left", System.StringComparison.OrdinalIgnoreCase);
        set
        {
            if (!value)
            {
                return;
            }

            IsActivityBarVisible = true;
            ActivityBarPosition = "Left";
        }
    }

    public bool IsActivityBarRight
    {
        get => IsActivityBarVisible && string.Equals(_activityBarPosition, "Right", System.StringComparison.OrdinalIgnoreCase);
        set
        {
            if (!value)
            {
                return;
            }

            IsActivityBarVisible = true;
            ActivityBarPosition = "Right";
        }
    }

    public string PrimaryPanelPosition
    {
        get => _primaryPanelPosition;
        set
        {
            var normalized = NormalizePrimaryPanelPosition(value);
            if (string.Equals(_primaryPanelPosition, normalized, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _primaryPanelPosition, normalized);
            {
                this.RaisePropertyChanged(nameof(IsPrimaryPanelLeft));
                this.RaisePropertyChanged(nameof(IsPrimaryPanelRight));
            }
        }
    }

    public bool IsPrimaryPanelLeft
    {
        get => string.Equals(_primaryPanelPosition, "Left", System.StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value)
            {
                PrimaryPanelPosition = "Left";
            }
        }
    }

    public bool IsPrimaryPanelRight
    {
        get => string.Equals(_primaryPanelPosition, "Right", System.StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value)
            {
                PrimaryPanelPosition = "Right";
            }
        }
    }

    public AutoSaveMode AutoSaveMode
    {
        get => _autoSaveMode;
        set => this.RaiseAndSetIfChanged(ref _autoSaveMode, value);
    }

    public string Encoding
    {
        get => _encoding;
        set => this.RaiseAndSetIfChanged(ref _encoding, value);
    }

    public string Indentation
    {
        get => _indentation;
        set => this.RaiseAndSetIfChanged(ref _indentation, value);
    }

    public string Eol
    {
        get => _eol;
        set => this.RaiseAndSetIfChanged(ref _eol, value);
    }

    public string FileType
    {
        get => _fileType;
        set => this.RaiseAndSetIfChanged(ref _fileType, value);
    }

    public string Theme
    {
        get => _theme;
        set => this.RaiseAndSetIfChanged(ref _theme, value);
    }

    public bool IsStatusBarVisible
    {
        get => _isStatusBarVisible;
        set => this.RaiseAndSetIfChanged(ref _isStatusBarVisible, value);
    }

    public double EditorFontSize
    {
        get => _editorFontSize;
        set => this.RaiseAndSetIfChanged(ref _editorFontSize, value);
    }

    public double UiFontSize
    {
        get => _uiFontSize;
        set => this.RaiseAndSetIfChanged(ref _uiFontSize, value);
    }

    public double MenuFontSize
    {
        get => _menuFontSize;
        set => this.RaiseAndSetIfChanged(ref _menuFontSize, value);
    }

    public double TabFontSize
    {
        get => _tabFontSize;
        set => this.RaiseAndSetIfChanged(ref _tabFontSize, value);
    }

    public double StatusBarFontSize
    {
        get => _statusBarFontSize;
        set => this.RaiseAndSetIfChanged(ref _statusBarFontSize, value);
    }

    public double RailIconSize
    {
        get => _railIconSize;
        set => this.RaiseAndSetIfChanged(ref _railIconSize, value);
    }

    public double PanelHeaderFontSize
    {
        get => _panelHeaderFontSize;
        set => this.RaiseAndSetIfChanged(ref _panelHeaderFontSize, value);
    }

    public double ScrollSpeed
    {
        get => _scrollSpeed;
        set => this.RaiseAndSetIfChanged(ref _scrollSpeed, value);
    }

    public double TiltScrollSpeed
    {
        get => _tiltScrollSpeed;
        set => this.RaiseAndSetIfChanged(ref _tiltScrollSpeed, value);
    }

    public double ScrollbarOpacity
    {
        get => _scrollbarOpacity;
        set => this.RaiseAndSetIfChanged(ref _scrollbarOpacity, value);
    }

    public double MinimapFadeSpeedMs
    {
        get => _minimapFadeSpeedMs;
        set => this.RaiseAndSetIfChanged(ref _minimapFadeSpeedMs, value);
    }

    public bool IsExplorerVisible
    {
        get => _isExplorerVisible;
        set => this.RaiseAndSetIfChanged(ref _isExplorerVisible, value);
    }

    public bool IsSearchVisible
    {
        get => _isSearchVisible;
        set => this.RaiseAndSetIfChanged(ref _isSearchVisible, value);
    }

    public bool IsBookmarksVisible
    {
        get => _isBookmarksVisible;
        set => this.RaiseAndSetIfChanged(ref _isBookmarksVisible, value);
    }

    public double ToolPanelWidth
    {
        get => _toolPanelWidth;
        set => this.RaiseAndSetIfChanged(ref _toolPanelWidth, value);
    }

    public bool IsMarkdownToolbarVisible
    {
        get => _isMarkdownToolbarVisible;
        set => this.RaiseAndSetIfChanged(ref _isMarkdownToolbarVisible, value);
    }

    public bool RestoreOpenDocumentsOnStartup
    {
        get => _restoreOpenDocumentsOnStartup;
        set => this.RaiseAndSetIfChanged(ref _restoreOpenDocumentsOnStartup, value);
    }

    public string DefaultNewFileType
    {
        get => _defaultNewFileType;
        set => this.RaiseAndSetIfChanged(ref _defaultNewFileType, value);
    }

    public bool AutoOpenDetectedWorkspaces
    {
        get => _autoOpenDetectedWorkspaces;
        set => this.RaiseAndSetIfChanged(ref _autoOpenDetectedWorkspaces, value);
    }

    public bool ExplorerOpenEditorsExpanded
    {
        get => _explorerOpenEditorsExpanded;
        set => this.RaiseAndSetIfChanged(ref _explorerOpenEditorsExpanded, value);
    }

    public bool ExplorerRecentEditorsExpanded
    {
        get => _explorerRecentEditorsExpanded;
        set => this.RaiseAndSetIfChanged(ref _explorerRecentEditorsExpanded, value);
    }

    public bool ExplorerFilesExpanded
    {
        get => _explorerFilesExpanded;
        set => this.RaiseAndSetIfChanged(ref _explorerFilesExpanded, value);
    }

    public bool ExplorerOpenEditorsVisible
    {
        get => _explorerOpenEditorsVisible;
        set => this.RaiseAndSetIfChanged(ref _explorerOpenEditorsVisible, value);
    }

    public bool ExplorerRecentEditorsVisible
    {
        get => _explorerRecentEditorsVisible;
        set => this.RaiseAndSetIfChanged(ref _explorerRecentEditorsVisible, value);
    }

    public bool ExplorerFilesVisible
    {
        get => _explorerFilesVisible;
        set => this.RaiseAndSetIfChanged(ref _explorerFilesVisible, value);
    }

    public bool IsMarkdownToolbarPinned
    {
        get => _isMarkdownToolbarPinned;
        set => this.RaiseAndSetIfChanged(ref _isMarkdownToolbarPinned, value);
    }

    public double MarkdownToolbarOpacity
    {
        get => _markdownToolbarOpacity;
        set => this.RaiseAndSetIfChanged(ref _markdownToolbarOpacity, value);
    }

    public double MarkdownToolbarX
    {
        get => _markdownToolbarX;
        set => this.RaiseAndSetIfChanged(ref _markdownToolbarX, value);
    }

    public double MarkdownToolbarY
    {
        get => _markdownToolbarY;
        set => this.RaiseAndSetIfChanged(ref _markdownToolbarY, value);
    }

    public void Apply(SettingsData data)
    {
        WordWrap = data.WordWrap;
        ShowLineNumbers = data.ShowLineNumbers;
        IsMinimapVisible = data.IsMinimapVisible;
        AutoIndentation = data.AutoIndentation;
        AutoBracketing = data.AutoBracketing;
        RenderWhitespace = data.RenderWhitespace;
        IsActivityBarVisible = data.IsActivityBarVisible;
        ActivityBarPosition = data.ActivityBarPosition;
        PrimaryPanelPosition = data.PrimaryPanelPosition;
        AutoSaveMode = data.AutoSaveMode;
        Encoding = data.Encoding;
        Indentation = data.Indentation;
        Eol = data.Eol;
        Theme = data.Theme;
        IsStatusBarVisible = data.IsStatusBarVisible;
        EditorFontSize = data.EditorFontSize;
        UiFontSize = data.UiFontSize;
        MenuFontSize = data.MenuFontSize;
        TabFontSize = data.TabFontSize;
        StatusBarFontSize = data.StatusBarFontSize;
        RailIconSize = data.RailIconSize;
        PanelHeaderFontSize = data.PanelHeaderFontSize;
        ScrollSpeed = data.ScrollSpeed;
        TiltScrollSpeed = data.TiltScrollSpeed;
        ScrollbarOpacity = data.ScrollbarOpacity;
        MinimapFadeSpeedMs = data.MinimapFadeSpeedMs;
        IsExplorerVisible = data.IsExplorerVisible;
        IsSearchVisible = data.IsSearchVisible;
        IsBookmarksVisible = data.IsBookmarksVisible;
        ToolPanelWidth = data.ToolPanelWidth;
        IsMarkdownToolbarVisible = data.IsMarkdownToolbarVisible;
        RestoreOpenDocumentsOnStartup = data.RestoreOpenDocumentsOnStartup;
        AutoOpenDetectedWorkspaces = data.AutoOpenDetectedWorkspaces;
        ExplorerOpenEditorsExpanded = data.ExplorerOpenEditorsExpanded;
        ExplorerRecentEditorsExpanded = data.ExplorerRecentEditorsExpanded;
        ExplorerFilesExpanded = data.ExplorerFilesExpanded;
        ExplorerOpenEditorsVisible = data.ExplorerOpenEditorsVisible;
        ExplorerRecentEditorsVisible = data.ExplorerRecentEditorsVisible;
        ExplorerFilesVisible = data.ExplorerFilesVisible;
        DefaultNewFileType = string.IsNullOrWhiteSpace(data.DefaultNewFileType) ? "Text" : data.DefaultNewFileType;
        IsMarkdownToolbarPinned = data.IsMarkdownToolbarPinned;
        MarkdownToolbarOpacity = data.MarkdownToolbarOpacity;
        MarkdownToolbarX = data.MarkdownToolbarX;
        MarkdownToolbarY = data.MarkdownToolbarY;
    }

    public SettingsData ToData()
    {
        return new SettingsData
        {
            WordWrap = WordWrap,
            ShowLineNumbers = ShowLineNumbers,
            IsMinimapVisible = IsMinimapVisible,
            AutoIndentation = AutoIndentation,
            AutoBracketing = AutoBracketing,
            RenderWhitespace = RenderWhitespace,
            IsActivityBarVisible = IsActivityBarVisible,
            ActivityBarPosition = ActivityBarPosition,
            PrimaryPanelPosition = PrimaryPanelPosition,
            AutoSaveMode = AutoSaveMode,
            Encoding = Encoding,
            Indentation = Indentation,
            Eol = Eol,
            Theme = Theme,
            IsStatusBarVisible = IsStatusBarVisible,
            EditorFontSize = EditorFontSize,
            UiFontSize = UiFontSize,
            MenuFontSize = MenuFontSize,
            TabFontSize = TabFontSize,
            StatusBarFontSize = StatusBarFontSize,
            RailIconSize = RailIconSize,
            PanelHeaderFontSize = PanelHeaderFontSize,
            ScrollSpeed = ScrollSpeed,
            TiltScrollSpeed = TiltScrollSpeed,
            ScrollbarOpacity = ScrollbarOpacity,
            MinimapFadeSpeedMs = MinimapFadeSpeedMs,
            IsExplorerVisible = IsExplorerVisible,
            IsSearchVisible = IsSearchVisible,
            IsBookmarksVisible = IsBookmarksVisible,
            ToolPanelWidth = ToolPanelWidth,
            IsMarkdownToolbarVisible = IsMarkdownToolbarVisible,
            RestoreOpenDocumentsOnStartup = RestoreOpenDocumentsOnStartup,
            AutoOpenDetectedWorkspaces = AutoOpenDetectedWorkspaces,
            ExplorerOpenEditorsExpanded = ExplorerOpenEditorsExpanded,
            ExplorerRecentEditorsExpanded = ExplorerRecentEditorsExpanded,
            ExplorerFilesExpanded = ExplorerFilesExpanded,
            ExplorerOpenEditorsVisible = ExplorerOpenEditorsVisible,
            ExplorerRecentEditorsVisible = ExplorerRecentEditorsVisible,
            ExplorerFilesVisible = ExplorerFilesVisible,
            DefaultNewFileType = DefaultNewFileType,
            IsMarkdownToolbarPinned = IsMarkdownToolbarPinned,
            MarkdownToolbarOpacity = MarkdownToolbarOpacity,
            MarkdownToolbarX = MarkdownToolbarX,
            MarkdownToolbarY = MarkdownToolbarY
        };
    }

    private static string NormalizePrimaryPanelPosition(string? value)
    {
        return string.Equals(value, "Right", System.StringComparison.OrdinalIgnoreCase)
            ? "Right"
            : "Left";
    }

    private static string NormalizeSidePosition(string? value)
    {
        return string.Equals(value, "Right", System.StringComparison.OrdinalIgnoreCase)
            ? "Right"
            : "Left";
    }
}
