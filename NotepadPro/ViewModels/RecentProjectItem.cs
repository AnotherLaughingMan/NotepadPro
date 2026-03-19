using System;
using System.IO;

namespace NotepadPro.ViewModels;

public sealed class RecentProjectItem
{
    public RecentProjectItem(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public bool IsWorkspace => Path.EndsWith(".code-workspace", StringComparison.OrdinalIgnoreCase);

    public string DisplayName
    {
        get
        {
            if (IsWorkspace)
            {
                return global::System.IO.Path.GetFileNameWithoutExtension(Path);
            }

            var folderName = global::System.IO.Path.GetFileName(
                Path.TrimEnd(global::System.IO.Path.DirectorySeparatorChar, global::System.IO.Path.AltDirectorySeparatorChar));
            return string.IsNullOrWhiteSpace(folderName) ? Path : folderName;
        }
    }

    public string TypeLabel => IsWorkspace ? "Workspace" : "Folder";

    public string Tooltip => Path;
}
