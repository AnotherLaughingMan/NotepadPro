using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using NotepadPro.Models;
using Control = Avalonia.Controls.Control;

namespace NotepadPro.Views;

/// <summary>
/// Custom control that renders line numbers pixel-perfectly aligned with a TextPresenter.
/// Creates an identical full-text TextLayout to read exact Y positions for each line,
/// guaranteeing zero drift from the TextPresenter's rendering.
/// </summary>
public class LineNumberMargin : Control
{
    private static readonly IBrush GlobalBookmarkBrush = new SolidColorBrush(Color.Parse("#5BC08A"));
    private static readonly IBrush StaleBookmarkBrush = new SolidColorBrush(Color.Parse("#F2C14E"));
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<LineNumberMargin, IBrush?>(nameof(Foreground), Brushes.Gray);

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<LineNumberMargin, IBrush?>(nameof(Background));

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    private TextPresenter? _presenter;
    private string _text = string.Empty;
    private int _lineCount;
    private bool _isWordWrap;

    private TextLayout? _cachedLayout;
    private List<int>? _cachedLineStarts;
    private string _cachedText = string.Empty;
    private double _cachedWidth = double.NaN;
    private bool _cachedWrap;
    private double _cachedFontSize = double.NaN;
    private FontFamily? _cachedFontFamily;
    private IBrush? _cachedForeground;
    private Func<int, BookmarkMarkerState>? _bookmarkLookup;

    // Reserve extra left padding to accommodate bookmark markers so the gutter
    // doesn't resize when markers appear. Increase if markers grow larger.
    private const double LeftPad = 24;
    private const double RightPad = 12;

    static LineNumberMargin()
    {
        AffectsRender<LineNumberMargin>(ForegroundProperty, BackgroundProperty);
    }

    public void SetPresenter(TextPresenter? presenter)
    {
        _presenter = presenter;
        InvalidateLayoutCache();
        UpdateDesiredWidth();
    }

    public void UpdateLines(string? text, int lineCount, bool isWordWrap)
    {
        _text = text ?? string.Empty;
        _lineCount = lineCount;
        _isWordWrap = isWordWrap;
        InvalidateLayoutCache();
        UpdateDesiredWidth();
        InvalidateVisual();
        InvalidateMeasure();
    }

    public void SetBookmarkLookup(Func<int, BookmarkMarkerState>? lookup)
    {
        _bookmarkLookup = lookup;
        InvalidateVisual();
    }

    private void UpdateDesiredWidth()
    {
        if (_presenter == null || _lineCount == 0)
        {
            Width = 0;
            MinWidth = 0;
            return;
        }

        var fontSize = _presenter.FontSize;
        if (!IsFinite(fontSize) || fontSize <= 0)
        {
            Width = 0;
            MinWidth = 0;
            return;
        }

        var maxDigits = Math.Max(1, _lineCount.ToString().Length);
        var typeface = new Typeface(_presenter.FontFamily);
        using var digitLayout = new TextLayout(
            new string('9', maxDigits), typeface, fontSize, Foreground);

        var width = digitLayout.TextLines[0].WidthIncludingTrailingWhitespace + LeftPad + RightPad;
        if (!IsFinite(width))
        {
            width = 0;
        }

        width = Math.Max(0, width);
        Width = Math.Ceiling(width);
        MinWidth = Width;
    }

