using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;

namespace NotepadPro.ViewModels;

public sealed class ExplorerViewModel : ViewModelBase
{
    private const string DefaultExplorerTitle = "Explorer";
    private const string DefaultRootHint = "Open a folder to browse files.";
    private string _rootPath = "Open a folder to browse files.";
    private string _explorerTitle = "Explorer";
    private ObservableCollection<EditorTabViewModel> _openEditors = new();
    private string? _currentFolderPath;
    private string? _currentWorkspacePath;
    private readonly ObservableCollection<string> _workspaceFolders = new();
    private bool _isOpenEditorsExpanded = true;
    private bool _isRecentEditorsExpanded = true;
    private bool _isFilesExpanded = true;
    private bool _isOpenEditorsVisible = true;
    private bool _isRecentEditorsVisible = true;
    private bool _isFilesVisible = true;
    private readonly HashSet<string> _expandedFolderPaths = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? ExpandedFolderPathsChanged;

    public ExplorerViewModel()
    {
        Items = new ObservableCollection<ExplorerItem>();
        RecentEditors = new ObservableCollection<RecentEditorItem>();
    }

    public ObservableCollection<ExplorerItem> Items { get; }

    public ObservableCollection<EditorTabViewModel> OpenEditors
    {
        get => _openEditors;
        private set => this.RaiseAndSetIfChanged(ref _openEditors, value);
    }

    public ObservableCollection<RecentEditorItem> RecentEditors { get; }

    public string RootPath
    {
        get => _rootPath;
        private set => this.RaiseAndSetIfChanged(ref _rootPath, value);
    }

    public string ExplorerTitle
    {
        get => _explorerTitle;
        private set => this.RaiseAndSetIfChanged(ref _explorerTitle, value);
    }

    public string? CurrentFolderPath => _currentFolderPath;

    public string? CurrentWorkspacePath => _currentWorkspacePath;

    public Func<string, Task>? OpenFileAsync { get; set; }

    public bool IsOpenEditorsExpanded
    {
        get => _isOpenEditorsExpanded;
        set
        {
            this.RaiseAndSetIfChanged(ref _isOpenEditorsExpanded, value);
            this.RaisePropertyChanged(nameof(IsOpenEditorsSectionVisible));
        }
    }

    public bool IsFilesExpanded
    {
        get => _isFilesExpanded;
        set
        {
            this.RaiseAndSetIfChanged(ref _isFilesExpanded, value);
            this.RaisePropertyChanged(nameof(IsFilesSectionVisible));
        }
    }

    public bool IsRecentEditorsExpanded
    {
        get => _isRecentEditorsExpanded;
        set
        {
            this.RaiseAndSetIfChanged(ref _isRecentEditorsExpanded, value);
            this.RaisePropertyChanged(nameof(IsRecentEditorsSectionVisible));
        }
    }

    public bool IsOpenEditorsVisible
    {
        get => _isOpenEditorsVisible;
        set
        {
            this.RaiseAndSetIfChanged(ref _isOpenEditorsVisible, value);
            this.RaisePropertyChanged(nameof(IsOpenEditorsSectionVisible));
        }
    }

    public bool IsRecentEditorsVisible
    {
        get => _isRecentEditorsVisible;
        set
        {
            this.RaiseAndSetIfChanged(ref _isRecentEditorsVisible, value);
            this.RaisePropertyChanged(nameof(IsRecentEditorsSectionVisible));
        }
    }

    public bool IsFilesVisible
    {
        get => _isFilesVisible;
        set
        {
            this.RaiseAndSetIfChanged(ref _isFilesVisible, value);
            this.RaisePropertyChanged(nameof(IsFilesSectionVisible));
        }
    }

    public bool IsOpenEditorsSectionVisible => IsOpenEditorsVisible && IsOpenEditorsExpanded;

    public bool IsRecentEditorsSectionVisible => IsRecentEditorsVisible && IsRecentEditorsExpanded;

    public bool IsFilesSectionVisible => IsFilesVisible && IsFilesExpanded;

    public void SetOpenEditors(ObservableCollection<EditorTabViewModel> tabs)
    {
        OpenEditors = tabs;
    }

