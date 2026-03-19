namespace NotepadPro.ViewModels;

public sealed class SearchResult
{
    public SearchResult(int index, int line, int column, string preview)
    {
        Index = index;
        Line = line;
        Column = column;
        Preview = preview;
    }

    public int Index { get; }

    public int Line { get; }

    public int Column { get; }

    public string Preview { get; }
}
