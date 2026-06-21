using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Drawing.Printing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using AvaloniaEdit;
using NotepadPro.Models;
using NotepadPro.Services;
using NotepadPro.ViewModels;
using NotepadPro.Views.Dialogs;
using Forms = System.Windows.Forms;

namespace NotepadPro.Views;

public partial class MainWindow : Window
{
    private const double MinDocumentThumbHeight = 4;
    private const double MaxDocumentThumbHeight = 20;
    private const int MaxGotoAnythingIndexedFiles = 4000;
    private const int MaxGotoAnythingResults = 80;
    private const int MaxGotoAnythingMatchesPerFile = 6;
    private static readonly HashSet<string> GotoAnythingIgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        "bin",
        "node_modules",
        "obj"
    };
    private static readonly Regex TypeSymbolRegex = new(@"^\s*(?:public|private|protected|internal|static|abstract|sealed|partial|readonly|unsafe|new\s+)*\b(class|interface|enum|record|struct)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MethodSymbolRegex = new(@"^\s*(?:public|private|protected|internal|static|abstract|sealed|partial|async|virtual|override|readonly|unsafe|new\s+)*(?:[A-Za-z_][A-Za-z0-9_<>,?.\[\]]*\s+)+([A-Za-z_][A-Za-z0-9_]*)\s*\([^;]*\)\s*(?:\{|=>)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex FunctionAssignmentRegex = new(@"^\s*(?:const|let|var)\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?:async\s*)?(?:\([^)]*\)|[A-Za-z_][A-Za-z0-9_]*)\s*=>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MarkdownHeadingRegex = new(@"^\s{0,3}(#{1,6})\s+(.+?)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex XmlTagRegex = new(@"^\s*<([A-Za-z_][A-Za-z0-9_.:-]*)\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private string _lastFindQuery = string.Empty;
    private string _lastReplaceText = string.Empty;
    private bool _lastMatchCase;
    private bool _lastWholeWord;
    private SettingsDialog? _settingsDialog;
    private readonly PrintDocument _printDocument = new();
    private PageSettings _pageSettings = new();
    private PrinterSettings _printerSettings = new();
    private int _printLineIndex;
    private string[] _printLines = Array.Empty<string>();
    private ScrollViewer? _tabScrollViewer;
    private ColumnDefinition? _toolPanelLeftColumn;
    private ColumnDefinition? _toolPanelRightColumn;
    private GridLength _savedToolPanelWidth = new GridLength(320);
    private Border? _toolPanelLeftHost;
    private Border? _toolPanelRightHost;
    private GridSplitter? _leftToolPanelSplitter;
    private GridSplitter? _rightToolPanelSplitter;

    private bool _toolPanelWidthInitialized;

    // Custom scrollbar state
    private Canvas? _scrollbarCanvas;
    private Border? _scrollbarThumb;
    private Border? _scrollbarCursorIndicator;
    private ScrollViewer? _activeEditorScrollViewer;
    private EditorView? _activeEditorView;
    private TextEditor? _activeTextEditor;
    private bool _scrollbarDragging;
    private double _scrollbarDragStartY;
    private double _scrollbarDragStartOffset;
    private EditorViewModel? _activeEditorModel;
    private bool _markdownToolbarDragging;
    private Avalonia.Point _markdownToolbarDragStart;
    private double _markdownToolbarStartX;
    private static readonly (string ButtonName, string OverflowButtonName)[] PinnedMarkdownToolItems =
    {
        ("PinnedMdTogglePreviewButton", "PinnedMdOverflowTogglePreview"),
        ("PinnedMdHeadingButton", "PinnedMdOverflowHeading"),
        ("PinnedMdBulletedListButton", "PinnedMdOverflowBulleted"),
        ("PinnedMdNumberedListButton", "PinnedMdOverflowNumbered"),
        ("PinnedMdBoldButton", "PinnedMdOverflowBold"),
        ("PinnedMdItalicButton", "PinnedMdOverflowItalic"),
        ("PinnedMdInlineCodeButton", "PinnedMdOverflowInlineCode"),
        ("PinnedMdLinkButton", "PinnedMdOverflowLink"),
        ("PinnedMdPinToggleButton", "PinnedMdOverflowPinToggle"),
        ("PinnedMdHideButton", "PinnedMdOverflowHide")
    };
    private double _markdownToolbarStartY;
    private bool _topResizeDragging;
    private double _topResizeStartScreenY;
    private PixelPoint _topResizeStartPosition;
    private double _topResizeStartHeight;
    private bool _markdownHeadingDecrementRequested;
    private bool _markdownNumberedToBulletedRequested;
    private bool _markdownBulletedToNumberedRequested;
    private WebBridgeService? _webBridge;
    private bool _suppressWebViewPushFromBridge;

    private enum GotoAnythingQueryMode
    {
        Default,
        Symbols,
        Text
    }

    public MainWindow()
    {
        InitializeComponent();
        Deactivated += OnWindowDeactivated;
        KeyDown += OnWindowKeyDown;
        Closing += OnWindowClosing;
        SizeChanged += OnWindowSizeChanged;
        Opened += (_, _) => UpdatePinnedMarkdownToolbarLayout();
        Opened += (_, _) => InitWebBridge();
        AddHandler(InputElement.PointerPressedEvent, OnWindowPointerPressedForResize, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerMovedEvent, OnWindowPointerMovedForResize, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerReleasedEvent, OnWindowPointerReleasedForResize, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerPressedEvent, OnHeadingButtonPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);

        // Track window state changes to update maximize/restore icon
        this.GetObservable(Window.WindowStateProperty).Subscribe(OnWindowStateChanged);

        _printDocument.BeginPrint += OnBeginPrint;
        _printDocument.PrintPage += OnPrintPage;
        _printDocument.DefaultPageSettings = _pageSettings;
        _printDocument.PrinterSettings = _printerSettings;

        var tabControl = this.FindControl<TabControl>("MainTabControl");
        if (tabControl != null)
        {
            tabControl.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, OnTabMiddleClick, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            tabControl.AddHandler(Avalonia.Input.InputElement.PointerWheelChangedEvent, OnTabStripWheelChanged, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        }

        var editorAreaGrid = this.FindControl<Grid>("EditorAreaGrid");
        if (editorAreaGrid?.ColumnDefinitions.Count >= 5)
        {
            _toolPanelLeftColumn = editorAreaGrid.ColumnDefinitions[0];
            _toolPanelRightColumn = editorAreaGrid.ColumnDefinitions[4];
        }

        _toolPanelLeftHost = this.FindControl<Border>("ToolPanelLeftHost");
        _toolPanelRightHost = this.FindControl<Border>("ToolPanelRightHost");
        _leftToolPanelSplitter = this.FindControl<GridSplitter>("LeftToolPanelSplitter");
        _rightToolPanelSplitter = this.FindControl<GridSplitter>("RightToolPanelSplitter");

        // Custom scrollbar setup
        _scrollbarCanvas = this.FindControl<Canvas>("ScrollbarCanvas");
        _scrollbarThumb = this.FindControl<Border>("ScrollbarThumb");
        _scrollbarCursorIndicator = this.FindControl<Border>("ScrollbarCursorIndicator");
        if (_scrollbarCanvas != null)
        {
            _scrollbarCanvas.PointerPressed += OnScrollbarPointerPressed;
            _scrollbarCanvas.PointerMoved += OnScrollbarPointerMoved;
            _scrollbarCanvas.PointerReleased += OnScrollbarPointerReleased;
            _scrollbarCanvas.PointerWheelChanged += OnScrollbarPointerWheelChanged;
            _scrollbarCanvas.SizeChanged += (_, _) => UpdateScrollbar();
        }

        DataContextChanged += (_, _) =>
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(MainWindowViewModel.IsToolPanelVisible))
                    {
                        UpdateToolPanelColumn();
                    }
                    else if (args.PropertyName == nameof(MainWindowViewModel.SelectedTab))
                    {
                        AttachEditorModel(ViewModel.Editor);
                        Avalonia.Threading.Dispatcher.UIThread.Post(ConnectActiveEditorScrollViewer, Avalonia.Threading.DispatcherPriority.Loaded);
                        PushActiveTabToWebView();
                    }
                    else if (args.PropertyName == nameof(MainWindowViewModel.IsMarkdownToolbarPinned) ||
                             args.PropertyName == nameof(MainWindowViewModel.IsMarkdownToolbarShown) ||
                             args.PropertyName == nameof(MainWindowViewModel.IsMarkdownToolbarPinnedShown))
                    {
                        UpdateMarkdownToolbarPinnedState();
                        UpdatePinnedMarkdownToolbarLayout();
                    }
                };

                if (!_toolPanelWidthInitialized && ViewModel.Settings.ToolPanelWidth > 0)
                {
                    _savedToolPanelWidth = new GridLength(ViewModel.Settings.ToolPanelWidth);
                    _toolPanelWidthInitialized = true;
                }

                UpdateToolPanelColumn();
                AttachEditorModel(ViewModel.Editor);
                Avalonia.Threading.Dispatcher.UIThread.Post(ConnectActiveEditorScrollViewer, Avalonia.Threading.DispatcherPriority.Loaded);
                UpdateMarkdownToolbarPinnedState();
                UpdatePinnedMarkdownToolbarLayout();

                RebuildOpenRecentMenu();
                ViewModel.RecentFiles.CollectionChanged += (_, _) => RebuildOpenRecentMenu();
                ViewModel.Bookmarks.CollectionChanged += (_, _) => RefreshActiveEditorBookmarks();
                ViewModel.GlobalBookmarks.CollectionChanged += (_, _) => RefreshActiveEditorBookmarks();
            }
        };
    }

    public MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private void OnWindowPointerPressedForResize(object? sender, PointerPressedEventArgs e)
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        var pointState = e.GetCurrentPoint(this);
        if (!pointState.Properties.IsLeftButtonPressed)
        {
            return;
        }

        const double resizeBorderThickness = 6;
        var point = e.GetPosition(this);
        if (point.Y > resizeBorderThickness)
        {
            return;
        }

        _topResizeDragging = true;
        _topResizeStartPosition = Position;
        _topResizeStartHeight = Height;
        _topResizeStartScreenY = Position.Y + point.Y;
        e.Pointer.Capture(this);
        Cursor = GetTopResizeCursor(point);
        e.Handled = true;
    }

    private void OnWindowPointerMovedForResize(object? sender, PointerEventArgs e)
    {
        if (!_topResizeDragging)
        {
            UpdateTopResizeCursor(e);
        }

        if (!_topResizeDragging || WindowState != WindowState.Normal)
        {
            return;
        }

        var point = e.GetPosition(this);
        var currentScreenY = Position.Y + point.Y;
        var deltaY = currentScreenY - _topResizeStartScreenY;

        var minHeight = Math.Max(MinHeight, 1);
        var maxHeight = MaxHeight > 0 ? MaxHeight : double.MaxValue;
        var newHeight = Math.Clamp(_topResizeStartHeight - deltaY, minHeight, maxHeight);
        var consumedDelta = _topResizeStartHeight - newHeight;
        var newY = _topResizeStartPosition.Y + consumedDelta;

        Position = new PixelPoint(_topResizeStartPosition.X, (int)Math.Round(newY));
        Height = newHeight;
        e.Handled = true;
    }

    private void OnWindowPointerReleasedForResize(object? sender, PointerReleasedEventArgs e)
    {
        if (!_topResizeDragging)
        {
            UpdateTopResizeCursor(e);
            return;
        }

        _topResizeDragging = false;
        e.Pointer.Capture(null);
        UpdateTopResizeCursor(e);
        e.Handled = true;
    }

    private void UpdateTopResizeCursor(PointerEventArgs e)
    {
        if (WindowState != WindowState.Normal)
        {
            Cursor = null;
            return;
        }

        var point = e.GetPosition(this);
        const double resizeBorderThickness = 6;
        Cursor = point.Y <= resizeBorderThickness
            ? GetTopResizeCursor(point)
            : null;
    }

    private Cursor GetTopResizeCursor(Avalonia.Point point)
    {
        const double cornerResizeWidth = 10;
        if (point.X <= cornerResizeWidth)
        {
            return new Cursor(StandardCursorType.TopLeftCorner);
        }

        if (point.X >= Bounds.Width - cornerResizeWidth)
        {
            return new Cursor(StandardCursorType.TopRightCorner);
        }

        return new Cursor(StandardCursorType.TopSide);
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (e.Handled)
            return;

        var source = e.Source as Visual;
        while (source != null && source != sender)
        {
            if (source is Button or MenuItem)
                return;
            source = source.GetVisualParent();
        }

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
        else
        {
            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarPointerMoved(object? sender, PointerEventArgs e)
    {
    }

    private void UpdateToolPanelColumn()
    {
        if (_toolPanelLeftColumn == null || _toolPanelRightColumn == null)
        {
            return;
        }

        var isToolPanelVisible = ViewModel?.IsToolPanelVisible == true;
        var isRight = string.Equals(ViewModel?.Settings.PrimaryPanelPosition, "Right", StringComparison.OrdinalIgnoreCase);

        if (!isToolPanelVisible)
        {
            if (_toolPanelLeftColumn.ActualWidth > 0)
            {
                _savedToolPanelWidth = new GridLength(_toolPanelLeftColumn.ActualWidth);
            }
            else if (_toolPanelRightColumn.ActualWidth > 0)
            {
                _savedToolPanelWidth = new GridLength(_toolPanelRightColumn.ActualWidth);
            }

            if (ViewModel != null)
            {
                ViewModel.Settings.ToolPanelWidth = _savedToolPanelWidth.Value;
            }

            SetToolPanelColumnState(_toolPanelLeftColumn, visible: false);
            SetToolPanelColumnState(_toolPanelRightColumn, visible: false);
            SetToolPanelSideVisibility(leftVisible: false, rightVisible: false, leftSplitterVisible: false, rightSplitterVisible: false);
            return;
        }

        if (isRight)
        {
            SetToolPanelColumnState(_toolPanelLeftColumn, visible: false);
            SetToolPanelColumnState(_toolPanelRightColumn, visible: true);
            SetToolPanelSideVisibility(leftVisible: false, rightVisible: true, leftSplitterVisible: false, rightSplitterVisible: true);
        }
        else
        {
            SetToolPanelColumnState(_toolPanelLeftColumn, visible: true);
            SetToolPanelColumnState(_toolPanelRightColumn, visible: false);
            SetToolPanelSideVisibility(leftVisible: true, rightVisible: false, leftSplitterVisible: true, rightSplitterVisible: false);
        }
    }

    private void SetToolPanelColumnState(ColumnDefinition column, bool visible)
    {
        if (visible)
        {
            column.Width = _savedToolPanelWidth;
            column.MinWidth = 180;
            column.MaxWidth = 600;
        }
        else
        {
            column.Width = new GridLength(0);
            column.MinWidth = 0;
            column.MaxWidth = 0;
        }
    }

    private void SetToolPanelSideVisibility(bool leftVisible, bool rightVisible, bool leftSplitterVisible, bool rightSplitterVisible)
    {
        if (_toolPanelLeftHost != null)
        {
            _toolPanelLeftHost.IsVisible = leftVisible;
        }

        if (_toolPanelRightHost != null)
        {
            _toolPanelRightHost.IsVisible = rightVisible;
        }

        if (_leftToolPanelSplitter != null)
        {
            _leftToolPanelSplitter.IsVisible = leftSplitterVisible;
        }

        if (_rightToolPanelSplitter != null)
        {
            _rightToolPanelSplitter.IsVisible = rightSplitterVisible;
        }
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnWindowStateChanged(WindowState state)
    {
        var icon = this.FindControl<TextBlock>("MaxRestoreIcon");
        if (icon != null)
        {
            icon.Text = state == WindowState.Maximized ? "\uE923" : "\uE922";
        }

        var btn = this.FindControl<Button>("MaxRestoreButton");
        if (btn != null)
        {
            btn.SetValue(ToolTip.TipProperty,
                state == WindowState.Maximized ? "Restore Down" : "Maximize");
        }

        UpdatePinnedMarkdownToolbarLayout();
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (ViewModel?.IsMarkdownToolbarShown == true || ViewModel?.IsMarkdownToolbarPinnedShown == true)
        {
            ClampMarkdownToolbarPosition(ViewModel.IsMarkdownToolbarPinnedShown);
        }

        UpdatePinnedMarkdownToolbarLayout();
    }

    private void OnMarkdownToolbarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel == null || ViewModel.IsMarkdownToolbarPinned)
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.Source is Visual source)
        {
            var current = source;
            while (current != null && current != sender)
            {
                if (current is Button)
                {
                    return;
                }

                current = current.GetVisualParent();
            }
        }

        _markdownToolbarDragging = true;
        _markdownToolbarDragStart = e.GetPosition(this);
        _markdownToolbarStartX = ViewModel.MarkdownToolbarX;
        _markdownToolbarStartY = ViewModel.MarkdownToolbarY;
        e.Pointer.Capture(sender as IInputElement);
        e.Handled = true;
    }

    private void OnMarkdownToolbarPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_markdownToolbarDragging || ViewModel == null)
        {
            return;
        }

        var current = e.GetPosition(this);
        var dx = current.X - _markdownToolbarDragStart.X;
        var dy = current.Y - _markdownToolbarDragStart.Y;
        ViewModel.MarkdownToolbarX = _markdownToolbarStartX + dx;
        ViewModel.MarkdownToolbarY = _markdownToolbarStartY + dy;
        ClampMarkdownToolbarPosition();
        e.Handled = true;
    }

    private void OnMarkdownToolbarPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_markdownToolbarDragging)
        {
            return;
        }

        _markdownToolbarDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnMarkdownToolbarOpacityDown(object? sender, RoutedEventArgs e)
    {
        ViewModel?.DecreaseMarkdownToolbarOpacity();
    }

    private void OnMarkdownToolbarOpacityUp(object? sender, RoutedEventArgs e)
    {
        ViewModel?.IncreaseMarkdownToolbarOpacity();
    }

    private void OnMarkdownToolbarOpacityPercentWheel(object? sender, PointerWheelEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        if (e.Delta.Y > 0)
        {
            ViewModel.AdjustMarkdownToolbarOpacityByPercent(1);
            e.Handled = true;
            return;
        }

        if (e.Delta.Y < 0)
        {
            ViewModel.AdjustMarkdownToolbarOpacityByPercent(-1);
            e.Handled = true;
        }
    }

    private void OnMarkdownToolbarPinToggle(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        ViewModel.IsMarkdownToolbarPinned = !ViewModel.IsMarkdownToolbarPinned;
        UpdateMarkdownToolbarPinnedState();
        UpdatePinnedMarkdownToolbarLayout();
    }

    private void OnMarkdownToolbarHide(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.IsMarkdownToolbarVisible = false;
        }

        UpdatePinnedMarkdownToolbarLayout();
    }

    private void OnMarkdownToolbarRecoverToPinned(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        ViewModel.RecoverMarkdownToolbarToPinned();
        UpdateMarkdownToolbarPinnedState();
        UpdatePinnedMarkdownToolbarLayout();
    }

    private void OnMarkdownToolbarResetFloatingPosition(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        ViewModel.ResetFloatingMarkdownToolbarPosition();
        ClampMarkdownToolbarPosition();
        UpdateMarkdownToolbarPinnedState();
        UpdatePinnedMarkdownToolbarLayout();
    }

    private void OnPinnedMarkdownOverflowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            FlyoutBase.ShowAttachedFlyout(button);
            e.Handled = true;
        }
    }

    private void UpdatePinnedMarkdownToolbarLayout()
    {
        var viewModel = ViewModel;
        var overflowButton = this.FindControl<Button>("PinnedMdOverflowButton");
        if (viewModel == null || overflowButton == null)
        {
            return;
        }

        if (!viewModel.IsMarkdownToolbarPinnedShown)
        {
            overflowButton.IsVisible = false;
            foreach (var (buttonName, overflowButtonName) in PinnedMarkdownToolItems)
            {
                var button = this.FindControl<Button>(buttonName);
                var overflowItem = this.FindControl<Button>(overflowButtonName);
                if (button != null) button.IsVisible = true;
                if (overflowItem != null) overflowItem.IsVisible = false;
            }

            return;
        }

        var centerRegion = this.FindControl<Grid>("TitleBarCenterRegion");
        var menu = this.FindControl<Menu>("MainMenuBar");

        const double buttonSlotWidth = 30;
        const double menuGap = 8;
        var regionWidth = centerRegion?.Bounds.Width ?? 0;
        var menuDesiredWidth = menu?.DesiredSize.Width ?? 0;
        var menuActualWidth = menu?.Bounds.Width ?? 0;
        var menuReservedWidth = menuDesiredWidth > 0 ? menuDesiredWidth : menuActualWidth;

        // Pinned toolbar is centered in the title bar; keep it from overlapping the left menu area
        // by limiting total centered width based on how much room remains around the menu.
        var availableWidth = Math.Max(0, regionWidth - ((menuReservedWidth + menuGap) * 2));

        var needsOverflow = availableWidth < (PinnedMarkdownToolItems.Length * buttonSlotWidth);
        var overflowWidth = needsOverflow ? buttonSlotWidth : 0;
        var iconCapacity = (int)Math.Floor((availableWidth - overflowWidth) / buttonSlotWidth);
        var visibleCount = Math.Clamp(iconCapacity, 0, PinnedMarkdownToolItems.Length);

        var hasOverflow = visibleCount < PinnedMarkdownToolItems.Length;
        overflowButton.IsVisible = hasOverflow;

        for (var i = 0; i < PinnedMarkdownToolItems.Length; i++)
        {
            var (buttonName, overflowButtonName) = PinnedMarkdownToolItems[i];
            var showMainButton = i < visibleCount;

            var button = this.FindControl<Button>(buttonName);
            if (button != null)
            {
                button.IsVisible = showMainButton;
            }

            var overflowItem = this.FindControl<Button>(overflowButtonName);
            if (overflowItem != null)
            {
                overflowItem.IsVisible = !showMainButton;
            }
        }
    }

    private void UpdateMarkdownToolbarPinnedState()
    {
        if (ViewModel == null)
        {
            return;
        }

        if (ViewModel.IsMarkdownToolbarPinned)
        {
            ViewModel.MarkdownToolbarY = 6;
            ClampMarkdownToolbarPosition(limitToTitleBar: true);
            return;
        }

        if (ViewModel.IsMarkdownToolbarShown)
        {
            ClampMarkdownToolbarPosition();
        }
    }

    private void ClampMarkdownToolbarPosition(bool limitToTitleBar = false)
    {
        if (ViewModel == null)
        {
            return;
        }

        var toolbar = this.FindControl<Border>("MarkdownToolbar");
        var toolbarWidth = toolbar?.Bounds.Width > 0 ? toolbar.Bounds.Width : 480;
        var toolbarHeight = toolbar?.Bounds.Height > 0 ? toolbar.Bounds.Height : 44;

        var minVisibleWidth = 48d;
        var minX = -toolbarWidth + minVisibleWidth;
        var maxX = Math.Max(minX, Bounds.Width - minVisibleWidth);
        ViewModel.MarkdownToolbarX = Math.Clamp(ViewModel.MarkdownToolbarX, minX, maxX);

        if (limitToTitleBar)
        {
            ViewModel.MarkdownToolbarY = 6;
            return;
        }

        var minVisibleHeight = 28d;
        var minY = 0d;
        var maxY = Math.Max(minY, Bounds.Height - minVisibleHeight);
        ViewModel.MarkdownToolbarY = Math.Clamp(ViewModel.MarkdownToolbarY, minY, maxY);
    }

    // ---- Save Prompt Logic ----

    private async Task<bool> PromptSaveIfDirtyAsync(EditorTabViewModel tab)
    {
        if (!tab.Editor.HasUnsavedChanges)
        {
            return true; // no unsaved changes, proceed
        }

        var result = await ShowSavePromptAsync(tab.Editor.FileName);

        if (result == SavePromptResult.Save)
        {
            if (string.IsNullOrWhiteSpace(tab.Editor.FilePath))
            {
                var path = await PickSaveFileAsync();
                if (string.IsNullOrWhiteSpace(path))
                {
                    return false; // user cancelled save-as
                }

                await tab.Editor.SaveAsAsync(path);
            }
            else
            {
                await tab.Editor.SaveAsync();
            }

            return true;
        }

        if (result == SavePromptResult.SaveAs)
        {
            var path = await PickSaveFileAsync();
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            await tab.Editor.SaveAsAsync(path);
            return true;
        }

        if (result == SavePromptResult.DontSave)
        {
            return true;
        }

        return false; // cancelled
    }

    private async Task<SavePromptResult> ShowSavePromptAsync(string fileName)
    {
        var dialog = new Window
        {
            Title = "Notepad Pro",
            Width = 540,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            SystemDecorations = SystemDecorations.None,
            Background = Avalonia.Media.Brushes.Transparent
        };

        var resultValue = SavePromptResult.Cancel;

        var saveBtn = new Button { Content = "Save", Width = 80, Margin = new Thickness(0, 0, 8, 0) };
        var saveAsBtn = new Button { Content = "Save As...", Width = 100, Margin = new Thickness(0, 0, 8, 0) };
        var dontSaveBtn = new Button
        {
            Content = "Don't Save",
            Width = 110,
            Margin = new Thickness(0, 0, 8, 0)
        };
        dontSaveBtn.SetValue(ToolTip.TipProperty, "Close without saving. Your unsaved changes will be lost.");
        var cancelBtn = new Button { Content = "Cancel", Width = 80 };

        saveBtn.Click += (_, _) => { resultValue = SavePromptResult.Save; dialog.Close(); };
        saveAsBtn.Click += (_, _) => { resultValue = SavePromptResult.SaveAs; dialog.Close(); };
        dontSaveBtn.Click += (_, _) => { resultValue = SavePromptResult.DontSave; dialog.Close(); };
        cancelBtn.Click += (_, _) => { resultValue = SavePromptResult.Cancel; dialog.Close(); };

        dialog.Content = new Border
        {
            Background = Avalonia.Media.Brush.Parse("#1E1E1E"),
            BorderBrush = Avalonia.Media.Brush.Parse("#3F3F46"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(20),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"Do you want to save changes to {fileName}?",
                        Foreground = Avalonia.Media.Brush.Parse("#D4D4D4"),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        FontSize = 13
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Children = { saveBtn, saveAsBtn, dontSaveBtn, cancelBtn }
                    }
                }
            }
        };

        await dialog.ShowDialog(this);
        return resultValue;
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        // Persist tool panel width if it's currently open
        var activePanelColumn = string.Equals(ViewModel.Settings.PrimaryPanelPosition, "Right", StringComparison.OrdinalIgnoreCase)
            ? _toolPanelRightColumn
            : _toolPanelLeftColumn;

        if (activePanelColumn != null && ViewModel.IsToolPanelVisible && activePanelColumn.ActualWidth > 0)
        {
            ViewModel.Settings.ToolPanelWidth = activePanelColumn.ActualWidth;
        }

        // Check all tabs for unsaved changes
        foreach (var tab in ViewModel.Tabs.ToList())
        {
            if (tab.Editor.HasUnsavedChanges)
            {
                e.Cancel = true;
                var canClose = await PromptSaveAllAndCloseAsync();
                if (canClose)
                {
                    Closing -= OnWindowClosing; // prevent re-entry
                    Close();
                }
                return;
            }
        }
    }

    private async Task<bool> PromptSaveAllAndCloseAsync()
    {
        if (ViewModel == null)
        {
            return true;
        }

        foreach (var tab in ViewModel.Tabs.ToList())
        {
            if (!await PromptSaveIfDirtyAsync(tab))
            {
                return false; // user cancelled on one tab
            }
        }

        return true;
    }

    private async void OnTabMiddleClick(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
        {
            return;
        }

        var source = e.Source as Control;
        while (source != null)
        {
            if (source is TabItem tabItem && tabItem.DataContext is EditorTabViewModel tab)
            {
                e.Handled = true;
                if (await PromptSaveIfDirtyAsync(tab))
                {
                    ViewModel?.CloseTab(tab);
                }
                return;
            }
            source = source.Parent as Control;
        }
    }

    private void OnTabStripWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var tabControl = this.FindControl<TabControl>("MainTabControl");
        if (tabControl == null)
        {
            return;
        }

        var pos = e.GetPosition(tabControl);
        if (pos.Y > 34)
        {
            return;
        }

        if (_tabScrollViewer == null)
        {
            _tabScrollViewer = tabControl.GetVisualDescendants()
                .OfType<ScrollViewer>()
                .FirstOrDefault(sv => sv.Name == "TabScrollViewer");
        }

        if (_tabScrollViewer == null)
        {
            return;
        }

        // Delta.X = tilt-scroll, Delta.Y = regular wheel → both drive horizontal scroll
        var scrollAmount = (e.Delta.X != 0 ? e.Delta.X : -e.Delta.Y) * 50;
        if (scrollAmount == 0)
        {
            return;
        }

        var maxOffset = Math.Max(0, _tabScrollViewer.Extent.Width - _tabScrollViewer.Viewport.Width);
        var newOffset = Math.Clamp(_tabScrollViewer.Offset.X + scrollAmount, 0, maxOffset);
        _tabScrollViewer.Offset = new Avalonia.Vector(newOffset, 0);
        e.Handled = true;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (ctrl && !shift)
        {
            switch (e.Key)
            {
                case Key.O:
                    OnFileOpen(sender, e);
                    e.Handled = true;
                    return;
                case Key.N:
                    OnFileNew(sender, e);
                    e.Handled = true;
                    return;
                case Key.S:
                    OnFileSave(sender, e);
                    e.Handled = true;
                    return;
                case Key.W:
                    CloseActiveTabWithPrompt();
                    e.Handled = true;
                    return;
                case Key.F:
                    OnEditFind(sender, e);
                    e.Handled = true;
                    return;
                case Key.H:
                    OnEditReplace(sender, e);
                    e.Handled = true;
                    return;
                case Key.G:
                    OnEditGoTo(sender, e);
                    e.Handled = true;
                    return;
                case Key.P:
                    OnGotoAnything(sender, e);
                    e.Handled = true;
                    return;
                case Key.F2:
                    OnEditToggleBookmark(sender, e);
                    e.Handled = true;
                    return;
                case Key.Tab:
                    ViewModel?.SelectNextTab();
                    e.Handled = true;
                    return;
                case Key.OemPlus:
                case Key.Add:
                    OnViewZoomIn(sender, e);
                    e.Handled = true;
                    return;
                case Key.OemMinus:
                case Key.Subtract:
                    OnViewZoomOut(sender, e);
                    e.Handled = true;
                    return;
                case Key.D0:
                case Key.NumPad0:
                    OnViewZoomReset(sender, e);
                    e.Handled = true;
                    return;
            }
        }

        if (ctrl && shift)
        {
            switch (e.Key)
            {
                case Key.M:
                    OnMarkdownTogglePreview(sender, e);
                    e.Handled = true;
                    return;
                case Key.F2:
                    OnEditToggleGlobalBookmark(sender, e);
                    e.Handled = true;
                    return;
                case Key.B:
                    OnViewToggleBookmarks(sender, e);
                    e.Handled = true;
                    return;
                case Key.P:
                    OnCommandPalette(sender, e);
                    e.Handled = true;
                    return;
                case Key.S:
                    OnFileSaveAs(sender, e);
                    e.Handled = true;
                    return;
                case Key.Tab:
                    ViewModel?.SelectPreviousTab();
                    e.Handled = true;
                    return;
            }
        }

        if (!ctrl && !shift)
        {
            switch (e.Key)
            {
                case Key.F2:
                    OnEditNextBookmark(sender, e);
                    e.Handled = true;
                    return;
                case Key.F3:
                    OnEditFindNext(sender, e);
                    e.Handled = true;
                    return;
                case Key.F5:
                    OnEditTimeDate(sender, e);
                    e.Handled = true;
                    return;
            }
        }

        if (!ctrl && shift)
        {
            switch (e.Key)
            {
                case Key.F2:
                    OnEditPreviousBookmark(sender, e);
                    e.Handled = true;
                    return;
            }
        }
    }

    private void OnFileNew(object? sender, RoutedEventArgs e)
    {
        ViewModel?.NewDocument();
    }

    private void OnFileNewText(object? sender, RoutedEventArgs e)
    {
        ViewModel?.NewTextDocument();
    }

    private void OnFileNewMarkdown(object? sender, RoutedEventArgs e)
    {
        ViewModel?.NewMarkdownDocument();
    }

    private void OnFileNewJson(object? sender, RoutedEventArgs e)
    {
        ViewModel?.NewJsonDocument();
    }

    private void OnFileNewXml(object? sender, RoutedEventArgs e)
    {
        ViewModel?.NewXmlDocument();
    }

    private void OnFileNewCSharp(object? sender, RoutedEventArgs e)
    {
        ViewModel?.NewCSharpDocument();
    }

    private void OnFileNewC(object? sender, RoutedEventArgs e)
    {
        ViewModel?.NewCDocument();
    }

    private void OnFileNewCpp(object? sender, RoutedEventArgs e)
    {
        ViewModel?.NewCppDocument();
    }

    private void OnFileNewXaml(object? sender, RoutedEventArgs e)
    {
        ViewModel?.NewXamlDocument();
    }

    private void OnFileNewAxaml(object? sender, RoutedEventArgs e)
    {
        ViewModel?.NewAxamlDocument();
    }

    private async void OnCommandPalette(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        await ShowQuickPickAsync(
            title: "Command Palette",
            watermark: "Type a command...",
            resolveItems: query => BuildCommandPaletteEntries()
                .Where(item => MatchesQuickPickQuery(item, query))
                .Take(40)
                .ToList());
    }

    private async void OnGotoAnything(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        var indexedFiles = BuildGotoAnythingFileIndex();
        await ShowQuickPickAsync(
            title: "Goto Anything",
            watermark: "Search files, @symbols, #text, or type :123:9",
            resolveItems: query => BuildGotoAnythingEntries(query, indexedFiles));
    }

    private void OnTabAddClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.NewDocument();
    }

    private void OnGreetingNewFileClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.NewDocument();
    }

    private void OnGreetingOpenFileClick(object? sender, RoutedEventArgs e)
    {
        OnFileOpen(sender, e);
    }

    private void OnGreetingOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        OnFileOpenFolder(sender, e);
    }

    private void OnGreetingOpenWorkspaceClick(object? sender, RoutedEventArgs e)
    {
        OnFileOpenWorkspace(sender, e);
    }

    private async void OnGreetingCreateWorkspaceClick(object? sender, RoutedEventArgs e)
    {
        await CreateWorkspaceFromFolderAsync();
    }

    private async void OnGreetingRecentFileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not RecentFileItem item || ViewModel == null)
        {
            return;
        }

        await ViewModel.OpenFileFromPathAsync(item.Path);
    }

    private async void OnGreetingRecentProjectClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not RecentProjectItem item || ViewModel == null)
        {
            return;
        }

        if (item.IsWorkspace)
        {
            ViewModel.Explorer.LoadWorkspace(item.Path);
            ViewModel.AddRecentProject(item.Path);
            return;
        }

        await OpenFolderWithWorkspaceDetectionAsync(item.Path);
    }

    private async void CloseActiveTabWithPrompt()
    {
        if (ViewModel?.SelectedTab == null)
        {
            return;
        }

        if (await PromptSaveIfDirtyAsync(ViewModel.SelectedTab))
        {
            ViewModel.CloseTab(ViewModel.SelectedTab);
        }
    }

    private async void OnFileOpen(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        var paths = await PickOpenFileAsync();
        var path = paths.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await ViewModel.OpenFileFromPathAsync(path);
        }
    }

    private async void OnFileOpenFolder(object? sender, RoutedEventArgs e)
    {
        var suggestedStart = await TryGetPreferredOpenStartLocationAsync();
        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            SuggestedStartLocation = suggestedStart
        });

        var folder = result.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            await OpenFolderWithWorkspaceDetectionAsync(folder);
        }
    }

    private async void OnFileOpenWorkspace(object? sender, RoutedEventArgs e)
    {
        var suggestedStart = await TryGetPreferredOpenStartLocationAsync();
        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            SuggestedStartLocation = suggestedStart,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("VS Code Workspace")
                {
                    Patterns = new List<string> { "*.code-workspace" }
                }
            }
        });

        var workspacePath = result.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(workspacePath))
        {
            ViewModel?.Explorer.LoadWorkspace(workspacePath);
            ViewModel?.AddRecentProject(workspacePath);
        }
    }

    private async void OnFileCreateWorkspace(object? sender, RoutedEventArgs e)
    {
        await CreateWorkspaceFromFolderAsync();
    }

    private async Task CreateWorkspaceFromFolderAsync()
    {
        if (ViewModel == null)
        {
            return;
        }

        var suggestedStart = await TryGetPreferredOpenStartLocationAsync();
        var folderResult = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Select folder for new workspace",
            SuggestedStartLocation = suggestedStart
        });

        var folderPath = folderResult.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return;
        }

        var folderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var suggestedName = string.IsNullOrWhiteSpace(folderName)
            ? "NewWorkspace.code-workspace"
            : $"{folderName}.code-workspace";

        var saveResult = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Create Workspace",
            SuggestedFileName = suggestedName,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("VS Code Workspace")
                {
                    Patterns = new List<string> { "*.code-workspace" }
                }
            }
        });

        var workspacePath = saveResult?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return;
        }

        if (!workspacePath.EndsWith(".code-workspace", StringComparison.OrdinalIgnoreCase))
        {
            workspacePath += ".code-workspace";
        }

        var workspaceDirectory = Path.GetDirectoryName(workspacePath) ?? folderPath;
        var relativeFolderPath = Path.GetRelativePath(workspaceDirectory, folderPath);
        if (string.IsNullOrWhiteSpace(relativeFolderPath))
        {
            relativeFolderPath = ".";
        }

        var workspacePayload = new
        {
            folders = new[] { new { path = relativeFolderPath } }
        };

        var json = JsonSerializer.Serialize(workspacePayload, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(workspacePath, json);

        ViewModel.Explorer.LoadWorkspace(workspacePath);
        ViewModel.AddRecentProject(workspacePath);
    }

    public async Task OpenFolderWithWorkspaceDetectionAsync(string folderPath)
    {
        if (ViewModel == null || string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return;
        }

        if (ViewModel.Settings.AutoOpenDetectedWorkspaces)
        {
            var detectedWorkspace = ViewModel.Explorer.DetectWorkspaceFileInFolder(folderPath);
            if (!string.IsNullOrWhiteSpace(detectedWorkspace))
            {
                var openWorkspace = await ShowDetectedWorkspacePromptAsync(detectedWorkspace);
                if (openWorkspace)
                {
                    ViewModel.Explorer.LoadWorkspace(detectedWorkspace);
                    ViewModel.AddRecentProject(detectedWorkspace);
                    return;
                }
            }
        }

        ViewModel.Explorer.LoadFolder(folderPath);
        ViewModel.AddRecentProject(folderPath);
    }

    private async Task<bool> ShowDetectedWorkspacePromptAsync(string workspacePath)
    {
        var workspaceName = Path.GetFileName(workspacePath);

        var dialog = new Window
        {
            Title = "Notepad Pro",
            Width = 460,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            SystemDecorations = SystemDecorations.None,
            Background = Avalonia.Media.Brushes.Transparent
        };

        var shouldOpenWorkspace = false;

        var openWorkspaceBtn = new Button { Content = "Open Workspace", Width = 120, Margin = new Thickness(0, 0, 8, 0) };
        var openFolderBtn = new Button { Content = "Open Folder", Width = 100 };

        openWorkspaceBtn.Click += (_, _) => { shouldOpenWorkspace = true; dialog.Close(); };
        openFolderBtn.Click += (_, _) => { shouldOpenWorkspace = false; dialog.Close(); };

        dialog.Content = new Border
        {
            Background = Avalonia.Media.Brush.Parse("#1E1E1E"),
            BorderBrush = Avalonia.Media.Brush.Parse("#3F3F46"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(20),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"Detected workspace file '{workspaceName}'. Open it instead of just the folder?",
                        Foreground = Avalonia.Media.Brush.Parse("#D4D4D4"),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        FontSize = 13
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Children = { openWorkspaceBtn, openFolderBtn }
                    }
                }
            }
        };

        await dialog.ShowDialog(this);
        return shouldOpenWorkspace;
    }

    private void OnFileCloseEditor(object? sender, RoutedEventArgs e)
    {
        CloseActiveTabWithPrompt();
    }

    private void OnFileCloseFolder(object? sender, RoutedEventArgs e)
    {
        ViewModel?.Explorer.CloseFolder();
    }

    private void OnFileCloseWorkspace(object? sender, RoutedEventArgs e)
    {
        ViewModel?.Explorer.CloseWorkspace();
    }

    private async void OnFileSave(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ViewModel.Editor.FilePath))
        {
            await SaveAsAsync();
            return;
        }

        await ViewModel.SaveCurrentAsync();
    }

    private async void OnFileSaveAs(object? sender, RoutedEventArgs e)
    {
        await SaveAsAsync();
    }

    private void OnFileExit(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnTabCloseClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is EditorTabViewModel tab)
        {
            if (await PromptSaveIfDirtyAsync(tab))
            {
                ViewModel?.CloseTab(tab);
            }
        }
    }

    private void OnTabHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { Tag: EditorTabViewModel tab } || ViewModel == null)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            ViewModel.ActivateTab(tab);
        }
    }

    private void OnTabPinToggleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: EditorTabViewModel tab } && ViewModel != null)
        {
            ViewModel.TogglePinTab(tab);
        }
    }

    private async void OnTabDuplicateClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: EditorTabViewModel tab } && ViewModel != null)
        {
            await ViewModel.DuplicateTabAsync(tab);
        }
    }

    private void OnTabRevealInExplorerClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: EditorTabViewModel tab } && ViewModel != null)
        {
            ViewModel.RevealTabInExplorer(tab);
        }
    }

    private async void OnTabContextCloseClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: EditorTabViewModel tab } && ViewModel != null)
        {
            if (await PromptSaveIfDirtyAsync(tab))
            {
                ViewModel.CloseTab(tab);
            }
        }
    }

    private async void OnTabCloseOthersClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: EditorTabViewModel tab } || ViewModel == null)
        {
            return;
        }

        var tabsToClose = ViewModel.Tabs
            .Where(candidate => !ReferenceEquals(candidate, tab) && !candidate.IsWelcomeTab)
            .ToList();

        foreach (var target in tabsToClose)
        {
            if (!await PromptSaveIfDirtyAsync(target))
            {
                return;
            }

            ViewModel.CloseTab(target);
        }

        ViewModel.ActivateTab(tab);
    }

    private async void OnTabCloseTabsToRightClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: EditorTabViewModel tab } || ViewModel == null)
        {
            return;
        }

        var tabIndex = ViewModel.Tabs.IndexOf(tab);
        if (tabIndex < 0)
        {
            return;
        }

        var tabsToClose = ViewModel.Tabs
            .Skip(tabIndex + 1)
            .Where(candidate => !candidate.IsWelcomeTab)
            .ToList();

        foreach (var target in tabsToClose)
        {
            if (!await PromptSaveIfDirtyAsync(target))
            {
                return;
            }

            ViewModel.CloseTab(target);
        }

        ViewModel.ActivateTab(tab);
    }

    private void OnFilePageSetup(object? sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.PageSetupDialog
        {
            PageSettings = _pageSettings,
            PrinterSettings = _printerSettings,
            AllowMargins = true,
            AllowOrientation = true,
            AllowPaper = true
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            _pageSettings = dialog.PageSettings;
            _printerSettings = dialog.PrinterSettings;
            _printDocument.DefaultPageSettings = _pageSettings;
            _printDocument.PrinterSettings = _printerSettings;
        }
    }

    private void OnFilePrint(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        using var dialog = new Forms.PrintDialog
        {
            Document = _printDocument,
            UseEXDialog = true
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            _printDocument.Print();
        }
    }

    private async void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (_settingsDialog != null)
        {
            _settingsDialog.Close();
            return;
        }

        _settingsDialog = new SettingsDialog
        {
            DataContext = ViewModel?.Settings
        };

        _settingsDialog.Closed += (_, _) => _settingsDialog = null;
        await _settingsDialog.ShowDialog(this);
    }

    private void OnClearRecent(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ClearRecentFiles();
    }

    private async void OnRecentFileClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null || sender is not MenuItem menuItem)
        {
            return;
        }

        if (menuItem.Tag is RecentFileItem item)
        {
            await ViewModel.OpenFileFromPathAsync(item.Path);
        }
    }

    private void RebuildOpenRecentMenu()
    {
        var menu = this.FindControl<MenuItem>("OpenRecentMenu");
        if (menu == null || ViewModel == null) return;

        menu.Items.Clear();

        if (ViewModel.RecentFiles.Count == 0)
        {
            var empty = new MenuItem { Header = "(No recent files)", IsEnabled = false };
            menu.Items.Add(empty);
        }
        else
        {
            foreach (var recent in ViewModel.RecentFiles)
            {
                var item = new MenuItem
                {
                    Header = recent.DisplayName,
                    Tag = recent,
                };
                ToolTip.SetTip(item, recent.Tooltip);
                item.Click += OnRecentFileClick;
                menu.Items.Add(item);
            }
        }

        menu.Items.Add(new Separator());
        var clearItem = new MenuItem { Header = "Clear Recent" };
        clearItem.Click += OnClearRecent;
        menu.Items.Add(clearItem);
    }

    private async void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        await ViewModel.TriggerAutoSaveAsync(AutoSaveMode.OnFocusChange);
        await ViewModel.TriggerAutoSaveAsync(AutoSaveMode.OnWindowChange);
    }

    private async Task SaveAsAsync()
    {
        if (ViewModel == null)
        {
            return;
        }

        var path = await PickSaveFileAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await ViewModel.SaveAsAsync(path);
    }

    private async Task<IReadOnlyList<string>> PickOpenFileAsync()
    {
        var suggestedStart = await TryGetPreferredOpenStartLocationAsync();
        var options = new FilePickerOpenOptions
        {
            AllowMultiple = false,
            SuggestedStartLocation = suggestedStart,
            FileTypeFilter = BuildEditorFileTypeChoices(includeAllFiles: true)
        };

        var result = await StorageProvider.OpenFilePickerAsync(options);
        return result.Select(file => file.TryGetLocalPath()).Where(path => !string.IsNullOrWhiteSpace(path)).ToList()!;
    }

    private async Task<string?> PickSaveFileAsync()
    {
        var options = new FilePickerSaveOptions
        {
            SuggestedFileName = ViewModel?.Editor.FileName ?? "Untitled",
            FileTypeChoices = BuildEditorFileTypeChoices(includeAllFiles: true)
        };

        var result = await StorageProvider.SaveFilePickerAsync(options);
        return result?.TryGetLocalPath();
    }

    private async Task<IStorageFolder?> TryGetPreferredOpenStartLocationAsync()
    {
        var provider = StorageProvider;
        var candidateFolders = GetOpenStartLocationCandidates();

        foreach (var folderPath in candidateFolders)
        {
            var folder = await provider.TryGetFolderFromPathAsync(folderPath);
            if (folder != null)
            {
                return folder;
            }
        }

        return null;
    }

    private IEnumerable<string> GetOpenStartLocationCandidates()
    {
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddFolder(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return;
            }

            unique.Add(path);
        }

        var activeFile = ViewModel?.Editor.FilePath;
        if (!string.IsNullOrWhiteSpace(activeFile))
        {
            AddFolder(Path.GetDirectoryName(activeFile));
        }

        if (ViewModel != null)
        {
            foreach (var recent in ViewModel.RecentFiles)
            {
                AddFolder(Path.GetDirectoryName(recent.Path));
            }

            AddFolder(ViewModel.Explorer.CurrentFolderPath);
            AddFolder(Path.GetDirectoryName(ViewModel.Explorer.CurrentWorkspacePath));
        }

        AddFolder(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        return unique;
    }

    private static List<FilePickerFileType> BuildEditorFileTypeChoices(bool includeAllFiles)
    {
        var choices = new List<FilePickerFileType>
        {
            new("All Supported Text/Code Files")
            {
                Patterns = new List<string>
                {
                    "*.txt", "*.text", "*.log",
                    "*.md", "*.markdown", "*.mdown", "*.mkd",
                    "*.json", "*.jsonc", "*.json5", "*.patch", "*.recipe", "*.item", "*.object", "*.frames", "*.config", "*.modinfo",
                    "*.xml", "*.xsd", "*.xsl", "*.xslt", "*.svg", "*.resx", "*.props", "*.targets", "*.xaml", "*.axaml",
                    "*.cs", "*.csx", "*.cake",
                    "*.c", "*.h", "*.cpp", "*.cc", "*.cxx", "*.c++", "*.hh", "*.hpp", "*.hxx", "*.inl", "*.ipp", "*.tpp",
                    "*.js", "*.jsx", "*.ts", "*.tsx", "*.html", "*.htm", "*.css", "*.scss", "*.less",
                    "*.py", "*.lua", "*.ps1", "*.psm1", "*.psd1", "*.ps1xml", "*.sh", "*.bash", "*.zsh", "*.fish",
                    "*.yml", "*.yaml", "*.sql", "*.java", "*.go", "*.rs", "*.php", "*.rb", "*.swift", "*.kt", "*.kts", "*.r"
                }
            },
            new("Plain Text")
            {
                Patterns = new List<string> { "*.txt", "*.text", "*.log" }
            },
            new("Markdown")
            {
                Patterns = new List<string> { "*.md", "*.markdown", "*.mdown", "*.mkd" }
            },
            new("JSON")
            {
                Patterns = new List<string> { "*.json", "*.jsonc", "*.json5" }
            },
            new("Starbound JSON-Like")
            {
                Patterns = new List<string> { "*.patch", "*.recipe", "*.item", "*.object", "*.frames", "*.config", "*.modinfo" }
            },
            new("XML / XAML")
            {
                Patterns = new List<string> { "*.xml", "*.xsd", "*.xsl", "*.xslt", "*.svg", "*.resx", "*.props", "*.targets", "*.xaml", "*.axaml" }
            },
            new("C#")
            {
                Patterns = new List<string> { "*.cs", "*.csx", "*.cake" }
            },
            new("C / C++")
            {
                Patterns = new List<string> { "*.c", "*.h", "*.cpp", "*.cc", "*.cxx", "*.c++", "*.hh", "*.hpp", "*.hxx", "*.inl", "*.ipp", "*.tpp" }
            },
            new("Web")
            {
                Patterns = new List<string> { "*.html", "*.htm", "*.css", "*.scss", "*.less", "*.js", "*.jsx", "*.ts", "*.tsx" }
            },
            new("Lua")
            {
                Patterns = new List<string> { "*.lua" }
            },
            new("Scripts")
            {
                Patterns = new List<string> { "*.py", "*.lua", "*.ps1", "*.psm1", "*.psd1", "*.ps1xml", "*.sh", "*.bash", "*.zsh", "*.fish" }
            }
        };

        if (includeAllFiles)
        {
            choices.Add(FilePickerFileTypes.All);
        }

        return choices;
    }

    private void OnEditUndo(object? sender, RoutedEventArgs e) => TryEditorAction(tb => tb.Undo());

    private void OnEditRedo(object? sender, RoutedEventArgs e) => TryEditorAction(tb => tb.Redo());

    private void OnEditCut(object? sender, RoutedEventArgs e) => TryEditorAction(tb => tb.Cut());

    private async void OnEditCopy(object? sender, RoutedEventArgs e)
    {
        var editor = FindEditorTextEditor();
        if (editor == null)
        {
            return;
        }

        if (ViewModel?.Settings.CleanCopyEnabled != true)
        {
            editor.Copy();
            return;
        }

        var selectedText = editor.SelectedText;
        if (string.IsNullOrEmpty(selectedText))
        {
            editor.Copy();
            return;
        }

        try
        {
            if (Clipboard == null)
            {
                editor.Copy();
                return;
            }

            await Clipboard.SetTextAsync(selectedText.Trim());
        }
        catch
        {
            editor.Copy();
        }
    }

    private void OnEditPaste(object? sender, RoutedEventArgs e) => TryEditorAction(tb => tb.Paste());

    private void OnEditDelete(object? sender, RoutedEventArgs e)
    {
        TryEditorAction(tb =>
        {
            var selectionStart = tb.SelectionStart;
            var selectionEnd = selectionStart + tb.SelectionLength;
            if (selectionStart != selectionEnd)
            {
                var text = tb.Text ?? string.Empty;
                tb.Text = text.Remove(selectionStart, selectionEnd - selectionStart);
                tb.CaretOffset = selectionStart;
                return;
            }

            if (tb.CaretOffset < (tb.Text?.Length ?? 0))
            {
                var text = tb.Text ?? string.Empty;
                tb.Text = text.Remove(tb.CaretOffset, 1);
            }
        });
    }

    private void OnEditIndent(object? sender, RoutedEventArgs e)
    {
        TryEditorAction(tb => ApplyIndentation(tb, indent: true));
    }

    private void OnEditOutdent(object? sender, RoutedEventArgs e)
    {
        TryEditorAction(tb => ApplyIndentation(tb, indent: false));
    }

    private void OnEditConvertTabsToSpaces(object? sender, RoutedEventArgs e)
    {
        TryEditorAction(tb => ConvertTabsToSpaces(tb));
    }

    private void OnEditConvertSpacesToTabs(object? sender, RoutedEventArgs e)
    {
        TryEditorAction(tb => ConvertSpacesToTabs(tb));
    }

    private async void OnEditFind(object? sender, RoutedEventArgs e)
    {
        await ShowFindDialogAsync(includeReplace: false);
    }

    private void OnEditFindNext(object? sender, RoutedEventArgs e)
    {
        FindNext();
    }

    private async void OnEditReplace(object? sender, RoutedEventArgs e)
    {
        await ShowFindDialogAsync(includeReplace: true);
    }

    private async void OnEditGoTo(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        ViewModel.Editor.EnsureUnfolded();

        var line = await GoToLineDialog.ShowAsync(this, ViewModel.Editor.CaretLine);
        if (line == null)
        {
            return;
        }

        var text = ViewModel.Editor.Text ?? string.Empty;
        var index = TextSearchService.GetIndexFromLine(text, line.Value);
        ViewModel.Editor.RequestSelection(index, 0);
    }

    private void OnEditToggleBookmark(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null || ViewModel.IsWelcomeTabSelected)
        {
            return;
        }

        ViewModel.ToggleBookmarkAtCaret();
        RefreshActiveEditorBookmarks();
    }

    private async void OnEditNextBookmark(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        var bookmark = ViewModel.GetNextBookmark();
        if (bookmark == null)
        {
            return;
        }

        await ViewModel.NavigateToBookmarkAsync(bookmark);
        RefreshActiveEditorBookmarks();
    }

    private async void OnEditPreviousBookmark(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        var bookmark = ViewModel.GetPreviousBookmark();
        if (bookmark == null)
        {
            return;
        }

        await ViewModel.NavigateToBookmarkAsync(bookmark);
        RefreshActiveEditorBookmarks();
    }

    private async void OnEditListBookmarks(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        var bookmarks = ViewModel.GetBookmarksForCurrentDocument();
        if (bookmarks.Count == 0)
        {
            return;
        }

        await ShowQuickPickAsync(
            title: "Bookmarks",
            watermark: "Jump to a bookmark in the current file",
            resolveItems: query => bookmarks
                .Select(bookmark => new QuickPickEntry(
                    $"Line {bookmark.LineNumber}",
                    () => ViewModel.NavigateToBookmarkAsync(bookmark),
                    string.IsNullOrWhiteSpace(bookmark.Text)
                        ? (bookmark.IsGlobal ? "Global bookmark" : "Scoped bookmark")
                        : bookmark.IsGlobal
                            ? $"{bookmark.Text} · global"
                            : bookmark.Text,
                    $"bookmark line {bookmark.LineNumber} {bookmark.Text}"))
                .Where(item => MatchesQuickPickQuery(item, query))
                .ToList());
    }

    private void OnEditToggleGlobalBookmark(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        ViewModel.ToggleGlobalBookmarkAtCaret();
        RefreshActiveEditorBookmarks();
    }

    private async void OnEditImportBookmarks(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Import Bookmarks",
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Notepad Pro Bookmarks")
                {
                    Patterns = new List<string> { "*.json", "*.notepadpro-bookmarks" }
                }
            }
        });

        var path = result.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var payload = JsonSerializer.Deserialize<BookmarkExchangeData>(json);
            if (payload?.Bookmarks == null || payload.Bookmarks.Count == 0)
            {
                return;
            }

            var importMode = await ShowBookmarkImportModePromptAsync();
            if (importMode == BookmarkImportMode.Cancel)
            {
                return;
            }

            var filteredBookmarks = payload.Bookmarks;
            var outOfScopeCount = CountOutOfScopeBookmarks(filteredBookmarks);
            if (outOfScopeCount > 0)
            {
                var outOfScopeChoice = await ShowOutOfScopeBookmarkPromptAsync(outOfScopeCount);
                if (outOfScopeChoice == OutOfScopeBookmarkChoice.Cancel)
                {
                    return;
                }

                if (outOfScopeChoice == OutOfScopeBookmarkChoice.InScopeOnly)
                {
                    filteredBookmarks = filteredBookmarks
                        .Where(bookmark => IsBookmarkInCurrentScope(bookmark.FilePath))
                        .ToList();
                }
            }

            if (filteredBookmarks.Count == 0)
            {
                return;
            }

            if (ViewModel.ImportBookmarksIntoCurrentScope(filteredBookmarks, replaceCurrentScope: importMode == BookmarkImportMode.Replace) > 0)
            {
                RefreshActiveEditorBookmarks();
            }
        }
        catch
        {
        }
    }

    private async void OnEditExportBookmarks(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        var exportTarget = await ShowBookmarkExportTargetPromptAsync();
        if (exportTarget == BookmarkExportTarget.Cancel)
        {
            return;
        }

        var bookmarks = exportTarget == BookmarkExportTarget.CurrentFile
            ? ViewModel.GetCurrentDocumentBookmarkData()
            : ViewModel.GetCurrentBookmarkScopeData();
        if (bookmarks.Count == 0)
        {
            return;
        }

        var exportName = exportTarget == BookmarkExportTarget.CurrentFile
            ? Path.GetFileNameWithoutExtension(ViewModel.Editor.FileName)
            : ViewModel.GetCurrentBookmarkScopeDisplayName();
        var suggestedName = $"{SanitizeFileName(exportName)}.notepadpro-bookmarks.json";
        var result = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Bookmarks",
            SuggestedFileName = suggestedName,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("Notepad Pro Bookmarks")
                {
                    Patterns = new List<string> { "*.notepadpro-bookmarks.json", "*.json" }
                }
            }
        });

        var path = result?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            path += ".json";
        }

        var payload = new BookmarkExchangeData
        {
            ScopeName = ViewModel.GetCurrentBookmarkScopeDisplayName(),
            ScopePath = GetCurrentBookmarkScopePath(),
            ExportTarget = exportTarget == BookmarkExportTarget.CurrentFile ? "current-file" : "current-scope",
            Bookmarks = bookmarks
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(path, json);
    }

    private async Task<BookmarkImportMode> ShowBookmarkImportModePromptAsync()
    {
        var dialog = new Window
        {
            Title = "Import Bookmarks",
            Width = 460,
            Height = 190,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            SystemDecorations = SystemDecorations.None,
            Background = Avalonia.Media.Brushes.Transparent
        };

        var result = BookmarkImportMode.Cancel;
        var mergeButton = new Button { Content = "Merge", Width = 90, Margin = new Thickness(0, 0, 8, 0) };
        var replaceButton = new Button { Content = "Replace", Width = 90, Margin = new Thickness(0, 0, 8, 0) };
        var cancelButton = new Button { Content = "Cancel", Width = 90 };

        mergeButton.Click += (_, _) => { result = BookmarkImportMode.Merge; dialog.Close(); };
        replaceButton.Click += (_, _) => { result = BookmarkImportMode.Replace; dialog.Close(); };
        cancelButton.Click += (_, _) => dialog.Close();

        dialog.Content = BuildDecisionDialogContent(
            "Import bookmarks into the current workspace or folder.",
            "Merge keeps current bookmarks and resolves duplicates by newest timestamp. Replace clears the current scope before importing.",
            mergeButton,
            replaceButton,
            cancelButton);

        await dialog.ShowDialog(this);
        return result;
    }

    private async Task<OutOfScopeBookmarkChoice> ShowOutOfScopeBookmarkPromptAsync(int outOfScopeCount)
    {
        var dialog = new Window
        {
            Title = "Import Bookmarks",
            Width = 500,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            SystemDecorations = SystemDecorations.None,
            Background = Avalonia.Media.Brushes.Transparent
        };

        var result = OutOfScopeBookmarkChoice.Cancel;
        var importAllButton = new Button { Content = "Import All", Width = 100, Margin = new Thickness(0, 0, 8, 0) };
        var inScopeOnlyButton = new Button { Content = "In-Scope Only", Width = 110, Margin = new Thickness(0, 0, 8, 0) };
        var cancelButton = new Button { Content = "Cancel", Width = 90 };

        importAllButton.Click += (_, _) => { result = OutOfScopeBookmarkChoice.ImportAll; dialog.Close(); };
        inScopeOnlyButton.Click += (_, _) => { result = OutOfScopeBookmarkChoice.InScopeOnly; dialog.Close(); };
        cancelButton.Click += (_, _) => { result = OutOfScopeBookmarkChoice.Cancel; dialog.Close(); };

        dialog.Content = BuildDecisionDialogContent(
            $"{outOfScopeCount} bookmark(s) point outside the current workspace or folder.",
            "Choose whether to import those paths too, or limit the import to bookmarks inside the current scope.",
            importAllButton,
            inScopeOnlyButton,
            cancelButton);

        await dialog.ShowDialog(this);
        return result;
    }

    private async Task<BookmarkExportTarget> ShowBookmarkExportTargetPromptAsync()
    {
        var dialog = new Window
        {
            Title = "Export Bookmarks",
            Width = 460,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            SystemDecorations = SystemDecorations.None,
            Background = Avalonia.Media.Brushes.Transparent
        };

        var result = BookmarkExportTarget.Cancel;
        var fileButton = new Button { Content = "Current File", Width = 110, Margin = new Thickness(0, 0, 8, 0) };
        var scopeButton = new Button { Content = "Current Scope", Width = 110, Margin = new Thickness(0, 0, 8, 0) };
        var cancelButton = new Button { Content = "Cancel", Width = 90 };

        fileButton.Click += (_, _) => { result = BookmarkExportTarget.CurrentFile; dialog.Close(); };
        scopeButton.Click += (_, _) => { result = BookmarkExportTarget.CurrentScope; dialog.Close(); };
        cancelButton.Click += (_, _) => dialog.Close();

        dialog.Content = BuildDecisionDialogContent(
            "Choose what to export.",
            "Current File exports bookmarks only for the active document. Current Scope exports every bookmark stored for the active workspace or folder.",
            fileButton,
            scopeButton,
            cancelButton);

        await dialog.ShowDialog(this);
        return result;
    }

    private static Border BuildDecisionDialogContent(string title, string body, params Button[] buttons)
    {
        return new Border
        {
            Background = Avalonia.Media.Brush.Parse("#1E1E1E"),
            BorderBrush = Avalonia.Media.Brush.Parse("#3F3F46"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(20),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        Foreground = Avalonia.Media.Brush.Parse("#D4D4D4"),
                        FontSize = 14,
                        FontWeight = Avalonia.Media.FontWeight.SemiBold,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = body,
                        Foreground = Avalonia.Media.Brush.Parse("#D4D4D4"),
                        FontSize = 13,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Children = { buttons[0], buttons[1], buttons[2] }
                    }
                }
            }
        };
    }

    private int CountOutOfScopeBookmarks(IEnumerable<BookmarkItemData> bookmarks)
    {
        return bookmarks.Count(bookmark => !IsBookmarkInCurrentScope(bookmark.FilePath));
    }

    private bool IsBookmarkInCurrentScope(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var roots = GetBookmarkScopeRoots();
        if (roots.Count == 0)
        {
            return true;
        }

        return roots.Any(root => IsPathWithinRoot(path, root));
    }

    private List<string> GetBookmarkScopeRoots()
    {
        var roots = new List<string>();
        if (ViewModel == null)
        {
            return roots;
        }

        var workspacePath = ViewModel.GetCurrentWorkspacePathData();
        if (!string.IsNullOrWhiteSpace(workspacePath))
        {
            roots.AddRange(ParseWorkspaceFolders(workspacePath));
        }

        var folderPath = ViewModel.GetCurrentFolderPathData();
        if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
        {
            roots.Add(folderPath);
        }

        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsPathWithinRoot(string path, string root)
    {
        try
        {
            var relative = Path.GetRelativePath(root, path);
            return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
        }
        catch
        {
            return false;
        }
    }

    private void OnEditClearBookmarksInFile(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        if (ViewModel.ClearBookmarksForCurrentDocument() > 0)
        {
            RefreshActiveEditorBookmarks();
        }
    }

    private void OnEditClearAllBookmarks(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        if (ViewModel.ClearAllBookmarks() > 0)
        {
            RefreshActiveEditorBookmarks();
        }
    }

    private void OnEditSelectAll(object? sender, RoutedEventArgs e) => TryEditorAction(tb => tb.SelectAll());

    private void OnEditTimeDate(object? sender, RoutedEventArgs e)
    {
        TryEditorAction(tb =>
        {
            var insert = DateTime.Now.ToString("g");
            var start = tb.SelectionStart;
            var end = start + tb.SelectionLength;
            var text = tb.Text ?? string.Empty;
            tb.Text = text.Remove(start, end - start).Insert(start, insert);
            tb.CaretOffset = start + insert.Length;
        });
    }

    private void OnViewZoomIn(object? sender, RoutedEventArgs e) => ViewModel?.ZoomIn();

    private void OnViewZoomOut(object? sender, RoutedEventArgs e) => ViewModel?.ZoomOut();

    private void OnViewZoomReset(object? sender, RoutedEventArgs e) => ViewModel?.ResetZoom();

    private void OnZoomPresetClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null || sender is not Button button)
        {
            return;
        }

        if (button.Tag is string text && int.TryParse(text, out var percent))
        {
            ViewModel.SetZoomFromPercent(percent);
            return;
        }

        if (button.Tag is int value)
        {
            ViewModel.SetZoomFromPercent(value);
        }
    }

    private void OnViewFoldAll(object? sender, RoutedEventArgs e)
    {
        ViewModel?.Editor.FoldAll();
    }

    private void OnViewUnfoldAll(object? sender, RoutedEventArgs e)
    {
        ViewModel?.Editor.UnfoldAll();
    }

    private void OnViewToggleExplorer(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ToggleExplorer();
    }

    private void OnViewToggleSearch(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ToggleSearch();
    }

    private void OnViewToggleBookmarks(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ToggleBookmarks();
    }

    private void OnViewActivityBarLeft(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        ViewModel.Settings.IsActivityBarVisible = true;
        ViewModel.Settings.ActivityBarPosition = "Left";
    }

    private void OnViewActivityBarRight(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        ViewModel.Settings.IsActivityBarVisible = true;
        ViewModel.Settings.ActivityBarPosition = "Right";
    }

    private void OnViewActivityBarHidden(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null) ViewModel.Settings.IsActivityBarVisible = false;
    }

    public async Task CloseTabWithPromptAsync(EditorTabViewModel tab)
    {
        if (ViewModel == null)
        {
            return;
        }

        if (!ViewModel.Tabs.Contains(tab))
        {
            return;
        }

        if (await PromptSaveIfDirtyAsync(tab))
        {
            ViewModel.CloseTab(tab);
        }
    }

    private void OnViewPrimaryPanelLeft(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        ViewModel.Settings.PrimaryPanelPosition = "Left";
        UpdateToolPanelColumn();
    }

    private void OnViewPrimaryPanelRight(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        ViewModel.Settings.PrimaryPanelPosition = "Right";
        UpdateToolPanelColumn();
    }

    private async void OnHelpAbout(object? sender, RoutedEventArgs e)
    {
        var dialog = new AboutDialog();
        await dialog.ShowDialog(this);
    }

    private void OnMarkdownTogglePreview(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ToggleMarkdownPreview();
    }

    private void OnMarkdownBold(object? sender, RoutedEventArgs e)
    {
        if (TrySendRenderedMarkdownCommand("bold"))
        {
            return;
        }

        ApplyMarkdownWrap("**", "**", "bold text");
    }

    private void OnMarkdownItalic(object? sender, RoutedEventArgs e)
    {
        if (TrySendRenderedMarkdownCommand("italic"))
        {
            return;
        }

        ApplyMarkdownWrap("*", "*", "italic text");
    }

    private void OnMarkdownHeading(object? sender, RoutedEventArgs e)
    {
        var decrement = _markdownHeadingDecrementRequested;
        _markdownHeadingDecrementRequested = false;

        if (TrySendRenderedMarkdownCommand("heading", new { decrement }))
        {
            return;
        }

        ApplyMarkdownHeadingCycle(decrement);
    }

    private void OnHeadingButtonPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _markdownHeadingDecrementRequested = false;
        _markdownNumberedToBulletedRequested = false;
        _markdownBulletedToNumberedRequested = false;

        if (e.Source is not Visual source)
        {
            return;
        }

        var current = source;
        while (current != null)
        {
            if (current is Button button)
            {
                if (IsHeadingToolbarButton(button))
                {
                    _markdownHeadingDecrementRequested = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                }
                else if (IsNumberedToolbarButton(button))
                {
                    _markdownNumberedToBulletedRequested = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                }
                else if (IsBulletedToolbarButton(button))
                {
                    _markdownBulletedToNumberedRequested = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                }

                return;
            }

            current = current.GetVisualParent();
        }
    }

    private static bool IsHeadingToolbarButton(Button button)
    {
        if (button.Name is "PinnedMdHeadingButton" or "PinnedMdOverflowHeading")
        {
            return true;
        }

        if (!button.Classes.Contains("MarkdownToolbarButton"))
        {
            return false;
        }

        var tip = button.GetValue(ToolTip.TipProperty) as string;
        return string.Equals(tip, "Heading", StringComparison.Ordinal);
    }

    private static bool IsNumberedToolbarButton(Button button)
    {
        if (button.Name is "PinnedMdNumberedListButton" or "PinnedMdOverflowNumbered")
        {
            return true;
        }

        if (!button.Classes.Contains("MarkdownToolbarButton"))
        {
            return false;
        }

        var tip = button.GetValue(ToolTip.TipProperty) as string;
        return string.Equals(tip, "Numbered List", StringComparison.Ordinal);
    }

    private static bool IsBulletedToolbarButton(Button button)
    {
        if (button.Name is "PinnedMdBulletedListButton" or "PinnedMdOverflowBulleted")
        {
            return true;
        }

        if (!button.Classes.Contains("MarkdownToolbarButton"))
        {
            return false;
        }

        var tip = button.GetValue(ToolTip.TipProperty) as string;
        return string.Equals(tip, "Bulleted List", StringComparison.Ordinal);
    }

    private void OnMarkdownInlineCode(object? sender, RoutedEventArgs e)
    {
        if (TrySendRenderedMarkdownCommand("inline-code"))
        {
            return;
        }

        ApplyMarkdownWrap("`", "`", "code");
    }

    private void OnMarkdownBulletedList(object? sender, RoutedEventArgs e)
    {
        if (TrySendRenderedMarkdownCommand("bulleted-list", new { convertToNumbered = _markdownBulletedToNumberedRequested }))
        {
            _markdownBulletedToNumberedRequested = false;
            return;
        }

        if (_markdownBulletedToNumberedRequested)
        {
            _markdownBulletedToNumberedRequested = false;
            ApplyMarkdownBulletedToNumbered();
            return;
        }

        ApplyMarkdownLinePrefix("- ");
    }

    private void OnMarkdownNumberedList(object? sender, RoutedEventArgs e)
    {
        var convertToBullets = _markdownNumberedToBulletedRequested;
        _markdownNumberedToBulletedRequested = false;

        if (TrySendRenderedMarkdownCommand("numbered-list", new { convertToBullets }))
        {
            return;
        }

        ApplyMarkdownNumberedList(convertToBullets);
    }

    private void OnMarkdownLink(object? sender, RoutedEventArgs e)
    {
        if (TrySendRenderedMarkdownCommand("link"))
        {
            return;
        }

        ApplyMarkdownLink();
    }

    private bool TrySendRenderedMarkdownCommand(string command, object? args = null)
    {
        if (_webBridge is null || ViewModel is null)
        {
            return false;
        }

        if (!ViewModel.IsRenderedViewToggleAvailable || !ViewModel.IsMarkdownPreviewVisible)
        {
            return false;
        }

        _webBridge.SendMarkdownCommand(command, args);
        return true;
    }

    private void OnBeginPrint(object? sender, PrintEventArgs e)
    {
        var text = ViewModel?.Editor.Text ?? string.Empty;
        _printLines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal).Split('\n');
        _printLineIndex = 0;
    }

    private void OnPrintPage(object? sender, PrintPageEventArgs e)
    {
        if (e.Graphics == null)
        {
            e.HasMorePages = false;
            return;
        }

        var fontSize = (float)(ViewModel?.Settings.EditorFontSize ?? 12);
        using var font = new System.Drawing.Font("Consolas", fontSize);
        var brush = System.Drawing.Brushes.Black;
        var lineHeight = font.GetHeight(e.Graphics);
        var y = (float)e.MarginBounds.Top;
        var maxY = (float)e.MarginBounds.Bottom;
        var format = new System.Drawing.StringFormat { FormatFlags = System.Drawing.StringFormatFlags.NoWrap };

        while (_printLineIndex < _printLines.Length && y + lineHeight <= maxY)
        {
            var line = _printLines[_printLineIndex];
            e.Graphics.DrawString(line, font, brush, new System.Drawing.RectangleF(e.MarginBounds.Left, y, e.MarginBounds.Width, lineHeight), format);
            y += lineHeight;
            _printLineIndex++;
        }

        e.HasMorePages = _printLineIndex < _printLines.Length;
    }

    private void TryEditorAction(Action<TextEditor> action)
    {
        var editor = FindEditorTextEditor();
        if (editor != null)
        {
            action(editor);
        }
    }

    private TextEditor? FindEditorTextEditor()
    {
        return this.GetVisualDescendants().OfType<TextEditor>()
            .FirstOrDefault(editor => editor.Name == "EditorTextBox" && editor.IsEffectivelyVisible);
    }

    private async Task ShowFindDialogAsync(bool includeReplace)
    {
        if (ViewModel == null)
        {
            return;
        }

        var result = await FindReplaceDialog.ShowAsync(
            this,
            includeReplace,
            _lastFindQuery,
            _lastReplaceText,
            _lastMatchCase,
            _lastWholeWord);

        if (result == null || result.Action == FindReplaceAction.Cancel)
        {
            return;
        }

        ViewModel.Editor.EnsureUnfolded();

        _lastFindQuery = result.Query;
        _lastReplaceText = result.Replacement;
        _lastMatchCase = result.MatchCase;
        _lastWholeWord = result.WholeWord;

        if (result.Action == FindReplaceAction.FindNext)
        {
            FindNext();
            return;
        }

        if (result.Action == FindReplaceAction.ReplaceAll)
        {
            ReplaceAll();
            return;
        }

        ReplaceNext();
    }

    private void FindNext()
    {
        if (ViewModel == null || string.IsNullOrWhiteSpace(_lastFindQuery))
        {
            return;
        }

        ViewModel.Editor.EnsureUnfolded();

        var text = ViewModel.Editor.Text ?? string.Empty;
        var start = ViewModel.Editor.CaretIndex;
        var index = TextSearchService.FindNext(text, _lastFindQuery, start, _lastMatchCase, _lastWholeWord);
        if (index < 0 && start > 0)
        {
            index = TextSearchService.FindNext(text, _lastFindQuery, 0, _lastMatchCase, _lastWholeWord);
        }

        if (index >= 0)
        {
            ViewModel.Editor.RequestSelection(index, _lastFindQuery.Length);
        }
    }

    private void ReplaceNext()
    {
        if (ViewModel == null || string.IsNullOrWhiteSpace(_lastFindQuery))
        {
            return;
        }

        ViewModel.Editor.EnsureUnfolded();

        var text = ViewModel.Editor.Text ?? string.Empty;
        var start = ViewModel.Editor.CaretIndex;
        var index = TextSearchService.FindNext(text, _lastFindQuery, start, _lastMatchCase, _lastWholeWord);
        if (index < 0)
        {
            return;
        }

        ViewModel.Editor.Text = text.Remove(index, _lastFindQuery.Length).Insert(index, _lastReplaceText);
        ViewModel.Editor.RequestSelection(index, _lastReplaceText.Length);
    }

    private void ReplaceAll()
    {
        if (ViewModel == null || string.IsNullOrWhiteSpace(_lastFindQuery))
        {
            return;
        }

        ViewModel.Editor.EnsureUnfolded();

        var text = ViewModel.Editor.Text ?? string.Empty;
        var count = TextSearchService.ReplaceAll(
            ref text,
            _lastFindQuery,
            _lastReplaceText,
            _lastMatchCase,
            _lastWholeWord);

        if (count > 0)
        {
            ViewModel.Editor.Text = text;
        }
    }

    private void AdjustZoom(int delta)
    {
        if (delta > 0)
        {
            ViewModel?.ZoomIn();
        }
        else if (delta < 0)
        {
            ViewModel?.ZoomOut();
        }
    }

    private void SetZoom(double size)
    {
        if (ViewModel == null)
        {
            return;
        }

        var percent = (int)Math.Round(size / 11d * 100d);
        ViewModel.SetZoomFromPercent(percent);
    }

    private void ApplyMarkdownWrap(string prefix, string suffix, string placeholder)
    {
        if (ViewModel?.IsActiveEditorMarkdown != true)
        {
            return;
        }

        TryEditorAction(editor =>
        {
            var text = editor.Text ?? string.Empty;
            var selectionStart = editor.SelectionStart;
            var selectionLength = editor.SelectionLength;
            var selectionEnd = selectionStart + selectionLength;

            if (selectionLength > 0)
            {
                if (HasWrapOutsideSelection(text, selectionStart, selectionEnd, prefix, suffix))
                {
                    var unwrapStart = selectionStart - prefix.Length;
                    var wrappedLength = selectionLength + prefix.Length + suffix.Length;
                    var unwrappedText = text.Substring(selectionStart, selectionLength);
                    var updated = text.Remove(unwrapStart, wrappedLength).Insert(unwrapStart, unwrappedText);
                    editor.Text = updated;
                    editor.Select(unwrapStart, unwrappedText.Length);
                    editor.CaretOffset = unwrapStart + unwrappedText.Length;
                    return;
                }

                var selected = text.Substring(selectionStart, selectionLength);
                if (selected.StartsWith(prefix, StringComparison.Ordinal) &&
                    selected.EndsWith(suffix, StringComparison.Ordinal) &&
                    selected.Length >= prefix.Length + suffix.Length)
                {
                    var inner = selected.Substring(prefix.Length, selected.Length - prefix.Length - suffix.Length);
                    var updated = text.Remove(selectionStart, selectionLength).Insert(selectionStart, inner);
                    editor.Text = updated;
                    editor.Select(selectionStart, inner.Length);
                    editor.CaretOffset = selectionStart + inner.Length;
                    return;
                }

                var wrapped = prefix + selected + suffix;
                var wrappedUpdated = text.Remove(selectionStart, selectionLength).Insert(selectionStart, wrapped);
                editor.Text = wrappedUpdated;
                var contentStart = selectionStart + prefix.Length;
                editor.Select(contentStart, selected.Length);
                editor.CaretOffset = contentStart + selected.Length;
                return;
            }

            var insertion = prefix + placeholder + suffix;
            var inserted = text.Insert(selectionStart, insertion);
            editor.Text = inserted;
            var placeholderStart = selectionStart + prefix.Length;
            editor.Select(placeholderStart, placeholder.Length);
            editor.CaretOffset = placeholderStart + placeholder.Length;
        });
    }

    private static bool HasWrapOutsideSelection(string text, int selectionStart, int selectionEnd, string prefix, string suffix)
    {
        if (selectionStart < prefix.Length || selectionEnd + suffix.Length > text.Length)
        {
            return false;
        }

        var hasPrefix = string.Compare(text, selectionStart - prefix.Length, prefix, 0, prefix.Length, StringComparison.Ordinal) == 0;
        var hasSuffix = string.Compare(text, selectionEnd, suffix, 0, suffix.Length, StringComparison.Ordinal) == 0;
        return hasPrefix && hasSuffix;
    }

    private void ApplyMarkdownLinePrefix(string prefix)
    {
        if (ViewModel?.IsActiveEditorMarkdown != true)
        {
            return;
        }

        TryEditorAction(editor =>
        {
            var text = editor.Text ?? string.Empty;
            var start = editor.SelectionStart;
            var end = start + editor.SelectionLength;

            var lineStart = GetLineStart(text, start);
            var lineEnd = GetLineEnd(text, end);

            var segment = text.Substring(lineStart, lineEnd - lineStart);
            var lines = segment.Split('\n');
            var allPrefixed = true;
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0)
                {
                    continue;
                }

                if (!lines[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    allPrefixed = false;
                    break;
                }
            }

            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0)
                {
                    continue;
                }

                lines[i] = allPrefixed && lines[i].StartsWith(prefix, StringComparison.Ordinal)
                    ? lines[i].Substring(prefix.Length)
                    : prefix + lines[i];
            }

            var replacement = string.Join("\n", lines);
            editor.Text = text.Remove(lineStart, segment.Length).Insert(lineStart, replacement);
            editor.Select(lineStart, replacement.Length);
            editor.CaretOffset = lineStart + replacement.Length;
        });
    }

    private void ApplyMarkdownHeadingCycle(bool decrement)
    {
        if (ViewModel?.IsActiveEditorMarkdown != true)
        {
            return;
        }

        TryEditorAction(editor =>
        {
            var text = editor.Text ?? string.Empty;
            var start = editor.SelectionStart;
            var end = start + editor.SelectionLength;

            var lineStart = GetLineStart(text, start);
            var lineEnd = GetLineEnd(text, end);

            var segment = text.Substring(lineStart, lineEnd - lineStart);
            var lines = segment.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                lines[i] = CycleMarkdownHeadingLine(lines[i], decrement);
            }

            var replacement = string.Join("\n", lines);
            editor.Text = text.Remove(lineStart, segment.Length).Insert(lineStart, replacement);
            editor.Select(lineStart, replacement.Length);
            editor.CaretOffset = lineStart + replacement.Length;
        });
    }

    private static string CycleMarkdownHeadingLine(string line, bool decrement)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return line;
        }

        var match = Regex.Match(line, @"^(?<indent>\s*)(?<hashes>#{1,6})\s+(?<content>.*)$");
        if (!match.Success)
        {
            if (decrement)
            {
                return line;
            }

            var indentMatch = Regex.Match(line, @"^(?<indent>\s*)(?<content>.*)$");
            var indent = indentMatch.Groups["indent"].Value;
            var content = indentMatch.Groups["content"].Value;
            return string.IsNullOrWhiteSpace(content) ? line : $"{indent}# {content}";
        }

        var existingHashes = match.Groups["hashes"].Value;
        var contentText = match.Groups["content"].Value;
        var leadingIndent = match.Groups["indent"].Value;

        if (decrement)
        {
            if (existingHashes.Length <= 1)
            {
                return $"{leadingIndent}{contentText}";
            }

            var previousHashes = new string('#', existingHashes.Length - 1);
            return $"{leadingIndent}{previousHashes} {contentText}";
        }

        if (existingHashes.Length >= 6)
        {
            return $"{leadingIndent}{contentText}";
        }

        var nextHashes = new string('#', existingHashes.Length + 1);
        return $"{leadingIndent}{nextHashes} {contentText}";
    }

    private void ApplyMarkdownNumberedList(bool convertToBullets)
    {
        if (ViewModel?.IsActiveEditorMarkdown != true)
        {
            return;
        }

        TryEditorAction(editor =>
        {
            var text = editor.Text ?? string.Empty;
            var start = editor.SelectionStart;
            var end = start + editor.SelectionLength;

            var lineStart = GetLineStart(text, start);
            var lineEnd = GetLineEnd(text, end);

            var segment = text.Substring(lineStart, lineEnd - lineStart);
            var lines = segment.Split('\n');

            if (convertToBullets)
            {
                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Length == 0)
                    {
                        continue;
                    }

                    var withoutNumber = Regex.Replace(lines[i], @"^\d+\.\s", string.Empty);
                    lines[i] = withoutNumber.StartsWith("- ", StringComparison.Ordinal)
                        ? withoutNumber
                        : $"- {withoutNumber}";
                }

                var bulletedReplacement = string.Join("\n", lines);
                editor.Text = text.Remove(lineStart, segment.Length).Insert(lineStart, bulletedReplacement);
                editor.Select(lineStart, bulletedReplacement.Length);
                editor.CaretOffset = lineStart + bulletedReplacement.Length;
                return;
            }

            var allNumbered = true;
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0)
                {
                    continue;
                }

                if (!Regex.IsMatch(lines[i], @"^\d+\.\s"))
                {
                    allNumbered = false;
                    break;
                }
            }

            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0)
                {
                    continue;
                }

                lines[i] = allNumbered
                    ? Regex.Replace(lines[i], @"^\d+\.\s", string.Empty)
                    : $"{i + 1}. {lines[i]}";
            }

            var replacement = string.Join("\n", lines);
            editor.Text = text.Remove(lineStart, segment.Length).Insert(lineStart, replacement);
            editor.Select(lineStart, replacement.Length);
            editor.CaretOffset = lineStart + replacement.Length;
        });
    }

    private void ApplyMarkdownBulletedToNumbered()
    {
        if (ViewModel?.IsActiveEditorMarkdown != true)
        {
            return;
        }

        TryEditorAction(editor =>
        {
            var text = editor.Text ?? string.Empty;
            var start = editor.SelectionStart;
            var end = start + editor.SelectionLength;

            var lineStart = GetLineStart(text, start);
            var lineEnd = GetLineEnd(text, end);

            var segment = text.Substring(lineStart, lineEnd - lineStart);
            var lines = segment.Split('\n');

            var number = 1;
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0)
                {
                    continue;
                }

                var withoutBullet = Regex.Replace(lines[i], @"^[-*+]\s", string.Empty);
                withoutBullet = Regex.Replace(withoutBullet, @"^\d+\.\s", string.Empty);
                lines[i] = $"{number}. {withoutBullet}";
                number++;
            }

            var replacement = string.Join("\n", lines);
            editor.Text = text.Remove(lineStart, segment.Length).Insert(lineStart, replacement);
            editor.Select(lineStart, replacement.Length);
            editor.CaretOffset = lineStart + replacement.Length;
        });
    }

    private void ApplyMarkdownLink()
    {
        if (ViewModel?.IsActiveEditorMarkdown != true)
        {
            return;
        }

        TryEditorAction(editor =>
        {
            var text = editor.Text ?? string.Empty;
            var selectionStart = editor.SelectionStart;
            var selectionEnd = selectionStart + editor.SelectionLength;

            if (selectionEnd > selectionStart)
            {
                var selectedText = text.Substring(selectionStart, selectionEnd - selectionStart);
                var fullLinkMatch = Regex.Match(selectedText, @"^\[(?<label>.*)\]\((?<url>.*)\)$");
                if (fullLinkMatch.Success)
                {
                    var label = fullLinkMatch.Groups["label"].Value;
                    var unwrappedLink = text.Remove(selectionStart, selectionEnd - selectionStart).Insert(selectionStart, label);
                    editor.Text = unwrappedLink;
                    editor.Select(selectionStart, label.Length);
                    editor.CaretOffset = selectionStart + label.Length;
                    return;
                }

                if (TryGetLinkBoundsOutsideSelection(text, selectionStart, selectionEnd, out var linkStart, out var linkEndExclusive))
                {
                    var innerLabel = text.Substring(selectionStart, selectionEnd - selectionStart);
                    var unwrappedOuterLink = text.Remove(linkStart, linkEndExclusive - linkStart).Insert(linkStart, innerLabel);
                    editor.Text = unwrappedOuterLink;
                    editor.Select(linkStart, innerLabel.Length);
                    editor.CaretOffset = linkStart + innerLabel.Length;
                    return;
                }
            }

            var selected = selectionEnd > selectionStart
                ? text.Substring(selectionStart, selectionEnd - selectionStart)
                : "link text";

            var replacement = $"[{selected}](https://)";
            var updated = text.Remove(selectionStart, selectionEnd - selectionStart).Insert(selectionStart, replacement);

            editor.Text = updated;
            var urlStart = selectionStart + replacement.IndexOf("https://", StringComparison.Ordinal);
            editor.Select(urlStart, "https://".Length);
            editor.CaretOffset = urlStart + "https://".Length;
        });
    }

    private static bool TryGetLinkBoundsOutsideSelection(string text, int selectionStart, int selectionEnd, out int linkStart, out int linkEndExclusive)
    {
        linkStart = -1;
        linkEndExclusive = -1;

        if (selectionStart < 1 || selectionEnd > text.Length)
        {
            return false;
        }

        if (text[selectionStart - 1] != '[')
        {
            return false;
        }

        if (selectionEnd + 1 >= text.Length || text[selectionEnd] != ']' || text[selectionEnd + 1] != '(')
        {
            return false;
        }

        var closingParen = text.IndexOf(')', selectionEnd + 2);
        if (closingParen < 0)
        {
            return false;
        }

        linkStart = selectionStart - 1;
        linkEndExclusive = closingParen + 1;
        return true;
    }

    private void ApplyIndentation(TextEditor editor, bool indent)
    {
        var text = editor.Text ?? string.Empty;
        var selectionStart = editor.SelectionStart;
        var selectionEnd = selectionStart + editor.SelectionLength;
        var hasSelection = selectionEnd > selectionStart;

        var lineStart = GetLineStart(text, hasSelection ? selectionStart : editor.CaretOffset);
        var lineEnd = GetLineEnd(text, hasSelection ? selectionEnd : editor.CaretOffset);
        var segment = text.Substring(lineStart, lineEnd - lineStart);

        var (indentToken, indentWidth) = GetIndentationToken();
        var updated = indent
            ? IndentLines(segment, indentToken)
            : OutdentLines(segment, indentToken, indentWidth);

        editor.Text = text.Remove(lineStart, segment.Length).Insert(lineStart, updated);
        editor.Select(lineStart, updated.Length);
        editor.CaretOffset = lineStart + updated.Length;
    }

    private void ConvertTabsToSpaces(TextEditor editor)
    {
        var text = editor.Text ?? string.Empty;
        var selectionStart = editor.SelectionStart;
        var selectionEnd = selectionStart + editor.SelectionLength;
        var hasSelection = selectionEnd > selectionStart;
        var (_, indentWidth) = GetIndentationToken();
        var spaces = new string(' ', indentWidth);

        if (!hasSelection)
        {
            editor.Text = text.Replace("\t", spaces, StringComparison.Ordinal);
            editor.CaretOffset = Math.Min(editor.CaretOffset, editor.Text?.Length ?? 0);
            return;
        }

        var segment = text.Substring(selectionStart, selectionEnd - selectionStart);
        var updated = segment.Replace("\t", spaces, StringComparison.Ordinal);
        editor.Text = text.Remove(selectionStart, segment.Length).Insert(selectionStart, updated);
        editor.Select(selectionStart, updated.Length);
        editor.CaretOffset = selectionStart + updated.Length;
    }

    private void ConvertSpacesToTabs(TextEditor editor)
    {
        var text = editor.Text ?? string.Empty;
        var selectionStart = editor.SelectionStart;
        var selectionEnd = selectionStart + editor.SelectionLength;
        var hasSelection = selectionEnd > selectionStart;
        var (_, indentWidth) = GetIndentationToken();
        var lineStart = GetLineStart(text, hasSelection ? selectionStart : 0);
        var lineEnd = GetLineEnd(text, hasSelection ? selectionEnd : text.Length);
        var segment = text.Substring(lineStart, lineEnd - lineStart);
        var updated = ConvertLeadingSpacesToTabs(segment, indentWidth);

        editor.Text = text.Remove(lineStart, segment.Length).Insert(lineStart, updated);
        editor.Select(lineStart, updated.Length);
        editor.CaretOffset = lineStart + updated.Length;
    }

    private (string token, int width) GetIndentationToken()
    {
        var width = 4;
        var useTabs = false;
        var value = ViewModel?.Settings.Indentation ?? "Spaces: 4";
        var parts = value.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && int.TryParse(parts[1], out var parsed))
        {
            width = parsed;
        }

        if (parts.Length > 0 && parts[0].StartsWith("Tabs", StringComparison.OrdinalIgnoreCase))
        {
            useTabs = true;
        }

        return useTabs ? ("\t", width) : (new string(' ', width), width);
    }

    private static int GetLineStart(string text, int index)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var safe = Math.Clamp(index, 0, text.Length);
        var start = text.LastIndexOf('\n', Math.Max(0, safe - 1));
        return start < 0 ? 0 : start + 1;
    }

    private static int GetLineEnd(string text, int index)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var safe = Math.Clamp(index, 0, text.Length);
        var end = text.IndexOf('\n', safe);
        return end < 0 ? text.Length : end;
    }

    private static string IndentLines(string segment, string indentToken)
    {
        var lines = segment.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = indentToken + lines[i];
        }

        return string.Join("\n", lines);
    }

    private static string OutdentLines(string segment, string indentToken, int indentWidth)
    {
        var lines = segment.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = RemoveIndent(lines[i], indentToken, indentWidth);
        }

        return string.Join("\n", lines);
    }

    private static string RemoveIndent(string line, string indentToken, int indentWidth)
    {
        if (string.IsNullOrEmpty(line))
        {
            return line;
        }

        if (indentToken == "\t")
        {
            return line.StartsWith("\t", StringComparison.Ordinal) ? line[1..] : line;
        }

        if (line.StartsWith(indentToken, StringComparison.Ordinal))
        {
            return line[indentToken.Length..];
        }

        var count = 0;
        while (count < line.Length && count < indentWidth && line[count] == ' ')
        {
            count++;
        }

        return count > 0 ? line[count..] : line;
    }

    private static string ConvertLeadingSpacesToTabs(string segment, int indentWidth)
    {
        var lines = segment.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var count = 0;
            while (count < line.Length && line[count] == ' ')
            {
                count++;
            }

            if (count < indentWidth)
            {
                lines[i] = line;
                continue;
            }

            var tabs = count / indentWidth;
            var remainder = count % indentWidth;
            lines[i] = new string('\t', tabs) + new string(' ', remainder) + line[count..];
        }

        return string.Join("\n", lines);
    }

    // ───────────── Custom scrollbar (VS Code-style) ─────────────

    private void ConnectActiveEditorScrollViewer()
    {
        // Disconnect previous
        if (_activeEditorScrollViewer != null)
        {
            _activeEditorScrollViewer.PropertyChanged -= OnEditorScrollViewerPropertyChanged;
            _activeEditorScrollViewer = null;
        }

        if (_activeEditorView != null)
        {
            _activeEditorView.ScrollViewerReady -= OnEditorScrollViewerReady;
            _activeEditorView.CaretMoved -= OnActiveEditorCaretMoved;
            _activeEditorView = null;
        }

        if (_activeTextEditor != null)
        {
            _activeTextEditor.PropertyChanged -= OnActiveEditorPropertyChanged;
            _activeTextEditor = null;
        }

        var minimap = this.FindControl<MinimapView>("EditorMinimap");
        minimap?.BindEditor(null);
        minimap?.BindEditorScrollViewer(null);

        // Find the current EditorView in the visual tree
        var tabControl = this.FindControl<TabControl>("MainTabControl");
        var activeEditor = ViewModel?.Editor;
        var editorView = tabControl?
            .GetVisualDescendants()
            .OfType<EditorView>()
            .FirstOrDefault(ev => ReferenceEquals(ev.DataContext, activeEditor));

        if (editorView == null) return;

        _activeEditorView = editorView;
        _activeEditorView.ScrollViewerReady += OnEditorScrollViewerReady;
        _activeEditorView.CaretMoved += OnActiveEditorCaretMoved;
        AttachActiveTextEditor(editorView.Editor);
        minimap?.BindEditor(editorView.Editor);

        var sv = editorView.EditorScrollViewer;
        if (sv != null)
        {
            AttachScrollViewer(sv);
        }
    }

    private void OnEditorScrollViewerReady(object? sender, ScrollViewer sv)
    {
        if (sender is EditorView ev)
        {
            ev.ScrollViewerReady -= OnEditorScrollViewerReady;

            if (!ReferenceEquals(ev.DataContext, ViewModel?.Editor))
            {
                return;
            }
        }

        AttachScrollViewer(sv);
    }

    private void AttachScrollViewer(ScrollViewer sv)
    {
        if (_activeEditorScrollViewer != null)
        {
            _activeEditorScrollViewer.PropertyChanged -= OnEditorScrollViewerPropertyChanged;
        }

        _activeEditorScrollViewer = sv;
        sv.PropertyChanged += OnEditorScrollViewerPropertyChanged;
        var minimap = this.FindControl<MinimapView>("EditorMinimap");
        minimap?.BindEditor(_activeEditorView?.Editor);
        minimap?.BindEditorScrollViewer(sv);
        RefreshActiveEditorBookmarks();
        UpdateScrollbar();
    }

    private void AttachActiveTextEditor(TextEditor? editor)
    {
        if (ReferenceEquals(_activeTextEditor, editor))
        {
            return;
        }

        if (_activeTextEditor != null)
        {
            _activeTextEditor.PropertyChanged -= OnActiveEditorPropertyChanged;
        }

        _activeTextEditor = editor;
        if (_activeTextEditor != null)
        {
            _activeTextEditor.PropertyChanged += OnActiveEditorPropertyChanged;
        }
    }

    private void OnActiveEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == "CaretOffset")
        {
            UpdateScrollbar();
        }
    }

    private void OnActiveEditorCaretMoved(object? sender, EventArgs e)
    {
        UpdateScrollbar();
    }

    private void AttachEditorModel(EditorViewModel editor)
    {
        if (_activeEditorModel != null)
        {
            _activeEditorModel.CaretRequested -= OnEditorCaretRequestedForBridge;
            _activeEditorModel.SelectionRequested -= OnEditorSelectionRequestedForBridge;
            _activeEditorModel.PropertyChanged -= OnEditorModelPropertyChanged;
            _activeEditorModel.PropertyChanged -= OnEditorContentChangedForBridge;
        }

        _activeEditorModel = editor;
        _activeEditorModel.CaretRequested += OnEditorCaretRequestedForBridge;
        _activeEditorModel.SelectionRequested += OnEditorSelectionRequestedForBridge;
        _activeEditorModel.PropertyChanged += OnEditorModelPropertyChanged;
        _activeEditorModel.PropertyChanged += OnEditorContentChangedForBridge;
        RefreshActiveEditorBookmarks();
        UpdateScrollbar();
    }

    private void OnEditorCaretRequestedForBridge(object? sender, int caretIndex)
    {
        if (_webBridge is null || sender is not EditorViewModel editor)
        {
            return;
        }

        var safeIndex = Math.Clamp(caretIndex, 0, (editor.Text ?? string.Empty).Length);
        var (line, column) = GetLineAndColumnFromIndex(editor.Text ?? string.Empty, safeIndex);
        _webBridge.SendNavigate(line, column);
    }

    private void OnEditorSelectionRequestedForBridge(object? sender, (int start, int length) request)
    {
        if (_webBridge is null || sender is not EditorViewModel editor)
        {
            return;
        }

        var text = editor.Text ?? string.Empty;
        var safeIndex = Math.Clamp(request.start, 0, text.Length);
        var (line, column) = GetLineAndColumnFromIndex(text, safeIndex);
        _webBridge.SendNavigate(line, column);
    }

    private static (int line, int column) GetLineAndColumnFromIndex(string text, int index)
    {
        var safeIndex = Math.Clamp(index, 0, text.Length);
        var line = 1;
        var column = 1;

        for (var i = 0; i < safeIndex; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return (line, column);
    }

    private void OnEditorContentChangedForBridge(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel?.IsWelcomeTabSelected == true) return;
        if (_suppressWebViewPushFromBridge) return;

        if (e.PropertyName is nameof(EditorViewModel.Text) or nameof(EditorViewModel.Language))
        {
            PushActiveTabToWebView();
        }
        else if (e.PropertyName == nameof(EditorViewModel.IsMarkdownPreviewVisible)
                 && _activeEditorModel is { } editor)
        {
            _webBridge?.SendPreviewToggle(editor.CanToggleRenderedMarkdownView && editor.IsMarkdownPreviewVisible);
        }
    }

    private void PushActiveTabToWebView()
    {
        if (_webBridge is null || ViewModel is not { } vm) return;

        if (vm.IsWelcomeTabSelected)
        {
            _webBridge.SendViewWelcome(BuildWelcomeData(vm));
            return;
        }

        var editor = vm.Editor;
        var path = editor.FilePath ?? string.Empty;
        var lang = editor.Language ?? "plaintext";
        _webBridge.SendViewEditor();
        _webBridge.SendFileOpen(path, editor.Text, lang);
        _webBridge.SendPreviewToggle(editor.CanToggleRenderedMarkdownView && editor.IsMarkdownPreviewVisible);
        RefreshActiveEditorBookmarks();
    }

    // ── WebView2 bridge ───────────────────────────────────────────────────────

    private void InitWebBridge()
    {
        var host = this.FindControl<WebViewHost>("EditorWebView");
        if (host is null) return;

        _webBridge = new WebBridgeService(host);

        _webBridge.EditorReady += (_, _) =>
        {
            // Push current settings and theme once Monaco signals it is ready
            if (ViewModel is not { } vm) return;
            _webBridge.SendSettings(BuildBridgeSettings(vm));
            _webBridge.SendTheme(vm.Settings.Theme, BuildBridgeTheme());

            // Push the active tab content (or welcome page)
            PushActiveTabToWebView();
        };

        _webBridge.SaveRequested += (_, content) =>
        {
            if (ViewModel?.Editor is not { } editor) return;
            // Sync Monaco content back to the ViewModel then save.
            // Text assignment internally triggers the dirty flag.
            editor.Text = content;
            _ = editor.SaveAsync();
        };

        _webBridge.CursorChanged += (_, args) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ViewModel?.Editor.UpdateCaretPosition(args.Line, args.Column);
            });
        };

        _webBridge.FileModified += (_, isDirty) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ViewModel?.Editor.SetExternalDirtyState(isDirty);
            });
        };

        _webBridge.MarkdownContentUpdated += (_, args) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (ViewModel?.Editor is not { } editor)
                {
                    return;
                }

                if (!editor.CanToggleRenderedMarkdownView || !editor.IsMarkdownPreviewVisible)
                {
                    return;
                }

                if (string.Equals(editor.Text, args.Content, StringComparison.Ordinal))
                {
                    return;
                }

                _suppressWebViewPushFromBridge = true;
                try
                {
                    editor.Text = args.Content;
                }
                finally
                {
                    _suppressWebViewPushFromBridge = false;
                }
            });
        };

        _webBridge.WelcomeAction += (_, args) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                switch (args.Action)
                {
                    case "welcome:new-file":
                        ViewModel?.NewDocument();
                        break;
                    case "welcome:open-file":
                        OnFileOpen(this, new Avalonia.Interactivity.RoutedEventArgs());
                        break;
                    case "welcome:open-folder":
                        OnFileOpenFolder(this, new Avalonia.Interactivity.RoutedEventArgs());
                        break;
                    case "welcome:open-workspace":
                        OnFileOpenWorkspace(this, new Avalonia.Interactivity.RoutedEventArgs());
                        break;
                    case "welcome:create-workspace":
                        OnFileCreateWorkspace(this, new Avalonia.Interactivity.RoutedEventArgs());
                        break;
                    case "welcome:open-recent":
                        if (!string.IsNullOrEmpty(args.Path))
                            _ = OpenRecentFromWelcomeAsync(args.Path, args.Kind);
                        break;
                }
            });
        };

        // Listen for tab changes so the webview reflects the active view
        if (ViewModel is { } initVm)
        {
            initVm.PropertyChanged += OnViewModelPropertyChangedForBridge;
            initVm.Settings.PropertyChanged += OnSettingsPropertyChangedForBridge;
        }
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChangedForBridge;
                vm.Settings.PropertyChanged += OnSettingsPropertyChangedForBridge;
            }
        };
    }

    private void OnSettingsPropertyChangedForBridge(object? sender, PropertyChangedEventArgs e)
    {
        if (_webBridge is null || ViewModel is not { } vm) return;

        if (e.PropertyName == nameof(SettingsViewModel.PrimaryPanelPosition))
        {
            UpdateToolPanelColumn();
        }

        if (e.PropertyName == nameof(SettingsViewModel.Theme))
        {
            // ThemeService.ApplyTheme runs via App.axaml.cs on the same thread.
            // Post at Background priority so Avalonia resources finish updating first.
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_webBridge is null || ViewModel is not { } vm2) return;
                _webBridge.SendTheme(vm2.Settings.Theme, BuildBridgeTheme());
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
        else
        {
            _webBridge.SendSettings(BuildBridgeSettings(vm));
        }

        // Always send scrollbar opacity to Monaco webview if it changes
        if (e.PropertyName == nameof(SettingsViewModel.ScrollbarOpacity))
        {
            _webBridge.SendScrollbarOpacity(vm.Settings.ScrollbarOpacity);
        }
    }

    private void OnViewModelPropertyChangedForBridge(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.IsWelcomeTabSelected)) return;
        if (_webBridge is null || ViewModel is not { } vm) return;

        if (vm.IsWelcomeTabSelected)
            _webBridge.SendViewWelcome(BuildWelcomeData(vm));
        else
            _webBridge.SendViewEditor();
    }

    private static WelcomeDataBridge BuildWelcomeData(MainWindowViewModel vm) => new()
    {
        RecentFiles = vm.RecentFiles
            .Take(10)
            .Select(f => new RecentItemBridge(f.DisplayName, f.Path))
            .ToArray(),
        RecentFolders = vm.RecentFolders
            .Take(10)
            .Select(f => new RecentItemBridge(f.DisplayName, f.Path))
            .ToArray(),
        RecentWorkspaces = vm.RecentWorkspaces
            .Take(10)
            .Select(w => new RecentItemBridge(w.DisplayName, w.Path))
            .ToArray(),
    };

    private async Task OpenRecentFromWelcomeAsync(string path, string kind)
    {
        if (kind == "workspace")
        {
            ViewModel?.Explorer.LoadWorkspace(path);
            ViewModel?.AddRecentProject(path);
        }
        else if (kind == "folder")
        {
            await OpenFolderWithWorkspaceDetectionAsync(path);
        }
        else
        {
            if (ViewModel != null)
                await ViewModel.OpenFileFromPathAsync(path);
        }
    }

    private static EditorBridgeSettings BuildBridgeSettings(MainWindowViewModel vm) => new()
    {
        WordWrap = vm.Settings.WordWrap,
        ShowLineNumbers = vm.Settings.ShowLineNumbers,
        IsMinimapVisible = vm.Settings.IsMinimapVisible,
        MinimapFadeSpeedMs = (int)Math.Clamp(vm.Settings.MinimapFadeSpeedMs, 60, 2000),
        AutoIndentation = vm.Settings.AutoIndentation,
        AutoBracketing = vm.Settings.AutoBracketing,
        RenderWhitespace = vm.Settings.RenderWhitespace,
        EditorFontSize = (int)vm.Settings.EditorFontSize,
        Indentation = vm.Settings.Indentation,
        Eol = vm.Settings.Eol,
    };

    private static ThemeColorsBridge BuildBridgeTheme()
    {
        // Read token colors from Application resources (set by ThemeService.ApplyTheme)
        static string Brush(string key)
        {
            if (Avalonia.Application.Current?.Resources.TryGetResource(key, Avalonia.Styling.ThemeVariant.Default, out var v) == true
                && v is Avalonia.Media.ISolidColorBrush b)
            {
                // Avalonia Color.ToString() returns #AARRGGBB but CSS needs #RRGGBB.
                var c = b.Color;
                return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            }
            return "#000000";
        }

        return new ThemeColorsBridge
        {
            Background = Brush("EditorBackground"),
            Foreground = Brush("ForegroundPrimary"),
            SelectionBackground = Brush("SelectionBackground"),
            LineHighlight = Brush("CurrentLineHighlight"),
            SyntaxKeyword = Brush("SyntaxKeyword"),
            SyntaxString = Brush("SyntaxString"),
            SyntaxComment = Brush("SyntaxComment"),
            SyntaxNumber = Brush("SyntaxNumber"),
            SyntaxType = Brush("SyntaxType"),
            SyntaxFunction = Brush("SyntaxFunction"),
        };
    }

    private void OnEditorModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditorViewModel.CaretLine) or nameof(EditorViewModel.LineCount))
        {
            UpdateScrollbar();
        }

        if (e.PropertyName is nameof(EditorViewModel.Text) or nameof(EditorViewModel.FilePath))
        {
            ViewModel?.RefreshBookmarksForActiveEditor();
            RefreshActiveEditorBookmarks();
        }
    }

    private void RefreshActiveEditorBookmarks()
    {
        if (ViewModel == null)
        {
            return;
        }

        ViewModel.RefreshBookmarksForActiveEditor();

        var bookmarkLookup = new Func<int, BookmarkMarkerState>(lineNumber => ViewModel.GetBookmarkMarkerState(ViewModel.Editor.FilePath, lineNumber));
        _activeEditorView?.SetBookmarkLookup(bookmarkLookup);
        _activeEditorView?.RefreshBookmarkMarkers();

        var bookmarkMarkers = ViewModel.GetBookmarksForCurrentDocument()
            .GroupBy(bookmark => bookmark.LineNumber)
            .Select(group => new EditorBookmarkMarker(
                group.Key,
                group.Any(bookmark => bookmark.IsStale)
                    ? "stale"
                    : group.Any(bookmark => bookmark.IsGlobal)
                        ? "global"
                        : "scoped"))
            .OrderBy(marker => marker.Line)
            .ToArray();

        _webBridge?.SendBookmarks(bookmarkMarkers);
    }

    private async Task ShowQuickPickAsync(string title, string watermark, Func<string, List<QuickPickEntry>> resolveItems)
    {
        var searchBox = new TextBox
        {
            Watermark = watermark,
            MinWidth = 420
        };

        var listBox = new ListBox
        {
            MinHeight = 240
        };

        var runButton = new Button
        {
            Content = "Open",
            Width = 80,
            IsDefault = true
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80,
            IsCancel = true
        };

        var pickerWindow = new Window
        {
            Title = title,
            Width = 560,
            Height = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new Border
            {
                Padding = new Thickness(12),
                Child = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        searchBox,
                        listBox,
                        new StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Spacing = 8,
                            Children = { runButton, cancelButton }
                        }
                    }
                }
            }
        };

        List<QuickPickEntry> items = new();

        void RefreshList(string query)
        {
            items = resolveItems(query);
            listBox.ItemsSource = items;
            listBox.SelectedIndex = items.Count > 0 ? 0 : -1;
        }

        async Task ExecuteSelectedAndCloseAsync()
        {
            if (listBox.SelectedItem is not QuickPickEntry selected)
            {
                return;
            }

            pickerWindow.Close();
            await selected.ExecuteAsync();
        }

        pickerWindow.Opened += (_, _) => searchBox.Focus();
        searchBox.GetObservable(TextBox.TextProperty).Subscribe(text => RefreshList(text ?? string.Empty));
        searchBox.KeyDown += async (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                await ExecuteSelectedAndCloseAsync();
                args.Handled = true;
                return;
            }

            if (args.Key == Key.Down && items.Count > 0)
            {
                listBox.SelectedIndex = Math.Clamp(listBox.SelectedIndex + 1, 0, items.Count - 1);
                args.Handled = true;
                return;
            }

            if (args.Key == Key.Up && items.Count > 0)
            {
                listBox.SelectedIndex = Math.Clamp(listBox.SelectedIndex - 1, 0, items.Count - 1);
                args.Handled = true;
            }
        };

        listBox.DoubleTapped += async (_, _) => await ExecuteSelectedAndCloseAsync();
        listBox.KeyDown += async (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                await ExecuteSelectedAndCloseAsync();
                args.Handled = true;
            }
        };
        runButton.Click += async (_, _) => await ExecuteSelectedAndCloseAsync();
        cancelButton.Click += (_, _) => pickerWindow.Close();

        RefreshList(string.Empty);
        await pickerWindow.ShowDialog(this);
    }

    private List<QuickPickEntry> BuildCommandPaletteEntries()
    {
        if (ViewModel == null)
        {
            return new List<QuickPickEntry>();
        }

        var entries = new List<QuickPickEntry>
        {
            new("File: New Text", () => ExecuteQuickActionAsync(() => ViewModel.NewTextDocument()), "Create a new text document"),
            new("File: New Markdown", () => ExecuteQuickActionAsync(() => ViewModel.NewMarkdownDocument()), "Create a new markdown document"),
            new("File: New JSON", () => ExecuteQuickActionAsync(() => ViewModel.NewJsonDocument()), "Create a new JSON document"),
            new("File: New XML", () => ExecuteQuickActionAsync(() => ViewModel.NewXmlDocument()), "Create a new XML document"),
            new("File: New C#", () => ExecuteQuickActionAsync(() => ViewModel.NewCSharpDocument()), "Create a new C# document"),
            new("File: New C", () => ExecuteQuickActionAsync(() => ViewModel.NewCDocument()), "Create a new C document"),
            new("File: New C++", () => ExecuteQuickActionAsync(() => ViewModel.NewCppDocument()), "Create a new C++ document"),
            new("File: New XAML", () => ExecuteQuickActionAsync(() => ViewModel.NewXamlDocument()), "Create a new XAML document"),
            new("File: New AXAML", () => ExecuteQuickActionAsync(() => ViewModel.NewAxamlDocument()), "Create a new AXAML document"),
            new("File: Open...", () => ExecuteQuickActionAsync(() => OnFileOpen(this, new RoutedEventArgs())), "Open a file from disk"),
            new("File: Open Folder...", () => ExecuteQuickActionAsync(() => OnFileOpenFolder(this, new RoutedEventArgs())), "Open a folder"),
            new("File: Open Workspace...", () => ExecuteQuickActionAsync(() => OnFileOpenWorkspace(this, new RoutedEventArgs())), "Open a workspace"),
            new("File: Create Workspace...", () => ExecuteQuickActionAsync(() => OnFileCreateWorkspace(this, new RoutedEventArgs())), "Create a new workspace file"),
            new("File: Save", () => ExecuteQuickActionAsync(() => OnFileSave(this, new RoutedEventArgs())), "Save the active file"),
            new("File: Save As...", () => ExecuteQuickActionAsync(() => OnFileSaveAs(this, new RoutedEventArgs())), "Save the active file to a new path"),
            new("File: Save All", () => ViewModel.SaveAllAsync(), "Save all modified open files"),
            new(ViewModel.SelectedTab?.IsPinned == true ? "Tab: Unpin Active Tab" : "Tab: Pin Active Tab", () => ExecuteQuickActionAsync(() => ViewModel.TogglePinTab(ViewModel.SelectedTab!)), "Keep the active tab at the front of the tab strip"),
            new("Tab: Duplicate Active Tab", () => ViewModel.SelectedTab != null ? ViewModel.DuplicateTabAsync(ViewModel.SelectedTab) : Task.CompletedTask, "Open a duplicate of the active tab"),
            new("Tab: Reveal Active File In Explorer", () => ExecuteQuickActionAsync(() =>
            {
                if (ViewModel.SelectedTab != null)
                {
                    ViewModel.RevealTabInExplorer(ViewModel.SelectedTab);
                }
            }), "Reveal the active file within the explorer"),
            new("Navigate: Goto Anything", () => ExecuteQuickActionAsync(() => OnGotoAnything(this, new RoutedEventArgs())), "Jump to files, tabs, bookmarks, or lines"),
            new("Navigate: Go To Line", () => ExecuteQuickActionAsync(() => OnEditGoTo(this, new RoutedEventArgs())), "Jump to a specific line"),
            new("Bookmarks: Toggle Bookmark", () => ExecuteQuickActionAsync(() => OnEditToggleBookmark(this, new RoutedEventArgs())), "Add or remove a bookmark on the current line"),
            new("Bookmarks: Toggle Global Bookmark", () => ExecuteQuickActionAsync(() => OnEditToggleGlobalBookmark(this, new RoutedEventArgs())), "Add or remove a global bookmark on the current line"),
            new("Bookmarks: Next Bookmark", () => ExecuteQuickActionAsync(() => OnEditNextBookmark(this, new RoutedEventArgs())), "Move to the next bookmark"),
            new("Bookmarks: Previous Bookmark", () => ExecuteQuickActionAsync(() => OnEditPreviousBookmark(this, new RoutedEventArgs())), "Move to the previous bookmark"),
            new("Bookmarks: List Bookmarks", () => ExecuteQuickActionAsync(() => OnEditListBookmarks(this, new RoutedEventArgs())), "List bookmarks in the current file"),
            new("Bookmarks: Import Bookmarks", () => ExecuteQuickActionAsync(() => OnEditImportBookmarks(this, new RoutedEventArgs())), "Import bookmarks into the current workspace or folder scope"),
            new("Bookmarks: Export Bookmarks", () => ExecuteQuickActionAsync(() => OnEditExportBookmarks(this, new RoutedEventArgs())), "Export bookmarks from the current workspace or folder scope"),
            new("Bookmarks: Clear File Bookmarks", () => ExecuteQuickActionAsync(() => OnEditClearBookmarksInFile(this, new RoutedEventArgs())), "Remove bookmarks from the active file"),
            new("Bookmarks: Clear All Bookmarks", () => ExecuteQuickActionAsync(() => OnEditClearAllBookmarks(this, new RoutedEventArgs())), "Remove all saved bookmarks"),
            new("View: Toggle Explorer", () => ExecuteQuickActionAsync(() => OnViewToggleExplorer(this, new RoutedEventArgs())), "Show or hide the explorer"),
            new("View: Toggle Search", () => ExecuteQuickActionAsync(() => OnViewToggleSearch(this, new RoutedEventArgs())), "Show or hide search"),
            new("View: Toggle Bookmarks", () => ExecuteQuickActionAsync(() => OnViewToggleBookmarks(this, new RoutedEventArgs())), "Show or hide the bookmarks panel")
        };

        entries.AddRange(BuildCommandPaletteToggleEntries());
        entries.AddRange(BuildCommandPaletteOptionEntries());
        entries.AddRange(BuildCommandPaletteRecentEntries());
        return entries;
    }

    private IEnumerable<QuickPickEntry> BuildCommandPaletteToggleEntries()
    {
        if (ViewModel == null)
        {
            return Enumerable.Empty<QuickPickEntry>();
        }

        return new[]
        {
            CreateSettingToggleEntry("View: Toggle Status Bar", "status bar footer chrome", () => ViewModel.Settings.IsStatusBarVisible, value => ViewModel.Settings.IsStatusBarVisible = value),
            CreateSettingToggleEntry("View: Toggle Word Wrap", "word wrap line wrapping alt+z", () => ViewModel.Settings.WordWrap, value => ViewModel.Settings.WordWrap = value),
            CreateSettingToggleEntry("View: Toggle Line Numbers", "line numbers gutter", () => ViewModel.Settings.ShowLineNumbers, value => ViewModel.Settings.ShowLineNumbers = value),
            CreateSettingToggleEntry("View: Toggle Minimap", "minimap overview", () => ViewModel.Settings.IsMinimapVisible, value => ViewModel.Settings.IsMinimapVisible = value),
            CreateSettingToggleEntry("View: Toggle Render Whitespace", "render whitespace spaces tabs", () => ViewModel.Settings.RenderWhitespace, value => ViewModel.Settings.RenderWhitespace = value),
            CreateSettingToggleEntry("Edit: Toggle Auto Indent", "auto indent indentation enter", () => ViewModel.Settings.AutoIndentation, value => ViewModel.Settings.AutoIndentation = value),
            CreateSettingToggleEntry("Edit: Toggle Auto Close Brackets", "auto close brackets braces quotes", () => ViewModel.Settings.AutoBracketing, value => ViewModel.Settings.AutoBracketing = value),
            CreateSettingToggleEntry("View: Toggle Activity Bar", "activity bar rail sidebar", () => ViewModel.Settings.IsActivityBarVisible, value => ViewModel.Settings.IsActivityBarVisible = value),
            CreateSettingToggleEntry("Markdown: Toggle Rendered View", "markdown rendered view", () => ViewModel.IsMarkdownPreviewVisible, value => ViewModel.IsMarkdownPreviewVisible = value),
            CreateSettingToggleEntry("Markdown: Toggle Toolbar", "markdown toolbar", () => ViewModel.IsMarkdownToolbarVisible, value => ViewModel.IsMarkdownToolbarVisible = value),
            CreateSettingToggleEntry("Markdown: Toggle Toolbar Pin", "markdown pin toolbar", () => ViewModel.IsMarkdownToolbarPinned, value => ViewModel.IsMarkdownToolbarPinned = value),
            new QuickPickEntry("Markdown: Recover Toolbar To Title Bar", () => ExecuteQuickActionAsync(() =>
            {
                ViewModel.RecoverMarkdownToolbarToPinned();
                UpdateMarkdownToolbarPinnedState();
                UpdatePinnedMarkdownToolbarLayout();
            }), "Pin and show the markdown toolbar in the title bar", "markdown toolbar recover reset pin title bar"),
            new QuickPickEntry("Markdown: Reset Floating Toolbar Position", () => ExecuteQuickActionAsync(() =>
            {
                ViewModel.ResetFloatingMarkdownToolbarPosition();
                ClampMarkdownToolbarPosition();
                UpdateMarkdownToolbarPinnedState();
                UpdatePinnedMarkdownToolbarLayout();
            }), "Restore the floating markdown toolbar to its default position", "markdown toolbar recover reset floating position")
        };
    }

    private IEnumerable<QuickPickEntry> BuildCommandPaletteOptionEntries()
    {
        if (ViewModel == null)
        {
            return Enumerable.Empty<QuickPickEntry>();
        }

        var settings = ViewModel.Settings;
        var entries = new List<QuickPickEntry>();

        entries.AddRange(settings.ThemeOptions.Select(option => CreateSettingOptionEntry(
            $"Preferences: Theme: {option}",
            string.Equals(settings.Theme, option, StringComparison.OrdinalIgnoreCase),
            $"theme appearance colors {option}",
            () => settings.Theme = option)));

        entries.AddRange(settings.EncodingOptions.Select(option => CreateSettingOptionEntry(
            $"Preferences: Encoding: {option}",
            string.Equals(settings.Encoding, option, StringComparison.OrdinalIgnoreCase),
            $"encoding file save {option}",
            () => settings.Encoding = option)));

        entries.AddRange(settings.EolOptions.Select(option => CreateSettingOptionEntry(
            $"Preferences: Line Endings: {option}",
            string.Equals(settings.Eol, option, StringComparison.OrdinalIgnoreCase),
            $"eol line endings newline {option}",
            () => settings.Eol = option)));

        entries.AddRange(settings.IndentationOptions.Select(option => CreateSettingOptionEntry(
            $"Preferences: Indentation: {option}",
            string.Equals(settings.Indentation, option, StringComparison.OrdinalIgnoreCase),
            $"indentation tabs spaces {option}",
            () => settings.Indentation = option)));

        return entries;
    }

    private IEnumerable<QuickPickEntry> BuildCommandPaletteRecentEntries()
    {
        if (ViewModel == null)
        {
            return Enumerable.Empty<QuickPickEntry>();
        }

        var entries = new List<QuickPickEntry>();

        entries.AddRange(ViewModel.RecentFiles.Take(10).Select(recent => new QuickPickEntry(
            $"Recent File: {recent.DisplayName}",
            () => ViewModel.OpenFileFromPathAsync(recent.Path),
            recent.Path,
            $"recent file reopen {recent.DisplayName} {recent.Path}")));

        entries.AddRange(ViewModel.RecentProjects.Take(10).Select(project => new QuickPickEntry(
            project.IsWorkspace
                ? $"Recent Workspace: {project.DisplayName}"
                : $"Recent Folder: {project.DisplayName}",
            () => OpenRecentProjectAsync(project),
            project.Path,
            project.IsWorkspace
                ? $"recent workspace reopen {project.DisplayName} {project.Path}"
                : $"recent folder reopen {project.DisplayName} {project.Path}")));

        if (ViewModel.RecentFiles.Count > 0)
        {
            entries.Add(new QuickPickEntry(
                "Recent Files: Clear Recent List",
                () => ExecuteQuickActionAsync(() => ViewModel.ClearRecentFiles()),
                "Remove all recent files",
                "recent files clear list history"));
        }

        return entries;
    }

    private List<QuickPickEntry> BuildGotoAnythingEntries(string query, IReadOnlyCollection<string> indexedFiles)
    {
        if (ViewModel == null)
        {
            return new List<QuickPickEntry>();
        }

        var parsedQuery = ParseGotoAnythingQuery(query);
        var roots = GetGotoAnythingSearchRoots();

        if (parsedQuery.Mode == GotoAnythingQueryMode.Symbols)
        {
            return BuildGotoAnythingSymbolEntries(parsedQuery.SearchText, indexedFiles, roots);
        }

        if (parsedQuery.Mode == GotoAnythingQueryMode.Text)
        {
            return BuildGotoAnythingTextEntries(parsedQuery.SearchText, indexedFiles, roots);
        }

        var results = new List<QuickPickEntry>();
        var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var searchText = parsedQuery.SearchText;
        var lineNumber = parsedQuery.LineNumber;
        var columnNumber = parsedQuery.ColumnNumber;

        if (lineNumber.HasValue)
        {
            var currentFile = string.IsNullOrWhiteSpace(ViewModel.Editor.FilePath) ? "Current document" : Path.GetFileName(ViewModel.Editor.FilePath);
            results.Add(new QuickPickEntry(
                columnNumber.HasValue
                    ? $"Go to line {lineNumber.Value}, column {columnNumber.Value}"
                    : $"Go to line {lineNumber.Value}",
                () => NavigateToLocationAsync(lineNumber.Value, columnNumber),
                currentFile,
                columnNumber.HasValue
                    ? $"line {lineNumber.Value} column {columnNumber.Value} {currentFile}"
                    : $"line {lineNumber.Value} {currentFile}"));
        }

        foreach (var bookmark in ViewModel.Bookmarks
                     .OrderBy(bookmark => Path.GetFileName(bookmark.FilePath), StringComparer.OrdinalIgnoreCase)
                     .ThenBy(bookmark => bookmark.LineNumber))
        {
            var bookmarkName = FormatPathForDisplay(bookmark.FilePath, roots);
            var entry = new QuickPickEntry(
                $"Bookmark: {bookmarkName}:{bookmark.LineNumber}",
                () => ViewModel.NavigateToBookmarkAsync(bookmark),
                string.IsNullOrWhiteSpace(bookmark.Text) ? "Saved bookmark" : bookmark.Text,
                $"bookmark {bookmarkName} {bookmark.LineNumber} {bookmark.Text}");

            if (MatchesQuickPickQuery(entry, searchText))
            {
                results.Add(entry);
            }
        }

        foreach (var tab in ViewModel.Tabs.Where(tab => !tab.IsWelcomeTab))
        {
            var path = tab.Editor.FilePath;
            if (string.IsNullOrWhiteSpace(path) || !addedPaths.Add(path))
            {
                continue;
            }

            var displayPath = FormatPathForDisplay(path, roots);
            var entry = new QuickPickEntry(
                $"Open tab: {displayPath}",
                () => OpenPathAndMaybeNavigateAsync(path, lineNumber, columnNumber),
                tab.Editor.FileName,
                $"tab {displayPath} {tab.Editor.FileName}");

            if (MatchesQuickPickQuery(entry, searchText))
            {
                results.Add(entry);
            }
        }

        foreach (var recent in ViewModel.RecentFiles)
        {
            if (string.IsNullOrWhiteSpace(recent.Path) || !File.Exists(recent.Path) || !addedPaths.Add(recent.Path))
            {
                continue;
            }

            var displayPath = FormatPathForDisplay(recent.Path, roots);
            var entry = new QuickPickEntry(
                $"Recent: {displayPath}",
                () => OpenPathAndMaybeNavigateAsync(recent.Path, lineNumber, columnNumber),
                "Recently opened file",
                $"recent {displayPath}");

            if (MatchesQuickPickQuery(entry, searchText))
            {
                results.Add(entry);
            }
        }

        foreach (var path in indexedFiles)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !addedPaths.Add(path))
            {
                continue;
            }

            var displayPath = FormatPathForDisplay(path, roots);
            var entry = new QuickPickEntry(
                $"File: {displayPath}",
                () => OpenPathAndMaybeNavigateAsync(path, lineNumber, columnNumber),
                Path.GetFileName(path),
                $"file {displayPath} {Path.GetFileName(path)}");

            if (MatchesQuickPickQuery(entry, searchText))
            {
                results.Add(entry);
            }

            if (results.Count >= MaxGotoAnythingResults)
            {
                break;
            }
        }

        return results.Take(MaxGotoAnythingResults).ToList();
    }

    private List<QuickPickEntry> BuildGotoAnythingSymbolEntries(string query, IReadOnlyCollection<string> indexedFiles, IReadOnlyCollection<string> roots)
    {
        if (ViewModel == null)
        {
            return new List<QuickPickEntry>();
        }

        var results = new List<QuickPickEntry>();
        var visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tab in ViewModel.Tabs.Where(tab => !tab.IsWelcomeTab))
        {
            AddSymbolEntries(results, query, tab.Editor.Text ?? string.Empty, GetTabDisplayPath(tab, roots),
                (line, column) => NavigateToTabLocationAsync(tab, line, column));

            if (!string.IsNullOrWhiteSpace(tab.Editor.FilePath))
            {
                visitedPaths.Add(tab.Editor.FilePath);
            }

            if (results.Count >= MaxGotoAnythingResults)
            {
                return results.Take(MaxGotoAnythingResults).ToList();
            }
        }

        foreach (var path in indexedFiles)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !visitedPaths.Add(path))
            {
                continue;
            }

            var text = TryReadAllText(path);
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            AddSymbolEntries(results, query, text, FormatPathForDisplay(path, roots),
                (line, column) => OpenPathAndMaybeNavigateAsync(path, line, column));

            if (results.Count >= MaxGotoAnythingResults)
            {
                break;
            }
        }

        return results.Take(MaxGotoAnythingResults).ToList();
    }

    private List<QuickPickEntry> BuildGotoAnythingTextEntries(string query, IReadOnlyCollection<string> indexedFiles, IReadOnlyCollection<string> roots)
    {
        if (ViewModel == null || string.IsNullOrWhiteSpace(query))
        {
            return new List<QuickPickEntry>();
        }

        var results = new List<QuickPickEntry>();
        var visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tab in ViewModel.Tabs.Where(tab => !tab.IsWelcomeTab))
        {
            AddTextEntries(results, query, tab.Editor.Text ?? string.Empty, GetTabDisplayPath(tab, roots),
                (line, column) => NavigateToTabLocationAsync(tab, line, column));

            if (!string.IsNullOrWhiteSpace(tab.Editor.FilePath))
            {
                visitedPaths.Add(tab.Editor.FilePath);
            }

            if (results.Count >= MaxGotoAnythingResults)
            {
                return results.Take(MaxGotoAnythingResults).ToList();
            }
        }

        foreach (var path in indexedFiles)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !visitedPaths.Add(path))
            {
                continue;
            }

            var text = TryReadAllText(path);
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            AddTextEntries(results, query, text, FormatPathForDisplay(path, roots),
                (line, column) => OpenPathAndMaybeNavigateAsync(path, line, column));

            if (results.Count >= MaxGotoAnythingResults)
            {
                break;
            }
        }

        return results.Take(MaxGotoAnythingResults).ToList();
    }

    private List<string> BuildGotoAnythingFileIndex()
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ViewModel == null)
        {
            return files.ToList();
        }

        foreach (var path in ViewModel.GetOpenDocumentPathsData())
        {
            if (File.Exists(path))
            {
                files.Add(path);
            }
        }

        foreach (var path in ViewModel.GetRecentFilesData())
        {
            if (File.Exists(path))
            {
                files.Add(path);
            }
        }

        foreach (var root in GetGotoAnythingSearchRoots())
        {
            foreach (var path in EnumerateSearchFiles(root, MaxGotoAnythingIndexedFiles - files.Count))
            {
                files.Add(path);
                if (files.Count >= MaxGotoAnythingIndexedFiles)
                {
                    break;
                }
            }

            if (files.Count >= MaxGotoAnythingIndexedFiles)
            {
                break;
            }
        }

        return files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private IEnumerable<string> EnumerateSearchFiles(string root, int remaining)
    {
        if (remaining <= 0 || string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            yield break;
        }

        var pending = new Stack<string>();
        pending.Push(root);
        var yielded = 0;

        while (pending.Count > 0 && yielded < remaining)
        {
            var current = pending.Pop();

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(current)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                continue;
            }

            foreach (var directory in directories.Reverse())
            {
                if (!ShouldSkipGotoAnythingDirectory(directory))
                {
                    pending.Push(directory);
                }
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
                yielded++;
                if (yielded >= remaining)
                {
                    yield break;
                }
            }
        }
    }

    private List<string> GetGotoAnythingSearchRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ViewModel == null)
        {
            return roots.ToList();
        }

        var folderPath = ViewModel.GetCurrentFolderPathData();
        if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
        {
            roots.Add(folderPath);
        }

        var workspacePath = ViewModel.GetCurrentWorkspacePathData();
        foreach (var root in ParseWorkspaceFolders(workspacePath))
        {
            roots.Add(root);
        }

        if (roots.Count == 0 && !string.IsNullOrWhiteSpace(ViewModel.Editor.FilePath))
        {
            var editorDirectory = Path.GetDirectoryName(ViewModel.Editor.FilePath);
            if (!string.IsNullOrWhiteSpace(editorDirectory) && Directory.Exists(editorDirectory))
            {
                roots.Add(editorDirectory);
            }
        }

        return roots.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> ParseWorkspaceFolders(string workspacePath)
    {
        var roots = new List<string>();
        if (string.IsNullOrWhiteSpace(workspacePath) || !File.Exists(workspacePath))
        {
            return roots;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(workspacePath));
            if (!doc.RootElement.TryGetProperty("folders", out var folders) || folders.ValueKind != JsonValueKind.Array)
            {
                return roots;
            }

            var workspaceDirectory = Path.GetDirectoryName(workspacePath) ?? string.Empty;
            foreach (var folder in folders.EnumerateArray())
            {
                if (!folder.TryGetProperty("path", out var pathElement))
                {
                    continue;
                }

                var rawPath = pathElement.GetString();
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    continue;
                }

                var fullPath = Path.IsPathRooted(rawPath)
                    ? rawPath
                    : Path.GetFullPath(Path.Combine(workspaceDirectory, rawPath));

                if (Directory.Exists(fullPath))
                {
                    roots.Add(fullPath);
                }
            }
        }
        catch
        {
        }

        return roots;
    }

    private static bool ShouldSkipGotoAnythingDirectory(string directoryPath)
    {
        var name = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return GotoAnythingIgnoredDirectories.Contains(name);
    }

    private static (string SearchText, int? LineNumber, int? ColumnNumber, GotoAnythingQueryMode Mode) ParseGotoAnythingQuery(string query)
    {
        var normalized = query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return (string.Empty, null, null, GotoAnythingQueryMode.Default);
        }

        if (normalized.StartsWith('@'))
        {
            return (normalized[1..].Trim(), null, null, GotoAnythingQueryMode.Symbols);
        }

        if (normalized.StartsWith('#'))
        {
            return (normalized[1..].Trim(), null, null, GotoAnythingQueryMode.Text);
        }

        if (TryParseLineAndColumn(normalized.TrimStart(':'), out var directLine, out var directColumn) && normalized.StartsWith(':'))
        {
            return (string.Empty, directLine, directColumn, GotoAnythingQueryMode.Default);
        }

        var match = Regex.Match(normalized, @":(?<line>\d+)(?::(?<column>\d+))?$", RegexOptions.CultureInvariant);
        if (match.Success && int.TryParse(match.Groups["line"].Value, out var lineNumber))
        {
            int? columnNumber = null;
            if (match.Groups["column"].Success && int.TryParse(match.Groups["column"].Value, out var parsedColumn))
            {
                columnNumber = parsedColumn;
            }

            return (normalized[..match.Index].Trim(), lineNumber, columnNumber, GotoAnythingQueryMode.Default);
        }

        return (normalized, null, null, GotoAnythingQueryMode.Default);
    }

    private static bool TryParseLineAndColumn(string value, out int lineNumber, out int? columnNumber)
    {
        lineNumber = 0;
        columnNumber = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.Length > 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out lineNumber))
        {
            return false;
        }

        if (parts.Length == 2)
        {
            if (!int.TryParse(parts[1], out var parsedColumn))
            {
                return false;
            }

            columnNumber = parsedColumn;
        }

        return true;
    }

    private static bool MatchesQuickPickQuery(QuickPickEntry entry, string query)
    {
        var normalized = query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.All(token => entry.SearchText.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private async Task OpenPathAndMaybeNavigateAsync(string path, int? lineNumber, int? columnNumber)
    {
        if (ViewModel == null)
        {
            return;
        }

        await ViewModel.OpenFileFromPathAsync(path);
        if (lineNumber.HasValue)
        {
            if (columnNumber.HasValue)
            {
                ViewModel.Editor.NavigateToLocation(lineNumber.Value, columnNumber.Value);
            }
            else
            {
                ViewModel.Editor.NavigateToLine(lineNumber.Value);
            }
        }

        RefreshActiveEditorBookmarks();
    }

    private Task NavigateToTabLocationAsync(EditorTabViewModel tab, int lineNumber, int? columnNumber)
    {
        if (ViewModel == null)
        {
            return Task.CompletedTask;
        }

        ViewModel.SelectedTab = tab;
        if (columnNumber.HasValue)
        {
            ViewModel.Editor.NavigateToLocation(lineNumber, columnNumber.Value);
        }
        else
        {
            ViewModel.Editor.NavigateToLine(lineNumber);
        }

        RefreshActiveEditorBookmarks();
        return Task.CompletedTask;
    }

    private Task NavigateToLocationAsync(int lineNumber, int? columnNumber)
    {
        if (ViewModel == null)
        {
            return Task.CompletedTask;
        }

        if (columnNumber.HasValue)
        {
            ViewModel.Editor.NavigateToLocation(lineNumber, columnNumber.Value);
        }
        else
        {
            ViewModel.Editor.NavigateToLine(lineNumber);
        }

        RefreshActiveEditorBookmarks();
        return Task.CompletedTask;
    }

    private void AddSymbolEntries(List<QuickPickEntry> results, string query, string text, string sourceLabel, Func<int, int?, Task> navigateAsync)
    {
        foreach (var symbol in ExtractSymbols(text))
        {
            var entry = new QuickPickEntry(
                $"Symbol: {symbol.Name}",
                () => navigateAsync(symbol.LineNumber, symbol.ColumnNumber),
                $"{sourceLabel}:{symbol.LineNumber} {symbol.Kind}",
                $"symbol {symbol.Kind} {symbol.Name} {sourceLabel} {symbol.SearchText}");

            if (MatchesQuickPickQuery(entry, query))
            {
                results.Add(entry);
            }

            if (results.Count >= MaxGotoAnythingResults)
            {
                return;
            }
        }
    }

    private void AddTextEntries(List<QuickPickEntry> results, string query, string text, string sourceLabel, Func<int, int?, Task> navigateAsync)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        foreach (var match in ExtractTextMatches(text, query))
        {
            var entry = new QuickPickEntry(
                $"Text: {sourceLabel}:{match.LineNumber}",
                () => navigateAsync(match.LineNumber, match.ColumnNumber),
                match.Preview,
                $"text {sourceLabel} {match.LineNumber} {match.Preview}");

            if (MatchesQuickPickQuery(entry, query))
            {
                results.Add(entry);
            }

            if (results.Count >= MaxGotoAnythingResults)
            {
                return;
            }
        }
    }

    private static IEnumerable<(string Name, string Kind, int LineNumber, int? ColumnNumber, string SearchText)> ExtractSymbols(string text)
    {
        var results = new List<(string Name, string Kind, int LineNumber, int? ColumnNumber, string SearchText)>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return results;
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (TryAddSymbolMatch(results, seen, TypeSymbolRegex.Match(line), "Type", index + 1, line))
            {
                continue;
            }

            if (TryAddSymbolMatch(results, seen, MethodSymbolRegex.Match(line), "Method", index + 1, line))
            {
                continue;
            }

            if (TryAddSymbolMatch(results, seen, FunctionAssignmentRegex.Match(line), "Function", index + 1, line))
            {
                continue;
            }

            var headingMatch = MarkdownHeadingRegex.Match(line);
            if (headingMatch.Success)
            {
                var heading = headingMatch.Groups[2].Value.Trim();
                if (!string.IsNullOrWhiteSpace(heading))
                {
                    var key = $"heading:{index + 1}:{heading}";
                    if (seen.Add(key))
                    {
                        results.Add((heading, "Heading", index + 1, line.IndexOf(heading, StringComparison.Ordinal) + 1, line.Trim()));
                    }
                }

                continue;
            }

            var tagMatch = XmlTagRegex.Match(line);
            if (tagMatch.Success)
            {
                var tag = tagMatch.Groups[1].Value;
                if (!tag.StartsWith("/", StringComparison.Ordinal) && !tag.StartsWith("!", StringComparison.Ordinal) && !tag.StartsWith("?", StringComparison.Ordinal))
                {
                    var key = $"tag:{index + 1}:{tag}";
                    if (seen.Add(key))
                    {
                        results.Add((tag, "Tag", index + 1, tagMatch.Groups[1].Index + 1, line.Trim()));
                    }
                }
            }
        }

        return results;
    }

    private static bool TryAddSymbolMatch(List<(string Name, string Kind, int LineNumber, int? ColumnNumber, string SearchText)> results, HashSet<string> seen, Match match, string kind, int lineNumber, string line)
    {
        if (!match.Success)
        {
            return false;
        }

        var nameGroup = match.Groups[match.Groups.Count - 1];
        var name = nameGroup.Value;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var key = $"{kind}:{lineNumber}:{name}";
        if (!seen.Add(key))
        {
            return true;
        }

        results.Add((name, kind, lineNumber, nameGroup.Index + 1, line.Trim()));
        return true;
    }

    private static IEnumerable<(int LineNumber, int? ColumnNumber, string Preview)> ExtractTextMatches(string text, string query)
    {
        var results = new List<(int LineNumber, int? ColumnNumber, string Preview)>();
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(query))
        {
            return results;
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var column = line.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (column < 0)
            {
                continue;
            }

            results.Add((index + 1, column + 1, line.Trim()));
            if (results.Count >= MaxGotoAnythingMatchesPerFile)
            {
                break;
            }
        }

        return results;
    }

    private static string TryReadAllText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetTabDisplayPath(EditorTabViewModel tab, IReadOnlyCollection<string> roots)
    {
        if (!string.IsNullOrWhiteSpace(tab.Editor.FilePath))
        {
            return FormatPathForDisplay(tab.Editor.FilePath, roots);
        }

        return tab.Editor.FileName;
    }

    private static Task ExecuteQuickActionAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    private QuickPickEntry CreateSettingToggleEntry(string label, string searchTerms, Func<bool> getter, Action<bool> setter)
    {
        var enabled = getter();
        return new QuickPickEntry(
            label,
            () => ExecuteQuickActionAsync(() => setter(!getter())),
            enabled ? "Currently On" : "Currently Off",
            $"{searchTerms} {(enabled ? "on enabled checked" : "off disabled unchecked")} {label}");
    }

    private QuickPickEntry CreateSettingOptionEntry(string label, bool isCurrent, string searchTerms, Action apply)
    {
        return new QuickPickEntry(
            label,
            () => ExecuteQuickActionAsync(apply),
            isCurrent ? "Current" : "Available",
            $"{searchTerms} {(isCurrent ? "current selected active" : "available option")} {label}");
    }

    private async Task OpenRecentProjectAsync(RecentProjectItem project)
    {
        if (ViewModel == null)
        {
            return;
        }

        if (project.IsWorkspace)
        {
            ViewModel.Explorer.LoadWorkspace(project.Path);
            ViewModel.AddRecentProject(project.Path);
            return;
        }

        await OpenFolderWithWorkspaceDetectionAsync(project.Path);
    }

    private string GetCurrentBookmarkScopePath()
    {
        if (ViewModel == null)
        {
            return string.Empty;
        }

        var workspacePath = ViewModel.GetCurrentWorkspacePathData();
        if (!string.IsNullOrWhiteSpace(workspacePath))
        {
            return workspacePath;
        }

        return ViewModel.GetCurrentFolderPathData();
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "bookmarks";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }

    private static string FormatPathForDisplay(string? path, IReadOnlyCollection<string> roots)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Untitled";
        }

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            try
            {
                var relative = Path.GetRelativePath(root, path);
                if (!relative.StartsWith("..", StringComparison.Ordinal))
                {
                    return relative;
                }
            }
            catch
            {
            }
        }

        return Path.GetFileName(path);
    }

    private void OnEditorScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.OffsetProperty ||
            e.Property == ScrollViewer.ExtentProperty ||
            e.Property == ScrollViewer.ViewportProperty)
        {
            UpdateScrollbar();
        }
    }

    private void UpdateScrollbar()
    {
        if (_scrollbarCanvas == null || _scrollbarThumb == null || _scrollbarCursorIndicator == null)
            return;

        var canvasHeight = _scrollbarCanvas.Bounds.Height;
        var canvasWidth = _scrollbarCanvas.Bounds.Width;
        if (canvasHeight <= 0 || canvasWidth <= 0) return;

        if (_activeEditorScrollViewer == null)
        {
            _scrollbarThumb.IsVisible = false;
            _scrollbarCursorIndicator.IsVisible = false;
            return;
        }

        var extentH = _activeEditorScrollViewer.Extent.Height;
        var viewportH = _activeEditorScrollViewer.Viewport.Height;
        var offsetY = _activeEditorScrollViewer.Offset.Y;

        if (extentH <= 0 || viewportH <= 0)
        {
            _scrollbarThumb.IsVisible = false;
            _scrollbarCursorIndicator.IsVisible = false;
            return;
        }

        _scrollbarThumb.IsVisible = true;

        // Thumb size depends on document length, not window height.
        var lineCountForSize = Math.Max(1, _activeEditorModel?.LineCount ?? ViewModel?.Editor.LineCount ?? 1);
        var rawThumbHeight = GetIndicatorHeightByLineCount(lineCountForSize, MinDocumentThumbHeight, MaxDocumentThumbHeight);
        var thumbHeight = Math.Clamp(rawThumbHeight, MinDocumentThumbHeight, canvasHeight);

        // Thumb position based on scroll offset
        var maxOffset = extentH - viewportH;
        var scrollRatio = maxOffset > 0 ? offsetY / maxOffset : 0;
        var thumbTop = scrollRatio * (canvasHeight - thumbHeight);

        Canvas.SetTop(_scrollbarThumb, thumbTop);
        _scrollbarThumb.Width = canvasWidth;
        _scrollbarThumb.Height = thumbHeight;

        // Cursor position indicator
        var activeEditor = _activeEditorModel ?? ViewModel?.Editor;
        if (activeEditor != null)
        {
            _scrollbarCursorIndicator.IsVisible = true;
            var editorControl = _activeEditorView?.Editor;
            var lineCount = Math.Max(1, editorControl?.Document?.LineCount ?? activeEditor.LineCount);
            var caretLine = activeEditor.CaretLine;
            if (editorControl?.Document != null)
            {
                var textLength = editorControl.Document.TextLength;
                var caretOffset = Math.Clamp(editorControl.CaretOffset, 0, Math.Max(0, textLength));
                caretLine = Math.Clamp(editorControl.Document.GetLineByOffset(caretOffset).LineNumber, 1, lineCount);
            }
            else
            {
                caretLine = Math.Clamp(caretLine, 1, lineCount);
            }
            var caretRatio = (double)(caretLine - 1) / Math.Max(1, lineCount - 1);
            var cursorY = caretRatio * (canvasHeight - 2);
            Canvas.SetTop(_scrollbarCursorIndicator, cursorY);
            _scrollbarCursorIndicator.Width = canvasWidth;
        }
        else
        {
            _scrollbarCursorIndicator.IsVisible = false;
        }
    }

    private void OnScrollbarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_scrollbarCanvas == null || _activeEditorScrollViewer == null || _scrollbarThumb == null) return;

        var point = e.GetPosition(_scrollbarCanvas);
        var thumbTop = Canvas.GetTop(_scrollbarThumb);
        var thumbHeight = _scrollbarThumb.Height;

        // If click is on thumb, start dragging
        if (point.Y >= thumbTop && point.Y <= thumbTop + thumbHeight)
        {
            _scrollbarDragging = true;
            _scrollbarDragStartY = point.Y;
            _scrollbarDragStartOffset = _activeEditorScrollViewer.Offset.Y;
            e.Pointer.Capture(_scrollbarCanvas);
        }
        else
        {
            // Click on track — jump to position
            ScrollToCanvasPosition(point.Y);
        }

        e.Handled = true;
    }

    private void OnScrollbarPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_scrollbarDragging || _scrollbarCanvas == null || _activeEditorScrollViewer == null || _scrollbarThumb == null)
            return;

        var point = e.GetPosition(_scrollbarCanvas);
        var canvasHeight = _scrollbarCanvas.Bounds.Height;
        var thumbHeight = _scrollbarThumb.Height;
        var maxThumbTop = canvasHeight - thumbHeight;
        if (maxThumbTop <= 0) return;

        var deltaY = point.Y - _scrollbarDragStartY;
        var maxOffset = _activeEditorScrollViewer.Extent.Height - _activeEditorScrollViewer.Viewport.Height;
        if (maxOffset <= 0) return;

        var newOffset = _scrollbarDragStartOffset + (deltaY / maxThumbTop) * maxOffset;
        newOffset = Math.Clamp(newOffset, 0, maxOffset);
        _activeEditorScrollViewer.Offset = new Vector(_activeEditorScrollViewer.Offset.X, newOffset);
    }

    private void OnScrollbarPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _scrollbarDragging = false;
        e.Pointer.Capture(null);
    }

    private void OnScrollbarPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_activeEditorScrollViewer == null) return;

        var lineHeight = ViewModel?.Editor?.Settings.EditorFontSize ?? 14;
        var delta = -e.Delta.Y * lineHeight * 3;
        var maxOffset = _activeEditorScrollViewer.Extent.Height - _activeEditorScrollViewer.Viewport.Height;
        var newOffset = Math.Clamp(_activeEditorScrollViewer.Offset.Y + delta, 0, maxOffset);
        _activeEditorScrollViewer.Offset = new Vector(_activeEditorScrollViewer.Offset.X, newOffset);
        e.Handled = true;
    }

    private void ScrollToCanvasPosition(double y)
    {
        if (_scrollbarCanvas == null || _activeEditorScrollViewer == null || _scrollbarThumb == null) return;

        var canvasHeight = _scrollbarCanvas.Bounds.Height;
        var thumbHeight = _scrollbarThumb.Height;
        if (canvasHeight <= 0) return;

        // Center the thumb at click position
        var thumbTop = y - thumbHeight / 2;
        var maxThumbTop = canvasHeight - thumbHeight;
        if (maxThumbTop <= 0) return;

        var ratio = Math.Clamp(thumbTop / maxThumbTop, 0, 1);
        var maxOffset = _activeEditorScrollViewer.Extent.Height - _activeEditorScrollViewer.Viewport.Height;
        _activeEditorScrollViewer.Offset = new Vector(_activeEditorScrollViewer.Offset.X, ratio * maxOffset);
    }

    private static T? FindVisualDescendant<T>(Control? root) where T : Control
    {
        if (root == null) return null;
        if (root is T match) return match;

        foreach (var child in root.GetVisualChildren())
        {
            if (child is T found) return found;
            if (child is Control control)
            {
                var result = FindVisualDescendant<T>(control);
                if (result != null) return result;
            }
        }

        return null;
    }

    private static double GetIndicatorHeightByLineCount(int lineCount, double minHeight, double maxHeight)
    {
        var normalized = Math.Max(1.0, lineCount / 160.0);
        var scaled = maxHeight / Math.Sqrt(normalized);
        return Math.Clamp(scaled, minHeight, maxHeight);
    }
}

