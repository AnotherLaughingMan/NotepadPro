using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using NotepadPro.Models;
using ReactiveUI;

namespace NotepadPro.ViewModels;

public enum BookmarkPanelFilterMode
{
    All,
    CurrentFileOnly,
    StaleOnly,
    ScopedOnly,
    GlobalOnly
}

public enum BookmarkPanelSortMode
{
    Line,
    Path,
    Newest
}

public sealed class BookmarksViewModel : ViewModelBase
{
    private readonly Func<BookmarkItem, Task> _openBookmarkAsync;
    private readonly Action<BookmarkItem> _removeBookmark;
    private readonly Action _toggleScopedBookmark;
    private readonly Action _toggleGlobalBookmark;
    private readonly Func<int> _clearScopedBookmarks;
    private readonly Func<int> _clearGlobalBookmarks;
    private readonly Func<string?> _getCurrentFilePath;
    private string _searchQuery = string.Empty;
    private BookmarkPanelFilterMode _filterMode;
    private BookmarkPanelSortMode _sortMode;

    public BookmarksViewModel(
        ObservableCollection<BookmarkItem> scopedBookmarks,
        ObservableCollection<BookmarkItem> globalBookmarks,
        Func<BookmarkItem, Task> openBookmarkAsync,
        Action<BookmarkItem> removeBookmark,
        Action toggleScopedBookmark,
        Action toggleGlobalBookmark,
        Func<int> clearScopedBookmarks,
        Func<int> clearGlobalBookmarks,
        Func<string?> getCurrentFilePath)
    {
        ScopedBookmarks = scopedBookmarks;
        GlobalBookmarks = globalBookmarks;
        _openBookmarkAsync = openBookmarkAsync;
        _removeBookmark = removeBookmark;
        _toggleScopedBookmark = toggleScopedBookmark;
        _toggleGlobalBookmark = toggleGlobalBookmark;
        _clearScopedBookmarks = clearScopedBookmarks;
        _clearGlobalBookmarks = clearGlobalBookmarks;
        _getCurrentFilePath = getCurrentFilePath;

        FilteredScopedBookmarks = new ObservableCollection<BookmarkItem>();
        FilteredGlobalBookmarks = new ObservableCollection<BookmarkItem>();
        FilterOptions = Enum.GetValues<BookmarkPanelFilterMode>();
        SortOptions = Enum.GetValues<BookmarkPanelSortMode>();

        AddScopedBookmarkCommand = ReactiveCommand.Create(() => _toggleScopedBookmark());
        AddGlobalBookmarkCommand = ReactiveCommand.Create(() => _toggleGlobalBookmark());
        ClearScopedBookmarksCommand = ReactiveCommand.Create(() => { _clearScopedBookmarks(); });
        ClearGlobalBookmarksCommand = ReactiveCommand.Create(() => { _clearGlobalBookmarks(); });
        ShowAllCommand = ReactiveCommand.Create(() => { FilterMode = BookmarkPanelFilterMode.All; });
        ShowCurrentFileCommand = ReactiveCommand.Create(() => { FilterMode = BookmarkPanelFilterMode.CurrentFileOnly; });
        ShowStaleOnlyCommand = ReactiveCommand.Create(() => { FilterMode = BookmarkPanelFilterMode.StaleOnly; });
        ShowScopedOnlyCommand = ReactiveCommand.Create(() => { FilterMode = BookmarkPanelFilterMode.ScopedOnly; });
        ShowGlobalOnlyCommand = ReactiveCommand.Create(() => { FilterMode = BookmarkPanelFilterMode.GlobalOnly; });
        ClearFiltersCommand = ReactiveCommand.Create(ClearFilters);

        ScopedBookmarks.CollectionChanged += OnBookmarksCollectionChanged;
        GlobalBookmarks.CollectionChanged += OnBookmarksCollectionChanged;
        RefreshView();
    }

    public ObservableCollection<BookmarkItem> ScopedBookmarks { get; }

    public ObservableCollection<BookmarkItem> GlobalBookmarks { get; }

    public ObservableCollection<BookmarkItem> FilteredScopedBookmarks { get; }

    public ObservableCollection<BookmarkItem> FilteredGlobalBookmarks { get; }

    public IReadOnlyList<BookmarkPanelFilterMode> FilterOptions { get; }

    public IReadOnlyList<BookmarkPanelSortMode> SortOptions { get; }

    public ReactiveCommand<Unit, Unit> AddScopedBookmarkCommand { get; }

