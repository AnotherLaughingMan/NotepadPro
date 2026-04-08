using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using NotepadPro.Models;
using NotepadPro.Services;
using NotepadPro.ViewModels;
using TextMateSharp.Grammars;

namespace NotepadPro.Views;

public partial class EditorView : UserControl
{
    private TextEditor? _editor;
    private EditorViewModel? _viewModel;
    private ScrollViewer? _editorScrollViewer;
    private SettingsViewModel? _settingsViewModel;
    private Grid? _editorLayoutGrid;
    private GridSplitter? _previewSplitter;
    private Border? _previewPane;
    private StackPanel? _previewContentHost;
    private ScrollViewer? _previewScrollViewer;
    private RegistryOptions? _registryOptions;
    private TextMate.Installation? _textMateInstallation;
    private bool _suppressEditorTextSync;
    private FoldingManager? _foldingManager;
    private readonly IndentationCodeFoldingStrategy _foldingStrategy = new();
    private MatchingBracketRenderer? _matchingBracketRenderer;
    private BookmarkMarkerRenderer? _bookmarkMarkerRenderer;
    private LineNumberMargin? _lineNumberMargin;
    private Func<int, BookmarkMarkerState>? _bookmarkLookup;

    /// <summary>Fires when the internal PART_ScrollViewer becomes available.</summary>
    public event EventHandler<ScrollViewer>? ScrollViewerReady;

    /// <summary>Fires when the caret offset changes in the editor.</summary>
    public event EventHandler? CaretMoved;

    /// <summary>The editor's internal ScrollViewer (available after template is applied).</summary>
    public ScrollViewer? EditorScrollViewer => _editorScrollViewer;

    /// <summary>The underlying AvaloniaEdit text editor control.</summary>
    public TextEditor? Editor => _editor;

