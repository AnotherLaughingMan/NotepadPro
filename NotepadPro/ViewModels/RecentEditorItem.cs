namespace NotepadPro.ViewModels;

public sealed class RecentEditorItem
{
    public RecentEditorItem(string title, string path)
    {
        Title = title;
        Path = path;
    }

    public string Title { get; }

    public string Path { get; }
}
