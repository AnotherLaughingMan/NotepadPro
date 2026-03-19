using System;

namespace NotepadPro.Services;

public static class TextSearchService
{
    public static int FindNext(string text, string query, int startIndex, bool matchCase, bool wholeWord)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query))
        {
            return -1;
        }

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var index = Math.Clamp(startIndex, 0, text.Length);

        while (index <= text.Length)
        {
            var matchIndex = text.IndexOf(query, index, comparison);
            if (matchIndex < 0)
            {
                return -1;
            }

            if (!wholeWord || IsWholeWordMatch(text, matchIndex, query.Length))
            {
                return matchIndex;
            }

            index = matchIndex + Math.Max(1, query.Length);
        }

        return -1;
    }

    public static int ReplaceAll(ref string text, string query, string replacement, bool matchCase, bool wholeWord)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query))
        {
            return 0;
        }

        var count = 0;
        var startIndex = 0;

        while (startIndex <= text.Length)
        {
            var matchIndex = FindNext(text, query, startIndex, matchCase, wholeWord);
            if (matchIndex < 0)
            {
                break;
            }

            text = text.Remove(matchIndex, query.Length).Insert(matchIndex, replacement);
            count++;
            startIndex = matchIndex + replacement.Length;
        }

        return count;
    }

    public static int GetIndexFromLine(string text, int lineNumber)
    {
        if (lineNumber <= 1)
        {
            return 0;
        }

        var line = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                if (line == lineNumber)
                {
                    return i + 1;
                }
            }
        }

        return text.Length;
    }

    public static int GetIndexFromLineAndColumn(string text, int lineNumber, int columnNumber)
    {
        var lineStart = GetIndexFromLine(text, lineNumber);
        var safeColumn = Math.Max(1, columnNumber);

        var index = lineStart;
        var remaining = safeColumn - 1;
        while (index < text.Length && remaining > 0)
        {
            if (text[index] == '\n')
            {
                break;
            }

            index++;
            remaining--;
        }

        return index;
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
