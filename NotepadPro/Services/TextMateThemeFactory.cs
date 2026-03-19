using System.Collections.Generic;
using TextMateSharp.Themes;

namespace NotepadPro.Services;

public static class TextMateThemeFactory
{
    public static IRawTheme? Create(string? appThemeName)
    {
        if (string.IsNullOrWhiteSpace(appThemeName))
        {
            return null;
        }

        if (!ThemeService.All.TryGetValue(appThemeName, out var theme))
        {
            return null;
        }

        var tokenSettings = new List<IRawThemeSetting>
        {
            CreateRule(
                name: "Default",
                scope: null,
                foreground: theme.ForegroundPrimary,
                background: theme.EditorBackground),

            CreateRule(
                name: "Comments",
                scope: "comment, punctuation.definition.comment",
                foreground: theme.SyntaxComment,
                background: null,
                fontStyle: "italic"),

            CreateRule(
                name: "Keywords",
                scope: "keyword, storage, keyword.control, keyword.operator",
                foreground: theme.SyntaxKeyword,
                background: null),

            CreateRule(
                name: "Strings",
                scope: "string, punctuation.definition.string",
                foreground: theme.SyntaxString,
                background: null),

            CreateRule(
                name: "URLs",
                scope: "constant.other.url, constant.other.uri, constant.other.reference.link, constant.other.reference.link.markdown, string.other.url, string.other.link, string.other.link.title, string.other.link.title.markdown, markup.underline.link, markup.underline.link.markdown, markup.underline.link.image.markdown, meta.link.inline, meta.link.inline.markdown, meta.link.reference, meta.link.reference.markdown, text.html.basic meta.link, text.html.markdown meta.link, markup.fenced_code.block.markdown constant.other.url, source.json string.unquoted, source.xml string.quoted, text.xml string.quoted, source.css string.unquoted.url, source.css constant.other.url, comment.block.documentation constant.other.url, comment.line.documentation constant.other.url, text.plain constant.other.url",
                foreground: theme.SyntaxLink,
                background: null,
                fontStyle: "underline"),

            CreateRule(
                name: "Numbers",
                scope: "constant.numeric",
                foreground: theme.SyntaxNumber,
                background: null),

            CreateRule(
                name: "Types",
                scope: "entity.name.type, support.type, storage.type",
                foreground: theme.SyntaxType,
                background: null),

            CreateRule(
                name: "Functions",
                scope: "entity.name.function, support.function, meta.function-call",
                foreground: theme.AccentBrush,
                background: null),

            CreateRule(
                name: "Methods",
                scope: "meta.method, variable.function, entity.name.method",
                foreground: theme.AccentBrush,
                background: null),

            CreateRule(
                name: "Classes",
                scope: "entity.name.class, entity.name.struct, entity.name.interface, entity.name.namespace",
                foreground: theme.SyntaxType,
                background: null,
                fontStyle: "bold"),

            CreateRule(
                name: "Parameters",
                scope: "variable.parameter, variable.parameter.function",
                foreground: theme.ForegroundSecondary,
                background: null),

            CreateRule(
                name: "Properties",
                scope: "variable.other.property, variable.object.property, entity.name.tag",
                foreground: theme.ForegroundPrimary,
                background: null),

            CreateRule(
                name: "Constants",
                scope: "constant.language, constant.character, constant.other",
                foreground: theme.SyntaxNumber,
                background: null,
                fontStyle: "bold"),

            CreateRule(
                name: "Enums",
                scope: "entity.name.enum, variable.other.enummember",
                foreground: theme.SyntaxType,
                background: null),

            CreateRule(
                name: "Operators",
                scope: "keyword.operator, punctuation.separator, punctuation.terminator",
                foreground: theme.ForegroundMuted,
                background: null),

            CreateRule(
                name: "Preprocessor",
                scope: "meta.preprocessor, keyword.control.directive",
                foreground: theme.ForegroundInactive,
                background: null),

            CreateRule(
                name: "Markdown Headings",
                scope: "markup.heading, entity.name.section.markdown",
                foreground: theme.SyntaxType,
                background: null,
                fontStyle: "bold"),

            CreateRule(
                name: "Markdown Emphasis",
                scope: "markup.bold, markup.italic",
                foreground: theme.SyntaxKeyword,
                background: null),

            CreateRule(
                name: "Markdown Links",
                scope: "markup.underline.link, markup.underline.link.markdown, string.other.link.title.markdown, constant.other.reference.link.markdown, meta.link.inline.markdown",
                foreground: theme.SyntaxLink,
                background: null,
                fontStyle: "underline"),

            CreateRule(
                name: "Markdown Blockquote",
                scope: "markup.quote.markdown",
                foreground: theme.ForegroundMuted,
                background: null,
                fontStyle: "italic"),

            CreateRule(
                name: "JSON Property",
                scope: "support.type.property-name.json, meta.structure.dictionary.key.json, meta.object-literal.key",
                foreground: theme.SyntaxType,
                background: null),

            CreateRule(
                name: "JSON Value",
                scope: "meta.structure.dictionary.value.json, constant.language.boolean.json, constant.language.null.json",
                foreground: theme.ForegroundPrimary,
                background: null),

            CreateRule(
                name: "XML Tags",
                scope: "entity.name.tag.xml, entity.name.tag, punctuation.definition.tag",
                foreground: theme.SyntaxKeyword,
                background: null),

            CreateRule(
                name: "XML Attributes",
                scope: "entity.other.attribute-name, entity.other.attribute-name.xml",
                foreground: theme.SyntaxType,
                background: null),

            CreateRule(
                name: "XML Attribute Values",
                scope: "string.quoted.double.xml, string.quoted.single.xml",
                foreground: theme.SyntaxLink,
                background: null)
        };

        var guiColors = new List<KeyValuePair<string, object>>();

        return new RawTheme(
            name: $"Notepad Pro {theme.Name}",
            settings: tokenSettings,
            tokenColors: tokenSettings,
            guiColors: guiColors);
    }

