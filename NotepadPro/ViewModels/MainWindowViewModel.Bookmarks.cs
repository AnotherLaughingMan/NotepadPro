using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NotepadPro.Models;

namespace NotepadPro.ViewModels;

public sealed partial class MainWindowViewModel
{
    private const string GlobalBookmarkScopeKey = "global::__all";
    private const int BookmarkRecoveryMaxFileScans = 5000;
    private static readonly HashSet<string> BookmarkRecoveryIgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        "bin",
        "node_modules",
        "obj"
    };

    public ObservableCollection<BookmarkItem> GlobalBookmarks { get; } = new();

    public BookmarksViewModel? BookmarksPanel { get; private set; }

    private static BookmarkItem CloneBookmark(BookmarkItem item)
    {
        return new BookmarkItem
        {
            FilePath = item.FilePath,
            LineNumber = item.LineNumber,
            Text = item.Text,
            CreatedAt = item.CreatedAt,
            AnchorFingerprint = item.AnchorFingerprint,
            ContextBefore = item.ContextBefore,
            ContextAfter = item.ContextAfter,
            IsGlobal = item.IsGlobal,
            IsStale = item.IsStale
        };
    }

    private static BookmarkItemData ToBookmarkData(BookmarkItem item)
    {
        return new BookmarkItemData
        {
            FilePath = item.FilePath,
            LineNumber = item.LineNumber,
            Text = item.Text,
            CreatedAt = item.CreatedAt,
            AnchorFingerprint = item.AnchorFingerprint,
            ContextBefore = item.ContextBefore,
            ContextAfter = item.ContextAfter,
            IsGlobal = item.IsGlobal,
            IsStale = item.IsStale
        };
    }

    private static BookmarkItem FromBookmarkData(BookmarkItemData item)
    {
        return new BookmarkItem
        {
            FilePath = item.FilePath,
            LineNumber = item.LineNumber,
            Text = item.Text,
            CreatedAt = item.CreatedAt,
            AnchorFingerprint = string.IsNullOrWhiteSpace(item.AnchorFingerprint) ? NormalizeBookmarkText(item.Text) : item.AnchorFingerprint,
            ContextBefore = item.ContextBefore,
            ContextAfter = item.ContextAfter,
            IsGlobal = item.IsGlobal,
            IsStale = item.IsStale
        };
    }

    private BookmarkItem CreateBookmark(EditorViewModel editor, int lineNumber, bool isGlobal)
    {
        var text = GetLinePreview(editor.Text, lineNumber);
        return new BookmarkItem
        {
            FilePath = editor.FilePath ?? string.Empty,
            LineNumber = lineNumber,
            Text = text,
            AnchorFingerprint = NormalizeBookmarkText(text),
            ContextBefore = GetLinePreview(editor.Text, lineNumber - 1),
            ContextAfter = GetLinePreview(editor.Text, lineNumber + 1),
            IsGlobal = isGlobal,
            IsStale = false
        };
    }

    private void UpdateBookmarkState(BookmarkItem bookmark, string text)
    {
        var lines = GetNormalizedLines(text);
        var currentIndex = Math.Clamp(bookmark.LineNumber - 1, 0, Math.Max(0, lines.Count - 1));

        if (lines.Count == 0)
        {
            bookmark.IsStale = true;
            return;
        }

        if (BookmarkMatchesAt(bookmark, lines, currentIndex, requireContext: true))
        {
            ApplyBookmarkLineState(bookmark, lines, currentIndex, isStale: false);
            return;
        }

        var relocatedIndex = FindRelocatedBookmarkIndex(bookmark, lines);
        if (relocatedIndex >= 0)
        {
            ApplyBookmarkLineState(bookmark, lines, relocatedIndex, isStale: false);
            return;
        }

        bookmark.IsStale = true;
    }

    private static string NormalizeBookmarkText(string? value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static List<string> GetNormalizedLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new List<string>();
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return normalized.Split('\n').Select(NormalizeBookmarkText).ToList();
    }

    private static bool BookmarkMatchesAt(BookmarkItem bookmark, IReadOnlyList<string> lines, int index, bool requireContext)
    {
        if (index < 0 || index >= lines.Count)
        {
            return false;
        }

        var fingerprint = NormalizeBookmarkText(bookmark.AnchorFingerprint);
        if (!string.Equals(lines[index], fingerprint, StringComparison.Ordinal))
        {
            return false;
        }

        if (!requireContext)
        {
            return true;
        }

        var before = index > 0 ? lines[index - 1] : string.Empty;
        var after = index + 1 < lines.Count ? lines[index + 1] : string.Empty;
        var beforeMatches = string.IsNullOrWhiteSpace(bookmark.ContextBefore)
            || string.Equals(before, NormalizeBookmarkText(bookmark.ContextBefore), StringComparison.Ordinal);
        var afterMatches = string.IsNullOrWhiteSpace(bookmark.ContextAfter)
            || string.Equals(after, NormalizeBookmarkText(bookmark.ContextAfter), StringComparison.Ordinal);
        return beforeMatches && afterMatches;
    }

    private static int FindRelocatedBookmarkIndex(BookmarkItem bookmark, IReadOnlyList<string> lines)
    {
        var exactCandidates = new List<int>();
        var fallbackCandidates = new List<int>();

        for (var index = 0; index < lines.Count; index++)
        {
            if (BookmarkMatchesAt(bookmark, lines, index, requireContext: true))
            {
                exactCandidates.Add(index);
                continue;
            }

            if (BookmarkMatchesAt(bookmark, lines, index, requireContext: false))
            {
                fallbackCandidates.Add(index);
            }
        }

        if (exactCandidates.Count > 0)
        {
            return ChooseNearestBookmarkIndex(bookmark.LineNumber - 1, exactCandidates);
        }

        if (fallbackCandidates.Count > 0)
        {
            return ChooseNearestBookmarkIndex(bookmark.LineNumber - 1, fallbackCandidates);
        }

        return -1;
    }

    private static int ChooseNearestBookmarkIndex(int originalIndex, IEnumerable<int> candidates)
    {
        return candidates
            .OrderBy(candidate => Math.Abs(candidate - originalIndex))
            .ThenBy(candidate => candidate)
            .FirstOrDefault(-1);
    }

    private static void ApplyBookmarkLineState(BookmarkItem bookmark, IReadOnlyList<string> lines, int index, bool isStale)
    {
        bookmark.LineNumber = index + 1;
        bookmark.Text = index >= 0 && index < lines.Count ? lines[index] : bookmark.Text;
        bookmark.ContextBefore = index > 0 ? lines[index - 1] : string.Empty;
        bookmark.ContextAfter = index + 1 < lines.Count ? lines[index + 1] : string.Empty;
        bookmark.IsStale = isStale;
    }

    private static void ReplaceBookmarks(ObservableCollection<BookmarkItem> target, IEnumerable<BookmarkItem> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(CloneBookmark(item));
        }
    }

    private static List<BookmarkItem> CreateBookmarkSnapshot(IEnumerable<BookmarkItem> items)
    {
        return items
            .Select(CloneBookmark)
            .OrderBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.LineNumber)
            .ThenBy(item => item.IsGlobal)
            .ToList();
    }

    private static void SortBookmarkCollection(ObservableCollection<BookmarkItem> bookmarks)
    {
        var ordered = CreateBookmarkSnapshot(bookmarks);
        bookmarks.Clear();
        foreach (var item in ordered)
        {
            bookmarks.Add(item);
        }
    }

    private async Task<bool> TryRecoverBookmarkPathAsync(BookmarkItem bookmark)
    {
        if (bookmark == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(bookmark.FilePath) && File.Exists(bookmark.FilePath))
        {
            return true;
        }

        var scopeRoots = Explorer.GetScopeRootPaths();
        if (scopeRoots.Count == 0)
        {
            return false;
        }

        var recoveredPath = await Task.Run(() => FindRecoveredBookmarkPath(bookmark, scopeRoots));
        if (string.IsNullOrWhiteSpace(recoveredPath))
        {
            return false;
        }

        bookmark.FilePath = recoveredPath;
        bookmark.IsStale = false;
        BookmarksPanel?.RefreshView();
        return true;
    }

    private static string? FindRecoveredBookmarkPath(BookmarkItem bookmark, IReadOnlyList<string> scopeRoots)
    {
        var originalPath = NormalizeBookmarkPath(bookmark.FilePath);
        var fileName = Path.GetFileName(originalPath);
        var extension = Path.GetExtension(originalPath);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var sameNameCandidates = new List<string>();
        var scopeFiles = EnumerateScopeFiles(scopeRoots);

        foreach (var candidate in scopeFiles)
        {
            if (!string.Equals(Path.GetFileName(candidate), fileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            sameNameCandidates.Add(candidate);
        }

        var bestSameName = sameNameCandidates
            .Select(candidate => new { Path = candidate, Score = ScoreBookmarkCandidate(candidate, originalPath, bookmark) })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (bestSameName != null && bestSameName.Score > 0)
        {
            return bestSameName.Path;
        }

        var fingerprintCandidates = new List<string>();
        foreach (var candidate in scopeFiles)
        {
            if (!string.IsNullOrWhiteSpace(extension)
                && !string.Equals(Path.GetExtension(candidate), extension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (BookmarkFileContainsFingerprint(candidate, bookmark))
            {
                fingerprintCandidates.Add(candidate);
            }
        }

        return fingerprintCandidates
            .Select(candidate => new { Path = candidate, Score = ScoreBookmarkCandidate(candidate, originalPath, bookmark) })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();
    }

    private static List<string> EnumerateScopeFiles(IReadOnlyList<string> scopeRoots)
    {
        var files = new List<string>();
        var scannedFiles = 0;

        foreach (var root in scopeRoots.Where(Directory.Exists))
        {
            foreach (var file in EnumerateScopeFiles(root))
            {
                scannedFiles++;
                if (scannedFiles > BookmarkRecoveryMaxFileScans)
                {
                    return files;
                }

                files.Add(file);
            }
        }

        return files;
    }

    private static IEnumerable<string> EnumerateScopeFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(current);
            }
            catch
            {
                continue;
            }

            foreach (var directory in directories)
            {
                if (ShouldSkipBookmarkRecoveryDirectory(directory))
                {
                    continue;
                }

                pending.Push(directory);
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }
        }
    }

    private static bool ShouldSkipBookmarkRecoveryDirectory(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return BookmarkRecoveryIgnoredDirectories.Contains(name);
    }

    private static bool BookmarkFileContainsFingerprint(string path, BookmarkItem bookmark)
    {
        try
        {
            var lines = File.ReadLines(path).Select(NormalizeBookmarkText).ToList();
            if (lines.Count == 0)
            {
                return false;
            }

            return FindRelocatedBookmarkIndex(bookmark, lines) >= 0;
        }
        catch
        {
            return false;
        }
    }

    private static int ScoreBookmarkCandidate(string candidatePath, string originalPath, BookmarkItem bookmark)
    {
        var score = 0;

        if (string.Equals(Path.GetFileName(candidatePath), Path.GetFileName(originalPath), StringComparison.OrdinalIgnoreCase))
        {
            score += 200;
        }

        if (string.Equals(Path.GetExtension(candidatePath), Path.GetExtension(originalPath), StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
        }

        score += CountCommonTrailingPathSegments(candidatePath, originalPath) * 40;

        try
        {
            var lines = File.ReadLines(candidatePath).Select(NormalizeBookmarkText).ToList();
            var relocatedIndex = FindRelocatedBookmarkIndex(bookmark, lines);
            if (relocatedIndex >= 0)
            {
                score += 500;

                if (BookmarkMatchesAt(bookmark, lines, relocatedIndex, requireContext: true))
                {
                    score += 250;
                }
            }
        }
        catch
        {
        }

        return score;
    }

    private static int CountCommonTrailingPathSegments(string leftPath, string rightPath)
    {
        var leftSegments = SplitPathSegments(leftPath);
        var rightSegments = SplitPathSegments(rightPath);
        var count = 0;

        var leftIndex = leftSegments.Length - 1;
        var rightIndex = rightSegments.Length - 1;

        while (leftIndex >= 0 && rightIndex >= 0)
        {
            if (!string.Equals(leftSegments[leftIndex], rightSegments[rightIndex], StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            count++;
            leftIndex--;
            rightIndex--;
        }

        return count;
    }

    private static string[] SplitPathSegments(string path)
    {
        return NormalizeBookmarkPath(path)
            .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static string NormalizeBookmarkPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }
}