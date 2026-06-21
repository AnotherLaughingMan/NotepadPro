using NotepadPro.Views;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NotepadPro.Services;

/// <summary>
/// Coordinates the typed JSON message protocol between the C# host and the Monaco webview.
/// All messages are JSON objects with a snake_case "type" discriminator field.
/// </summary>
public sealed class WebBridgeService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private readonly WebViewHost _host;

    public WebBridgeService(WebViewHost host)
    {
        _host = host;
        _host.MessageReceived += OnRawMessage;
    }

    // ── Inbound events (webview → C#) ────────────────────────────────────────

    /// <summary>Raised when the Monaco editor reports it is fully initialised.</summary>
    public event EventHandler? EditorReady;

    /// <summary>Raised when the document dirty state changes.</summary>
    public event EventHandler<bool>? FileModified;

    /// <summary>Raised when the editor cursor moves. Args: (line, column, selectionLength).</summary>
    public event EventHandler<CursorChangedArgs>? CursorChanged;

    /// <summary>Raised when the user triggers Ctrl+S inside the editor.</summary>
    public event EventHandler<string>? SaveRequested;

    /// <summary>Raised when a welcome-screen action button is clicked.</summary>
    public event EventHandler<WelcomeActionArgs>? WelcomeAction;

    /// <summary>Raised on debounced status updates (word count, language, line count).</summary>
    public event EventHandler<StatusUpdateArgs>? StatusUpdated;

    /// <summary>Raised when editable rendered markdown pushes content back to host.</summary>
    public event EventHandler<MarkdownContentUpdateArgs>? MarkdownContentUpdated;

    // ── Outbound helpers (C# → webview) ──────────────────────────────────────

    public void SendFileOpen(string path, string content, string language) =>
        Post(new { type = "file:open", path, content, language });

    public void SendFileSaved() =>
        Post(new { type = "file:saved" });

    public void SendSettings(EditorBridgeSettings settings) =>
        Post(new { type = "settings:apply", settings });

    public void SendTheme(string themeName, ThemeColorsBridge colors) =>
        Post(new { type = "theme:apply", theme = themeName, colors });

    public void SendNavigate(int line, int column = 1) =>
        Post(new { type = "editor:navigate", line, column });

    public void SendBookmarks(IEnumerable<EditorBookmarkMarker> bookmarks) =>
        Post(new { type = "editor:bookmarks", bookmarks });


    public void SendScrollbarOpacity(double opacity) =>
        Post(new { type = "editor:scrollbarOpacity", opacity });

    public void SendCommand(string command, object? args = null) =>
        Post(new { type = "editor:command", command, args });

    public void SendMarkdownCommand(string command, object? args = null) =>
        Post(new { type = "markdown:command", command, args });

    public void SendPreviewToggle(bool visible) =>
        Post(new { type = "preview:toggle", visible });

    public void SendViewEditor() =>
        Post(new { type = "view:show", view = "editor" });

    public void SendViewWelcome(WelcomeDataBridge data) =>
        Post(new { type = "view:show", view = "welcome", data });

    // ── Internals ────────────────────────────────────────────────────────────

    private void Post(object message) =>
        _host.PostMessage(JsonSerializer.Serialize(message, SerializerOptions));

    private void OnRawMessage(object? sender, string json)
    {
        BridgeMessage? msg;
        try
        {
            msg = JsonSerializer.Deserialize<BridgeMessage>(json, SerializerOptions);
        }
        catch
        {
            return; // Malformed — ignore
        }

        if (msg is null) return;

        switch (msg.Type)
        {
            case "editor:ready":
                EditorReady?.Invoke(this, EventArgs.Empty);
                break;
            case "file:modified":
                FileModified?.Invoke(this, msg.IsDirty);
                break;
            case "cursor:changed":
                CursorChanged?.Invoke(this, new CursorChangedArgs(msg.Line, msg.Column, msg.SelectionLength));
                break;
            case "file:save:request":
                SaveRequested?.Invoke(this, msg.Content ?? string.Empty);
                break;
            case "status:update":
                StatusUpdated?.Invoke(this, new StatusUpdateArgs(msg.WordCount, msg.Language ?? string.Empty, msg.LineCount));
                break;
            case "markdown:content:update":
                MarkdownContentUpdated?.Invoke(this, new MarkdownContentUpdateArgs(msg.Content ?? string.Empty, msg.SourceMode ?? "rendered"));
                break;
            case "welcome:new-file":
            case "welcome:open-file":
            case "welcome:open-folder":
            case "welcome:open-workspace":
            case "welcome:create-workspace":
                WelcomeAction?.Invoke(this, new WelcomeActionArgs(msg.Type, null, "file"));
                break;
            case "welcome:open-recent":
                WelcomeAction?.Invoke(this, new WelcomeActionArgs(msg.Type, msg.Path, msg.Kind ?? "file"));
                break;
        }
    }
}

public sealed record EditorBookmarkMarker(int Line, string State);

// ── Inbound message envelope ──────────────────────────────────────────────────

internal sealed class BridgeMessage
{
    public string Type { get; init; } = string.Empty;
    public string? Path { get; init; }
    public string? Kind { get; init; }
    public string? Content { get; init; }
    public string? Language { get; init; }
    public bool IsDirty { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
    public int SelectionLength { get; init; }
    public int WordCount { get; init; }
    public int LineCount { get; init; }
    public string? SourceMode { get; init; }
}

// ── Event arg records ─────────────────────────────────────────────────────────

public sealed record CursorChangedArgs(int Line, int Column, int SelectionLength);
public sealed record StatusUpdateArgs(int WordCount, string Language, int LineCount);
public sealed record MarkdownContentUpdateArgs(string Content, string SourceMode);
public sealed record WelcomeActionArgs(string Action, string? Path, string Kind);

// ── Outbound data shapes ──────────────────────────────────────────────────────

public sealed record EditorBridgeSettings
{
    public bool WordWrap { get; init; }
    public bool ShowLineNumbers { get; init; }
    public bool IsMinimapVisible { get; init; }
    public int MinimapFadeSpeedMs { get; init; }
    public bool AutoIndentation { get; init; }
    public bool AutoBracketing { get; init; }
    public bool RenderWhitespace { get; init; }
    public int EditorFontSize { get; init; }
    public string Indentation { get; init; } = "    ";
    public string Eol { get; init; } = "LF";
}

public sealed record WelcomeDataBridge
{
    public RecentItemBridge[] RecentFiles { get; init; } = [];
    public RecentItemBridge[] RecentFolders { get; init; } = [];
    public RecentItemBridge[] RecentWorkspaces { get; init; } = [];
}

public sealed record RecentItemBridge(string DisplayName, string Path);

public sealed record ThemeColorsBridge
{
    public string Background { get; init; } = string.Empty;
    public string Foreground { get; init; } = string.Empty;
    public string SelectionBackground { get; init; } = string.Empty;
    public string LineHighlight { get; init; } = string.Empty;
    public string SyntaxKeyword { get; init; } = string.Empty;
    public string SyntaxString { get; init; } = string.Empty;
    public string SyntaxComment { get; init; } = string.Empty;
    public string SyntaxNumber { get; init; } = string.Empty;
    public string SyntaxType { get; init; } = string.Empty;
    public string SyntaxFunction { get; init; } = string.Empty;
}
