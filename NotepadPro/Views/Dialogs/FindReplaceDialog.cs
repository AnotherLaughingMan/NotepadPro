using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace NotepadPro.Views.Dialogs;

public enum FindReplaceAction
{
    FindNext,
    ReplaceNext,
    ReplaceAll,
    Cancel
}

public sealed class FindReplaceResult
{
    public FindReplaceResult(FindReplaceAction action, string query, string replacement, bool matchCase, bool wholeWord)
    {
        Action = action;
        Query = query;
        Replacement = replacement;
        MatchCase = matchCase;
        WholeWord = wholeWord;
    }

    public FindReplaceAction Action { get; }

    public string Query { get; }

    public string Replacement { get; }

    public bool MatchCase { get; }

    public bool WholeWord { get; }
}

public sealed class FindReplaceDialog : Window
{
    private readonly TextBox _queryBox;
    private readonly TextBox _replaceBox;
    private readonly CheckBox _matchCase;
    private readonly CheckBox _wholeWord;
    private readonly bool _includeReplace;

    public FindReplaceDialog(bool includeReplace)
    {
        _includeReplace = includeReplace;
        Title = includeReplace ? "Replace" : "Find";
        Width = 420;
        Height = includeReplace ? 260 : 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Res("PanelBackground");

        var root = new StackPanel { Margin = new Thickness(16), Spacing = 12 };

        root.Children.Add(new TextBlock { Text = "Find", FontWeight = FontWeight.SemiBold, Foreground = Res("ForegroundPrimary") });
        _queryBox = new TextBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
        root.Children.Add(_queryBox);

        if (includeReplace)
        {
            root.Children.Add(new TextBlock { Text = "Replace With", FontWeight = FontWeight.SemiBold, Foreground = Res("ForegroundPrimary") });
            _replaceBox = new TextBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
            root.Children.Add(_replaceBox);
        }
        else
        {
            _replaceBox = new TextBox { IsVisible = false };
        }

        _matchCase = new CheckBox { Content = "Match Case" };
        _wholeWord = new CheckBox { Content = "Whole Word" };
        root.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { _matchCase, _wholeWord }
        });

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        if (includeReplace)
        {
            buttonPanel.Children.Add(BuildIconButton("\uE8C8", "Replace", (_, _) => Close(BuildResult(FindReplaceAction.ReplaceNext))));
            buttonPanel.Children.Add(BuildIconButton("\uE8C7", "Replace All", (_, _) => Close(BuildResult(FindReplaceAction.ReplaceAll))));
        }

        buttonPanel.Children.Add(BuildIconButton("\uE721", "Find Next", (_, _) => Close(BuildResult(FindReplaceAction.FindNext))));
        buttonPanel.Children.Add(BuildIconButton("\uE711", "Cancel", (_, _) => Close(BuildResult(FindReplaceAction.Cancel))));

        root.Children.Add(buttonPanel);
        Content = root;
    }

    public void SetDefaults(string query, string replacement, bool matchCase, bool wholeWord)
    {
        _queryBox.Text = query;
        _replaceBox.Text = replacement;
        _matchCase.IsChecked = matchCase;
        _wholeWord.IsChecked = wholeWord;
    }

    public static async Task<FindReplaceResult?> ShowAsync(Window owner, bool includeReplace, string query, string replacement, bool matchCase, bool wholeWord)
    {
        var dialog = new FindReplaceDialog(includeReplace);
        dialog.SetDefaults(query, replacement, matchCase, wholeWord);
        var result = await dialog.ShowDialog<FindReplaceResult?>(owner);
        return result;
    }

    private Button BuildIconButton(string glyph, string tooltip, EventHandler<RoutedEventArgs> onClick)
    {
        var button = new Button
        {
            Width = 32,
            Height = 32,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Content = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 12,
                Foreground = Res("ForegroundPrimary"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        ToolTip.SetTip(button, tooltip);
        button.Click += onClick;
        return button;
    }

    private FindReplaceResult BuildResult(FindReplaceAction action)
    {
        return new FindReplaceResult(
            action,
            _queryBox.Text ?? string.Empty,
            _replaceBox.Text ?? string.Empty,
            _matchCase.IsChecked ?? false,
            _wholeWord.IsChecked ?? false);
    }

    private static ISolidColorBrush Res(string key)
    {
        if (Application.Current!.Resources.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) && value is ISolidColorBrush brush)
            return brush;
        return new SolidColorBrush(Colors.Magenta);
    }
}
