using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace NotepadPro.Views.Dialogs;

public sealed class GoToLineDialog : Window
{
    private readonly TextBox _lineBox;

    public GoToLineDialog()
    {
        Title = "Go To Line";
        Width = 320;
        Height = 180;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Res("PanelBackground");

        var root = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
        root.Children.Add(new TextBlock { Text = "Line Number", Foreground = Res("ForegroundPrimary") });
        _lineBox = new TextBox
        {
            Watermark = "Enter a line number",
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        root.Children.Add(_lineBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        var okButton = BuildIconButton("\uE73E", "Go", (_, _) => Close(ParseLine()));
        var cancelButton = BuildIconButton("\uE711", "Cancel", (_, _) => Close(null));

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        root.Children.Add(buttonPanel);

        Content = root;
    }

    public void SetDefault(int line)
    {
        _lineBox.Text = line.ToString();
    }

    public static async Task<int?> ShowAsync(Window owner, int currentLine)
    {
        var dialog = new GoToLineDialog();
        dialog.SetDefault(currentLine);
        return await dialog.ShowDialog<int?>(owner);
    }

    private int? ParseLine()
    {
        if (int.TryParse(_lineBox.Text, out var line) && line > 0)
        {
            return line;
        }

        return null;
    }

    private static Button BuildIconButton(string glyph, string tooltip, EventHandler<RoutedEventArgs> onClick)
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

    private static ISolidColorBrush Res(string key)
    {
        if (Application.Current!.Resources.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) && value is ISolidColorBrush brush)
            return brush;
        return new SolidColorBrush(Colors.Magenta);
    }
}