    private static IRawThemeSetting CreateRule(string name, object? scope, string? foreground, string? background, object? fontStyle = null)
    {
        return new RawThemeSetting(name, scope, new ThemeSetting(fontStyle, background, foreground));
    }

    private sealed class RawTheme : IRawTheme
    {
        private readonly string _name;
        private readonly ICollection<IRawThemeSetting> _settings;
        private readonly ICollection<IRawThemeSetting> _tokenColors;
        private readonly ICollection<KeyValuePair<string, object>> _guiColors;

        public RawTheme(
            string name,
            ICollection<IRawThemeSetting> settings,
            ICollection<IRawThemeSetting> tokenColors,
            ICollection<KeyValuePair<string, object>> guiColors)
        {
            _name = name;
            _settings = settings;
            _tokenColors = tokenColors;
            _guiColors = guiColors;
        }

        public string GetName() => _name;

        public string GetInclude() => string.Empty;

        public ICollection<IRawThemeSetting> GetSettings() => _settings;

        public ICollection<IRawThemeSetting> GetTokenColors() => _tokenColors;

        public ICollection<KeyValuePair<string, object>> GetGuiColors() => _guiColors;
    }

    private sealed class RawThemeSetting : IRawThemeSetting
    {
        private readonly string _name;
        private readonly object? _scope;
        private readonly IThemeSetting _setting;

        public RawThemeSetting(string name, object? scope, IThemeSetting setting)
        {
            _name = name;
            _scope = scope;
            _setting = setting;
        }

        public string GetName() => _name;

        public object GetScope() => _scope ?? string.Empty;

        public IThemeSetting GetSetting() => _setting;
    }

    private sealed class ThemeSetting : IThemeSetting
    {
        private readonly object? _fontStyle;
        private readonly string? _background;
        private readonly string? _foreground;

        public ThemeSetting(object? fontStyle, string? background, string? foreground)
        {
            _fontStyle = fontStyle;
            _background = background;
            _foreground = foreground;
        }

        public object GetFontStyle() => _fontStyle ?? string.Empty;

        public string GetBackground() => _background ?? string.Empty;

        public string GetForeground() => _foreground ?? string.Empty;
    }
}