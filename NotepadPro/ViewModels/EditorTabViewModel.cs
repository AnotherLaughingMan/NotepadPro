using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using Avalonia.Media;
using NotepadPro.Services;
using ReactiveUI;

namespace NotepadPro.ViewModels;

public sealed class EditorTabViewModel : ViewModelBase
{
    private readonly ObservableAsPropertyHelper<string> _title;
    private readonly ObservableAsPropertyHelper<TabFileTypeIcon> _fileTypeIcon;
    private readonly ObservableAsPropertyHelper<bool> _isDirty;
    private bool _isPinned;

    private static readonly TabFileTypeIcon PlainTextIcon = new("\uE8A5", "Plain Text", CreateBrush("#9E9E9E"));
    private static readonly TabFileTypeIcon MarkdownIcon = new("\uE8A1", "Markdown", CreateBrush("#6A9955"));
    private static readonly TabFileTypeIcon CSharpIcon = new("\uE943", "C#", CreateBrush("#519ABA"));
    private static readonly TabFileTypeIcon CIcon = new("\uE943", "C", CreateBrush("#689F63"));
    private static readonly TabFileTypeIcon CppIcon = new("\uE943", "C++", CreateBrush("#9B59B6"));
    private static readonly TabFileTypeIcon XmlIcon = new("\uE943", "XML", CreateBrush("#D19A66"));
    private static readonly TabFileTypeIcon XamlIcon = new("\uE943", "XAML", CreateBrush("#4EC9B0"));
    private static readonly TabFileTypeIcon AxamlIcon = new("\uE943", "AXAML", CreateBrush("#3FB7D6"));
    private static readonly TabFileTypeIcon JsonIcon = new("\uE943", "JSON", CreateBrush("#D7BA7D"));
    private static readonly TabFileTypeIcon JavaScriptIcon = new("\uE943", "JavaScript", CreateBrush("#D7BA7D"));
    private static readonly TabFileTypeIcon TypeScriptIcon = new("\uE943", "TypeScript", CreateBrush("#4FC1FF"));
    private static readonly TabFileTypeIcon HtmlIcon = new("\uE943", "HTML", CreateBrush("#F16529"));
    private static readonly TabFileTypeIcon CssIcon = new("\uE943", "CSS", CreateBrush("#42A5F5"));
    private static readonly TabFileTypeIcon PowerShellIcon = new("\uE943", "PowerShell", CreateBrush("#2C7DD1"));
    private static readonly TabFileTypeIcon ShellIcon = new("\uE943", "Shell", CreateBrush("#89D185"));
    private static readonly TabFileTypeIcon PythonIcon = new("\uE943", "Python", CreateBrush("#EBCB8B"));
    private static readonly TabFileTypeIcon YamlIcon = new("\uE943", "YAML", CreateBrush("#C586C0"));
    private static readonly TabFileTypeIcon SqlIcon = new("\uE943", "SQL", CreateBrush("#C678DD"));
    private static readonly TabFileTypeIcon GenericCodeIcon = new("\uE943", "Code", CreateBrush("#C586C0"));

    private static readonly IReadOnlyDictionary<string, TabFileTypeIcon> ExtensionIcons = new Dictionary<string, TabFileTypeIcon>(StringComparer.OrdinalIgnoreCase)
    {
        [".txt"] = PlainTextIcon,
        [".text"] = PlainTextIcon,
        [".log"] = PlainTextIcon,
        [".md"] = MarkdownIcon,
        [".markdown"] = MarkdownIcon,
        [".mdown"] = MarkdownIcon,
        [".mkd"] = MarkdownIcon,
        [".cs"] = CSharpIcon,
        [".csx"] = CSharpIcon,
        [".cake"] = CSharpIcon,
        [".c"] = CIcon,
        [".cpp"] = CppIcon,
        [".cc"] = CppIcon,
        [".cxx"] = CppIcon,
        [".c++"] = CppIcon,
        [".h"] = CppIcon,
        [".hh"] = CppIcon,
        [".hpp"] = CppIcon,
        [".hxx"] = CppIcon,
        [".inl"] = CppIcon,
        [".ipp"] = CppIcon,
        [".tpp"] = CppIcon,
        [".xml"] = XmlIcon,
        [".xsd"] = XmlIcon,
        [".xsl"] = XmlIcon,
        [".xslt"] = XmlIcon,
        [".svg"] = XmlIcon,
        [".resx"] = XmlIcon,
        [".props"] = XmlIcon,
        [".targets"] = XmlIcon,
        [".config"] = XmlIcon,
        [".xaml"] = XamlIcon,
        [".axaml"] = AxamlIcon,
        [".json"] = JsonIcon,
        [".jsonc"] = JsonIcon,
        [".json5"] = JsonIcon,
        [".js"] = JavaScriptIcon,
        [".jsx"] = JavaScriptIcon,
        [".ts"] = TypeScriptIcon,
        [".tsx"] = TypeScriptIcon,
        [".html"] = HtmlIcon,
        [".htm"] = HtmlIcon,
        [".css"] = CssIcon,
        [".scss"] = CssIcon,
        [".less"] = CssIcon,
        [".ps1"] = PowerShellIcon,
        [".psm1"] = PowerShellIcon,
        [".psd1"] = PowerShellIcon,
        [".ps1xml"] = PowerShellIcon,
        [".sh"] = ShellIcon,
        [".bash"] = ShellIcon,
        [".zsh"] = ShellIcon,
        [".fish"] = ShellIcon,
        [".py"] = PythonIcon,
        [".yml"] = YamlIcon,
        [".yaml"] = YamlIcon,
        [".sql"] = SqlIcon,
    };

