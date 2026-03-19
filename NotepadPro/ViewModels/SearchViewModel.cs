using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;

namespace NotepadPro.ViewModels;

public sealed class SearchViewModel : ViewModelBase
{
    private EditorViewModel _editor;
    private string _query = string.Empty;
    private string _replaceText = string.Empty;
    private bool _matchCase;
    private bool _wholeWord;
    private int _resultCount;
    private IDisposable? _updatesSubscription;
    private readonly IObservable<Unit> _queryChanges;

    public SearchViewModel(EditorViewModel editor)
    {
        _editor = editor;
        Results = new ObservableCollection<SearchResult>();

        SearchCommand = ReactiveCommand.Create(UpdateResults);
        GoToResultCommand = ReactiveCommand.Create<SearchResult>(GoToResult);
        ReplaceCommand = ReactiveCommand.Create(ReplaceNext);

        _queryChanges = this.WhenAnyValue(x => x.Query, x => x.MatchCase, x => x.WholeWord)
            .Select(_ => Unit.Default);

        SetEditor(editor);
    }

    public ObservableCollection<SearchResult> Results { get; }

    public ReactiveCommand<Unit, Unit> SearchCommand { get; }

    public ReactiveCommand<SearchResult, Unit> GoToResultCommand { get; }

    public ReactiveCommand<Unit, Unit> ReplaceCommand { get; }

    public string Query
    {
        get => _query;
        set => this.RaiseAndSetIfChanged(ref _query, value);
    }

    public bool MatchCase
    {
        get => _matchCase;
        set => this.RaiseAndSetIfChanged(ref _matchCase, value);
    }

    public bool WholeWord
    {
        get => _wholeWord;
        set => this.RaiseAndSetIfChanged(ref _wholeWord, value);
    }

    public string ReplaceText
    {
        get => _replaceText;
        set => this.RaiseAndSetIfChanged(ref _replaceText, value);
    }

    public int ResultCount
    {
        get => _resultCount;
        private set => this.RaiseAndSetIfChanged(ref _resultCount, value);
    }

    public void SetEditor(EditorViewModel editor)
    {
        _editor = editor;
        _updatesSubscription?.Dispose();

        var textChanges = _editor.WhenAnyValue(x => x.Text)
            .Select(_ => Unit.Default);

        _updatesSubscription = _queryChanges.Merge(textChanges)
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateResults());

        UpdateResults();
    }

    private void UpdateResults()
    {
        Results.Clear();

        if (string.IsNullOrWhiteSpace(Query))
        {
            ResultCount = 0;
            return;
        }

        var text = _editor.Text ?? string.Empty;
        var comparison = MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var index = 0;

        while (index < text.Length)
        {
            var matchIndex = text.IndexOf(Query, index, comparison);
            if (matchIndex < 0)
            {
                break;
            }

            if (WholeWord && !IsWholeWordMatch(text, matchIndex, Query.Length))
            {
                index = matchIndex + Query.Length;
                continue;
            }

            var (line, column) = GetLineColumn(text, matchIndex);
            var preview = BuildPreview(text, matchIndex, Query.Length);
            Results.Add(new SearchResult(matchIndex, line, column, preview));

            index = matchIndex + Math.Max(1, Query.Length);
        }

        ResultCount = Results.Count;
    }

    private void GoToResult(SearchResult result)
    {
        _editor.RequestCaretIndex(result.Index);
    }

    private void ReplaceNext()
    {
        if (Results.Count == 0 || string.IsNullOrEmpty(Query))
        {
            return;
        }

        var result = Results[0];
        var text = _editor.Text ?? string.Empty;
        _editor.Text = text.Remove(result.Index, Query.Length).Insert(result.Index, ReplaceText ?? string.Empty);
        _editor.RequestCaretIndex(result.Index + (ReplaceText?.Length ?? 0));
    }

    private static (int line, int column) GetLineColumn(string text, int index)
    {
        var line = 1;
        var column = 1;

        for (var i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                column = 1;
                continue;
            }

            column++;
        }

        return (line, column);
    }

    private static string BuildPreview(string text, int index, int length)
    {
        const int context = 20;
        var start = Math.Max(0, index - context);
        var end = Math.Min(text.Length, index + length + context);
        var snippet = text.Substring(start, end - start);
        return snippet.Replace("\n", " ").Replace("\r", " ");
    }

    private static bool IsWholeWordMatch(string text, int index, int length)
    {
        var before = index > 0 ? text[index - 1] : ' ';
        var afterIndex = index + length;
        var after = afterIndex < text.Length ? text[afterIndex] : ' ';

        return !IsWordChar(before) && !IsWordChar(after);
    }

    private static bool IsWordChar(char ch)
    {
        return char.IsLetterOrDigit(ch) || ch == '_';
    }
}