public enum SavePromptResult
{
    Save,
    SaveAs,
    DontSave,
    Cancel
}

sealed class QuickPickEntry
{
    public QuickPickEntry(string label, Func<Task> executeAsync, string detail = "", string? searchText = null)
    {
        Label = label;
        ExecuteAsync = executeAsync;
        Detail = detail;
        SearchText = string.IsNullOrWhiteSpace(searchText)
            ? string.Join(' ', new[] { label, detail }.Where(value => !string.IsNullOrWhiteSpace(value)))
            : searchText;
    }

    public string Label { get; }

    public string Detail { get; }

    public string SearchText { get; }

    public Func<Task> ExecuteAsync { get; }

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Detail)
            ? Label
            : $"{Label}  ({Detail})";
    }
}

file sealed class BookmarkExchangeData
{
    public string Format { get; set; } = "notepadpro-bookmarks-v1";

    public string ScopeName { get; set; } = string.Empty;

    public string ScopePath { get; set; } = string.Empty;

    public string ExportTarget { get; set; } = "current-scope";

    public List<BookmarkItemData> Bookmarks { get; set; } = new();
}

enum BookmarkImportMode
{
    Cancel,
    Merge,
    Replace
}

enum BookmarkExportTarget
{
    Cancel,
    CurrentFile,
    CurrentScope
}

enum OutOfScopeBookmarkChoice
{
    Cancel,
    ImportAll,
    InScopeOnly
}