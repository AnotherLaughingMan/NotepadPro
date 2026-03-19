using System;

namespace NotepadPro.ViewModels;

public sealed class RecentFileItem
{
    public RecentFileItem(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public string DisplayName => System.IO.Path.GetFileName(Path);

    public string Tooltip => Path;
}