    private static readonly IReadOnlyDictionary<string, TabFileTypeIcon> LanguageIcons = new Dictionary<string, TabFileTypeIcon>(StringComparer.OrdinalIgnoreCase)
    {
        ["Plain Text"] = PlainTextIcon,
        ["Markdown"] = MarkdownIcon,
        ["C#"] = CSharpIcon,
        ["C"] = CIcon,
        ["C++"] = CppIcon,
        ["XML"] = XmlIcon,
        ["XAML"] = XamlIcon,
        ["AXAML"] = AxamlIcon,
        ["JSON"] = JsonIcon,
        ["JavaScript"] = JavaScriptIcon,
        ["TypeScript"] = TypeScriptIcon,
        ["HTML"] = HtmlIcon,
        ["CSS"] = CssIcon,
        ["SCSS"] = CssIcon,
        ["LESS"] = CssIcon,
        ["PowerShell"] = PowerShellIcon,
        ["Shell"] = ShellIcon,
        ["Python"] = PythonIcon,
        ["YAML"] = YamlIcon,
        ["SQL"] = SqlIcon,
    };

    public EditorTabViewModel(EditorViewModel editor, bool isWelcomeTab = false)
    {
        Editor = editor;
        IsWelcomeTab = isWelcomeTab;

        _title = Editor.WhenAnyValue(x => x.FileName)
            .ToProperty(this, x => x.Title);

        _fileTypeIcon = Editor.WhenAnyValue(x => x.Language, x => x.FileName,
                (language, fileName) => ResolveFileTypeIcon(language, fileName))
            .ToProperty(this, x => x.FileTypeIcon);

        _isDirty = Editor.WhenAnyValue(x => x.HasUnsavedChanges)
            .ToProperty(this, x => x.IsDirty);
    }

    public EditorViewModel Editor { get; }

    public bool IsWelcomeTab { get; }

    public string Title => _title.Value;

    public TabFileTypeIcon FileTypeIcon => IsWelcomeTab ? PlainTextIcon : _fileTypeIcon.Value;

    public bool IsFileTypeIconVisible => !IsWelcomeTab;

    public bool IsFileTypeBadgeVisible => IsFileTypeIconVisible;

    public bool IsDirty => _isDirty.Value;

    public bool IsCloseButtonVisible => !IsWelcomeTab;

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            this.RaiseAndSetIfChanged(ref _isPinned, value);
            this.RaisePropertyChanged(nameof(IsPinnedIndicatorVisible));
        }
    }

    public bool IsPinnedIndicatorVisible => IsPinned && !IsWelcomeTab;

    private static TabFileTypeIcon ResolveFileTypeIcon(string language, string fileName)
    {
        var normalizedLanguage = TextMateLanguageService.NormalizeDisplayLanguage(language);
        var extension = Path.GetExtension(fileName ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(extension)
            && ExtensionIcons.TryGetValue(extension, out var extensionIcon))
        {
            return extensionIcon;
        }

        if (LanguageIcons.TryGetValue(normalizedLanguage, out var languageIcon))
        {
            return languageIcon;
        }

        if (string.Equals(normalizedLanguage, "Plain Text", StringComparison.OrdinalIgnoreCase))
        {
            return PlainTextIcon;
        }

        return new TabFileTypeIcon(GenericCodeIcon.Glyph, normalizedLanguage, GenericCodeIcon.Brush);
    }

    private static SolidColorBrush CreateBrush(string hexColor)
    {
        return new SolidColorBrush(Color.Parse(hexColor));
    }

    public sealed record TabFileTypeIcon(string Glyph, string Label, IBrush Brush);
}
