using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using NotepadPro.ViewModels;

namespace NotepadPro.Views;

public enum MinimapTone
{
    Normal,
    Keyword,
    String,
    Comment,
    Number,
    Type
}

public readonly record struct MinimapLineSample(double WidthRatio, MinimapTone Tone);

public class MinimapRenderControl : Control
{
    private const int SmallDocumentLineThreshold = 120;
    private const int MediumDocumentLineThreshold = 900;
    private const double SmallDocLinePitch = 2.4;
    private const double MediumDocLinePitch = 1.5;

    private IReadOnlyList<MinimapLineSample> _samples = Array.Empty<MinimapLineSample>();
    private bool _renderCharacters;
    private double _contentOffsetY;

    public void SetContent(IReadOnlyList<MinimapLineSample> samples, bool renderCharacters)
    {
        _samples = samples;
        _renderCharacters = renderCharacters;
        InvalidateVisual();
    }

    public void SetContentOffsetY(double contentOffsetY)
    {
        _contentOffsetY = contentOffsetY;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        var width = bounds.Width;
        var height = bounds.Height;
        if (width <= 0 || height <= 0 || _samples.Count == 0)
        {
            return;
        }

        var normalBrush = TryGetBrush("ForegroundInactive") ?? Brushes.Gray;
        var keywordBrush = TryGetBrush("SyntaxKeyword") ?? normalBrush;
        var stringBrush = TryGetBrush("SyntaxString") ?? normalBrush;
        var commentBrush = TryGetBrush("SyntaxComment") ?? normalBrush;
        var numberBrush = TryGetBrush("SyntaxNumber") ?? normalBrush;
        var typeBrush = TryGetBrush("SyntaxType") ?? normalBrush;

        var documentLines = _samples.Count;
        var contentWidth = Math.Max(1, width - 4);

        if (documentLines <= MediumDocumentLineThreshold)
        {
            var linePitch = documentLines <= SmallDocumentLineThreshold ? SmallDocLinePitch : MediumDocLinePitch;
            var naturalHeight = Math.Max(1, documentLines * linePitch);
            var drawnHeight = Math.Min(height, naturalHeight);
            var lineHeight = Math.Max(1, drawnHeight / Math.Max(1, documentLines));

            for (var lineIndex = 0; lineIndex < documentLines; lineIndex++)
            {
                var sample = _samples[lineIndex];
                var brush = sample.Tone switch
                {
                    MinimapTone.Keyword => keywordBrush,
                    MinimapTone.String => stringBrush,
                    MinimapTone.Comment => commentBrush,
                    MinimapTone.Number => numberBrush,
                    MinimapTone.Type => typeBrush,
                    _ => normalBrush
                };

                var y = (lineIndex * lineHeight) - _contentOffsetY;
                if (y > height)
                {
                    break;
                }

                if (y + lineHeight < 0)
                {
                    continue;
                }

                var barWidth = Math.Max(1, contentWidth * Math.Clamp(sample.WidthRatio, 0.08, 1));
                var blockHeight = Math.Max(1, lineHeight * 0.62);
                var blockY = y + ((lineHeight - blockHeight) * 0.5);

                using (context.PushOpacity(GetToneOpacity(sample.Tone)))
                {
                    context.FillRectangle(brush, new Rect(2, blockY, barWidth, blockHeight));
                }
            }

            return;
        }

        // Long-file mode: compress, but aggregate rows so output is smoother and less noisy.
        var drawRows = Math.Max(1, (int)Math.Ceiling(height));
        var sampleStep = documentLines / (double)drawRows;
        var rowHeight = Math.Max(1, height / drawRows);

        for (var row = 0; row < drawRows; row++)
        {
            var startIndex = Math.Min(documentLines - 1, (int)Math.Floor(row * sampleStep));
            var endIndex = Math.Min(documentLines, (int)Math.Floor((row + 1) * sampleStep) + 1);

            var maxWidthRatio = 0.08;
            var tone = MinimapTone.Normal;
            for (var i = startIndex; i < endIndex; i++)
            {
                var candidate = _samples[i];
                if (candidate.WidthRatio >= maxWidthRatio)
                {
                    maxWidthRatio = candidate.WidthRatio;
                    tone = candidate.Tone;
                }
            }

            var brush = tone switch
            {
                MinimapTone.Keyword => keywordBrush,
                MinimapTone.String => stringBrush,
                MinimapTone.Comment => commentBrush,
                MinimapTone.Number => numberBrush,
                MinimapTone.Type => typeBrush,
                _ => normalBrush
            };

            var y = (row * rowHeight) - _contentOffsetY;
            var barWidth = Math.Max(1, contentWidth * Math.Clamp(maxWidthRatio, 0.08, 1));

            if (y + rowHeight < 0)
            {
                continue;
            }

            if (y > height)
            {
                break;
            }
            if (_renderCharacters)
            {
                // Character-like tiny segments (VS Code-like feel), still cheap to render.
                var segmentHeight = Math.Max(1, rowHeight * 0.6);
                var segmentY = y + ((rowHeight - segmentHeight) * 0.5);

                var firstWidth = Math.Max(1, barWidth * 0.58);
                var secondWidth = Math.Max(1, barWidth * 0.27);
                var gap = 1.5;

                using (context.PushOpacity(GetToneOpacity(tone)))
                {
                    context.FillRectangle(brush, new Rect(2, segmentY, firstWidth, segmentHeight));
                    if (firstWidth + gap + secondWidth < contentWidth)
                    {
                        context.FillRectangle(brush, new Rect(2 + firstWidth + gap, segmentY, secondWidth, segmentHeight));
                    }
                }
            }
            else
            {
                // Faster block mode.
                var blockHeight = Math.Max(1, rowHeight * 0.72);
                var blockY = y + ((rowHeight - blockHeight) * 0.5);
                using (context.PushOpacity(GetToneOpacity(tone)))
                {
                    context.FillRectangle(brush, new Rect(2, blockY, barWidth, blockHeight));
                }
            }
        }
    }

