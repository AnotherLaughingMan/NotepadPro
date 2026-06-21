using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NotepadPro.Services;
using ReactiveUI;

namespace NotepadPro.ViewModels;

public sealed class EditorViewModel : ViewModelBase
{
    private const int LineNumberStringLimit = 2000;
    private const int SyncWordCountLimit = 50_000;
    private const int MarkdownPreviewRenderLimit = 100_000;
    private readonly SettingsViewModel _settings;
    private string _text = string.Empty;
    private string _lineNumbersText = "1";
    private int _caretLine = 1;
    private int _caretColumn = 1;
    private int _caretIndex;
    private int _wordCount;
    private int _lineCount = 1;
    private string? _filePath;
    private string? _untitledName;
    private bool _hasUnsavedChanges;
    private string _language = "Plain Text";
    private bool _suppressDirty;
    private bool _isFolded;
    private bool _isMarkdownPreviewVisible;
    private string _markdownPreviewText = string.Empty;
    private string? _unfoldedText;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private int _wordCountVersion;
    private List<int> _lineStartOffsets = new() { 0 };
    private CancellationTokenSource? _metricsDebounce;
    private bool _isLoading;

    public event EventHandler<int>? CaretRequested;
    public event EventHandler<(int start, int length)>? SelectionRequested;

    public EditorViewModel(SettingsViewModel settings)
    {
        _settings = settings;
        UpdateMarkdownPreviewText();
        UpdateMetrics();
    }

    public SettingsViewModel Settings => _settings;

    public string Text
    {
        get => _text;
        set
        {
            if (!string.Equals(_text, value, StringComparison.Ordinal))
            {
                this.RaiseAndSetIfChanged(ref _text, value);
                if (!_suppressDirty)
                {
                    HasUnsavedChanges = true;
                }

                UpdateMarkdownPreviewText();
                UpdateMetrics();
            }
        }
    }

    public string? FilePath
    {
        get => _filePath;
        private set
        {
            this.RaiseAndSetIfChanged(ref _filePath, value);
            this.RaisePropertyChanged(nameof(FileName));
        }
    }

