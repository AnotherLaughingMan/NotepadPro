using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using TextMateSharp.Grammars;

namespace NotepadPro.Services;

public static class TextMateLanguageService
{
    private const int MaxJsonSniffLength = 1_000_000;
    private static readonly Lazy<RegistryOptions> Registry = new(() => new RegistryOptions(ThemeName.DarkPlus));
    private static readonly IReadOnlyDictionary<string, string> ExtensionDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".txt"] = "Plain Text",
        [".text"] = "Plain Text",
        [".log"] = "Plain Text",
        [".md"] = "Markdown",
        [".markdown"] = "Markdown",
        [".mdown"] = "Markdown",
        [".mkd"] = "Markdown",
        [".cs"] = "C#",
        [".csx"] = "C#",
        [".cake"] = "C#",
        [".c"] = "C",
        [".cpp"] = "C++",
        [".cc"] = "C++",
        [".cxx"] = "C++",
        [".c++"] = "C++",
        [".h"] = "C++",
        [".hh"] = "C++",
        [".hpp"] = "C++",
        [".hxx"] = "C++",
        [".inl"] = "C++",
        [".ipp"] = "C++",
        [".tpp"] = "C++",
        [".json"] = "JSON",
        [".jsonc"] = "JSON",
        [".json5"] = "JSON",
        [".patch"] = "JSON",
        [".recipe"] = "JSON",
        [".item"] = "JSON",
        [".object"] = "JSON",
        [".frames"] = "JSON",
        [".npctype"] = "JSON",
        [".particle"] = "JSON",
        [".particlesource"] = "JSON",
        [".macros"] = "JSON",
        [".questtemplate"] = "JSON",
        [".species"] = "JSON",
        [".cursor"] = "JSON",
        [".weather"] = "JSON",
        [".aimission"] = "JSON",
        [".animation"] = "JSON",
        [".stagehand"] = "JSON",
        [".treasurepools"] = "JSON",
        [".treasurechests"] = "JSON",
        [".dance"] = "JSON",
        [".cinematic"] = "JSON",
        [".functions"] = "JSON",
        [".tenant"] = "JSON",
        [".collection"] = "JSON",
        [".namesource"] = "JSON",
        [".radiomessages"] = "JSON",
        [".augment"] = "JSON",
        [".consumable"] = "JSON",
        [".harvestingtool"] = "JSON",
        [".miningtool"] = "JSON",
        [".flashlight"] = "JSON",
        [".tillingtool"] = "JSON",
        [".painttool"] = "JSON",
        [".wiretool"] = "JSON",
        [".activeitem"] = "JSON",
        [".effectsource"] = "JSON",
        [".matmod"] = "JSON",
        [".configfunctions"] = "JSON",
        [".2functions"] = "JSON",
        [".modinfo"] = "JSON",
        [".xml"] = "XML",
        [".xsd"] = "XML",
        [".xsl"] = "XML",
        [".xslt"] = "XML",
        [".svg"] = "XML",
        [".resx"] = "XML",
        [".props"] = "XML",
        [".targets"] = "XML",
        [".config"] = "XML",
        [".xaml"] = "XAML",
        [".axaml"] = "AXAML",
        [".html"] = "HTML",
        [".htm"] = "HTML",
        [".css"] = "CSS",
        [".scss"] = "SCSS",
        [".less"] = "LESS",
        [".js"] = "JavaScript",
        [".jsx"] = "JavaScript",
        [".ts"] = "TypeScript",
        [".tsx"] = "TypeScript",
        [".py"] = "Python",
        [".ps1"] = "PowerShell",
        [".psm1"] = "PowerShell",
        [".psd1"] = "PowerShell",
        [".ps1xml"] = "PowerShell",
        [".sh"] = "Shell",
        [".bash"] = "Shell",
        [".zsh"] = "Shell",
        [".fish"] = "Shell",
        [".yml"] = "YAML",
        [".yaml"] = "YAML",
        [".sql"] = "SQL",
        [".java"] = "Java",
        [".go"] = "Go",
        [".rs"] = "Rust",
        [".php"] = "PHP",
        [".rb"] = "Ruby",
        [".lua"] = "Lua",
        [".swift"] = "Swift",
        [".kt"] = "Kotlin",
        [".kts"] = "Kotlin",
        [".r"] = "R",
    };

    private static readonly IReadOnlyDictionary<string, string> FileNameDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["dockerfile"] = "Dockerfile",
        ["makefile"] = "Makefile",
        ["readme.md"] = "Markdown",
        ["_metadata"] = "JSON",
        [".metadata"] = "JSON",
    };

    public static string DetectLanguageFromPath(string path, string? fileText = null, bool detectJsonFromContent = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return detectJsonFromContent && IsJsonContent(fileText) ? "JSON" : "Plain Text";
        }

        var fileName = Path.GetFileName(path);
        if (!string.IsNullOrWhiteSpace(fileName) && FileNameDisplayNames.TryGetValue(fileName, out var fileNameLanguage))
        {
            return fileNameLanguage;
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return detectJsonFromContent && IsJsonContent(fileText) ? "JSON" : "Plain Text";
        }

        if (ExtensionDisplayNames.TryGetValue(extension, out var extensionLanguage))
        {
            if (string.Equals(extension, ".config", StringComparison.OrdinalIgnoreCase)
                && detectJsonFromContent
                && IsJsonContent(fileText))
            {
                return "JSON";
            }

            return extensionLanguage;
        }

        try
        {
            var language = Registry.Value.GetLanguageByExtension(extension);
            if (language is not null)
            {
                return ToDisplayLanguage(language.Id);
            }

            var scope = Registry.Value.GetScopeByExtension(extension);
            if (!string.IsNullOrWhiteSpace(scope))
            {
                return ToDisplayLanguage(scope);
            }
        }
        catch
        {
        }

        if (detectJsonFromContent && IsJsonContent(fileText))
        {
            return "JSON";
        }

        return "Plain Text";
    }

    public static string NormalizeDisplayLanguage(string? idOrScope)
    {
        return ToDisplayLanguage(idOrScope ?? string.Empty);
    }

    private static string ToDisplayLanguage(string idOrScope)
    {
        if (string.IsNullOrWhiteSpace(idOrScope))
        {
            return "Plain Text";
        }

        var key = idOrScope.Trim().ToLowerInvariant();
        return key switch
        {
            "text" => "Plain Text",
            "txt" => "Plain Text",
            "plaintext" => "Plain Text",
            "plain text" => "Plain Text",
            "csharp" => "C#",
            "cs" => "C#",
            "c#" => "C#",
            "source.cs" => "C#",
            "source.csharp" => "C#",
            "c" => "C",
            "source.c" => "C",
            "cpp" => "C++",
            "c++" => "C++",
            "cxx" => "C++",
            "source.cpp" => "C++",
            "source.c++" => "C++",
            "javascript" => "JavaScript",
            "js" => "JavaScript",
            "jsx" => "JavaScript",
            "source.js" => "JavaScript",
            "typescript" => "TypeScript",
            "ts" => "TypeScript",
            "tsx" => "TypeScript",
            "source.ts" => "TypeScript",
            "json" => "JSON",
            "jsonc" => "JSON",
            "json5" => "JSON",
            "source.json" => "JSON",
            "markdown" => "Markdown",
            "md" => "Markdown",
            "xml" => "XML",
            "source.xml" => "XML",
            "text.xml" => "XML",
            "xaml" => "XAML",
            "text.xml.xaml" => "XAML",
            "html" => "HTML",
            "htm" => "HTML",
            "text.html.basic" => "HTML",
            "css" => "CSS",
            "scss" => "SCSS",
            "less" => "LESS",
            "python" => "Python",
            "source.python" => "Python",
            "powershell" => "PowerShell",
            "pwsh" => "PowerShell",
            "shell" => "Shell",
            "shellscript" => "Shell",
            "bash" => "Shell",
            "sh" => "Shell",
            "zsh" => "Shell",
            "yaml" => "YAML",
            "yml" => "YAML",
            "sql" => "SQL",
            "java" => "Java",
            "go" => "Go",
            "rust" => "Rust",
            "php" => "PHP",
            "ruby" => "Ruby",
            "lua" => "Lua",
            "source.lua" => "Lua",
            "swift" => "Swift",
            "kotlin" => "Kotlin",
            "r" => "R",
            "text.html.markdown" => "Markdown",
            _ => HumanizeKey(key)
        };
    }

    private static string HumanizeKey(string value)
    {
        var normalized = value
            .Replace("source.", string.Empty, StringComparison.Ordinal)
            .Replace("text.", string.Empty, StringComparison.Ordinal)
            .Replace('.', ' ')
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Plain Text";
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized);
    }

    private static bool IsJsonContent(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxJsonSniffLength)
        {
            return false;
        }

        var span = text.AsSpan().TrimStart();
        if (span.IsEmpty)
        {
            return false;
        }

        var firstChar = span[0];
        if (firstChar != '{' && firstChar != '[')
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        }
        catch
        {
            return false;
        }
    }
}
