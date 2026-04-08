using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace NotepadPro.Services;

public static class SyntaxHighlightService
{
    private static IBrush KeywordBrush => Res("SyntaxKeyword");
    private static IBrush StringBrush => Res("SyntaxString");
    private static IBrush CommentBrush => Res("SyntaxComment");
    private static IBrush NumberBrush => Res("SyntaxNumber");
    private static IBrush TypeBrush => Res("SyntaxType");

    private static readonly Regex CSharpKeywords = new(
        "\\b(class|struct|interface|enum|public|private|protected|internal|static|readonly|sealed|new|return|void|using|namespace|if|else|switch|case|break|for|foreach|while|do|try|catch|finally|throw|async|await|var|bool|int|string|double|float|decimal)\\b",
        RegexOptions.Compiled);

    private static readonly Regex CSharpStrings = new("\"(?:\\\\.|[^\"])*\"", RegexOptions.Compiled);
    private static readonly Regex CSharpComments = new("//.*?$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex CSharpNumbers = new("\\b[0-9]+(\\.[0-9]+)?\\b", RegexOptions.Compiled);

    private static readonly Regex JsonKeys = new("\"[^\"]+\"(?=\\s*:)", RegexOptions.Compiled);
    private static readonly Regex JsonStrings = new("\"(?:\\\\.|[^\"])*\"", RegexOptions.Compiled);
    private static readonly Regex JsonNumbers = new("\\b-?[0-9]+(\\.[0-9]+)?\\b", RegexOptions.Compiled);

    private static readonly Regex MarkdownHeadings = new("^#{1,6}\\s.*$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex MarkdownInlineCode = new("`[^`]+`", RegexOptions.Compiled);

    private static readonly Regex XmlTags = new("</?[^>]+>", RegexOptions.Compiled);
    private static readonly Regex XmlStrings = new("\"[^\"]*\"", RegexOptions.Compiled);
    private static readonly Regex LuaKeywords = new(
        "\\b(and|break|do|else|elseif|end|false|for|function|goto|if|in|local|nil|not|or|repeat|return|then|true|until|while)\\b",
        RegexOptions.Compiled);
    private static readonly Regex LuaStrings = new(
        "\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'|\\[(=*)\\[(.|\\r|\\n)*?\\]\\1\\]",
        RegexOptions.Compiled);
    private static readonly Regex LuaComments = new(
        "--\\[(=*)\\[(.|\\r|\\n)*?\\]\\1\\]|--.*?$",
        RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex LuaNumbers = new(
        "\\b(?:0[xX][0-9a-fA-F]+|\\d+(?:\\.\\d+)?(?:[eE][+\\-]?\\d+)?)\\b",
        RegexOptions.Compiled);

    public static IReadOnlyList<Inline> Build(string text, string language)
    {
        var spans = new List<HighlightSpan>();

        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<Inline>();
        }

        switch (language)
        {
            case "C#":
                AddSpans(spans, CSharpComments, CommentBrush, text);
                AddSpans(spans, CSharpStrings, StringBrush, text);
                AddSpans(spans, CSharpKeywords, KeywordBrush, text);
                AddSpans(spans, CSharpNumbers, NumberBrush, text);
                break;
            case "JSON":
                AddSpans(spans, JsonKeys, TypeBrush, text);
                AddSpans(spans, JsonStrings, StringBrush, text);
                AddSpans(spans, JsonNumbers, NumberBrush, text);
                break;
            case "Markdown":
                AddSpans(spans, MarkdownHeadings, KeywordBrush, text);
                AddSpans(spans, MarkdownInlineCode, StringBrush, text);
                break;
            case "XML":
            case "XAML":
                AddSpans(spans, XmlTags, KeywordBrush, text);
                AddSpans(spans, XmlStrings, StringBrush, text);
                break;
            case "Lua":
                AddSpans(spans, LuaComments, CommentBrush, text);
                AddSpans(spans, LuaStrings, StringBrush, text);
                AddSpans(spans, LuaKeywords, KeywordBrush, text);
                AddSpans(spans, LuaNumbers, NumberBrush, text);
                break;
        }

        return BuildInlines(text, spans);
    }

    private static void AddSpans(ICollection<HighlightSpan> target, Regex regex, IBrush brush, string text)
    {
        foreach (Match match in regex.Matches(text))
        {
            if (match.Length == 0)
            {
                continue;
            }

            if (OverlapsExisting(target, match.Index, match.Length))
            {
                continue;
            }

            target.Add(new HighlightSpan(match.Index, match.Length, brush));
        }
    }

    private static bool OverlapsExisting(IEnumerable<HighlightSpan> spans, int start, int length)
    {
        var end = start + length;
        return spans.Any(span => start < span.End && end > span.Start);
    }

    private static IReadOnlyList<Inline> BuildInlines(string text, List<HighlightSpan> spans)
    {
        if (spans.Count == 0)
        {
            return new List<Inline> { new Run(text) };
        }

        var ordered = spans.OrderBy(span => span.Start).ToList();
        var inlines = new List<Inline>();
        var index = 0;

        foreach (var span in ordered)
        {
            if (span.Start > index)
            {
                inlines.Add(new Run(text.Substring(index, span.Start - index)));
            }

            var run = new Run(text.Substring(span.Start, span.Length))
            {
                Foreground = span.Brush
            };
            inlines.Add(run);
            index = span.End;
        }

        if (index < text.Length)
        {
            inlines.Add(new Run(text.Substring(index)));
        }

        return inlines;
    }

    private sealed class HighlightSpan
    {
        public HighlightSpan(int start, int length, IBrush brush)
        {
            Start = start;
            Length = length;
            Brush = brush;
        }

        public int Start { get; }

        public int Length { get; }

        public int End => Start + Length;

        public IBrush Brush { get; }
    }

    private static IBrush Res(string key)
    {
        if (Application.Current!.Resources.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) && value is IBrush brush)
            return brush;
        return Brushes.Magenta;
    }
}
