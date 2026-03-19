using System.Collections.ObjectModel;
using ReactiveUI;

namespace NotepadPro.ViewModels;

public sealed class ExplorerItem : ReactiveObject
{
    private bool _areChildrenLoaded;
    private bool _isLoadingChildren;
    private bool _isExpanded;

    public ExplorerItem(string name, string fullPath, bool isFolder, bool isPlaceholder = false, int depth = 0)
    {
        Name = name;
        FullPath = fullPath;
        IsFolder = isFolder;
        IsPlaceholder = isPlaceholder;
        Depth = depth < 0 ? 0 : depth;
        Children = new ObservableCollection<ExplorerItem>();
        _areChildrenLoaded = !isFolder;
    }

    public static ExplorerItem CreatePlaceholder(string name = "", int depth = 0)
    {
        return new ExplorerItem(name, string.Empty, isFolder: false, isPlaceholder: true, depth: depth)
        {
            AreChildrenLoaded = true
        };
    }

    public string Name { get; }

    public string FullPath { get; }

    public bool IsFolder { get; }

    public bool IsPlaceholder { get; }

    public int Depth { get; }

    public double IndentWidth => Depth * 16.0;

    public bool AreChildrenLoaded
    {
        get => _areChildrenLoaded;
        set => this.RaiseAndSetIfChanged(ref _areChildrenLoaded, value);
    }

    public bool IsLoadingChildren
    {
        get => _isLoadingChildren;
        set => this.RaiseAndSetIfChanged(ref _isLoadingChildren, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public ObservableCollection<ExplorerItem> Children { get; }
}