    private void InvalidateLayoutCache()
    {
        _cachedLayout?.Dispose();
        _cachedLayout = null;
        _cachedLineStarts = null;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private TextLayout? EnsureLayout()
    {
        if (_presenter == null)
        {
            return null;
        }

        var wrap = _isWordWrap;
        var presenterWidth = _presenter.Bounds.Width;
        var hasFiniteWidth = IsFinite(presenterWidth) && presenterWidth > 0;
        var width = hasFiniteWidth ? presenterWidth : (wrap ? 0 : 10000);

        if (wrap && width <= 0)
        {
            return null;
        }

        var fontSize = _presenter.FontSize;
        if (!IsFinite(fontSize) || fontSize <= 0)
        {
            return null;
        }
        var fontFamily = _presenter.FontFamily;
        var foreground = _presenter.Foreground;
        var text = _text ?? string.Empty;

        if (_cachedLayout != null &&
            _cachedText == text &&
            _cachedWrap == wrap &&
            _cachedWidth.Equals(width) &&
            _cachedFontSize.Equals(fontSize) &&
            Equals(_cachedFontFamily, fontFamily) &&
            Equals(_cachedForeground, foreground))
        {
            return _cachedLayout;
        }

        _cachedLayout?.Dispose();

        var layoutText = string.IsNullOrEmpty(text) ? " " : text;
        var typeface = new Typeface(fontFamily);

        _cachedLayout = new TextLayout(
            layoutText,
            typeface,
            fontSize,
            foreground,
            textWrapping: wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            maxWidth: width);

        _cachedText = text;
        _cachedWrap = wrap;
        _cachedWidth = width;
        _cachedFontSize = fontSize;
        _cachedFontFamily = fontFamily;
        _cachedForeground = foreground;

        _cachedLineStarts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                _cachedLineStarts.Add(i + 1);
            }
        }

        return _cachedLayout;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(0, 0);
    }

    public override void Render(DrawingContext context)
    {
        // Fill background
        if (Background != null)
            context.FillRectangle(Background, new Rect(Bounds.Size));

        if (_presenter == null) return;

        var layout = EnsureLayout();
        if (layout == null) return;

        var typeface = new Typeface(_presenter.FontFamily);
        var fontSize = _presenter.FontSize;
        var brush = Foreground ?? Brushes.Gray;

        if (string.IsNullOrEmpty(_text))
        {
            // Still draw "1" for empty document
            using var numLayout = new TextLayout("1", typeface, fontSize, brush);
            numLayout.Draw(context, new Point(LeftPad, 0));
            return;
        }

        var maxDigits = Math.Max(1, _lineCount.ToString().Length);
        var lineStartOffsets = _cachedLineStarts ?? new List<int> { 0 };

        // For each visual text line, find what logical line it belongs to.
        // Only draw the number for the *first* visual line of each logical line.
        double y = 0;
        var logicalLine = 0;
        var lastDrawnLogicalLine = -1;

        foreach (var textLine in layout.TextLines)
        {
            // Advance logical line pointer based on character offset
            var lineStartChar = textLine.FirstTextSourceIndex;
            while (logicalLine + 1 < lineStartOffsets.Count &&
                   lineStartChar >= lineStartOffsets[logicalLine + 1])
            {
                logicalLine++;
            }

            // Draw number only on the first visual line of each logical line
            if (logicalLine != lastDrawnLogicalLine)
            {
                var bookmarkState = _bookmarkLookup?.Invoke(logicalLine + 1) ?? BookmarkMarkerState.None;
                if (bookmarkState != BookmarkMarkerState.None)
                {
                    var bookmarkBrush = bookmarkState switch
                    {
                        BookmarkMarkerState.Global => GlobalBookmarkBrush,
                        BookmarkMarkerState.Stale => StaleBookmarkBrush,
                        _ => Application.Current?.Resources["AccentBrush"] as IBrush ?? Brushes.DodgerBlue
                    };

                    var markerSize = Math.Min(6d, Math.Max(4d, textLine.Height - 8d));
                    var markerY = y + Math.Max(0d, (textLine.Height - markerSize) / 2d);
                    context.FillRectangle(bookmarkBrush, new Rect(4, markerY, markerSize, markerSize));
                }

                var numStr = (logicalLine + 1).ToString().PadLeft(maxDigits);
                using var numLayout = new TextLayout(numStr, typeface, fontSize, brush);
                var numWidth = numLayout.TextLines[0].WidthIncludingTrailingWhitespace;
                var x = Bounds.Width - numWidth - RightPad;
                numLayout.Draw(context, new Point(Math.Max(LeftPad, x), y));
                lastDrawnLogicalLine = logicalLine;
            }

            y += textLine.Height;
        }
    }
}
