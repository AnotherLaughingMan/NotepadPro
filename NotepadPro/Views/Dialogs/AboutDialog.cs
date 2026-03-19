using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace NotepadPro.Views.Dialogs;

public sealed class AboutDialog : Window
{
    public AboutDialog()
    {
        Title = "About Notepad Pro";
        Width = 520;
        Height = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Res("PanelBackground");

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionText = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";

        var root = new StackPanel { Margin = new Thickness(24), Spacing = 8 };

        root.Children.Add(new TextBlock
        {
            Text = "Notepad Pro",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            Foreground = Res("ForegroundPrimary")
        });

        root.Children.Add(new TextBlock
        {
            Text = $"Version {versionText}",
            FontSize = 13,
            Foreground = Res("ForegroundInactive")
        });

        root.Children.Add(new Separator { Margin = new Thickness(0, 6) });

        root.Children.Add(new TextBlock
        {
            Text = "A modern text editor built with Avalonia UI, inspired by VS Code and Windows 11 Notepad.",
            FontSize = 12,
            Foreground = Res("ForegroundSecondary"),
            TextWrapping = TextWrapping.Wrap
        });

        root.Children.Add(new TextBlock
        {
            Text = "Main Contributor & Author: AnotherLaughingMan",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = Res("ForegroundPrimary"),
            Margin = new Thickness(0, 4, 0, 0)
        });

        root.Children.Add(new TextBlock
        {
            Text = "Frameworks and APIs",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = Res("ForegroundPrimary"),
            Margin = new Thickness(0, 6, 0, 0)
        });

        root.Children.Add(new TextBlock
        {
            Text = "• .NET 9\n• Avalonia UI + AvaloniaEdit\n• ReactiveUI\n• WebView2 + Monaco Editor\n• TypeScript + Vite webview pipeline\n• TextMate grammar theming",
            FontSize = 11,
            Foreground = Res("ForegroundSecondary"),
            TextWrapping = TextWrapping.Wrap
        });

        root.Children.Add(new Separator { Margin = new Thickness(0, 6) });

        root.Children.Add(new TextBlock
        {
            Text = "© 2026 Notepad Pro contributors",
            FontSize = 11,
            Foreground = Res("ForegroundMuted")
        });

        var okButton = new Button
        {
            Width = 84,
            Height = 32,
            Content = "OK",
            HorizontalAlignment = HorizontalAlignment.Right,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Background = Res("ButtonAccentBrush"),
            Foreground = Res("ForegroundOnAccent"),
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 8, 0, 0)
        };
        okButton.Click += (_, _) => Close();
        root.Children.Add(okButton);

        Content = root;
    }

    private static ISolidColorBrush Res(string key)
    {
        if (Application.Current!.Resources.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) && value is ISolidColorBrush brush)
            return brush;
        return new SolidColorBrush(Colors.Magenta);
    }
}