    public void LoadFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        _currentWorkspacePath = null;
        this.RaisePropertyChanged(nameof(CurrentWorkspacePath));
        _workspaceFolders.Clear();
        _currentFolderPath = path;
        this.RaisePropertyChanged(nameof(CurrentFolderPath));

        ExplorerTitle = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(ExplorerTitle))
        {
            ExplorerTitle = path;
        }

        RootPath = path;
        RebuildItemsForFolder(path);
    }

    public void LoadWorkspace(string workspaceFilePath)
    {
        if (!File.Exists(workspaceFilePath))
        {
            return;
        }

        var folders = ParseWorkspaceFolders(workspaceFilePath);
        if (folders.Count == 0)
        {
            return;
        }

        _currentFolderPath = null;
    this.RaisePropertyChanged(nameof(CurrentFolderPath));
        _currentWorkspacePath = workspaceFilePath;
    this.RaisePropertyChanged(nameof(CurrentWorkspacePath));
        _workspaceFolders.Clear();
        foreach (var folder in folders)
        {
            _workspaceFolders.Add(folder);
        }

        var workspaceName = Path.GetFileNameWithoutExtension(workspaceFilePath);
        ExplorerTitle = string.IsNullOrWhiteSpace(workspaceName)
            ? $"{Path.GetFileName(workspaceFilePath)} (Workspace)"
            : $"{workspaceName} (Workspace)";
        RootPath = workspaceFilePath;

        RebuildItemsForWorkspace(_workspaceFolders);
    }

    public void CloseFolder()
    {
        _currentFolderPath = null;
        this.RaisePropertyChanged(nameof(CurrentFolderPath));
        Items.Clear();
        ExplorerTitle = DefaultExplorerTitle;
        RootPath = DefaultRootHint;
    }

    public void CloseWorkspace()
    {
        _currentWorkspacePath = null;
        this.RaisePropertyChanged(nameof(CurrentWorkspacePath));
        _workspaceFolders.Clear();
        Items.Clear();
        ExplorerTitle = DefaultExplorerTitle;
        RootPath = DefaultRootHint;
    }

    public void Refresh()
    {
        if (!string.IsNullOrWhiteSpace(_currentWorkspacePath) && _workspaceFolders.Count > 0)
        {
            RebuildItemsForWorkspace(_workspaceFolders);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_currentFolderPath) && Directory.Exists(_currentFolderPath))
        {
            RebuildItemsForFolder(_currentFolderPath);
        }
    }

    public void CollapseAll()
    {
        _expandedFolderPaths.Clear();
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (Items[i].Depth > 0)
                Items.RemoveAt(i);
            else
                Items[i].IsExpanded = false;
        }
        ExpandedFolderPathsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ExpandAll()
    {
        _expandedFolderPaths.Clear();

        foreach (var root in GetScopeRootPaths().Where(Directory.Exists))
        {
            var normalizedRoot = NormalizeFolderPath(root);
            if (!string.IsNullOrWhiteSpace(normalizedRoot))
            {
                _expandedFolderPaths.Add(normalizedRoot);
            }

            AddDescendantFoldersRecursive(root);
        }

        Refresh();
        ExpandedFolderPathsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetLayout()
    {
        IsOpenEditorsVisible = true;
        IsRecentEditorsVisible = true;
        IsFilesVisible = true;

        IsOpenEditorsExpanded = true;
        IsRecentEditorsExpanded = true;
        IsFilesExpanded = true;

        _expandedFolderPaths.Clear();
        Refresh();
    }

    public async Task CreateNewFileInRootAsync()
    {
        var root = GetPrimaryRootPath();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return;
        }

        var filePath = GetUniquePath(root, "NewFile", ".txt", isFolder: false);
        await File.WriteAllTextAsync(filePath, string.Empty);
        Refresh();

        if (OpenFileAsync != null)
        {
            await OpenFileAsync(filePath);
        }
    }

    public void CreateNewFolderInRoot()
    {
        var root = GetPrimaryRootPath();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return;
        }

        var folderPath = GetUniquePath(root, "NewFolder", string.Empty, isFolder: true);
        Directory.CreateDirectory(folderPath);
        Refresh();
    }

    public async Task TryOpenFileAsync(string path)
    {
        if (OpenFileAsync == null)
        {
            return;
        }

        await OpenFileAsync(path);
    }

    public void SetExpandedFolderPaths(IEnumerable<string> paths)
    {
        _expandedFolderPaths.Clear();
        foreach (var path in paths)
        {
            var normalized = NormalizeFolderPath(path);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                _expandedFolderPaths.Add(normalized);
            }
        }

        ExpandedFolderPathsChanged?.Invoke(this, EventArgs.Empty);
    }

    public List<string> GetExpandedFolderPathsData() => _expandedFolderPaths.ToList();

    public IReadOnlyList<string> GetScopeRootPaths()
    {
        if (!string.IsNullOrWhiteSpace(_currentFolderPath) && Directory.Exists(_currentFolderPath))
        {
            return new[] { _currentFolderPath };
        }

        return _workspaceFolders
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool RevealPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var fullPath = NormalizeFolderPath(path);
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }

        var roots = GetScopeRootPaths();
        var root = roots.FirstOrDefault(candidate => IsPathWithinRoot(fullPath, candidate));
        if (string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        var current = Path.GetDirectoryName(fullPath);
        while (!string.IsNullOrWhiteSpace(current) && IsPathWithinRoot(current, root))
        {
            var normalized = NormalizeFolderPath(current);
            if (!string.IsNullOrWhiteSpace(normalized) && !string.Equals(normalized, NormalizeFolderPath(root), StringComparison.OrdinalIgnoreCase))
            {
                _expandedFolderPaths.Add(normalized);
            }

            if (string.Equals(normalized, NormalizeFolderPath(root), StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = Path.GetDirectoryName(current);
        }

        Refresh();
        ExpandedFolderPathsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public string? DetectWorkspaceFileInFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return null;
        }

        try
        {
            return Directory
                .EnumerateFiles(folderPath, "*.code-workspace", SearchOption.TopDirectoryOnly)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private ExplorerItem CreateFlatItem(string path, int depth)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(name))
        {
            name = path;
        }

        return new ExplorerItem(name, path, isFolder: true, depth: depth);
    }

    public async Task ToggleItemAsync(ExplorerItem item)
    {
        if (!item.IsFolder || item.IsPlaceholder) return;
        if (item.IsExpanded)
            CollapseItem(item);
        else
            await ExpandItemAsync(item);
    }

    private async Task ExpandItemAsync(ExplorerItem item)
    {
        if (!item.IsFolder || item.IsPlaceholder || item.IsExpanded) return;

        var index = Items.IndexOf(item);
        if (index < 0) return;

        var children = await Task.Run(() => EnumerateChildren(item.FullPath, item.Depth, CancellationToken.None));

        // Re-check after the await in case the list was rebuilt (Refresh/CollapseAll)
        index = Items.IndexOf(item);
        if (index < 0 || item.IsExpanded) return;

        var insertAt = index + 1;
        foreach (var child in children)
        {
            Items.Insert(insertAt++, child);
        }

        item.IsExpanded = true;

        var normalized = NormalizeFolderPath(item.FullPath);
        if (!string.IsNullOrWhiteSpace(normalized) && _expandedFolderPaths.Add(normalized))
        {
            ExpandedFolderPathsChanged?.Invoke(this, EventArgs.Empty);
        }

        // Recursively expand children that were previously expanded
        foreach (var child in children.Where(c => c.IsFolder && ShouldBeExpanded(c)))
        {
            await ExpandItemAsync(child);
        }
    }

    private void CollapseItem(ExplorerItem item)
    {
        if (!item.IsFolder || item.IsPlaceholder || !item.IsExpanded) return;

        var index = Items.IndexOf(item);
        if (index < 0) return;

        while (index + 1 < Items.Count && Items[index + 1].Depth > item.Depth)
        {
            Items.RemoveAt(index + 1);
        }

        item.IsExpanded = false;

        var normalized = NormalizeFolderPath(item.FullPath);
        if (!string.IsNullOrWhiteSpace(normalized) && _expandedFolderPaths.Remove(normalized))
        {
            ExpandedFolderPathsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool ShouldBeExpanded(ExplorerItem item)
    {
        var normalized = NormalizeFolderPath(item.FullPath);
        return !string.IsNullOrWhiteSpace(normalized) && _expandedFolderPaths.Contains(normalized);
    }

    private void RebuildItemsForFolder(string path)
    {
        Items.Clear();
        Items.Add(CreateFlatItem(path, depth: 0));
        _ = RestoreExpandedBranchesAsync();
    }

    private void RebuildItemsForWorkspace(ObservableCollection<string> folders)
    {
        Items.Clear();
        foreach (var folder in folders.Where(Directory.Exists))
        {
            Items.Add(CreateFlatItem(folder, depth: 0));
        }

        _ = RestoreExpandedBranchesAsync();
    }

    private async Task RestoreExpandedBranchesAsync()
    {
        foreach (var root in Items.Where(i => i.IsFolder && ShouldBeExpanded(i)).ToList())
        {
            await ExpandItemAsync(root);
        }
    }

    private static bool IsReparsePoint(string directoryPath)
    {
        try
        {
            var attributes = File.GetAttributes(directoryPath);
            return (attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }
        catch
        {
            return true;
        }
    }

    private static bool HasAnyChildren(string folderPath)
    {
        try
        {
            using var enumerator = Directory.EnumerateFileSystemEntries(folderPath).GetEnumerator();
            return enumerator.MoveNext();
        }
        catch
        {
            return false;
        }
    }

    private void AddDescendantFoldersRecursive(string folderPath)
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(folderPath))
            {
                if (IsReparsePoint(directory))
                {
                    continue;
                }

                var normalized = NormalizeFolderPath(directory);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    _expandedFolderPaths.Add(normalized);
                }

                AddDescendantFoldersRecursive(directory);
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }
    }

    private List<ExplorerItem> EnumerateChildren(string folderPath, int parentDepth, CancellationToken cancellationToken)
    {
        var children = new List<ExplorerItem>();

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(folderPath).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IsReparsePoint(directory))
                {
                    continue;
                }

                children.Add(CreateFlatItem(directory, depth: parentDepth + 1));
            }

            foreach (var file in Directory.EnumerateFiles(folderPath).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                children.Add(new ExplorerItem(Path.GetFileName(file), file, isFolder: false, depth: parentDepth + 1));
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }

        return children;
    }

    private static List<string> ParseWorkspaceFolders(string workspaceFilePath)
    {
        var result = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(workspaceFilePath));
            if (!doc.RootElement.TryGetProperty("folders", out var folders) || folders.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            var workspaceDir = Path.GetDirectoryName(workspaceFilePath) ?? string.Empty;
            foreach (var folder in folders.EnumerateArray())
            {
                if (!folder.TryGetProperty("path", out var pathElement))
                {
                    continue;
                }

                var rawPath = pathElement.GetString();
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    continue;
                }

                var fullPath = Path.IsPathRooted(rawPath)
                    ? rawPath
                    : Path.GetFullPath(Path.Combine(workspaceDir, rawPath));

                if (Directory.Exists(fullPath))
                {
                    result.Add(fullPath);
                }
            }
        }
        catch
        {
        }

        return result;
    }

    private string? GetPrimaryRootPath()
    {
        if (!string.IsNullOrWhiteSpace(_currentFolderPath) && Directory.Exists(_currentFolderPath))
        {
            return _currentFolderPath;
        }

        return _workspaceFolders.FirstOrDefault(Directory.Exists);
    }

    private static string GetUniquePath(string root, string baseName, string extension, bool isFolder)
    {
        for (var i = 1; i < 10000; i++)
        {
            var suffix = i == 1 ? string.Empty : i.ToString();
            var name = $"{baseName}{suffix}{extension}";
            var fullPath = Path.Combine(root, name);

            if (isFolder)
            {
                if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
                {
                    return fullPath;
                }

                continue;
            }

            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return Path.Combine(root, $"{baseName}_{Guid.NewGuid():N}{extension}");
    }

    private static string NormalizeFolderPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsPathWithinRoot(string path, string root)
    {
        var normalizedPath = NormalizeFolderPath(path);
        var normalizedRoot = NormalizeFolderPath(root);
        if (string.IsNullOrWhiteSpace(normalizedPath) || string.IsNullOrWhiteSpace(normalizedRoot))
        {
            return false;
        }

        return normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}
