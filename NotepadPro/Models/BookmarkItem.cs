using System;
using ReactiveUI;

namespace NotepadPro.Models;

public enum BookmarkMarkerState
{
    None,
    Scoped,
    Global,
    Stale
}

public sealed class BookmarkItem : ReactiveObject
{
    private string _filePath = string.Empty;
    private int _lineNumber;
    private string _text = string.Empty;
    private DateTime _createdAt = DateTime.Now;
    private string _anchorFingerprint = string.Empty;
    private string _contextBefore = string.Empty;
    private string _contextAfter = string.Empty;
    private bool _isGlobal;
    private bool _isStale;

    public string FilePath
    {
        get => _filePath;
        set => this.RaiseAndSetIfChanged(ref _filePath, value);
    }

    public int LineNumber
    {
        get => _lineNumber;
        set => this.RaiseAndSetIfChanged(ref _lineNumber, value);
    }

    public string Text
    {
        get => _text;
        set => this.RaiseAndSetIfChanged(ref _text, value);
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => this.RaiseAndSetIfChanged(ref _createdAt, value);
    }

    public string AnchorFingerprint
    {
        get => _anchorFingerprint;
        set => this.RaiseAndSetIfChanged(ref _anchorFingerprint, value);
    }

    public string ContextBefore
    {
        get => _contextBefore;
        set => this.RaiseAndSetIfChanged(ref _contextBefore, value);
    }

    public string ContextAfter
    {
        get => _contextAfter;
        set => this.RaiseAndSetIfChanged(ref _contextAfter, value);
    }

    public bool IsGlobal
    {
        get => _isGlobal;
        set => this.RaiseAndSetIfChanged(ref _isGlobal, value);
    }

    public bool IsStale
    {
        get => _isStale;
        set => this.RaiseAndSetIfChanged(ref _isStale, value);
    }
}