    public ReactiveCommand<Unit, Unit> AddGlobalBookmarkCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearScopedBookmarksCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearGlobalBookmarksCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowAllCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowCurrentFileCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowStaleOnlyCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowScopedOnlyCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowGlobalOnlyCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchQuery, value);
            RefreshView();
        }
    }

    public BookmarkPanelFilterMode FilterMode
    {
        get => _filterMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _filterMode, value);
            this.RaisePropertyChanged(nameof(IsShowingAll));
            this.RaisePropertyChanged(nameof(IsShowingCurrentFileOnly));
            this.RaisePropertyChanged(nameof(IsShowingStaleOnly));
            this.RaisePropertyChanged(nameof(IsShowingScopedOnly));
            this.RaisePropertyChanged(nameof(IsShowingGlobalOnly));
            this.RaisePropertyChanged(nameof(IsAnyFilterActive));
            RefreshView();
        }
    }

    public BookmarkPanelSortMode SortMode
    {
        get => _sortMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _sortMode, value);
            RefreshView();
        }
    }

    public int ScopedCount => ScopedBookmarks.Count;

    public int GlobalCount => GlobalBookmarks.Count;

    public int VisibleScopedCount => FilteredScopedBookmarks.Count;

    public int VisibleGlobalCount => FilteredGlobalBookmarks.Count;

    public bool HasScopedBookmarks => ScopedCount > 0;

    public bool HasGlobalBookmarks => GlobalCount > 0;

    public bool HasVisibleScopedBookmarks => VisibleScopedCount > 0;

    public bool HasVisibleGlobalBookmarks => VisibleGlobalCount > 0;

    public bool IsShowingAll => FilterMode == BookmarkPanelFilterMode.All;

    public bool IsShowingCurrentFileOnly => FilterMode == BookmarkPanelFilterMode.CurrentFileOnly;

    public bool IsShowingStaleOnly => FilterMode == BookmarkPanelFilterMode.StaleOnly;

    public bool IsShowingScopedOnly => FilterMode == BookmarkPanelFilterMode.ScopedOnly;

    public bool IsShowingGlobalOnly => FilterMode == BookmarkPanelFilterMode.GlobalOnly;

    public bool IsAnyFilterActive => FilterMode != BookmarkPanelFilterMode.All || !string.IsNullOrWhiteSpace(SearchQuery);

    public Task OpenBookmarkAsync(BookmarkItem? bookmark)
    {
        if (bookmark == null)
        {
            return Task.CompletedTask;
        }

        return _openBookmarkAsync(bookmark);
    }

    public void RemoveBookmark(BookmarkItem? bookmark)
    {
        if (bookmark == null)
        {
            return;
        }

        _removeBookmark(bookmark);
    }

    public void RefreshView()
    {
        ReplaceFilteredBookmarks(FilteredScopedBookmarks, BuildVisibleBookmarks(ScopedBookmarks));
        ReplaceFilteredBookmarks(FilteredGlobalBookmarks, BuildVisibleBookmarks(GlobalBookmarks));

        this.RaisePropertyChanged(nameof(ScopedCount));
        this.RaisePropertyChanged(nameof(GlobalCount));
        this.RaisePropertyChanged(nameof(VisibleScopedCount));
        this.RaisePropertyChanged(nameof(VisibleGlobalCount));
        this.RaisePropertyChanged(nameof(HasScopedBookmarks));
        this.RaisePropertyChanged(nameof(HasGlobalBookmarks));
        this.RaisePropertyChanged(nameof(HasVisibleScopedBookmarks));
        this.RaisePropertyChanged(nameof(HasVisibleGlobalBookmarks));
        this.RaisePropertyChanged(nameof(IsAnyFilterActive));
    }

    private void ClearFilters()
    {
        SearchQuery = string.Empty;
        FilterMode = BookmarkPanelFilterMode.All;
        SortMode = BookmarkPanelSortMode.Line;
    }

    private IEnumerable<BookmarkItem> BuildVisibleBookmarks(IEnumerable<BookmarkItem> source)
    {
        var currentFilePath = _getCurrentFilePath() ?? string.Empty;
        var query = SearchQuery?.Trim() ?? string.Empty;
        var filtered = source.Where(bookmark => MatchesFilter(bookmark, currentFilePath) && MatchesSearch(bookmark, query));
        return SortBookmarks(filtered);
    }

    private bool MatchesFilter(BookmarkItem bookmark, string currentFilePath)
    {
        return FilterMode switch
        {
            BookmarkPanelFilterMode.CurrentFileOnly => !string.IsNullOrWhiteSpace(currentFilePath)
                && string.Equals(bookmark.FilePath, currentFilePath, StringComparison.OrdinalIgnoreCase),
            BookmarkPanelFilterMode.StaleOnly => bookmark.IsStale,
            BookmarkPanelFilterMode.ScopedOnly => !bookmark.IsGlobal,
            BookmarkPanelFilterMode.GlobalOnly => bookmark.IsGlobal,
            _ => true
        };
    }

    private static bool MatchesSearch(BookmarkItem bookmark, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return bookmark.Text.Contains(query, StringComparison.OrdinalIgnoreCase)
            || bookmark.FilePath.Contains(query, StringComparison.OrdinalIgnoreCase)
            || bookmark.LineNumber.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<BookmarkItem> SortBookmarks(IEnumerable<BookmarkItem> bookmarks)
    {
        return SortMode switch
        {
            BookmarkPanelSortMode.Path => bookmarks
                .OrderBy(bookmark => bookmark.FilePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(bookmark => bookmark.LineNumber)
                .ThenByDescending(bookmark => bookmark.CreatedAt),
            BookmarkPanelSortMode.Newest => bookmarks
                .OrderByDescending(bookmark => bookmark.CreatedAt)
                .ThenBy(bookmark => bookmark.FilePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(bookmark => bookmark.LineNumber),
            _ => bookmarks
                .OrderBy(bookmark => bookmark.LineNumber)
                .ThenBy(bookmark => bookmark.FilePath, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(bookmark => bookmark.CreatedAt)
        };
    }

    private static void ReplaceFilteredBookmarks(ObservableCollection<BookmarkItem> target, IEnumerable<BookmarkItem> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private void OnBookmarksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshView();
    }
}