    public EditorView()
    {
        InitializeComponent();
        _editor = this.FindControl<TextEditor>("EditorTextBox");
        _editorLayoutGrid = this.FindControl<Grid>("EditorLayoutGrid");
        _previewSplitter = this.FindControl<GridSplitter>("PreviewSplitter");
        _previewPane = this.FindControl<Border>("PreviewPane");
        _previewContentHost = this.FindControl<StackPanel>("PreviewContentHost");
        _previewScrollViewer = this.FindControl<ScrollViewer>("PreviewScrollViewer");
        if (_editor != null)
        {
            _editor.Options.EnableHyperlinks = false;
            _editor.Options.EnableEmailHyperlinks = false;
            _editor.Options.HighlightCurrentLine = true;
            _editor.PropertyChanged += EditorOnPropertyChanged;
            _editor.TextArea.Caret.PositionChanged += OnEditorCaretPositionChanged;
            _editor.KeyDown += EditorOnKeyDown;
            _editor.TextInput += EditorOnTextInput;
            _editor.SizeChanged += (_, _) => RefreshLineNumbers();
            _editor.TemplateApplied += OnEditorTemplateApplied;

            try
            {
                _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
                _textMateInstallation = _editor.InstallTextMate(_registryOptions);
            }
            catch
            {
                _registryOptions = null;
                _textMateInstallation = null;
            }
        }

        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_editor != null)
        {
            _editor.TextArea.Caret.PositionChanged -= OnEditorCaretPositionChanged;

            if (_matchingBracketRenderer != null)
            {
                _editor.TextArea.TextView.BackgroundRenderers.Remove(_matchingBracketRenderer);
                _matchingBracketRenderer = null;
            }

            if (_bookmarkMarkerRenderer != null)
            {
                _editor.TextArea.TextView.BackgroundRenderers.Remove(_bookmarkMarkerRenderer);
                _bookmarkMarkerRenderer = null;
            }

            if (_foldingManager != null)
            {
                FoldingManager.Uninstall(_foldingManager);
                _foldingManager = null;
            }
        }
    }

    private void OnEditorCaretPositionChanged(object? sender, EventArgs e)
    {
        if (_editor == null)
        {
            return;
        }

        if (DataContext is EditorViewModel viewModel)
        {
            viewModel.UpdateCaretFromIndex(_editor.CaretOffset);
        }

        UpdateMatchingBracketHighlight();

        CaretMoved?.Invoke(this, EventArgs.Empty);
    }

    private void OnEditorTemplateApplied(object? sender, Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        _editorScrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");

        if (_editorScrollViewer != null)
        {
            ScrollViewerReady?.Invoke(this, _editorScrollViewer);
            SyncHorizontalScroll();
        }

        EnsureCodeFeatures();
        EnsureCustomLineNumberMargin();
        UpdateCodeFolding();
        UpdateMatchingBracketHighlight();

        ApplyTextMateGrammar();
        UpdateLineNumberVisibility();
        RefreshLineNumbers();
    }

    private void EditorOnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == "CaretOffset" && DataContext is EditorViewModel viewModel && e.NewValue is int caretIndex)
        {
            viewModel.UpdateCaretFromIndex(caretIndex);
            CaretMoved?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (e.Property.Name == "FontSize" || e.Property.Name == "FontFamily")
        {
            RefreshLineNumbers();
        }

        if (e.Property.Name == "Text")
        {
            if (!_suppressEditorTextSync && _viewModel != null)
            {
                var editorText = _editor?.Text ?? string.Empty;
                if (!string.Equals(_viewModel.Text, editorText, StringComparison.Ordinal))
                {
                    _viewModel.Text = editorText;
                }
            }

            UpdateCodeFolding();
            UpdateMatchingBracketHighlight();
            RenderMarkdownPreview();
            RefreshLineNumbers();
            RefreshBookmarkMarkers();
        }
    }

    private void EnsureCodeFeatures()
    {
        if (_editor == null)
        {
            return;
        }

        if (_matchingBracketRenderer == null)
        {
            _matchingBracketRenderer = new MatchingBracketRenderer();
            _editor.TextArea.TextView.BackgroundRenderers.Add(_matchingBracketRenderer);
        }

        if (_bookmarkMarkerRenderer == null)
        {
            _bookmarkMarkerRenderer = new BookmarkMarkerRenderer();
            _bookmarkMarkerRenderer.SetLookup(_bookmarkLookup);
            _editor.TextArea.TextView.BackgroundRenderers.Add(_bookmarkMarkerRenderer);
        }
    }

    public void SetBookmarkLookup(Func<int, BookmarkMarkerState>? lookup)
    {
        _bookmarkLookup = lookup;
        _bookmarkMarkerRenderer?.SetLookup(lookup);
        _lineNumberMargin?.SetBookmarkLookup(lookup);
    }

    public void RefreshBookmarkMarkers()
    {
        _editor?.TextArea.TextView.Redraw();
        _lineNumberMargin?.InvalidateVisual();
    }

    private void EnsureCustomLineNumberMargin()
    {
        if (_editor == null)
        {
            return;
        }

        _lineNumberMargin ??= new LineNumberMargin();
        _lineNumberMargin.Foreground = Application.Current?.Resources["ForegroundMuted"] as IBrush ?? Brushes.Gray;
        _lineNumberMargin.Background = Application.Current?.Resources["EditorSurface"] as IBrush;
        _lineNumberMargin.SetBookmarkLookup(_bookmarkLookup);

        if (!_editor.TextArea.LeftMargins.Contains(_lineNumberMargin))
        {
            _editor.TextArea.LeftMargins.Insert(0, _lineNumberMargin);
        }

        var presenter = _editor.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        _lineNumberMargin.SetPresenter(presenter);

        // Ensure the margin reserves enough space for bookmark markers so the
        // gutter doesn't shift when markers appear. Min value should match
        // `LineNumberMargin.LeftPad` plus some extra room.
        try
        {
            _lineNumberMargin.MinWidth = Math.Max(_lineNumberMargin.MinWidth, 28);
            _lineNumberMargin.InvalidateMeasure();
            _lineNumberMargin.InvalidateVisual();
        }
        catch
        {
            // Defensive: ignore failures here to avoid breaking template application.
        }
    }

    private void UpdateCodeFolding()
    {
        if (_editor == null)
        {
            return;
        }

        var language = _viewModel?.Language ?? string.Empty;
        var shouldEnableFolding = ShouldEnableFolding(language);

        if (!shouldEnableFolding)
        {
            if (_foldingManager != null)
            {
                FoldingManager.Uninstall(_foldingManager);
                _foldingManager = null;
            }

            return;
        }

        _foldingManager ??= FoldingManager.Install(_editor.TextArea);
        _foldingStrategy.UpdateFoldings(_foldingManager, _editor.Document);
    }

    private static bool ShouldEnableFolding(string language)
    {
        return language switch
        {
            "C#" or "C" or "C++" or "JavaScript" or "TypeScript" or "JSON" or "Python" or "Lua" or "HTML" or "CSS" or "XML" or "XAML" or "AXAML" => true,
            _ => false
        };
    }

    private void UpdateMatchingBracketHighlight()
    {
        if (_editor?.Document == null || _matchingBracketRenderer == null)
        {
            return;
        }

        if (!TryFindMatchingBracketPair(_editor.Document, _editor.CaretOffset, out var firstOffset, out var secondOffset))
        {
            _matchingBracketRenderer.SetPair(-1, -1);
            _editor.TextArea.TextView.Redraw();
            return;
        }

        _matchingBracketRenderer.SetPair(firstOffset, secondOffset);
        _editor.TextArea.TextView.Redraw();
    }

    private static bool TryFindMatchingBracketPair(TextDocument document, int caretOffset, out int firstOffset, out int secondOffset)
    {
        firstOffset = -1;
        secondOffset = -1;

        if (document.TextLength == 0)
        {
            return false;
        }

        var probeOffsets = new[]
        {
            Math.Clamp(caretOffset - 1, 0, Math.Max(0, document.TextLength - 1)),
            Math.Clamp(caretOffset, 0, Math.Max(0, document.TextLength - 1))
        };

        foreach (var probeOffset in probeOffsets)
        {
            var ch = document.GetCharAt(probeOffset);
            if (!TryGetBracketPair(ch, out var openBracket, out var closeBracket, out var isOpening))
            {
                continue;
            }

            var matchOffset = isOpening
                ? FindMatchingForward(document, probeOffset, openBracket, closeBracket)
                : FindMatchingBackward(document, probeOffset, openBracket, closeBracket);

            if (matchOffset < 0)
            {
                continue;
            }

            firstOffset = probeOffset;
            secondOffset = matchOffset;
            return true;
        }

        return false;
    }

    private static bool TryGetBracketPair(char ch, out char openBracket, out char closeBracket, out bool isOpening)
    {
        switch (ch)
        {
            case '(':
                openBracket = '(';
                closeBracket = ')';
                isOpening = true;
                return true;
            case '[':
                openBracket = '[';
                closeBracket = ']';
                isOpening = true;
                return true;
            case '{':
                openBracket = '{';
                closeBracket = '}';
                isOpening = true;
                return true;
            case ')':
                openBracket = '(';
                closeBracket = ')';
                isOpening = false;
                return true;
            case ']':
                openBracket = '[';
                closeBracket = ']';
                isOpening = false;
                return true;
            case '}':
                openBracket = '{';
                closeBracket = '}';
                isOpening = false;
                return true;
            default:
                openBracket = default;
                closeBracket = default;
                isOpening = false;
                return false;
        }
    }

    private static int FindMatchingForward(TextDocument document, int startOffset, char openBracket, char closeBracket)
    {
        var depth = 0;
        for (var i = startOffset; i < document.TextLength; i++)
        {
            var ch = document.GetCharAt(i);
            if (ch == openBracket)
            {
                depth++;
            }
            else if (ch == closeBracket)
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static int FindMatchingBackward(TextDocument document, int startOffset, char openBracket, char closeBracket)
    {
        var depth = 0;
        for (var i = startOffset; i >= 0; i--)
        {
            var ch = document.GetCharAt(i);
            if (ch == closeBracket)
            {
                depth++;
            }
            else if (ch == openBracket)
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private void SyncHorizontalScroll()
    {
        if (_editorScrollViewer == null || _editor == null) return;
        _editorScrollViewer.HorizontalScrollBarVisibility =
            _editor.WordWrap
                ? Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
                : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
    }

    private void EditorOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_editor == null || DataContext is not EditorViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Enter && viewModel.Settings.AutoIndentation)
        {
            e.Handled = true;
            InsertAutoIndentedLine(_editor);
        }
    }

    private void EditorOnTextInput(object? sender, TextInputEventArgs e)
    {
        if (_editor == null || DataContext is not EditorViewModel viewModel)
        {
            return;
        }

        if (!viewModel.Settings.AutoBracketing || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        if (TryHandleAutoBracketing(_editor, e.Text))
        {
            e.Handled = true;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.CaretRequested -= OnCaretRequested;
            _viewModel.SelectionRequested -= OnSelectionRequested;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (_settingsViewModel != null)
        {
            _settingsViewModel.PropertyChanged -= OnSettingsPropertyChanged;
            _settingsViewModel = null;
        }

        _viewModel = DataContext as EditorViewModel;
        if (_viewModel != null)
        {
            _viewModel.CaretRequested += OnCaretRequested;
            _viewModel.SelectionRequested += OnSelectionRequested;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            _settingsViewModel = _viewModel.Settings;
            _settingsViewModel.PropertyChanged += OnSettingsPropertyChanged;

            if (_editor != null && !string.Equals(_editor.Text ?? string.Empty, _viewModel.Text ?? string.Empty, StringComparison.Ordinal))
            {
                _suppressEditorTextSync = true;
                _editor.Text = _viewModel.Text ?? string.Empty;
                _suppressEditorTextSync = false;
            }

            ApplyTextMateTheme();
            ApplyTextMateGrammar();
            UpdateMarkdownPreviewLayout();
            RenderMarkdownPreview();
            UpdateLineNumberVisibility();
            RefreshLineNumbers();
        }
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsViewModel.EditorFontSize) or nameof(SettingsViewModel.WordWrap) or nameof(SettingsViewModel.ShowLineNumbers))
        {
            RenderMarkdownPreview();
            RefreshLineNumbers();
            return;
        }

        if (e.PropertyName is nameof(SettingsViewModel.Theme))
        {
            ApplyTextMateTheme();
            RefreshLineNumbers();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditorViewModel.Text) or nameof(EditorViewModel.LineCount))
        {
            if (_editor != null)
            {
                var editorText = _editor.Text ?? string.Empty;
                var viewModelText = _viewModel?.Text ?? string.Empty;
                if (!string.Equals(editorText, viewModelText, StringComparison.Ordinal))
                {
                    _suppressEditorTextSync = true;
                    _editor.Text = viewModelText;
                    _suppressEditorTextSync = false;
                }
            }

            RenderMarkdownPreview();
            RefreshLineNumbers();
            return;
        }

        if (e.PropertyName is nameof(EditorViewModel.Language))
        {
            ApplyTextMateGrammar();
            UpdateCodeFolding();
            return;
        }

        if (e.PropertyName is nameof(EditorViewModel.IsMarkdownPreviewActive))
        {
            UpdateMarkdownPreviewLayout();
            RenderMarkdownPreview();
        }
    }

    private void UpdateLineNumberVisibility()
    {
        if (_editor != null && _viewModel != null)
        {
            _editor.ShowLineNumbers = false;
            if (_lineNumberMargin != null)
            {
                _lineNumberMargin.IsVisible = _viewModel.Settings.ShowLineNumbers;
            }
        }
    }

    private void ApplyTextMateGrammar()
    {
        if (_registryOptions == null || _textMateInstallation == null || _viewModel == null)
        {
            return;
        }

        var languageId = _viewModel.Language switch
        {
            "C#" => "csharp",
            "C" => "c",
            "C++" => "cpp",
            "JavaScript" => "javascript",
            "TypeScript" => "typescript",
            "JSON" => "json",
            "Markdown" => "markdown",
            "Python" => "python",
            "Lua" => "lua",
            "HTML" => "html",
            "CSS" => "css",
            "XML" => "xml",
            "XAML" => "xml",
            "AXAML" => "xml",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(languageId))
        {
            return;
        }

        try
        {
            var scope = _registryOptions.GetScopeByLanguageId(languageId);
            if (!string.IsNullOrWhiteSpace(scope))
            {
                _textMateInstallation.SetGrammar(scope);
            }
        }
        catch
        {
        }
    }

    private void ApplyTextMateTheme()
    {
        if (_registryOptions == null || _textMateInstallation == null)
        {
            return;
        }

        var appTheme = _settingsViewModel?.Theme ?? "Dark+";
        try
        {
            var rawTheme = TextMateThemeFactory.Create(appTheme)
                ?? _registryOptions.LoadTheme(GetFallbackThemeName(appTheme));
            _textMateInstallation.SetTheme(rawTheme);
            ApplyEditorBaseBrushes();
        }
        catch
        {
        }
    }

    private void ApplyEditorBaseBrushes()
    {
        if (_editor == null || Application.Current == null)
        {
            return;
        }

        if (Application.Current.Resources.TryGetResource("ForegroundPrimary", Application.Current.ActualThemeVariant, out var foreground)
            && foreground is IBrush foregroundBrush)
        {
            _editor.Foreground = foregroundBrush;
        }

        if (Application.Current.Resources.TryGetResource("EditorSurface", Application.Current.ActualThemeVariant, out var background)
            && background is IBrush backgroundBrush)
        {
            _editor.Background = backgroundBrush;
        }
    }

    private static ThemeName GetFallbackThemeName(string appTheme)
    {
        return appTheme switch
        {
            "Dark+" => ThemeName.DarkPlus,
            "Dark Modern" => ThemeName.VisualStudioDark,
            "Dark High Contrast" => ThemeName.HighContrastDark,
            "One Dark Pro" => ThemeName.OneDark,
            "Monokai Pro" => ThemeName.Monokai,
            "Solarized Dark" => ThemeName.SolarizedDark,
            "Sand" => ThemeName.DarkPlus,
            "Goth" => ThemeName.DarkPlus,
            "Vampire" => ThemeName.DarkPlus,
            "Peach Sunset Light" => ThemeName.LightPlus,
            "Peach Sunset Soft" => ThemeName.LightPlus,
            "Light+" => ThemeName.LightPlus,
            _ => ThemeName.DarkPlus
        };
    }

    private void UpdateMarkdownPreviewLayout()
    {
        if (_editorLayoutGrid == null || _editorLayoutGrid.ColumnDefinitions.Count < 3)
        {
            return;
        }

        var isPreviewActive = _viewModel?.IsMarkdownPreviewActive == true;
        var editorColumn = _editorLayoutGrid.ColumnDefinitions[0];
        var splitterColumn = _editorLayoutGrid.ColumnDefinitions[1];
        var previewColumn = _editorLayoutGrid.ColumnDefinitions[2];

        if (isPreviewActive)
        {
            editorColumn.Width = new GridLength(1, GridUnitType.Star);
            splitterColumn.Width = GridLength.Auto;
            previewColumn.Width = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            editorColumn.Width = new GridLength(1, GridUnitType.Star);
            splitterColumn.Width = new GridLength(0);
            previewColumn.Width = new GridLength(0);
        }

        if (_previewSplitter != null)
        {
            _previewSplitter.IsVisible = isPreviewActive;
        }

        if (_previewPane != null)
        {
            _previewPane.IsVisible = isPreviewActive;
        }
    }

    private void RenderMarkdownPreview()
    {
        if (_previewContentHost == null)
        {
            return;
        }

        _previewContentHost.Children.Clear();

        if (_viewModel?.IsMarkdownPreviewActive != true)
        {
            return;
        }

        var markdown = _viewModel.Text ?? string.Empty;
        var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        var baseSize = Math.Max(10, _viewModel.Settings.EditorFontSize);

        var inCodeFence = false;
        var codeBuilder = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                if (!inCodeFence)
                {
                    inCodeFence = true;
                    codeBuilder.Clear();
                }
                else
                {
                    inCodeFence = false;
                    AddCodeBlock(codeBuilder.ToString(), baseSize);
                    codeBuilder.Clear();
                }

                continue;
            }

            if (inCodeFence)
            {
                codeBuilder.AppendLine(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                _previewContentHost.Children.Add(new Border { Height = Math.Max(4, baseSize * 0.35) });
                continue;
            }

            var headingMatch = Regex.Match(trimmed, "^(#{1,6})\\s+(.+)$");
            if (headingMatch.Success)
            {
                var level = headingMatch.Groups[1].Value.Length;
                var headingText = RenderInlineMarkdown(headingMatch.Groups[2].Value);
                AddHeading(headingText, level, baseSize);
                continue;
            }

            if (Regex.IsMatch(trimmed, "^(-{3,}|\\*{3,}|_{3,})$"))
            {
                _previewContentHost.Children.Add(new Border
                {
                    Height = 1,
                    Margin = new Thickness(0, baseSize * 0.8, 0, baseSize * 0.8),
                    Background = Application.Current?.Resources["BorderMedium"] as IBrush
                });
                continue;
            }

            if (trimmed.StartsWith(">", StringComparison.Ordinal))
            {
                var quoteText = RenderInlineMarkdown(trimmed.TrimStart('>', ' '));
                AddQuote(quoteText, baseSize);
                continue;
            }

            var numberedMatch = Regex.Match(trimmed, "^(\\d+)\\.\\s+(.+)$");
            if (numberedMatch.Success)
            {
                AddListItem($"{numberedMatch.Groups[1].Value}.", RenderInlineMarkdown(numberedMatch.Groups[2].Value), baseSize);
                continue;
            }

            var bulletMatch = Regex.Match(trimmed, "^[-*+]\\s+(.+)$");
            if (bulletMatch.Success)
            {
                AddListItem("•", RenderInlineMarkdown(bulletMatch.Groups[1].Value), baseSize);
                continue;
            }

            AddParagraph(RenderInlineMarkdown(line), baseSize);
        }

        if (inCodeFence && codeBuilder.Length > 0)
        {
            AddCodeBlock(codeBuilder.ToString(), baseSize);
        }

        _previewScrollViewer?.ScrollToHome();
    }

    private void AddHeading(string text, int level, double baseSize)
    {
        if (_previewContentHost == null)
        {
            return;
        }

        var size = level switch
        {
            1 => baseSize * 2.1,
            2 => baseSize * 1.8,
            3 => baseSize * 1.55,
            4 => baseSize * 1.35,
            5 => baseSize * 1.2,
            _ => baseSize * 1.1,
        };

        _previewContentHost.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, baseSize * 0.7, 0, baseSize * 0.25),
            Foreground = Application.Current?.Resources["ForegroundPrimary"] as IBrush
        });
    }

    private void AddParagraph(string text, double baseSize)
    {
        if (_previewContentHost == null)
        {
            return;
        }

        _previewContentHost.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = baseSize,
            FontFamily = new FontFamily("Segoe UI"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = baseSize * 1.55,
            Margin = new Thickness(0, 0, 0, baseSize * 0.3),
            Foreground = Application.Current?.Resources["ForegroundPrimary"] as IBrush
        });
    }

    private void AddListItem(string marker, string text, double baseSize)
    {
        if (_previewContentHost == null)
        {
            return;
        }

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(baseSize * 0.6, 0, 0, baseSize * 0.15)
        };

        row.Children.Add(new TextBlock
        {
            Text = marker,
            FontSize = baseSize,
            Margin = new Thickness(0, 0, baseSize * 0.55, 0),
            Foreground = Application.Current?.Resources["ForegroundPrimary"] as IBrush
        });

        var content = new TextBlock
        {
            Text = text,
            FontSize = baseSize,
            FontFamily = new FontFamily("Segoe UI"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = baseSize * 1.5,
            Foreground = Application.Current?.Resources["ForegroundPrimary"] as IBrush
        };
        Grid.SetColumn(content, 1);
        row.Children.Add(content);

        _previewContentHost.Children.Add(row);
    }

    private void AddQuote(string text, double baseSize)
    {
        if (_previewContentHost == null)
        {
            return;
        }

        var quote = new Border
        {
            BorderBrush = Application.Current?.Resources["AccentBrush"] as IBrush,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Margin = new Thickness(0, baseSize * 0.3, 0, baseSize * 0.5),
            Padding = new Thickness(baseSize * 0.7, baseSize * 0.35, 0, baseSize * 0.35),
            Child = new TextBlock
            {
                Text = text,
                FontSize = baseSize,
                FontStyle = FontStyle.Italic,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = baseSize * 1.5,
                Foreground = Application.Current?.Resources["ForegroundSecondary"] as IBrush
            }
        };

        _previewContentHost.Children.Add(quote);
    }

    private void AddCodeBlock(string code, double baseSize)
    {
        if (_previewContentHost == null)
        {
            return;
        }

        var block = new Border
        {
            Background = Application.Current?.Resources["InputBackground"] as IBrush,
            BorderBrush = Application.Current?.Resources["BorderSubtle"] as IBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, baseSize * 0.35, 0, baseSize * 0.5),
            Padding = new Thickness(baseSize * 0.75),
            Child = new TextBlock
            {
                Text = code.TrimEnd('\r', '\n'),
                FontSize = Math.Max(10, baseSize * 0.95),
                FontFamily = new FontFamily("Consolas"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = baseSize * 1.45,
                Foreground = Application.Current?.Resources["ForegroundPrimary"] as IBrush
            }
        };

        _previewContentHost.Children.Add(block);
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

    private void RefreshLineNumbers()
    {
        if (_viewModel == null || _editor == null) return;

        EnsureCustomLineNumberMargin();
        UpdateLineNumberVisibility();
        _lineNumberMargin?.UpdateLines(_viewModel.Text, _viewModel.LineCount, _viewModel.Settings.WordWrap);
        UpdateMarkdownPreviewLayout();
    }

    private void OnCaretRequested(object? sender, int caretIndex)
    {
        if (_editor == null)
        {
            return;
        }

        _editor.CaretOffset = Math.Clamp(caretIndex, 0, _editor.Text?.Length ?? 0);
        _editor.Select(_editor.CaretOffset, 0);
    }

    private void OnSelectionRequested(object? sender, (int start, int length) request)
    {
        if (_editor == null)
        {
            return;
        }

        var start = Math.Clamp(request.start, 0, _editor.Text?.Length ?? 0);
        var end = Math.Clamp(start + request.length, 0, _editor.Text?.Length ?? 0);
        _editor.Select(start, end - start);
        _editor.CaretOffset = end;
    }


    private static void InsertAutoIndentedLine(TextEditor editor)
    {
        var text = editor.Text ?? string.Empty;
        var caret = editor.CaretOffset;
        var lineStart = text.LastIndexOf('\n', Math.Max(0, caret - 1));
        var indentStart = lineStart < 0 ? 0 : lineStart + 1;
        var indentLength = 0;

        while (indentStart + indentLength < text.Length)
        {
            var ch = text[indentStart + indentLength];
            if (ch != ' ' && ch != '\t')
            {
                break;
            }

            indentLength++;
        }

        var indent = indentLength > 0 ? text.Substring(indentStart, indentLength) : string.Empty;
        ReplaceSelection(editor, "\n" + indent, caretOffset: indent.Length + 1);
    }

    private static bool TryHandleAutoBracketing(TextEditor editor, string input)
    {
        if (input.Length != 1)
        {
            return false;
        }

        var (open, close) = input[0] switch
        {
            '(' => ("(", ")"),
            '[' => ("[", "]"),
            '{' => ("{", "}"),
            '"' => ("\"", "\""),
            '\'' => ("'", "'"),
            _ => (string.Empty, string.Empty)
        };

        if (string.IsNullOrEmpty(open))
        {
            return false;
        }

        var selectionStart = editor.SelectionStart;
        var selectionEnd = selectionStart + editor.SelectionLength;

        if (selectionStart != selectionEnd)
        {
            var text = editor.Text ?? string.Empty;
            var selected = text.Substring(selectionStart, selectionEnd - selectionStart);
            ReplaceSelection(editor, open + selected + close, caretOffset: selected.Length + open.Length);
            return true;
        }

        ReplaceSelection(editor, open + close, caretOffset: open.Length);
        return true;
    }

    private static void ReplaceSelection(TextEditor editor, string insert, int caretOffset)
    {
        var text = editor.Text ?? string.Empty;
        var start = editor.SelectionStart;
        var end = start + editor.SelectionLength;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        var updated = text.Remove(start, end - start).Insert(start, insert);
        editor.Text = updated;
        editor.CaretOffset = start + caretOffset;
        editor.Select(editor.CaretOffset, 0);
    }

    private sealed class MatchingBracketRenderer : IBackgroundRenderer
    {
        private int _firstOffset = -1;
        private int _secondOffset = -1;

        public KnownLayer Layer => KnownLayer.Selection;

        public void SetPair(int firstOffset, int secondOffset)
        {
            _firstOffset = firstOffset;
            _secondOffset = secondOffset;
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_firstOffset < 0 || _secondOffset < 0 || !textView.VisualLinesValid)
            {
                return;
            }

            DrawOffset(textView, drawingContext, _firstOffset);
            DrawOffset(textView, drawingContext, _secondOffset);
        }

        private static void DrawOffset(TextView textView, DrawingContext drawingContext, int offset)
        {
            if (offset < 0 || offset >= textView.Document.TextLength)
            {
                return;
            }

            var accent = Application.Current?.Resources["AccentBrush"] as IBrush;
            var background = Application.Current?.Resources["CurrentLineHighlight"] as IBrush;
            if (accent == null && background == null)
            {
                return;
            }

            var segment = new TextSegment
            {
                StartOffset = offset,
                EndOffset = offset + 1
            };

            var geometryBuilder = new BackgroundGeometryBuilder
            {
                AlignToWholePixels = true,
                CornerRadius = 1
            };
            geometryBuilder.AddSegment(textView, segment);

            var geometry = geometryBuilder.CreateGeometry();
            if (geometry == null)
            {
                return;
            }

            if (background != null)
            {
                drawingContext.DrawGeometry(background, null, geometry);
            }

            if (accent != null)
            {
                drawingContext.DrawGeometry(null, new Pen(accent, 1), geometry);
            }
        }
    }

    private sealed class BookmarkMarkerRenderer : IBackgroundRenderer
    {
        private static readonly IBrush GlobalBookmarkBrush = new SolidColorBrush(Color.Parse("#5BC08A"));
        private static readonly IBrush StaleBookmarkBrush = new SolidColorBrush(Color.Parse("#F2C14E"));
        private Func<int, BookmarkMarkerState>? _lookup;

        public KnownLayer Layer => KnownLayer.Selection;

        public void SetLookup(Func<int, BookmarkMarkerState>? lookup)
        {
            _lookup = lookup;
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (!textView.VisualLinesValid || _lookup == null)
            {
                return;
            }

            var scopedBookmarkBrush = Application.Current?.Resources["AccentBrush"] as IBrush ?? Brushes.DodgerBlue;

            foreach (var visualLine in textView.VisualLines)
            {
                var lineNumber = visualLine.FirstDocumentLine.LineNumber;
                var markerState = _lookup(lineNumber);
                if (markerState == BookmarkMarkerState.None)
                {
                    continue;
                }

                var markerBrush = markerState switch
                {
                    BookmarkMarkerState.Global => GlobalBookmarkBrush,
                    BookmarkMarkerState.Stale => StaleBookmarkBrush,
                    _ => scopedBookmarkBrush
                };

                var markerSize = Math.Min(7d, Math.Max(4d, visualLine.Height - 6d));
                var x = 2d;
                var y = visualLine.VisualTop - textView.ScrollOffset.Y + Math.Max(0d, (visualLine.Height - markerSize) / 2d);
                drawingContext.FillRectangle(markerBrush, new Rect(x, y, markerSize, markerSize));
            }
        }
    }

    private sealed class IndentationCodeFoldingStrategy
    {
        private sealed record Frame(int Indent, int StartOffset, int StartLineNumber);

        public void UpdateFoldings(FoldingManager manager, TextDocument? document)
        {
            if (document == null)
            {
                manager.UpdateFoldings(Array.Empty<NewFolding>(), -1);
                return;
            }

            var foldings = CreateFoldings(document);
            manager.UpdateFoldings(foldings, -1);
        }

        private static List<NewFolding> CreateFoldings(TextDocument document)
        {
            var foldings = new List<NewFolding>();
            var stack = new Stack<Frame>();

            DocumentLine? previousCodeLine = null;
            var previousIndent = 0;

            foreach (var line in document.Lines)
            {
                var lineText = document.GetText(line);
                if (string.IsNullOrWhiteSpace(lineText))
                {
                    continue;
                }

                var currentIndent = GetIndentationLevel(lineText);

                while (stack.Count > 0 && currentIndent <= stack.Peek().Indent)
                {
                    var frame = stack.Pop();
                    var endOffset = previousCodeLine?.EndOffset ?? line.Offset;
                    if (line.LineNumber - frame.StartLineNumber >= 2 && endOffset > frame.StartOffset + 1)
                    {
                        foldings.Add(new NewFolding(frame.StartOffset, endOffset));
                    }
                }

                if (previousCodeLine != null && currentIndent > previousIndent)
                {
                    stack.Push(new Frame(previousIndent, previousCodeLine.Offset, previousCodeLine.LineNumber));
                }

                previousCodeLine = line;
                previousIndent = currentIndent;
            }

            while (stack.Count > 0)
            {
                var frame = stack.Pop();
                var endOffset = previousCodeLine?.EndOffset ?? document.TextLength;
                if ((previousCodeLine?.LineNumber ?? 0) - frame.StartLineNumber >= 1 && endOffset > frame.StartOffset + 1)
                {
                    foldings.Add(new NewFolding(frame.StartOffset, endOffset));
                }
            }

            foldings.Sort((left, right) => left.StartOffset.CompareTo(right.StartOffset));
            return foldings;
        }

        private static int GetIndentationLevel(string text)
        {
            var indent = 0;
            foreach (var ch in text)
            {
                if (ch == ' ')
                {
                    indent++;
                    continue;
                }

                if (ch == '\t')
                {
                    indent += 4;
                    continue;
                }

                break;
            }

            return indent;
        }
    }

}