    public string FileName => string.IsNullOrWhiteSpace(FilePath)
        ? (_untitledName ?? "Untitled (1)")
        : Path.GetFileName(FilePath);

    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        private set => this.RaiseAndSetIfChanged(ref _hasUnsavedChanges, value);
    }

    public void SetExternalDirtyState(bool isDirty)
    {
        HasUnsavedChanges = isDirty;
    }

    public string Language
    {
        get => _language;
        private set => this.RaiseAndSetIfChanged(ref _language, value);
    }

    public bool IsFolded
    {
        get => _isFolded;
        private set => this.RaiseAndSetIfChanged(ref _isFolded, value);
    }

    public bool IsMarkdownPreviewVisible
    {
        get => _isMarkdownPreviewVisible;
        set
        {
            this.RaiseAndSetIfChanged(ref _isMarkdownPreviewVisible, value);
            this.RaisePropertyChanged(nameof(IsMarkdownPreviewActive));
        }
    }

    public bool IsMarkdown => string.Equals(Language, "Markdown", StringComparison.OrdinalIgnoreCase);

    public bool IsMarkdownFile
    {
        get
        {
            var extension = Path.GetExtension(FilePath ?? string.Empty);

            return string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".markdown", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".mdown", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".mkd", StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool CanToggleRenderedMarkdownView => IsMarkdown && IsMarkdownFile;

    public bool IsMarkdownPreviewActive => IsMarkdown && IsMarkdownPreviewVisible;

    public string MarkdownPreviewText
    {
        get => _markdownPreviewText;
        private set => this.RaiseAndSetIfChanged(ref _markdownPreviewText, value);
    }

    public string LineNumbersText
    {
        get => _lineNumbersText;
        private set => this.RaiseAndSetIfChanged(ref _lineNumbersText, value);
    }

    public int CaretLine
    {
        get => _caretLine;
        private set => this.RaiseAndSetIfChanged(ref _caretLine, value);
    }

    public int CaretColumn
    {
        get => _caretColumn;
        private set => this.RaiseAndSetIfChanged(ref _caretColumn, value);
    }

    public int WordCount
    {
        get => _wordCount;
        private set => this.RaiseAndSetIfChanged(ref _wordCount, value);
    }

    public int LineCount
    {
        get => _lineCount;
        private set => this.RaiseAndSetIfChanged(ref _lineCount, value);
    }

    public int CaretIndex => _caretIndex;

    public void SetUntitledName(string name)
    {
        _untitledName = name;
        this.RaisePropertyChanged(nameof(FileName));
    }

    public void NewDocument()
    {
        CreateNewDocument("Plain Text", string.Empty, markDirty: false);
    }

    public void NewMarkdownDocument()
    {
        CreateNewDocument("Markdown", string.Empty, markDirty: false);
    }

    public void NewDocument(string language, string initialText = "", bool markDirty = false)
    {
        CreateNewDocument(language, initialText, markDirty);
    }

    private void CreateNewDocument(string language, string initialText, bool markDirty)
    {
        var displayLanguage = TextMateLanguageService.NormalizeDisplayLanguage(language);

        IsFolded = false;
        _unfoldedText = null;
        _suppressDirty = true;
        Text = initialText;
        _suppressDirty = false;

        FilePath = null;
        Language = displayLanguage;
        IsMarkdownPreviewVisible = false;
        UpdateMarkdownPreviewText();
        this.RaisePropertyChanged(nameof(IsMarkdown));
        this.RaisePropertyChanged(nameof(IsMarkdownFile));
        this.RaisePropertyChanged(nameof(CanToggleRenderedMarkdownView));
        this.RaisePropertyChanged(nameof(IsMarkdownPreviewActive));
        HasUnsavedChanges = markDirty;
    }

    public async Task LoadFromFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        _isLoading = true;
        try
        {
            string text;
            Encoding detectedEncoding;

            await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true, 65536))
            {
                text = await reader.ReadToEndAsync();
                detectedEncoding = reader.CurrentEncoding;
            }

            IsFolded = false;
            _unfoldedText = null;

            // Build line offsets on a background thread for large files
            var offsets = text.Length > 100_000
                ? await Task.Run(() => BuildLineStartOffsets(text))
                : BuildLineStartOffsets(text);

            _lineStartOffsets = offsets;
            LineCount = offsets.Count;

            LineNumbersText = LineCount <= LineNumberStringLimit
                ? BuildLineNumbers(LineCount)
                : string.Empty;

            _suppressDirty = true;
            // Set the text without triggering UpdateMetrics (we already computed everything)
            if (!string.Equals(_text, text, StringComparison.Ordinal))
            {
                this.RaiseAndSetIfChanged(ref _text, text, nameof(Text));
            }
            _suppressDirty = false;

            FilePath = path;
            HasUnsavedChanges = false;
            UpdateLanguageFromPath(path);
            Settings.Encoding = MapEncodingToLabel(detectedEncoding);
            Settings.Eol = DetectEol(text);
            UpdateCaretFromIndex(0);

            // Kick off word count asynchronously
            ScheduleWordCount(text);
        }
        finally
        {
            _isLoading = false;
        }
    }

    public async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            return;
        }

        EnsureUnfolded();
        await SaveToPathAsync(FilePath);
    }

    public async Task SaveAsAsync(string path)
    {
        FilePath = path;
        UpdateLanguageFromPath(path);
        EnsureUnfolded();
        await SaveToPathAsync(path);
    }

    public async Task AutoSaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath) || !HasUnsavedChanges)
        {
            return;
        }

        EnsureUnfolded();
        await SaveToPathAsync(FilePath);
    }

    public void FoldAll()
    {
        if (IsFolded)
        {
            return;
        }

        var source = Text;
        var folded = FoldText(source, Language);
        if (string.Equals(source, folded, StringComparison.Ordinal))
        {
            return;
        }

        _unfoldedText = source;
        ReplaceTextWithoutDirty(folded);
        IsFolded = true;
    }

    public void UnfoldAll()
    {
        if (!IsFolded)
        {
            return;
        }

        if (_unfoldedText != null)
        {
            ReplaceTextWithoutDirty(_unfoldedText);
        }

        _unfoldedText = null;
        IsFolded = false;
    }

    public void EnsureUnfolded()
    {
        if (IsFolded)
        {
            UnfoldAll();
        }
    }

    public void UpdateCaretFromIndex(int caretIndex)
    {
        var safeIndex = Math.Clamp(caretIndex, 0, _text.Length);
        _caretIndex = safeIndex;
        var lineIndex = FindLineIndexForOffset(safeIndex);
        var lineStart = _lineStartOffsets.Count > 0 && lineIndex >= 0 && lineIndex < _lineStartOffsets.Count
            ? _lineStartOffsets[lineIndex]
            : 0;
        CaretLine = lineIndex + 1;
        CaretColumn = (safeIndex - lineStart) + 1;
    }

    /// <summary>Updates caret position directly from a 1-based line/column pair (e.g. from Monaco).</summary>
    public void UpdateCaretPosition(int line, int column)
    {
        CaretLine = Math.Max(1, line);
        CaretColumn = Math.Max(1, column);
    }

    public void RequestCaretIndex(int index)
    {
        CaretRequested?.Invoke(this, index);
        UpdateCaretFromIndex(index);
    }

    public void RequestSelection(int start, int length)
    {
        SelectionRequested?.Invoke(this, (start, length));
        UpdateCaretFromIndex(start + length);
    }

    public void NavigateToLine(int line)
    {
        var targetLine = Math.Clamp(line, 1, LineCount);
        var lineIndex = targetLine - 1;
        var index = lineIndex >= 0 && lineIndex < _lineStartOffsets.Count
            ? _lineStartOffsets[lineIndex]
            : 0;
        RequestCaretIndex(index);
    }

    public void NavigateToLocation(int line, int column)
    {
        var targetLine = Math.Clamp(line, 1, LineCount);
        var index = TextSearchService.GetIndexFromLineAndColumn(_text, targetLine, Math.Max(1, column));
        RequestCaretIndex(index);
    }

    private void UpdateMetrics()
    {
        // During file loading, metrics are computed directly — skip redundant work
        if (_isLoading) return;

        var text = _text;

        if (text.Length <= 100_000)
        {
            // Small file: compute synchronously for immediate feedback
            _lineStartOffsets = BuildLineStartOffsets(text);
            LineCount = _lineStartOffsets.Count;
            LineNumbersText = LineCount <= LineNumberStringLimit
                ? BuildLineNumbers(LineCount)
                : string.Empty;
            UpdateCaretFromIndex(_caretIndex);
            ScheduleWordCount(text);
        }
        else
        {
            // Large file: debounce metrics update to avoid blocking on every keystroke
            _metricsDebounce?.Cancel();
            var cts = new CancellationTokenSource();
            _metricsDebounce = cts;

            // Update caret immediately (cheap)
            UpdateCaretFromIndex(_caretIndex);

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(150, cts.Token);
                    var offsets = BuildLineStartOffsets(text);
                    var lineCount = offsets.Count;
                    var lineNumText = lineCount <= LineNumberStringLimit
                        ? BuildLineNumbers(lineCount)
                        : string.Empty;

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (cts.IsCancellationRequested || !string.Equals(_text, text, StringComparison.Ordinal)) return;
                        _lineStartOffsets = offsets;
                        LineCount = lineCount;
                        LineNumbersText = lineNumText;
                        UpdateCaretFromIndex(_caretIndex);
                    });

                    ScheduleWordCount(text);
                }
                catch (OperationCanceledException) { }
            }, cts.Token);
        }
    }

    private void ScheduleWordCount(string text)
    {
        var version = ++_wordCountVersion;

        if (text.Length <= SyncWordCountLimit)
        {
            WordCount = CountWords(text);
            return;
        }

        // Large text: count words in background
        _ = Task.Run(() =>
        {
            var result = CountWords(text);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (version == _wordCountVersion)
                    WordCount = result;
            });
        });
    }

    private int FindLineIndexForOffset(int offset)
    {
        if (_lineStartOffsets.Count == 0)
        {
            return 0;
        }

        var index = _lineStartOffsets.BinarySearch(offset);
        if (index >= 0)
        {
            return index;
        }

        var nextIndex = ~index;
        return Math.Max(0, nextIndex - 1);
    }

    private static List<int> BuildLineStartOffsets(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        return starts;
    }

    private async Task SaveToPathAsync(string path)
    {
        await _saveLock.WaitAsync();
        try
        {
            var normalized = NormalizeLineEndings(Text, Settings.Eol);
            var encoding = ResolveEncoding(Settings.Encoding);
            await File.WriteAllTextAsync(path, normalized, encoding);
            HasUnsavedChanges = false;
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private void UpdateLanguageFromPath(string path)
    {
        Language = TextMateLanguageService.DetectLanguageFromPath(
            path,
            Text,
            _settings.DetectJsonFromContent);

        if (!CanToggleRenderedMarkdownView && IsMarkdownPreviewVisible)
        {
            IsMarkdownPreviewVisible = false;
        }

        UpdateMarkdownPreviewText();
        this.RaisePropertyChanged(nameof(IsMarkdown));
        this.RaisePropertyChanged(nameof(IsMarkdownFile));
        this.RaisePropertyChanged(nameof(CanToggleRenderedMarkdownView));
        this.RaisePropertyChanged(nameof(IsMarkdownPreviewActive));
    }

    private void UpdateMarkdownPreviewText()
    {
        if (!IsMarkdown || _text.Length > MarkdownPreviewRenderLimit)
        {
            MarkdownPreviewText = string.Empty;
            return;
        }

        MarkdownPreviewText = RenderMarkdownPreviewText(_text);
    }

    private static string RenderMarkdownPreviewText(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return string.Empty;
        }

        var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        var builder = new StringBuilder();
        var inCodeFence = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine ?? string.Empty;
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                inCodeFence = !inCodeFence;
                builder.AppendLine(inCodeFence ? "──────── code ────────" : "──────────────");
                continue;
            }

            if (inCodeFence)
            {
                builder.AppendLine(line);
                continue;
            }

            if (TryRenderHeading(trimmed, out var heading))
            {
                builder.AppendLine(heading);
                continue;
            }

            if (trimmed.StartsWith(">", StringComparison.Ordinal))
            {
                builder.AppendLine($"│ {RenderInlineMarkdown(trimmed.TrimStart('>', ' '))}");
                continue;
            }

            if (TryStripBulletPrefix(trimmed, out var bulletContent))
            {
                builder.AppendLine($"• {RenderInlineMarkdown(bulletContent)}");
                continue;
            }

            builder.AppendLine(RenderInlineMarkdown(line));
        }

        return builder.ToString().TrimEnd();
    }

    private static bool TryRenderHeading(string line, out string heading)
    {
        heading = string.Empty;
        var i = 0;
        while (i < line.Length && i < 6 && line[i] == '#')
        {
            i++;
        }

        if (i == 0 || i >= line.Length || !char.IsWhiteSpace(line[i]))
        {
            return false;
        }

        var text = line[(i + 1)..].Trim();
        heading = string.IsNullOrWhiteSpace(text) ? string.Empty : text.ToUpperInvariant();
        return true;
    }

    private static bool TryStripBulletPrefix(string line, out string content)
    {
        content = string.Empty;
        if (line.Length < 2)
        {
            return false;
        }

        if ((line[0] == '-' || line[0] == '*' || line[0] == '+') && char.IsWhiteSpace(line[1]))
        {
            content = line[2..].TrimStart();
            return true;
        }

        return false;
    }

    private static string RenderInlineMarkdown(string text)
    {
        var rendered = text;
        rendered = Regex.Replace(rendered, "!\\[(.*?)\\]\\((.*?)\\)", "[image: $1] ($2)");
        rendered = Regex.Replace(rendered, "\\[(.*?)\\]\\((.*?)\\)", "$1 ($2)");
        rendered = Regex.Replace(rendered, "`([^`]+)`", "$1");
        rendered = Regex.Replace(rendered, "\\*\\*(.+?)\\*\\*", "$1");
        rendered = Regex.Replace(rendered, "__(.+?)__", "$1");
        rendered = Regex.Replace(rendered, "\\*(.+?)\\*", "$1");
        rendered = Regex.Replace(rendered, "_(.+?)_", "$1");
        rendered = Regex.Replace(rendered, "~~(.+?)~~", "$1");
        return rendered;
    }

    private void ReplaceTextWithoutDirty(string text)
    {
        var wasDirty = HasUnsavedChanges;
        _suppressDirty = true;
        Text = text;
        _suppressDirty = false;
        HasUnsavedChanges = wasDirty;
    }

    private static string FoldText(string text, string language)
    {
        return language switch
        {
            "XML" => FoldXmlTags(text),
            "XAML" => FoldXmlTags(text),
            _ => FoldBraces(text)
        };
    }

    private static string FoldBraces(string text)
    {
        var ranges = new List<(int start, int end)>();
        var stack = new Stack<int>();

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                stack.Push(i);
                continue;
            }

            if (text[i] == '}' && stack.Count > 0)
            {
                var start = stack.Pop();
                var end = i;
                if (start + 1 < end && text.IndexOf('\n', start + 1) > -1)
                {
                    ranges.Add((start, end));
                }
            }
        }

        return ApplyBraceRanges(text, ranges);
    }

    private static string ApplyBraceRanges(string text, List<(int start, int end)> ranges)
    {
        if (ranges.Count == 0)
        {
            return text;
        }

        var folded = text;
        ranges.Sort((a, b) => b.start.CompareTo(a.start));
        foreach (var range in ranges)
        {
            var innerStart = range.start + 1;
            var innerLength = range.end - range.start - 1;
            if (innerLength <= 0 || innerStart + innerLength > folded.Length)
            {
                continue;
            }

            if (folded.IndexOf('\n', innerStart, innerLength) < 0)
            {
                continue;
            }

            folded = folded.Remove(innerStart, innerLength).Insert(innerStart, " ... ");
        }

        return folded;
    }

    private static string FoldXmlTags(string text)
    {
        var ranges = new List<(int start, int end)>();
        var stack = new Stack<(string name, int startOffset)>();

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '<')
            {
                continue;
            }

            if (i + 3 < text.Length && text.Substring(i, 4) == "<!--")
            {
                var close = text.IndexOf("-->", i + 4, StringComparison.Ordinal);
                if (close < 0)
                {
                    break;
                }

                i = close + 2;
                continue;
            }

            if (i + 1 < text.Length && (text[i + 1] == '?' || text[i + 1] == '!'))
            {
                var close = text.IndexOf('>', i + 2);
                if (close < 0)
                {
                    break;
                }

                i = close;
                continue;
            }

            var isClosing = i + 1 < text.Length && text[i + 1] == '/';
            var nameStart = i + (isClosing ? 2 : 1);
            var nameEnd = nameStart;
            while (nameEnd < text.Length)
            {
                var ch = text[nameEnd];
                if (ch == '>' || ch == '/' || char.IsWhiteSpace(ch))
                {
                    break;
                }

                nameEnd++;
            }

            if (nameEnd <= nameStart)
            {
                continue;
            }

            var tagName = text.Substring(nameStart, nameEnd - nameStart);
            var tagEnd = text.IndexOf('>', nameEnd);
            if (tagEnd < 0)
            {
                break;
            }

            var isSelfClosing = text[tagEnd - 1] == '/';
            if (isClosing)
            {
                while (stack.Count > 0)
                {
                    var entry = stack.Pop();
                    if (!string.Equals(entry.name, tagName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (entry.startOffset < i)
                    {
                        ranges.Add((entry.startOffset, i));
                    }
                    break;
                }
            }
            else if (!isSelfClosing)
            {
                stack.Push((tagName, tagEnd + 1));
            }

            i = tagEnd;
        }

        return ApplyTagRanges(text, ranges);
    }

    private static string ApplyTagRanges(string text, List<(int start, int end)> ranges)
    {
        if (ranges.Count == 0)
        {
            return text;
        }

        var folded = text;
        ranges.Sort((a, b) => b.start.CompareTo(a.start));
        foreach (var range in ranges)
        {
            var innerStart = range.start;
            var innerLength = range.end - range.start;
            if (innerLength <= 0 || innerStart + innerLength > folded.Length)
            {
                continue;
            }

            if (folded.IndexOf('\n', innerStart, innerLength) < 0)
            {
                continue;
            }

            folded = folded.Remove(innerStart, innerLength).Insert(innerStart, " ... ");
        }

        return folded;
    }

    private static string DetectEol(string text)
    {
        return text.Contains("\r\n", StringComparison.Ordinal) ? "CRLF" : "LF";
    }

    private static string NormalizeLineEndings(string text, string eol)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        return eol == "CRLF" ? normalized.Replace("\n", "\r\n", StringComparison.Ordinal) : normalized;
    }

    private static Encoding ResolveEncoding(string label)
    {
        return label switch
        {
            "UTF-16" => Encoding.Unicode,
            "ANSI" => Encoding.Default,
            _ => Encoding.UTF8
        };
    }

    private static string MapEncodingToLabel(Encoding encoding)
    {
        if (encoding.Equals(Encoding.Unicode))
        {
            return "UTF-16";
        }

        if (encoding.Equals(Encoding.ASCII) || encoding.Equals(Encoding.Default))
        {
            return "ANSI";
        }

        return "UTF-8";
    }

    private static int CountWords(string text)
    {
        var count = 0;
        var inWord = false;

        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                if (inWord)
                {
                    count++;
                    inWord = false;
                }

                continue;
            }

            inWord = true;
        }

        if (inWord)
        {
            count++;
        }

        return count;
    }

    private static string BuildLineNumbers(int lines)
    {
        var builder = new StringBuilder();

        for (var i = 1; i <= lines; i++)
        {
            builder.Append(i);

            if (i < lines)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
}
