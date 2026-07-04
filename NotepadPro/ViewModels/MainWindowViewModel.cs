using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NotepadPro.Models;
using NotepadPro.Services;
using ReactiveUI;

namespace NotepadPro.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private const string DefaultBookmarkScopeKey = "default";
    private const double DefaultEditorFontSize = 11;
    private const double DefaultMarkdownToolbarX = 220;
    private const double DefaultMarkdownToolbarY = 70;
    private static readonly double[] ZoomLevels = { 8, 9, 10, 11, 12, 13, 14, 16, 18, 20, 22 };
    private readonly ObservableAsPropertyHelper<string> _lineColumnText;
    private readonly ObservableAsPropertyHelper<bool> _isMinimapVisible;
    private readonly ObservableAsPropertyHelper<string> _windowTitle;
    private readonly EditorViewModel _fallbackEditor;
    private EditorTabViewModel? _selectedTab;
    private ViewModelBase? _toolPanelViewModel;
    private string _toolPanelTitle = string.Empty;
    private bool _isZoomFlyoutOpen;
    private double _zoomSliderIndex;
    private bool _syncingZoomSlider;
    private bool _isMarkdownToolbarVisible = true;
    private bool _isMarkdownToolbarPinned;
    private double _markdownToolbarOpacity = 0.9;
    private double _markdownToolbarX = DefaultMarkdownToolbarX;
    private double _markdownToolbarY = DefaultMarkdownToolbarY;
    private readonly Dictionary<string, List<BookmarkItem>> _bookmarkScopes = new(StringComparer.OrdinalIgnoreCase);
    private string _activeBookmarkScopeKey = DefaultBookmarkScopeKey;

    public MainWindowViewModel(SettingsViewModel? settings = null)
    {
        Settings = settings ?? new SettingsViewModel();
        _isMarkdownToolbarVisible = Settings.IsMarkdownToolbarVisible;
        _isMarkdownToolbarPinned = Settings.IsMarkdownToolbarPinned;
        _markdownToolbarOpacity = Math.Clamp(Settings.MarkdownToolbarOpacity, 0.35, 1.0);
        _markdownToolbarX = double.IsFinite(Settings.MarkdownToolbarX) ? Settings.MarkdownToolbarX : DefaultMarkdownToolbarX;
        _markdownToolbarY = double.IsFinite(Settings.MarkdownToolbarY) ? Settings.MarkdownToolbarY : DefaultMarkdownToolbarY;

        Settings.MarkdownToolbarOpacity = _markdownToolbarOpacity;
        Settings.MarkdownToolbarX = _markdownToolbarX;
        Settings.MarkdownToolbarY = _markdownToolbarY;
        Explorer = new ExplorerViewModel();
        Explorer.PropertyChanged += OnExplorerPropertyChanged;
        Explorer.IsOpenEditorsExpanded = Settings.ExplorerOpenEditorsExpanded;
        Explorer.IsRecentEditorsExpanded = Settings.ExplorerRecentEditorsExpanded;
        Explorer.IsFilesExpanded = Settings.ExplorerFilesExpanded;
        Explorer.IsOpenEditorsVisible = Settings.ExplorerOpenEditorsVisible;
        Explorer.IsRecentEditorsVisible = Settings.ExplorerRecentEditorsVisible;
        Explorer.IsFilesVisible = Settings.ExplorerFilesVisible;
        RecentFiles = new ObservableCollection<RecentFileItem>();
        RecentProjects = new ObservableCollection<RecentProjectItem>();
        Bookmarks = new ObservableCollection<BookmarkItem>();
        Tabs = new ObservableCollection<EditorTabViewModel>();

        Explorer.SetOpenEditors(Tabs);

        _fallbackEditor = new EditorViewModel(Settings);
        _fallbackEditor.SetUntitledName("Welcome");
        _fallbackEditor.NewDocument();

        Search = new SearchViewModel(_fallbackEditor);
        EnsureWelcomeTab();
        BookmarksPanel = new BookmarksViewModel(
            Bookmarks,
            GlobalBookmarks,
            NavigateToBookmarkAsync,
            RemoveBookmark,
            ToggleBookmarkAtCaret,
            ToggleGlobalBookmarkAtCaret,
            ClearScopedBookmarks,
            ClearGlobalBookmarks,
            () => Editor.FilePath);

        Tabs.CollectionChanged += (_, _) =>
        {
            this.RaisePropertyChanged(nameof(IsGreetingVisible));
            this.RaisePropertyChanged(nameof(WindowTitle));
        };

        RecentFiles.CollectionChanged += (_, _) =>
        {
            this.RaisePropertyChanged(nameof(HasRecentFiles));
            this.RaisePropertyChanged(nameof(HasNoRecentFilesAndFolders));
            this.RaisePropertyChanged(nameof(HasNoRecents));
        };

        RecentProjects.CollectionChanged += (_, _) =>
        {
            this.RaisePropertyChanged(nameof(HasRecentProjects));
            this.RaisePropertyChanged(nameof(RecentWorkspaces));
            this.RaisePropertyChanged(nameof(RecentFolders));
            this.RaisePropertyChanged(nameof(HasRecentWorkspaces));
            this.RaisePropertyChanged(nameof(HasRecentFolders));
            this.RaisePropertyChanged(nameof(HasNoRecentFilesAndFolders));
            this.RaisePropertyChanged(nameof(HasNoRecents));
        };

        ShowExplorerCommand = ReactiveCommand.Create(ToggleExplorer);
        ShowSearchCommand = ReactiveCommand.Create(ToggleSearch);
        ShowBookmarksCommand = ReactiveCommand.Create(ToggleBookmarks);
        CloseToolPanelCommand = ReactiveCommand.Create(CloseToolPanel);
        NewTabCommand = ReactiveCommand.Create(AddNewTab);
        CloseTabCommand = ReactiveCommand.Create<EditorTabViewModel>(CloseTab);
        CloseActiveTabCommand = ReactiveCommand.Create(CloseActiveTab);
        NextTabCommand = ReactiveCommand.Create(SelectNextTab);
        PreviousTabCommand = ReactiveCommand.Create(SelectPreviousTab);
        SaveAllCommand = ReactiveCommand.CreateFromTask(SaveAllAsync);
        CloseAllTabsCommand = ReactiveCommand.Create(CloseAllTabs);
        ToggleZoomFlyoutCommand = ReactiveCommand.Create(ToggleZoomFlyout);

        _zoomSliderIndex = GetNearestZoomIndex(Settings.EditorFontSize);

        Explorer.OpenFileAsync = OpenFileFromPathAsync;

        // Restore panel state from saved settings
        if (Settings.IsExplorerVisible)
        {
            ShowToolPanel(Explorer, "Explorer");
        }
        else if (Settings.IsSearchVisible)
        {
            ShowToolPanel(Search, "Search");
        }
        else if (Settings.IsBookmarksVisible && BookmarksPanel != null)
        {
            ShowToolPanel(BookmarksPanel!, "Bookmarks");
        }

        var activeEditor = this.WhenAnyValue(x => x.SelectedTab)
            .Select(tab => tab?.Editor ?? _fallbackEditor);

        activeEditor
            .Select(editor => editor.WhenAnyValue(x => x.Language, x => x.IsMarkdownPreviewVisible, (_, _) => Unit.Default))
            .Switch()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsActiveEditorMarkdown));
                this.RaisePropertyChanged(nameof(IsRenderedViewToggleAvailable));
                this.RaisePropertyChanged(nameof(IsMarkdownPreviewVisible));
                this.RaisePropertyChanged(nameof(MarkdownRenderedToggleIconText));
                this.RaisePropertyChanged(nameof(IsMarkdownViewModeIndicatorVisible));
                this.RaisePropertyChanged(nameof(MarkdownViewModeIndicatorText));
                this.RaisePropertyChanged(nameof(IsMarkdownToolbarPinAvailable));
                this.RaisePropertyChanged(nameof(IsMarkdownToolbarShown));
                this.RaisePropertyChanged(nameof(IsMarkdownToolbarPinnedShown));
            });

        activeEditor
            .Select(editor => editor.WhenAnyValue(x => x.FilePath))
            .Switch()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsRenderedViewToggleAvailable));
                this.RaisePropertyChanged(nameof(MarkdownRenderedToggleIconText));
                this.RaisePropertyChanged(nameof(IsMarkdownViewModeIndicatorVisible));
                this.RaisePropertyChanged(nameof(MarkdownViewModeIndicatorText));
            });

        activeEditor
            .Select(editor => editor.WhenAnyValue(x => x.Language))
            .Switch()
            .Select(TextMateLanguageService.NormalizeDisplayLanguage)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(language => Settings.FileType = language);

        _lineColumnText = activeEditor
            .Select(editor => editor.WhenAnyValue(
                x => x.CaretLine,
                x => x.CaretColumn,
                (line, col) => $"Ln {line}, Col {col}"))
            .Switch()
            .ToProperty(this, x => x.LineColumnText);

        _isMinimapVisible = this.WhenAnyValue(x => x.Settings.IsMinimapVisible)
            .ToProperty(this, x => x.IsMinimapVisible);

        this.WhenAnyValue(x => x.Settings.IsMinimapVisible)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(MinimapWidth)));

        _windowTitle = activeEditor
            .Select(editor => editor.WhenAnyValue(
                x => x.FileName,
                x => x.HasUnsavedChanges,
                (fileName, dirty) => dirty
                    ? $"{fileName} * - Notepad Pro"
                    : $"{fileName} - Notepad Pro"))
            .Switch()
            .ToProperty(this, x => x.WindowTitle);

        activeEditor
            .Select(editor => editor.WhenAnyValue(x => x.Text).Select(_ => editor))
            .Switch()
            .Throttle(TimeSpan.FromSeconds(2))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Where(_ => Settings.AutoSaveMode == AutoSaveMode.AfterDelay)
            .Subscribe(editor => TriggerDelayedAutoSave(editor));

        this.WhenAnyValue(x => x.Settings.EditorFontSize)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(fontSize =>
            {
                this.RaisePropertyChanged(nameof(ZoomLevelText));

                if (_syncingZoomSlider)
                {
                    return;
                }

                var nearest = GetNearestZoomIndex(fontSize);
                if (Math.Abs(_zoomSliderIndex - nearest) < 0.001)
                {
                    return;
                }

                _syncingZoomSlider = true;
                this.RaiseAndSetIfChanged(ref _zoomSliderIndex, nearest, nameof(ZoomSliderIndex));
                _syncingZoomSlider = false;
            });
    }

    public ObservableCollection<EditorTabViewModel> Tabs { get; }

    public EditorViewModel Editor => SelectedTab?.Editor ?? _fallbackEditor;

    public ExplorerViewModel Explorer { get; }

    public SearchViewModel Search { get; }

    public SettingsViewModel Settings { get; }

    public ObservableCollection<RecentFileItem> RecentFiles { get; }

    public ObservableCollection<RecentProjectItem> RecentProjects { get; }

    public ObservableCollection<BookmarkItem> Bookmarks { get; }

    public EditorTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTab, value);
            this.RaisePropertyChanged(nameof(Editor));
            this.RaisePropertyChanged(nameof(IsWelcomeTabSelected));
            this.RaisePropertyChanged(nameof(IsMinimapPanelVisible));
            this.RaisePropertyChanged(nameof(MinimapWidth));
            this.RaisePropertyChanged(nameof(IsGreetingVisible));
            this.RaisePropertyChanged(nameof(WindowTitle));
            this.RaisePropertyChanged(nameof(IsRenderedViewToggleAvailable));
            this.RaisePropertyChanged(nameof(MarkdownRenderedToggleIconText));
            this.RaisePropertyChanged(nameof(IsMarkdownViewModeIndicatorVisible));
            this.RaisePropertyChanged(nameof(MarkdownViewModeIndicatorText));
            Search.SetEditor(Editor);
            BookmarksPanel?.RefreshView();
        }
    }

    public ViewModelBase? ToolPanelViewModel
    {
        get => _toolPanelViewModel;
        private set
        {
            this.RaiseAndSetIfChanged(ref _toolPanelViewModel, value);
            this.RaisePropertyChanged(nameof(IsToolPanelVisible));
            this.RaisePropertyChanged(nameof(IsExplorerVisible));
            this.RaisePropertyChanged(nameof(IsSearchVisible));
            this.RaisePropertyChanged(nameof(IsBookmarksVisible));
        }
    }

    public string ToolPanelTitle
    {
        get => _toolPanelTitle;
        private set => this.RaiseAndSetIfChanged(ref _toolPanelTitle, value);
    }

    public string LineColumnText => _lineColumnText.Value;

    public bool IsMinimapVisible => _isMinimapVisible.Value;

    public bool IsWelcomeTabSelected => SelectedTab?.IsWelcomeTab == true;

    public bool IsMinimapPanelVisible => IsMinimapVisible && !IsWelcomeTabSelected;

    public double MinimapWidth => IsMinimapPanelVisible ? 140 : 0;

    public string WindowTitle => IsGreetingVisible ? "Notepad Pro" : _windowTitle.Value;

    public bool IsToolPanelVisible => ToolPanelViewModel != null;

    public bool IsExplorerVisible => ToolPanelViewModel == Explorer;

    public bool IsSearchVisible => ToolPanelViewModel == Search;

    public bool IsBookmarksVisible => ToolPanelViewModel == BookmarksPanel;

    public bool IsGreetingVisible => IsWelcomeTabSelected;

    public bool HasRecentFiles => RecentFiles.Count > 0;

    public bool HasRecentProjects => RecentProjects.Count > 0;

    public IEnumerable<RecentProjectItem> RecentWorkspaces => RecentProjects.Where(item => item.IsWorkspace);

    public IEnumerable<RecentProjectItem> RecentFolders => RecentProjects.Where(item => !item.IsWorkspace);

    public bool HasRecentWorkspaces => RecentProjects.Any(item => item.IsWorkspace);

    public bool HasRecentFolders => RecentProjects.Any(item => !item.IsWorkspace);

    public bool HasNoRecentFilesAndFolders => !HasRecentFiles && !HasRecentFolders;

    public bool HasNoRecents => !HasRecentWorkspaces && !HasRecentFolders && !HasRecentFiles;

    public bool IsActiveEditorMarkdown => Editor.IsMarkdown;

    public bool IsRenderedViewToggleAvailable => Editor.CanToggleRenderedMarkdownView;

    public bool IsMarkdownPreviewVisible
    {
        get => Editor.IsMarkdownPreviewVisible;
        set
        {
            if (value && !IsRenderedViewToggleAvailable)
            {
                return;
            }

            if (!Editor.IsMarkdownPreviewVisible.Equals(value))
            {
                Editor.IsMarkdownPreviewVisible = value;
                this.RaisePropertyChanged(nameof(IsMarkdownPreviewVisible));
                this.RaisePropertyChanged(nameof(MarkdownRenderedToggleIconText));
                this.RaisePropertyChanged(nameof(MarkdownViewModeIndicatorText));
            }
        }
    }

    public string MarkdownRenderedToggleIconText => IsMarkdownPreviewVisible ? "Aa" : "{}";

    public bool IsMarkdownViewModeIndicatorVisible => IsRenderedViewToggleAvailable;

    public string MarkdownViewModeIndicatorText => IsMarkdownPreviewVisible ? "Rendered" : "Source";

    public bool IsZoomFlyoutOpen
    {
        get => _isZoomFlyoutOpen;
        set => this.RaiseAndSetIfChanged(ref _isZoomFlyoutOpen, value);
    }

    public bool IsMarkdownToolbarVisible
    {
        get => _isMarkdownToolbarVisible;
        set
        {
            this.RaiseAndSetIfChanged(ref _isMarkdownToolbarVisible, value);
            Settings.IsMarkdownToolbarVisible = value;
            this.RaisePropertyChanged(nameof(IsMarkdownToolbarPinAvailable));
            this.RaisePropertyChanged(nameof(IsMarkdownToolbarShown));
            this.RaisePropertyChanged(nameof(IsMarkdownToolbarPinnedShown));
        }
    }

    public bool IsMarkdownToolbarPinned
    {
        get => _isMarkdownToolbarPinned;
        set
        {
            this.RaiseAndSetIfChanged(ref _isMarkdownToolbarPinned, value);
            Settings.IsMarkdownToolbarPinned = value;
            this.RaisePropertyChanged(nameof(IsMarkdownToolbarShown));
            this.RaisePropertyChanged(nameof(IsMarkdownToolbarPinnedShown));
        }
    }

    public double MarkdownToolbarOpacity
    {
        get => _markdownToolbarOpacity;
        set
        {
            var clamped = Math.Clamp(value, 0.35, 1.0);
            this.RaiseAndSetIfChanged(ref _markdownToolbarOpacity, clamped);
            Settings.MarkdownToolbarOpacity = clamped;
            this.RaisePropertyChanged(nameof(MarkdownToolbarOpacityText));
        }
    }

    public string MarkdownToolbarOpacityText => $"{Math.Round(MarkdownToolbarOpacity * 100):0}%";

    public double MarkdownToolbarX
    {
        get => _markdownToolbarX;
        set
        {
            var next = double.IsFinite(value) ? value : _markdownToolbarX;
            this.RaiseAndSetIfChanged(ref _markdownToolbarX, next);
            Settings.MarkdownToolbarX = next;
        }
    }

    public double MarkdownToolbarY
    {
        get => _markdownToolbarY;
        set
        {
            var next = double.IsFinite(value) ? value : _markdownToolbarY;
            this.RaiseAndSetIfChanged(ref _markdownToolbarY, next);
            Settings.MarkdownToolbarY = next;
        }
    }

    public bool IsMarkdownToolbarShown => IsActiveEditorMarkdown && IsMarkdownToolbarVisible && !IsMarkdownToolbarPinned;

    public bool IsMarkdownToolbarPinnedShown => IsActiveEditorMarkdown && IsMarkdownToolbarVisible && IsMarkdownToolbarPinned;

    public bool IsMarkdownToolbarPinAvailable => IsActiveEditorMarkdown && IsMarkdownToolbarVisible;

    public string ZoomLevelText => $"{Math.Round(Settings.EditorFontSize / DefaultEditorFontSize * 100):0}%";

    public double ZoomSliderIndex
    {
        get => _zoomSliderIndex;
        set
        {
            var snapped = Math.Clamp((int)Math.Round(value), 0, ZoomLevels.Length - 1);
            if (Math.Abs(_zoomSliderIndex - snapped) < 0.001)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _zoomSliderIndex, snapped);
            if (_syncingZoomSlider)
            {
                return;
            }

            SetZoomByIndex(snapped, updateSlider: false);
        }
    }

    public int ZoomSliderMax => ZoomLevels.Length - 1;


    public ReactiveCommand<Unit, Unit> ShowExplorerCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowSearchCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowBookmarksCommand { get; }

    public ReactiveCommand<Unit, Unit> CloseToolPanelCommand { get; }

    public ReactiveCommand<Unit, Unit> NewTabCommand { get; }

    public ReactiveCommand<EditorTabViewModel, Unit> CloseTabCommand { get; }

    public ReactiveCommand<Unit, Unit> CloseActiveTabCommand { get; }

    public ReactiveCommand<Unit, Unit> NextTabCommand { get; }

    public ReactiveCommand<Unit, Unit> PreviousTabCommand { get; }

    public ReactiveCommand<Unit, Unit> SaveAllCommand { get; }

    public ReactiveCommand<Unit, Unit> CloseAllTabsCommand { get; }

    public ReactiveCommand<Unit, Unit> ToggleZoomFlyoutCommand { get; }


    public void NewDocument()
    {
        var fileType = Settings.DefaultNewFileType?.Trim();
        if (string.IsNullOrWhiteSpace(fileType))
        {
            NewTextDocument();
            return;
        }

        if (string.Equals(fileType, "Markdown", StringComparison.OrdinalIgnoreCase))
        {
            NewMarkdownDocument();
            return;
        }

        if (string.Equals(fileType, "JSON", StringComparison.OrdinalIgnoreCase))
        {
            NewJsonDocument();
            return;
        }

        if (string.Equals(fileType, "XML", StringComparison.OrdinalIgnoreCase))
        {
            NewXmlDocument();
            return;
        }

        if (string.Equals(fileType, "C#", StringComparison.OrdinalIgnoreCase))
        {
            NewCSharpDocument();
            return;
        }

        if (string.Equals(fileType, "C", StringComparison.OrdinalIgnoreCase))
        {
            NewCDocument();
            return;
        }

        if (string.Equals(fileType, "C++", StringComparison.OrdinalIgnoreCase))
        {
            NewCppDocument();
            return;
        }

        if (string.Equals(fileType, "XAML", StringComparison.OrdinalIgnoreCase))
        {
            NewXamlDocument();
            return;
        }

        if (string.Equals(fileType, "AXAML", StringComparison.OrdinalIgnoreCase))
        {
            NewAxamlDocument();
            return;
        }

        NewTextDocument();
    }

    public void NewTextDocument()
    {
        AddNewTab();
    }

    public void NewMarkdownDocument()
    {
        var tab = AddNewTabInternal(isUntitled: true, isMarkdown: true);
        SelectedTab = tab;
    }

    public void NewJsonDocument()
    {
        const string template = "{\n  \"name\": \"\",\n  \"version\": 1\n}\n";
        var tab = AddTemplatedTabInternal("JSON", "json", template);
        SelectedTab = tab;
    }

    public void NewXmlDocument()
    {
        const string template = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root>\n</root>\n";
        var tab = AddTemplatedTabInternal("XML", "xml", template);
        SelectedTab = tab;
    }

    public void NewCSharpDocument()
    {
        const string template = "using System;\n\nnamespace NotepadProNamespace;\n\npublic class NewClass\n{\n}\n";
        var tab = AddTemplatedTabInternal("C#", "cs", template);
        SelectedTab = tab;
    }

    public void NewCDocument()
    {
        const string template = "#include <stdio.h>\n\nint main(void)\n{\n    return 0;\n}\n";
        var tab = AddTemplatedTabInternal("C", "c", template);
        SelectedTab = tab;
    }

    public void NewCppDocument()
    {
        const string template = "#include <iostream>\n\nint main()\n{\n    return 0;\n}\n";
        var tab = AddTemplatedTabInternal("C++", "cpp", template);
        SelectedTab = tab;
    }

    public void NewXamlDocument()
    {
        const string template = "<Window xmlns=\"https://github.com/avaloniaui\"\n        xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"\n        x:Class=\"NotepadPro.Views.NewWindow\">\n</Window>\n";
        var tab = AddTemplatedTabInternal("XAML", "xaml", template);
        SelectedTab = tab;
    }

    public void NewAxamlDocument()
    {
        const string template = "<UserControl xmlns=\"https://github.com/avaloniaui\"\n             xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"\n             x:Class=\"NotepadPro.Views.NewControl\">\n</UserControl>\n";
        var tab = AddTemplatedTabInternal("XAML", "axaml", template);
        SelectedTab = tab;
    }

    public void ToggleMarkdownPreview()
    {
        if (!IsRenderedViewToggleAvailable)
        {
            return;
        }

        IsMarkdownPreviewVisible = !IsMarkdownPreviewVisible;
    }

    public void IncreaseMarkdownToolbarOpacity()
    {
        AdjustMarkdownToolbarOpacityByPercent(1);
    }

    public void DecreaseMarkdownToolbarOpacity()
    {
        AdjustMarkdownToolbarOpacityByPercent(-1);
    }

    public void AdjustMarkdownToolbarOpacityByPercent(int percentDelta)
    {
        if (percentDelta == 0)
        {
            return;
        }

        MarkdownToolbarOpacity = Math.Clamp(
            MarkdownToolbarOpacity + (percentDelta / 100d),
            0.35,
            1.0);
    }

    public void RecoverMarkdownToolbarToPinned()
    {
        if (!IsActiveEditorMarkdown)
        {
            return;
        }

        IsMarkdownToolbarVisible = true;
        IsMarkdownToolbarPinned = true;
    }

    public void ResetFloatingMarkdownToolbarPosition()
    {
        if (!IsActiveEditorMarkdown)
        {
            return;
        }

        MarkdownToolbarX = DefaultMarkdownToolbarX;
        MarkdownToolbarY = DefaultMarkdownToolbarY;
        IsMarkdownToolbarVisible = true;
        IsMarkdownToolbarPinned = false;
    }

    public void ZoomIn() => StepZoom(1);

    public void ZoomOut() => StepZoom(-1);

    public void ResetZoom() => SetZoom(DefaultEditorFontSize);

    public void SetZoomFromPercent(int percent)
    {
        if (percent <= 0)
        {
            return;
        }

        SetZoom(DefaultEditorFontSize * percent / 100d);
    }

    public async Task OpenFileFromPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            RemoveRecentFile(path);
            RemoveRecentEditorByPath(path);
            return;
        }

        if (path.EndsWith(".code-workspace", StringComparison.OrdinalIgnoreCase))
        {
            Explorer.LoadWorkspace(path);
            AddRecentProject(path);
            RemoveRecentFile(path);
            RemoveRecentEditorByPath(path);
            return;
        }

        var existing = Tabs.FirstOrDefault(t =>
            !t.IsWelcomeTab &&
            string.Equals(t.Editor.FilePath, path, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            SelectedTab = existing;
            return;
        }

        RemoveWelcomeTabIfPresent();
        var tab = FindReusableUntitledTab() ?? AddNewTabInternal(isUntitled: false);
        await tab.Editor.LoadFromFileAsync(path);
        AddRecentFile(path);
        SelectedTab = tab;
    }

    public void ToggleBookmarkAtCaret()
    {
        ToggleBookmarkAtCaret(isGlobal: false);
    }

    public void ToggleGlobalBookmarkAtCaret()
    {
        ToggleBookmarkAtCaret(isGlobal: true);
    }

    private void ToggleBookmarkAtCaret(bool isGlobal)
    {
        var editor = Editor;
        var lineNumber = Math.Max(1, editor.CaretLine);
        var filePath = editor.FilePath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var target = isGlobal ? GlobalBookmarks : Bookmarks;

        var existing = target.FirstOrDefault(bookmark =>
            string.Equals(bookmark.FilePath, filePath, StringComparison.OrdinalIgnoreCase)
            && bookmark.LineNumber == lineNumber);

        if (existing != null)
        {
            target.Remove(existing);
            return;
        }

        target.Add(CreateBookmark(editor, lineNumber, isGlobal));
        SortBookmarkCollection(target);
    }

    public BookmarkMarkerState GetBookmarkMarkerState(string? filePath, int lineNumber)
    {
        var normalizedPath = filePath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return BookmarkMarkerState.None;
        }

        var bookmarksAtLine = Bookmarks.Concat(GlobalBookmarks)
            .Where(bookmark =>
            string.Equals(bookmark.FilePath, normalizedPath, StringComparison.OrdinalIgnoreCase)
            && bookmark.LineNumber == lineNumber)
            .ToList();

        if (bookmarksAtLine.Count == 0)
        {
            return BookmarkMarkerState.None;
        }

        if (bookmarksAtLine.Any(bookmark => bookmark.IsStale))
        {
            return BookmarkMarkerState.Stale;
        }

        return bookmarksAtLine.Any(bookmark => bookmark.IsGlobal)
            ? BookmarkMarkerState.Global
            : BookmarkMarkerState.Scoped;
    }

    public void RefreshBookmarksForActiveEditor()
    {
        var editor = Editor;
        var filePath = editor.FilePath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            BookmarksPanel?.RefreshView();
            return;
        }

        var updatedScoped = false;
        var updatedGlobal = false;

        foreach (var bookmark in Bookmarks.Concat(GlobalBookmarks)
                     .Where(bookmark => string.Equals(bookmark.FilePath, filePath, StringComparison.OrdinalIgnoreCase)))
        {
            var originalLine = bookmark.LineNumber;
            UpdateBookmarkState(bookmark, editor.Text);

            if (bookmark.IsGlobal)
            {
                updatedGlobal |= bookmark.LineNumber != originalLine;
            }
            else
            {
                updatedScoped |= bookmark.LineNumber != originalLine;
            }
        }

        if (updatedScoped)
        {
            SortBookmarkCollection(Bookmarks);
        }

        if (updatedGlobal)
        {
            SortBookmarkCollection(GlobalBookmarks);
        }

        BookmarksPanel?.RefreshView();
    }

    public BookmarkItem? GetNextBookmark()
    {
        return GetRelativeBookmark(forward: true);
    }

    public BookmarkItem? GetPreviousBookmark()
    {
        return GetRelativeBookmark(forward: false);
    }

    public IReadOnlyList<BookmarkItem> GetBookmarksForCurrentDocument()
    {
        var filePath = Editor.FilePath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Array.Empty<BookmarkItem>();
        }

        return Bookmarks
            .Concat(GlobalBookmarks)
            .Where(bookmark => string.Equals(bookmark.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            .OrderBy(bookmark => bookmark.LineNumber)
            .ThenBy(bookmark => bookmark.IsGlobal)
            .ToList();
    }

    public async Task NavigateToBookmarkAsync(BookmarkItem bookmark)
    {
        if (bookmark == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(bookmark.FilePath) || !File.Exists(bookmark.FilePath))
        {
            var recovered = await TryRecoverBookmarkPathAsync(bookmark);
            if (!recovered)
            {
                bookmark.IsStale = true;
                BookmarksPanel?.RefreshView();
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(bookmark.FilePath))
        {
            await OpenFileFromPathAsync(bookmark.FilePath);
        }

        RefreshBookmarksForActiveEditor();
        Editor.NavigateToLine(bookmark.LineNumber);
    }

    public int ClearBookmarksForCurrentDocument()
    {
        var filePath = Editor.FilePath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return 0;
        }

        var targets = Bookmarks
            .Concat(GlobalBookmarks)
            .Where(bookmark => string.Equals(bookmark.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var bookmark in targets)
        {
            RemoveBookmark(bookmark);
        }

        return targets.Count;
    }

    public int ClearAllBookmarks()
    {
        var count = Bookmarks.Count + GlobalBookmarks.Count;
        Bookmarks.Clear();
        GlobalBookmarks.Clear();
        return count;
    }

    public int ClearScopedBookmarks()
    {
        var count = Bookmarks.Count;
        Bookmarks.Clear();
        return count;
    }

    public int ClearGlobalBookmarks()
    {
        var count = GlobalBookmarks.Count;
        GlobalBookmarks.Clear();
        return count;
    }

    public void RemoveBookmark(BookmarkItem bookmark)
    {
        if (bookmark.IsGlobal)
        {
            GlobalBookmarks.Remove(bookmark);
            return;
        }

        Bookmarks.Remove(bookmark);
    }

    public void SetBookmarkScopes(IDictionary<string, List<BookmarkItemData>> bookmarkScopes)
    {
        _bookmarkScopes.Clear();
        GlobalBookmarks.Clear();
        foreach (var pair in bookmarkScopes)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            if (string.Equals(pair.Key, GlobalBookmarkScopeKey, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in pair.Value.Where(item => item != null).Select(FromBookmarkData))
                {
                    item.IsGlobal = true;
                    GlobalBookmarks.Add(item);
                }

                SortBookmarkCollection(GlobalBookmarks);
                continue;
            }

            _bookmarkScopes[pair.Key] = pair.Value
                .Where(item => item != null)
                .Select(FromBookmarkData)
                .OrderBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.LineNumber)
                .ToList();
        }

        _activeBookmarkScopeKey = GetBookmarkScopeKey();
        ReplaceBookmarksForScope(_activeBookmarkScopeKey);
    }

    public Dictionary<string, List<BookmarkItemData>> GetBookmarkScopesData()
    {
        PersistCurrentBookmarkScope();

        return _bookmarkScopes.ToDictionary(
            pair => pair.Key,
            pair => pair.Value
                .Select(ToBookmarkData)
                .ToList(),
            StringComparer.OrdinalIgnoreCase)
            .Append(new KeyValuePair<string, List<BookmarkItemData>>(
                GlobalBookmarkScopeKey,
                GlobalBookmarks.Select(ToBookmarkData).ToList()))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    public List<BookmarkItemData> GetCurrentBookmarkScopeData()
    {
        PersistCurrentBookmarkScope();

        return Bookmarks
            .Select(ToBookmarkData)
            .OrderBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.LineNumber)
            .ToList();
    }

    public List<BookmarkItemData> GetCurrentDocumentBookmarkData()
    {
        var filePath = Editor.FilePath ?? string.Empty;
        return Bookmarks
            .Concat(GlobalBookmarks)
            .Where(item => string.Equals(item.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            .Select(ToBookmarkData)
            .OrderBy(item => item.LineNumber)
            .ThenBy(item => item.IsGlobal)
            .ToList();
    }

    public int ImportBookmarksIntoCurrentScope(IEnumerable<BookmarkItemData> bookmarks, bool replaceCurrentScope = false)
    {
        if (replaceCurrentScope)
        {
            Bookmarks.Clear();
        }

        var imported = 0;
        foreach (var bookmark in bookmarks)
        {
            if (bookmark == null || string.IsNullOrWhiteSpace(bookmark.FilePath) || bookmark.LineNumber <= 0)
            {
                continue;
            }

            var target = bookmark.IsGlobal ? GlobalBookmarks : Bookmarks;

            var existing = target.FirstOrDefault(item =>
                string.Equals(item.FilePath, bookmark.FilePath, StringComparison.OrdinalIgnoreCase)
                && item.LineNumber == bookmark.LineNumber);

            if (existing != null)
            {
                if (bookmark.CreatedAt >= existing.CreatedAt)
                {
                    existing.Text = bookmark.Text;
                    existing.CreatedAt = bookmark.CreatedAt;
                    existing.AnchorFingerprint = string.IsNullOrWhiteSpace(bookmark.AnchorFingerprint)
                        ? NormalizeBookmarkText(bookmark.Text)
                        : bookmark.AnchorFingerprint;
                    existing.ContextBefore = bookmark.ContextBefore;
                    existing.ContextAfter = bookmark.ContextAfter;
                    existing.IsGlobal = bookmark.IsGlobal;
                    existing.IsStale = bookmark.IsStale;
                    imported++;
                }

                continue;
            }

            target.Add(FromBookmarkData(bookmark));
            imported++;
        }

        SortBookmarkCollection(Bookmarks);
        SortBookmarkCollection(GlobalBookmarks);
        PersistCurrentBookmarkScope();
        return imported;
    }

    public string GetCurrentBookmarkScopeDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(Explorer.CurrentWorkspacePath))
        {
            return Path.GetFileNameWithoutExtension(Explorer.CurrentWorkspacePath);
        }

        if (!string.IsNullOrWhiteSpace(Explorer.CurrentFolderPath))
        {
            var folderName = Path.GetFileName(Explorer.CurrentFolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.IsNullOrWhiteSpace(folderName) ? "folder" : folderName;
        }

        return "bookmarks";
    }

    public async Task SaveCurrentAsync()
    {
        await Editor.SaveAsync();
        if (!string.IsNullOrWhiteSpace(Editor.FilePath))
        {
            AddRecentFile(Editor.FilePath);
        }
    }

    public async Task SaveAsAsync(string path)
    {
        await Editor.SaveAsAsync(path);
        AddRecentFile(path);
    }

    public void ClearRecentFiles()
    {
        RecentFiles.Clear();
    }

    public bool RemoveRecentFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var removed = false;
        for (var i = RecentFiles.Count - 1; i >= 0; i--)
        {
            if (!string.Equals(RecentFiles[i].Path, path, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            RecentFiles.RemoveAt(i);
            removed = true;
        }

        return removed;
    }

    public void SetRecentFiles(IEnumerable<string> paths)
    {
        RecentFiles.Clear();
        foreach (var path in paths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                RecentFiles.Add(new RecentFileItem(path));
            }
        }
    }

    public void SetRecentProjects(IEnumerable<string> paths)
    {
        RecentProjects.Clear();
        foreach (var path in paths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                RecentProjects.Add(new RecentProjectItem(path));
            }
        }
    }

    public List<string> GetRecentFilesData()
    {
        return RecentFiles
            .Where(f => !string.IsNullOrWhiteSpace(f.Path))
            .Select(f => f.Path)
            .ToList();
    }

    public List<string> GetRecentProjectsData()
    {
        return RecentProjects
            .Where(project => !string.IsNullOrWhiteSpace(project.Path))
            .Select(project => project.Path)
            .ToList();
    }

    public string GetCurrentWorkspacePathData()
    {
        return Explorer.CurrentWorkspacePath ?? string.Empty;
    }

    public string GetCurrentFolderPathData()
    {
        return Explorer.CurrentFolderPath ?? string.Empty;
    }

    public List<string> GetOpenDocumentPathsData()
    {
        return Tabs
            .Select(tab => tab.Editor.FilePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public List<OpenDocumentStateData> GetOpenDocumentSessionData()
    {
        return Tabs
            .Where(tab => !string.IsNullOrWhiteSpace(tab.Editor.FilePath))
            .Select(tab => new OpenDocumentStateData
            {
                Path = tab.Editor.FilePath!,
                CaretIndex = tab.Editor.CaretIndex
            })
            .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    public int GetActiveOpenDocumentIndexData()
    {
        if (SelectedTab == null)
        {
            return 0;
        }

        var openDocumentTabs = Tabs
            .Where(tab => !string.IsNullOrWhiteSpace(tab.Editor.FilePath))
            .ToList();

        if (openDocumentTabs.Count == 0)
        {
            return 0;
        }

        var activeIndex = openDocumentTabs.IndexOf(SelectedTab);
        return activeIndex >= 0 ? activeIndex : 0;
    }

    public async Task RestoreOpenDocumentsSessionAsync(IEnumerable<string> paths, int activeIndex = 0)
    {
        var candidates = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(System.IO.File.Exists)
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        foreach (var path in candidates)
        {
            await OpenFileFromPathAsync(path);
        }

        var openDocumentTabs = Tabs
            .Where(tab => !string.IsNullOrWhiteSpace(tab.Editor.FilePath))
            .ToList();

        if (openDocumentTabs.Count == 0)
        {
            return;
        }

        var normalizedIndex = Math.Clamp(activeIndex, 0, openDocumentTabs.Count - 1);
        SelectedTab = openDocumentTabs[normalizedIndex];
    }

    public async Task RestoreOpenDocumentsSessionAsync(IEnumerable<OpenDocumentStateData> documents, int activeIndex = 0)
    {
        var candidates = documents
            .Where(item => !string.IsNullOrWhiteSpace(item.Path))
            .GroupBy(item => item.Path.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Where(item => System.IO.File.Exists(item.Path))
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        foreach (var document in candidates)
        {
            await OpenFileFromPathAsync(document.Path);
            var opened = SelectedTab?.Editor;
            if (opened == null || !string.Equals(opened.FilePath, document.Path, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var safeCaret = Math.Clamp(document.CaretIndex, 0, opened.Text?.Length ?? 0);
            opened.RequestCaretIndex(safeCaret);
        }

        var openDocumentTabs = Tabs
            .Where(tab => !string.IsNullOrWhiteSpace(tab.Editor.FilePath))
            .ToList();

        if (openDocumentTabs.Count == 0)
        {
            return;
        }

        var normalizedIndex = Math.Clamp(activeIndex, 0, openDocumentTabs.Count - 1);
        SelectedTab = openDocumentTabs[normalizedIndex];
    }

    /// <summary>
    /// Adds all currently open tabs (that have file paths) to the recently closed list.
    /// Call this before the app exits so that open editors are preserved.
    /// </summary>
    public void AddOpenTabsToRecentEditors()
    {
        foreach (var tab in Tabs)
        {
            AddRecentEditor(tab);
        }
    }

    public async Task TriggerAutoSaveAsync(AutoSaveMode mode)
    {
        if (Settings.AutoSaveMode != mode)
        {
            return;
        }

        await Editor.AutoSaveAsync();
    }

    private void TriggerDelayedAutoSave(EditorViewModel editor)
    {
        _ = editor.AutoSaveAsync();
    }

    private void OnExplorerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ExplorerViewModel.CurrentWorkspacePath) or nameof(ExplorerViewModel.CurrentFolderPath))
        {
            SyncBookmarksToCurrentScope();
        }
    }

    private void SyncBookmarksToCurrentScope()
    {
        var nextScopeKey = GetBookmarkScopeKey();
        if (string.Equals(_activeBookmarkScopeKey, nextScopeKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        PersistCurrentBookmarkScope();
        _activeBookmarkScopeKey = nextScopeKey;
        ReplaceBookmarksForScope(nextScopeKey);
    }

    private void PersistCurrentBookmarkScope()
    {
        var snapshot = CreateBookmarkSnapshot(Bookmarks);

        if (snapshot.Count == 0)
        {
            _bookmarkScopes.Remove(_activeBookmarkScopeKey);
            return;
        }

        _bookmarkScopes[_activeBookmarkScopeKey] = snapshot;
    }

    private void ReplaceBookmarksForScope(string scopeKey)
    {
        if (!_bookmarkScopes.TryGetValue(scopeKey, out var bookmarks))
        {
            Bookmarks.Clear();
            return;
        }

        ReplaceBookmarks(Bookmarks, bookmarks);
    }

    private void SortBookmarks()
    {
        SortBookmarkCollection(Bookmarks);
    }

    private List<BookmarkItem> CreateBookmarkSnapshot()
    {
        return CreateBookmarkSnapshot(Bookmarks);
    }

    private string GetBookmarkScopeKey()
    {
        if (!string.IsNullOrWhiteSpace(Explorer.CurrentWorkspacePath))
        {
            return $"workspace::{Explorer.CurrentWorkspacePath}";
        }

        if (!string.IsNullOrWhiteSpace(Explorer.CurrentFolderPath))
        {
            return $"folder::{Explorer.CurrentFolderPath}";
        }

        return DefaultBookmarkScopeKey;
    }

    private BookmarkItem? GetRelativeBookmark(bool forward)
    {
        var allBookmarks = Bookmarks.Concat(GlobalBookmarks)
            .OrderBy(bookmark => bookmark.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(bookmark => bookmark.LineNumber)
            .ThenBy(bookmark => bookmark.IsGlobal)
            .ToList();

        if (allBookmarks.Count == 0)
        {
            return null;
        }

        var editor = Editor;
        var activeFilePath = editor.FilePath ?? string.Empty;

        var currentIndex = allBookmarks.FindIndex(bookmark =>
            string.Equals(bookmark.FilePath, activeFilePath, StringComparison.OrdinalIgnoreCase)
            && bookmark.LineNumber >= editor.CaretLine);

        if (forward)
        {
            if (currentIndex >= 0)
            {
                var exact = allBookmarks[currentIndex];
                if (string.Equals(exact.FilePath, activeFilePath, StringComparison.OrdinalIgnoreCase)
                    && exact.LineNumber == editor.CaretLine)
                {
                    currentIndex++;
                }

                return allBookmarks[currentIndex % allBookmarks.Count];
            }

            return allBookmarks[0];
        }

        var previousIndex = allBookmarks.FindLastIndex(bookmark =>
            string.Compare(bookmark.FilePath, activeFilePath, StringComparison.OrdinalIgnoreCase) < 0
            || (string.Equals(bookmark.FilePath, activeFilePath, StringComparison.OrdinalIgnoreCase)
                && bookmark.LineNumber < editor.CaretLine));

        return previousIndex >= 0 ? allBookmarks[previousIndex] : allBookmarks[^1];
    }

    private static string GetLinePreview(string text, int lineNumber)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n');
        if (lineNumber <= 0 || lineNumber > lines.Length)
        {
            return string.Empty;
        }

        return lines[lineNumber - 1].Trim();
    }

    private void ToggleZoomFlyout()
    {
        IsZoomFlyoutOpen = !IsZoomFlyoutOpen;
    }

    private void StepZoom(int delta)
    {
        var currentIndex = GetNearestZoomIndex(Settings.EditorFontSize);
        var nextIndex = Math.Clamp(currentIndex + delta, 0, ZoomLevels.Length - 1);
        SetZoomByIndex(nextIndex, updateSlider: true);
    }

    private void SetZoomByIndex(int index, bool updateSlider)
    {
        index = Math.Clamp(index, 0, ZoomLevels.Length - 1);

        if (updateSlider && Math.Abs(_zoomSliderIndex - index) >= 0.001)
        {
            _syncingZoomSlider = true;
            this.RaiseAndSetIfChanged(ref _zoomSliderIndex, index, nameof(ZoomSliderIndex));
            _syncingZoomSlider = false;
        }

        SetZoom(ZoomLevels[index]);
    }

    private void SetZoom(double size)
    {
        var clamped = Math.Clamp(size, ZoomLevels[0], ZoomLevels[^1]);
        if (Math.Abs(Settings.EditorFontSize - clamped) < 0.001)
        {
            return;
        }

        Settings.EditorFontSize = clamped;
    }

    private static int GetNearestZoomIndex(double size)
    {
        var nearestIndex = 0;
        var nearestDistance = double.MaxValue;

        for (var i = 0; i < ZoomLevels.Length; i++)
        {
            var distance = Math.Abs(ZoomLevels[i] - size);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    public void ToggleExplorer()
    {
        if (IsExplorerVisible)
        {
            CloseToolPanel();
            return;
        }

        ShowToolPanel(Explorer, "Explorer");
    }

    public void ToggleSearch()
    {
        if (IsSearchVisible)
        {
            CloseToolPanel();
            return;
        }

        ShowToolPanel(Search, "Search");
    }

    public void ToggleBookmarks()
    {
        if (BookmarksPanel == null)
        {
            return;
        }

        if (IsBookmarksVisible)
        {
            CloseToolPanel();
            return;
        }

        ShowToolPanel(BookmarksPanel, "Bookmarks");
    }

    private void ShowToolPanel(ViewModelBase viewModel, string title)
    {
        ToolPanelViewModel = viewModel;
        ToolPanelTitle = title;
        Settings.IsExplorerVisible = viewModel == Explorer;
        Settings.IsSearchVisible = viewModel == Search;
        Settings.IsBookmarksVisible = viewModel == BookmarksPanel;
    }

    private void CloseToolPanel()
    {
        ToolPanelViewModel = null;
        ToolPanelTitle = string.Empty;
        Settings.IsExplorerVisible = false;
        Settings.IsSearchVisible = false;
        Settings.IsBookmarksVisible = false;
    }

    private void AddRecentFile(string path)
    {
        for (var i = RecentFiles.Count - 1; i >= 0; i--)
        {
            if (string.Equals(RecentFiles[i].Path, path, StringComparison.OrdinalIgnoreCase))
            {
                RecentFiles.RemoveAt(i);
            }
        }

        RecentFiles.Insert(0, new RecentFileItem(path));

        const int maxRecentFiles = 10;
        while (RecentFiles.Count > maxRecentFiles)
        {
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
        }
    }

    private void AddNewTab()
    {
        var tab = AddNewTabInternal(isUntitled: true, isMarkdown: false);
        SelectedTab = tab;
    }

    private EditorTabViewModel AddNewTabInternal(bool isUntitled, bool isMarkdown = false)
    {
        RemoveWelcomeTabIfPresent();

        var editor = new EditorViewModel(Settings);
        if (isUntitled)
        {
            editor.SetUntitledName(isMarkdown ? GetNextUntitledMarkdownName() : GetNextUntitledName());
            if (isMarkdown)
            {
                editor.NewMarkdownDocument();
            }
            else
            {
                editor.NewDocument();
            }
        }
        var tab = new EditorTabViewModel(editor);
        InsertTab(tab);
        return tab;
    }

    private EditorTabViewModel AddTemplatedTabInternal(string language, string extension, string templateContent)
    {
        RemoveWelcomeTabIfPresent();

        var editor = new EditorViewModel(Settings);
        editor.SetUntitledName(GetNextUntitledNameForExtension(extension));
        editor.NewDocument(language, templateContent, markDirty: !string.IsNullOrEmpty(templateContent));

        var tab = new EditorTabViewModel(editor);
        InsertTab(tab);
        return tab;
    }

    private EditorTabViewModel? FindReusableUntitledTab()
    {
        foreach (var tab in Tabs)
        {
            if (tab.IsWelcomeTab)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(tab.Editor.FilePath))
            {
                continue;
            }

            if (tab.Editor.HasUnsavedChanges)
            {
                continue;
            }

            if (string.IsNullOrEmpty(tab.Editor.Text))
            {
                return tab;
            }
        }

        return Tabs.FirstOrDefault(tab =>
            string.IsNullOrWhiteSpace(tab.Editor.FilePath) && !tab.Editor.HasUnsavedChanges);
    }

    public void CloseTab(EditorTabViewModel tab)
    {
        if (!Tabs.Contains(tab))
        {
            return;
        }

        AddRecentEditor(tab);
        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        if (Tabs.Count == 0)
        {
            EnsureWelcomeTab();
            return;
        }

        var nextIndex = Math.Clamp(index - 1, 0, Tabs.Count - 1);
        SelectedTab = Tabs[nextIndex];
    }

    public void TogglePinTab(EditorTabViewModel tab)
    {
        if (!Tabs.Contains(tab) || tab.IsWelcomeTab)
        {
            return;
        }

        var wasSelected = SelectedTab == tab;
        tab.IsPinned = !tab.IsPinned;
        MoveTabToPinnedSection(tab);

        if (wasSelected)
        {
            SelectedTab = tab;
        }
    }

    public async Task<EditorTabViewModel?> DuplicateTabAsync(EditorTabViewModel sourceTab)
    {
        if (!Tabs.Contains(sourceTab) || sourceTab.IsWelcomeTab)
        {
            return null;
        }

        RemoveWelcomeTabIfPresent();

        var sourceEditor = sourceTab.Editor;
        var duplicateEditor = new EditorViewModel(Settings);
        var duplicateTab = new EditorTabViewModel(duplicateEditor)
        {
            IsPinned = sourceTab.IsPinned
        };

        if (!string.IsNullOrWhiteSpace(sourceEditor.FilePath) && !sourceEditor.HasUnsavedChanges)
        {
            await duplicateEditor.LoadFromFileAsync(sourceEditor.FilePath);
        }
        else
        {
            var extension = Path.GetExtension(sourceEditor.FileName ?? string.Empty);
            duplicateEditor.SetUntitledName(GetNextUntitledNameForExtension(extension));
            duplicateEditor.NewDocument(sourceEditor.Language, sourceEditor.Text, markDirty: true);
        }

        InsertTab(duplicateTab, sourceTab);
        SelectedTab = duplicateTab;
        return duplicateTab;
    }

    public bool RevealTabInExplorer(EditorTabViewModel tab)
    {
        if (tab == null || string.IsNullOrWhiteSpace(tab.Editor.FilePath))
        {
            return false;
        }

        var revealed = Explorer.RevealPath(tab.Editor.FilePath);
        if (revealed)
        {
            ShowToolPanel(Explorer, "Explorer");
        }

        return revealed;
    }

    private void CloseActiveTab()
    {
        if (SelectedTab != null)
        {
            CloseTab(SelectedTab);
        }
    }

    public void SelectNextTab()
    {
        if (Tabs.Count <= 1 || SelectedTab == null) return;
        var index = Tabs.IndexOf(SelectedTab);
        SelectedTab = Tabs[(index + 1) % Tabs.Count];
    }

    public void SelectPreviousTab()
    {
        if (Tabs.Count <= 1 || SelectedTab == null) return;
        var index = Tabs.IndexOf(SelectedTab);
        SelectedTab = Tabs[(index - 1 + Tabs.Count) % Tabs.Count];
    }

    public async Task SaveAllAsync()
    {
        foreach (var tab in Tabs)
        {
            if (tab.Editor.HasUnsavedChanges && !string.IsNullOrWhiteSpace(tab.Editor.FilePath))
            {
                await tab.Editor.SaveAsync();
            }
        }
    }

    public void CloseAllTabs()
    {
        foreach (var tab in Tabs)
        {
            AddRecentEditor(tab);
        }
        Tabs.Clear();
        EnsureWelcomeTab();
    }

    public void AddRecentProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        for (var i = RecentProjects.Count - 1; i >= 0; i--)
        {
            if (string.Equals(RecentProjects[i].Path, path, StringComparison.OrdinalIgnoreCase))
            {
                RecentProjects.RemoveAt(i);
            }
        }

        RecentProjects.Insert(0, new RecentProjectItem(path));

        const int maxRecentProjects = 10;
        while (RecentProjects.Count > maxRecentProjects)
        {
            RecentProjects.RemoveAt(RecentProjects.Count - 1);
        }
    }

    public void ClearRecentEditors()
    {
        Explorer.RecentEditors.Clear();
    }

    public bool RemoveRecentEditor(RecentEditorItem? item)
    {
        if (item == null)
        {
            return false;
        }

        var removed = RemoveRecentEditorByPath(item.Path);
        if (removed)
        {
            RemoveRecentFile(item.Path);
        }

        return removed;
    }

    public void SetRecentEditors(IEnumerable<RecentEditorData> items)
    {
        Explorer.RecentEditors.Clear();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Path))
            {
                continue;
            }
            Explorer.RecentEditors.Add(new RecentEditorItem(item.Title, item.Path));
        }
    }

    public List<RecentEditorData> GetRecentEditorsData()
    {
        var list = new List<RecentEditorData>();
        foreach (var item in Explorer.RecentEditors)
        {
            if (string.IsNullOrWhiteSpace(item.Path))
            {
                continue;
            }

            list.Add(new RecentEditorData
            {
                Title = item.Title,
                Path = item.Path
            });
        }

        return list;
    }

    public void ActivateTab(EditorTabViewModel tab)
    {
        if (Tabs.Contains(tab))
        {
            SelectedTab = tab;
        }
    }

    private void InsertTab(EditorTabViewModel tab, EditorTabViewModel? afterTab = null)
    {
        if (Tabs.Count == 0)
        {
            Tabs.Add(tab);
            return;
        }

        var insertIndex = Tabs.Count;

        if (afterTab != null)
        {
            var afterIndex = Tabs.IndexOf(afterTab);
            if (afterIndex >= 0)
            {
                insertIndex = afterIndex + 1;
            }
        }

        if (tab.IsPinned)
        {
            var lastPinnedIndex = -1;
            for (var index = 0; index < Tabs.Count; index++)
            {
                if (Tabs[index].IsPinned)
                {
                    lastPinnedIndex = index;
                }
            }

            if (afterTab?.IsPinned == true && Tabs.Contains(afterTab))
            {
                insertIndex = Tabs.IndexOf(afterTab) + 1;
            }
            else
            {
                insertIndex = lastPinnedIndex + 1;
            }
        }
        else
        {
            var lastPinnedIndex = Tabs.TakeWhile(candidate => candidate.IsPinned).Count();
            insertIndex = Math.Max(insertIndex, lastPinnedIndex);
        }

        insertIndex = Math.Clamp(insertIndex, 0, Tabs.Count);
        Tabs.Insert(insertIndex, tab);
    }

    private void MoveTabToPinnedSection(EditorTabViewModel tab)
    {
        var currentIndex = Tabs.IndexOf(tab);
        if (currentIndex < 0)
        {
            return;
        }

        Tabs.RemoveAt(currentIndex);

        var insertIndex = tab.IsPinned
            ? Tabs.TakeWhile(candidate => candidate.IsPinned).Count()
            : Tabs.TakeWhile(candidate => candidate.IsPinned).Count();

        Tabs.Insert(insertIndex, tab);
    }

    public async Task OpenRecentEditorAsync(RecentEditorItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Path))
        {
            return;
        }

        await OpenFileFromPathAsync(item.Path);
    }

    private void AddRecentEditor(EditorTabViewModel tab)
    {
        if (tab.IsWelcomeTab)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(tab.Editor.FilePath))
        {
            return;
        }

        var title = tab.Editor.FileName;
        var path = tab.Editor.FilePath;
        if (path == null) return;

        for (var i = Explorer.RecentEditors.Count - 1; i >= 0; i--)
        {
            if (string.Equals(Explorer.RecentEditors[i].Path, path, StringComparison.OrdinalIgnoreCase))
            {
                Explorer.RecentEditors.RemoveAt(i);
            }
        }

        Explorer.RecentEditors.Insert(0, new RecentEditorItem(title, path));

        const int maxRecent = 10;
        while (Explorer.RecentEditors.Count > maxRecent)
        {
            Explorer.RecentEditors.RemoveAt(Explorer.RecentEditors.Count - 1);
        }
    }

    private bool RemoveRecentEditorByPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var removed = false;
        for (var i = Explorer.RecentEditors.Count - 1; i >= 0; i--)
        {
            if (!string.Equals(Explorer.RecentEditors[i].Path, path, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Explorer.RecentEditors.RemoveAt(i);
            removed = true;
        }

        return removed;
    }

    private string GetNextUntitledName()
    {
        var max = 0;
        foreach (var tab in Tabs)
        {
            if (tab.IsWelcomeTab)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(tab.Editor.FilePath))
            {
                continue;
            }

            var number = ParseUntitledNumber(tab.Editor.FileName);
            max = Math.Max(max, number);
        }

        if (max == 0)
        {
            return "Untitled (1)";
        }

        return $"Untitled ({max + 1})";
    }

    private string GetNextUntitledMarkdownName()
    {
        var max = 0;
        foreach (var tab in Tabs)
        {
            if (tab.IsWelcomeTab)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(tab.Editor.FilePath))
            {
                continue;
            }

            var number = ParseUntitledMarkdownNumber(tab.Editor.FileName);
            max = Math.Max(max, number);
        }

        if (max == 0)
        {
            return "Untitled (1).md";
        }

        return $"Untitled ({max + 1}).md";
    }

    private string GetNextUntitledNameForExtension(string extension)
    {
        var normalizedExtension = extension.StartsWith('.') ? extension : $".{extension}";
        var max = 0;

        foreach (var tab in Tabs)
        {
            if (tab.IsWelcomeTab)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(tab.Editor.FilePath))
            {
                continue;
            }

            var number = ParseUntitledNumberForExtension(tab.Editor.FileName, normalizedExtension);
            max = Math.Max(max, number);
        }

        if (max == 0)
        {
            return $"Untitled (1){normalizedExtension}";
        }

        return $"Untitled ({max + 1}){normalizedExtension}";
    }

    private static int ParseUntitledNumber(string name)
    {
        var open = name.IndexOf('(');
        var close = name.IndexOf(')');
        if (open >= 0 && close > open)
        {
            var slice = name.Substring(open + 1, close - open - 1);
            if (int.TryParse(slice, out var number))
            {
                return number;
            }
        }

        return name.StartsWith("Untitled", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private static int ParseUntitledMarkdownNumber(string name)
    {
        if (!name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var baseName = name[..^3];
        return ParseUntitledNumber(baseName);
    }

    private static int ParseUntitledNumberForExtension(string name, string extension)
    {
        if (!name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var baseName = name[..^extension.Length];
        return ParseUntitledNumber(baseName);
    }

    private void EnsureWelcomeTab()
    {
        if (Tabs.Any(tab => tab.IsWelcomeTab))
        {
            SelectedTab = Tabs.First(tab => tab.IsWelcomeTab);
            return;
        }

        var welcomeEditor = new EditorViewModel(Settings);
        welcomeEditor.SetUntitledName("Welcome");
        welcomeEditor.NewDocument();

        var welcomeTab = new EditorTabViewModel(welcomeEditor, isWelcomeTab: true);
        Tabs.Insert(0, welcomeTab);
        SelectedTab = welcomeTab;
    }

    private void RemoveWelcomeTabIfPresent()
    {
        for (var i = Tabs.Count - 1; i >= 0; i--)
        {
            if (Tabs[i].IsWelcomeTab)
            {
                Tabs.RemoveAt(i);
            }
        }
    }

}