    private static double GetToneOpacity(MinimapTone tone)
    {
        return tone switch
        {
            MinimapTone.Comment => 0.26,
            MinimapTone.Keyword => 0.42,
            MinimapTone.String => 0.38,
            MinimapTone.Number => 0.34,
            MinimapTone.Type => 0.4,
            _ => 0.3
        };
    }

    private IBrush? TryGetBrush(string key)
    {
        if (Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) == true && value is IBrush brush)
        {
            return brush;
        }

        return null;
    }
}

public partial class MinimapView : UserControl
{
    private const double MinViewportIndicatorHeight = 30;
    private const double HoverBandHeight = 20;
    private const double BottomPaddingFudgePx = 55;
    private static readonly bool EnableDebugScrollLog = false;

    private MinimapRenderControl? _minimapRender;
    private Canvas? _overlayCanvas;
    private Border? _viewportIndicator;
    private Border? _hoverBand;

    private ScrollViewer? _editorScrollViewer;
    private TextEditor? _editor;
    private TextView? _textView;
    private EditorViewModel? _viewModel;

    private readonly List<MinimapLineSample> _lineSamples = new();
    private bool _contentDirty = true;
    private bool _renderCharacters = false;
    private SettingsViewModel? _settings;

    private bool _isDragging;
    private double _dragOffsetWithinIndicator;
    private double _minimapScaleY = 1;

    private double _effectiveTotalHeight;
    private double _viewportHeight;
    private double _maxScroll;
    private double _indicatorHeight;
    private double _indicatorTop;

    public MinimapView()
    {
        InitializeComponent();

        _minimapRender = this.FindControl<MinimapRenderControl>("MinimapRender");
        _overlayCanvas = this.FindControl<Canvas>("MinimapOverlayCanvas");
        _viewportIndicator = this.FindControl<Border>("MinimapViewportIndicator");
        _hoverBand = this.FindControl<Border>("MinimapHoverBand");

        if (_overlayCanvas != null)
        {
            _overlayCanvas.PointerPressed += HandlePointerPressed;
            _overlayCanvas.PointerMoved += HandlePointerMoved;
            _overlayCanvas.PointerReleased += HandlePointerReleased;
            _overlayCanvas.PointerExited += HandlePointerExited;
            _overlayCanvas.PointerEntered += HandlePointerEntered;
            _overlayCanvas.PointerWheelChanged += HandlePointerWheelChanged;
            _overlayCanvas.SizeChanged += (_, _) => RecomputeScale();
        }

        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_editorScrollViewer != null)
        {
            _editorScrollViewer.PropertyChanged -= OnEditorScrollViewerPropertyChanged;
            _editorScrollViewer = null;
        }

        DetachTextViewEvents();

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }

        if (_settings != null)
        {
            _settings.PropertyChanged -= OnSettingsPropertyChanged;
            _settings = null;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as EditorViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _settings = _viewModel.Settings;
            _settings.PropertyChanged += OnSettingsPropertyChanged;
        }
        else if (_settings != null)
        {
            _settings.PropertyChanged -= OnSettingsPropertyChanged;
            _settings = null;
        }

        _contentDirty = true;
        RebuildLineCache();
        RecomputeScaleAndRedraw();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        if (e.PropertyName is nameof(EditorViewModel.Text) or nameof(EditorViewModel.LineCount))
        {
            _contentDirty = true;
            RebuildLineCache();
            RecomputeScaleAndRedraw();
            return;
        }

        if (e.PropertyName is nameof(EditorViewModel.CaretLine) or nameof(EditorViewModel.Settings.EditorFontSize))
        {
            UpdateIndicatorPosition();
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsViewModel.EditorFontSize) or nameof(SettingsViewModel.Theme))
        {
            _contentDirty = true;
            RecomputeScaleAndRedraw();
        }
    }

    public void BindEditor(TextEditor? editor)
    {
        if (ReferenceEquals(_editor, editor))
        {
            return;
        }

        DetachTextViewEvents();

        _editor = editor;
        _textView = _editor?.TextArea?.TextView;

        InitializeMinimapSync();

        _contentDirty = true;
        RecomputeScaleAndRedraw();
    }

    public void BindEditorScrollViewer(ScrollViewer? scrollViewer)
    {
        if (_editorScrollViewer != null)
        {
            _editorScrollViewer.PropertyChanged -= OnEditorScrollViewerPropertyChanged;
        }

        _editorScrollViewer = scrollViewer;
        if (_editorScrollViewer != null)
        {
            _editorScrollViewer.PropertyChanged += OnEditorScrollViewerPropertyChanged;
        }

        _contentDirty = true;
        RecomputeScaleAndRedraw();
    }

    // 1) Initialize minimap sync and ensure visual lines exist before reading metrics.
    private void InitializeMinimapSync()
    {
        if (_textView == null)
        {
            return;
        }

        _textView.ScrollOffsetChanged -= OnMainScrollChanged;
        _textView.VisualLinesChanged -= OnVisualLinesChanged;
        _textView.SizeChanged -= OnTextViewSizeChanged;

        _textView.ScrollOffsetChanged += OnMainScrollChanged;
        _textView.VisualLinesChanged += OnVisualLinesChanged;
        _textView.SizeChanged += OnTextViewSizeChanged;

        EnsureVisualLines();
        RecomputeScaleAndRedraw();
    }

    private void DetachTextViewEvents()
    {
        if (_textView != null)
        {
            _textView.ScrollOffsetChanged -= OnMainScrollChanged;
            _textView.VisualLinesChanged -= OnVisualLinesChanged;
            _textView.SizeChanged -= OnTextViewSizeChanged;
        }

        _textView = null;
        _editor = null;
    }

    private void OnMainScrollChanged(object? sender, EventArgs e)
    {
        UpdateIndicatorPosition();
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e)
    {
        _contentDirty = true;
        RecomputeScaleAndRedraw();
    }

    private void OnTextViewSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _contentDirty = true;
        RecomputeScaleAndRedraw();
    }

    private void OnEditorScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.OffsetProperty)
        {
            UpdateIndicatorPosition();
            return;
        }

        if (e.Property == ScrollViewer.ExtentProperty || e.Property == ScrollViewer.ViewportProperty)
        {
            _contentDirty = true;
            RecomputeScaleAndRedraw();
        }
    }

    // 2) Recompute scale and redraw minimap content only when needed (content/metrics changes).
    private void RecomputeScaleAndRedraw()
    {
        RecomputeScale();

        if (_contentDirty)
        {
            RebuildLineCache();
        }

        _minimapRender?.SetContent(_lineSamples, _renderCharacters);
        UpdateIndicatorPosition();
    }

    private void RecomputeScale()
    {
        if (_overlayCanvas == null)
        {
            return;
        }

        EnsureVisualLines();

        var minimapHeight = _overlayCanvas.Bounds.Height;
        if (minimapHeight <= 0)
        {
            return;
        }

        _effectiveTotalHeight = GetEffectiveTotalHeight();
        _viewportHeight = GetViewportHeight();
        _maxScroll = Math.Max(0, _effectiveTotalHeight - _viewportHeight);

        _minimapScaleY = _effectiveTotalHeight > 0
            ? Math.Min(1.0, minimapHeight / _effectiveTotalHeight)
            : 1;
    }

    // 3) Fractional indicator mapping: bottoms out exactly when editor reaches max scroll.
    private void UpdateIndicatorPosition()
    {
        if (_overlayCanvas == null || _viewportIndicator == null)
        {
            return;
        }

        EnsureVisualLines();

        var minimapHeight = _overlayCanvas.Bounds.Height;
        var minimapWidth = _overlayCanvas.Bounds.Width;
        if (minimapHeight <= 0 || minimapWidth <= 0)
        {
            return;
        }

        RecomputeScale();

        if (_effectiveTotalHeight <= 0 || _viewportHeight <= 0)
        {
            _viewportIndicator.IsVisible = false;
            return;
        }

        var scrollY = Math.Clamp(GetScrollOffsetY(), 0, _maxScroll);
        var scrollFraction = _maxScroll > 0 ? Math.Clamp(scrollY / _maxScroll, 0, 1) : 0;

        // Subtle VS Code-style parallax on long documents.
        var contentOffsetY = _lineSamples.Count >= 800
            ? scrollY * 0.12 * _minimapScaleY
            : 0.0;
        _minimapRender?.SetContentOffsetY(contentOffsetY);

        _indicatorHeight = Math.Clamp(_viewportHeight * _minimapScaleY, MinViewportIndicatorHeight, minimapHeight);
        var trackHeight = Math.Max(0, minimapHeight - _indicatorHeight);
        _indicatorTop = scrollFraction * trackHeight;

        if (EnableDebugScrollLog)
        {
            Console.WriteLine($"[Minimap] scrollY={scrollY:F2} maxScroll={_maxScroll:F2} fraction={scrollFraction:F4} top={_indicatorTop:F2} track={trackHeight:F2}");
        }

        _viewportIndicator.IsVisible = true;
        _viewportIndicator.Width = minimapWidth;
        _viewportIndicator.Height = _indicatorHeight;
        Canvas.SetTop(_viewportIndicator, Math.Clamp(_indicatorTop, 0, trackHeight));
    }

    private void EnsureVisualLines()
    {
        if (_textView == null)
        {
            return;
        }

        try
        {
            _textView.EnsureVisualLines();
        }
        catch
        {
        }
    }

    private void RebuildLineCache()
    {
        _lineSamples.Clear();

        var text = _viewModel?.Text;
        if (string.IsNullOrEmpty(text))
        {
            _lineSamples.Add(new MinimapLineSample(0.1, MinimapTone.Normal));
            _contentDirty = false;
            return;
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n');

        var maxLength = 1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length > maxLength)
            {
                maxLength = lines[i].Length;
            }
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var widthRatio = Math.Clamp((double)Math.Max(1, line.Length) / maxLength, 0.04, 1);
            var tone = ClassifyLineTone(line);
            _lineSamples.Add(new MinimapLineSample(widthRatio, tone));
        }

        _contentDirty = false;
    }

    private static MinimapTone ClassifyLineTone(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0)
        {
            return MinimapTone.Normal;
        }

        if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
            trimmed.StartsWith("/*", StringComparison.Ordinal) ||
            trimmed.StartsWith("*", StringComparison.Ordinal) ||
            trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            return MinimapTone.Comment;
        }

        if (trimmed.Contains('"', StringComparison.Ordinal) || trimmed.Contains('\'', StringComparison.Ordinal))
        {
            return MinimapTone.String;
        }

        if (trimmed.Contains("class ", StringComparison.Ordinal) ||
            trimmed.Contains("interface ", StringComparison.Ordinal) ||
            trimmed.Contains("struct ", StringComparison.Ordinal) ||
            trimmed.Contains("enum ", StringComparison.Ordinal))
        {
            return MinimapTone.Type;
        }

        if (ContainsKeyword(trimmed))
        {
            return MinimapTone.Keyword;
        }

        if (ContainsDigit(trimmed))
        {
            return MinimapTone.Number;
        }

        return MinimapTone.Normal;
    }

    private static bool ContainsKeyword(string line)
    {
        return line.Contains("public ", StringComparison.Ordinal)
               || line.Contains("private ", StringComparison.Ordinal)
               || line.Contains("protected ", StringComparison.Ordinal)
               || line.Contains("internal ", StringComparison.Ordinal)
               || line.Contains("static ", StringComparison.Ordinal)
               || line.Contains("async ", StringComparison.Ordinal)
               || line.Contains("await ", StringComparison.Ordinal)
               || line.Contains("return ", StringComparison.Ordinal)
               || line.Contains("if ", StringComparison.Ordinal)
               || line.Contains("for ", StringComparison.Ordinal)
               || line.Contains("while ", StringComparison.Ordinal)
               || line.Contains("switch ", StringComparison.Ordinal)
               || line.Contains("var ", StringComparison.Ordinal)
               || line.Contains("using ", StringComparison.Ordinal)
               || line.Contains("namespace ", StringComparison.Ordinal);
    }

    private static bool ContainsDigit(string line)
    {
        for (var i = 0; i < line.Length; i++)
        {
            if (char.IsDigit(line[i]))
            {
                return true;
            }
        }

        return false;
    }

    private double GetExtentHeight()
    {
        if (_editorScrollViewer != null && _editorScrollViewer.Extent.Height > 0)
        {
            return _editorScrollViewer.Extent.Height;
        }

        return Math.Max(1, _lineSamples.Count);
    }

    private double GetEffectiveTotalHeight()
    {
        EnsureVisualLines();

        var totalHeight = GetExtentHeight();

        if (totalHeight <= 0)
        {
            return 0;
        }

        var viewportHeight = GetViewportHeight();
        var adjusted = totalHeight - BottomPaddingFudgePx;
        if (viewportHeight > 0)
        {
            adjusted = Math.Max(adjusted, viewportHeight + 1);
        }

        return Math.Max(1, adjusted);
    }

    private double GetViewportHeight()
    {
        if (_editorScrollViewer != null && _editorScrollViewer.Viewport.Height > 0)
        {
            return _editorScrollViewer.Viewport.Height;
        }

        return _overlayCanvas?.Bounds.Height ?? 0;
    }

    private double GetScrollOffsetY()
    {
        if (_editorScrollViewer != null)
        {
            return Math.Max(0, _editorScrollViewer.Offset.Y);
        }

        if (_textView != null)
        {
            return Math.Max(0, _textView.ScrollOffset.Y);
        }

        return 0;
    }

    private void ScrollToFraction(double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        var targetOffset = _maxScroll * fraction;

        if (_editor != null)
        {
            _editor.ScrollToVerticalOffset(targetOffset);
            return;
        }

        if (_editorScrollViewer != null)
        {
            _editorScrollViewer.Offset = new Vector(_editorScrollViewer.Offset.X, targetOffset);
        }
    }

    private void HandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_overlayCanvas == null)
        {
            return;
        }

        if (!e.GetCurrentPoint(_overlayCanvas).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var point = e.GetPosition(_overlayCanvas);
        var currentTop = _indicatorTop;
        var currentBottom = _indicatorTop + _indicatorHeight;

        var isOnIndicator = point.Y >= currentTop && point.Y <= currentBottom;
        if (isOnIndicator)
        {
            _isDragging = true;
            _dragOffsetWithinIndicator = point.Y - currentTop;
            e.Pointer.Capture(_overlayCanvas);
        }
        else
        {
            _isDragging = false;
            var fraction = Math.Clamp(point.Y / Math.Max(1, _overlayCanvas.Bounds.Height), 0, 1);
            ScrollToFraction(fraction);
        }

        e.Handled = true;
    }

    private void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_overlayCanvas == null)
        {
            return;
        }

        var point = e.GetPosition(_overlayCanvas);

        if (_isDragging)
        {
            UpdateDragScroll(point.Y);
        }

        if (_hoverBand != null)
        {
            _hoverBand.Width = _overlayCanvas.Bounds.Width;
            _hoverBand.Height = HoverBandHeight;
            var hoverTop = Math.Clamp(point.Y - (HoverBandHeight * 0.5), 0, Math.Max(0, _overlayCanvas.Bounds.Height - HoverBandHeight));
            Canvas.SetTop(_hoverBand, hoverTop);
        }
    }

    private void HandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        e.Pointer.Capture(null);
    }

    private void HandlePointerEntered(object? sender, PointerEventArgs e)
    {
        if (_hoverBand != null)
        {
            _hoverBand.IsVisible = true;
        }
    }

    private void HandlePointerExited(object? sender, PointerEventArgs e)
    {
        _isDragging = false;
        if (_hoverBand != null)
        {
            _hoverBand.IsVisible = false;
        }
    }

    private void UpdateDragScroll(double pointerY)
    {
        if (_overlayCanvas == null)
        {
            return;
        }

        var minimapHeight = _overlayCanvas.Bounds.Height;
        var trackHeight = Math.Max(0, minimapHeight - _indicatorHeight);
        if (trackHeight <= 0)
        {
            ScrollToFraction(0);
            return;
        }

        var top = Math.Clamp(pointerY - _dragOffsetWithinIndicator, 0, trackHeight);
        var fraction = top / trackHeight;
        ScrollToFraction(fraction);
    }

    private void HandlePointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        var speed = Math.Max(0.1, _viewModel.Settings.ScrollSpeed);
        var deltaPixels = -e.Delta.Y * speed * 40;

        var current = GetScrollOffsetY();
        var target = Math.Clamp(current + deltaPixels, 0, _maxScroll);

        if (_editor != null)
        {
            _editor.ScrollToVerticalOffset(target);
        }
        else if (_editorScrollViewer != null)
        {
            _editorScrollViewer.Offset = new Vector(_editorScrollViewer.Offset.X, target);
        }

        e.Handled = true;
    }

}
