using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace NotepadPro.Services;

public sealed record ThemeDefinition(
    string Name,
    ThemeVariant Variant,
    // Backgrounds
    string PanelBackground,
    string SideBarBackground,
    string EditorBackground,
    string EditorSurface,
    string TitleBarBackground,
    string MenuBarBackground,
    string StatusBarBackground,
    string StatusBarForeground,
    string TabBarBackground,
    string ActiveTabBackground,
    string InactiveTabBackground,
    string TabHoverBackground,
    string ToolPanelBackground,
    string MinimapBackground,
    string LineNumberGutter,
    string CurrentLineHighlight,
    string InputBackground,
    string IconButtonHover,
    // Selection & Accent
    string SelectionBrush,
    string AccentBrush,
    string ButtonAccentBrush,
    // Foregrounds
    string ForegroundPrimary,
    string ForegroundSecondary,
    string ForegroundMuted,
    string ForegroundDimmed,
    string ForegroundInactive,
    string ForegroundSectionHeader,
    string ForegroundOnAccent,
    // Borders
    string BorderSubtle,
    string BorderMedium,
    string BorderAccent,
    // Scrollbar
    string ScrollbarTrack,
    // Syntax
    string SyntaxKeyword,
    string SyntaxString,
    string SyntaxComment,
    string SyntaxNumber,
    string SyntaxType,
    string SyntaxLink);

public static class ThemeService
{
    private static readonly Dictionary<string, ThemeDefinition> Themes = new(StringComparer.OrdinalIgnoreCase);

    static ThemeService()
    {
        Register(DarkPlus);
        Register(DarkModern);
        Register(DarkHighContrast);
        Register(OneDarkPro);
        Register(MonokaiPro);
        Register(SolarizedDark);
        Register(Sand);
        Register(Goth);
        Register(Vampire);
        Register(PeachSunsetLight);
        Register(PeachSunsetSoft);
        Register(LightPlus);
    }

    public static IReadOnlyDictionary<string, ThemeDefinition> All => Themes;

    public static void ApplyTheme(string themeName)
    {
        if (Application.Current is null) return;
        if (!Themes.TryGetValue(themeName, out var theme)) return;

        var res = Application.Current.Resources;

        SetBrush(res, "PanelBackground", theme.PanelBackground);
        SetBrush(res, "SideBarBackground", theme.SideBarBackground);
        SetBrush(res, "EditorBackground", theme.EditorBackground);
        SetBrush(res, "EditorSurface", theme.EditorSurface);
        SetBrush(res, "TitleBarBackground", theme.TitleBarBackground);
        SetBrush(res, "MenuBarBackground", theme.MenuBarBackground);
        SetBrush(res, "StatusBarBackground", theme.StatusBarBackground);
        SetBrush(res, "StatusBarForeground", theme.StatusBarForeground);
        SetBrush(res, "TabBarBackground", theme.TabBarBackground);
        SetBrush(res, "ActiveTabBackground", theme.ActiveTabBackground);
        SetBrush(res, "InactiveTabBackground", theme.InactiveTabBackground);
        SetBrush(res, "TabHoverBackground", theme.TabHoverBackground);
        SetBrush(res, "ToolPanelBackground", theme.ToolPanelBackground);
        SetBrush(res, "MinimapBackground", theme.MinimapBackground);
        SetBrush(res, "LineNumberGutter", theme.LineNumberGutter);
        SetBrush(res, "CurrentLineHighlight", theme.CurrentLineHighlight);
        SetBrush(res, "InputBackground", theme.InputBackground);
        SetBrush(res, "IconButtonHover", theme.IconButtonHover);
        SetBrush(res, "SelectionBrush", theme.SelectionBrush);
        SetBrush(res, "AccentBrush", theme.AccentBrush);
        SetBrush(res, "WelcomeLinkBrush", GetWelcomeLinkBrush(theme));
        SetBrush(res, "ButtonAccentBrush", theme.ButtonAccentBrush);
        SetBrush(res, "ForegroundPrimary", theme.ForegroundPrimary);
        SetBrush(res, "ForegroundSecondary", theme.ForegroundSecondary);
        SetBrush(res, "ForegroundMuted", theme.ForegroundMuted);
        SetBrush(res, "ForegroundDimmed", theme.ForegroundDimmed);
        SetBrush(res, "ForegroundInactive", theme.ForegroundInactive);
        SetBrush(res, "ForegroundSectionHeader", theme.ForegroundSectionHeader);
        SetBrush(res, "ForegroundOnAccent", theme.ForegroundOnAccent);
        SetBrush(res, "BorderSubtle", theme.BorderSubtle);
        SetBrush(res, "BorderMedium", theme.BorderMedium);
        SetBrush(res, "BorderAccent", theme.BorderAccent);
        SetBrush(res, "ScrollbarTrack", theme.ScrollbarTrack);
        SetBrush(res, "SyntaxKeyword", theme.SyntaxKeyword);
        SetBrush(res, "SyntaxString", theme.SyntaxString);
        SetBrush(res, "SyntaxComment", theme.SyntaxComment);
        SetBrush(res, "SyntaxNumber", theme.SyntaxNumber);
        SetBrush(res, "SyntaxType", theme.SyntaxType);
        SetBrush(res, "SyntaxLink", theme.SyntaxLink);

        Application.Current.RequestedThemeVariant = theme.Variant;
    }

    private static string GetWelcomeLinkBrush(ThemeDefinition theme)
    {
        if (string.Equals(theme.Name, "Monokai Pro", StringComparison.OrdinalIgnoreCase)
            || string.Equals(theme.Name, "Dark High Contrast", StringComparison.OrdinalIgnoreCase))
        {
            return theme.AccentBrush;
        }

        return Lighten(theme.AccentBrush, 0.24);
    }

    private static string Lighten(string colorValue, double amount)
    {
        try
        {
            var parsed = Color.Parse(colorValue);
            byte Blend(byte value) => (byte)Math.Clamp(value + ((255 - value) * amount), 0, 255);
            return $"#{Blend(parsed.R):X2}{Blend(parsed.G):X2}{Blend(parsed.B):X2}";
        }
        catch
        {
            return colorValue;
        }
    }

    private static void Register(ThemeDefinition theme) => Themes[theme.Name] = theme;

    private static void SetBrush(IResourceDictionary res, string key, string hex)
    {
        res[key] = new SolidColorBrush(Color.Parse(hex));
    }

    // ── Theme Definitions ──────────────────────────────────────────────

    public static readonly ThemeDefinition DarkPlus = new(
        Name: "Dark+",
        Variant: ThemeVariant.Dark,
        PanelBackground: "#1E1E1E",
        SideBarBackground: "#1F1F1F",
        EditorBackground: "#141414",
        EditorSurface: "#141414",
        TitleBarBackground: "#1C1C1C",
        MenuBarBackground: "#2A2A2A",
        StatusBarBackground: "#007ACC",
        StatusBarForeground: "#FFFFFF",
        TabBarBackground: "#252526",
        ActiveTabBackground: "#1E1E1E",
        InactiveTabBackground: "#2D2D2D",
        TabHoverBackground: "#2A2D2E",
        ToolPanelBackground: "#1B1B1B",
        MinimapBackground: "#1B1B1B",
        LineNumberGutter: "#1B1B1B",
        CurrentLineHighlight: "#2A2D2E",
        InputBackground: "#3C3C3C",
        IconButtonHover: "#383838",
        SelectionBrush: "#6644AAFF",
        AccentBrush: "#007ACC",
        ButtonAccentBrush: "#0E639C",
        ForegroundPrimary: "#D4D4D4",
        ForegroundSecondary: "#CCCCCC",
        ForegroundMuted: "#858585",
        ForegroundDimmed: "#6F6F6F",
        ForegroundInactive: "#9E9E9E",
        ForegroundSectionHeader: "#C8C8C8",
        ForegroundOnAccent: "#FFFFFF",
        BorderSubtle: "#2A2A2A",
        BorderMedium: "#3E3E3E",
        BorderAccent: "#3A3A3A",
        ScrollbarTrack: "#171717",
        SyntaxKeyword: "#C586C0",
        SyntaxString: "#CE9178",
        SyntaxComment: "#6A9955",
        SyntaxNumber: "#B5CEA8",
        SyntaxType: "#4EC9B0",
        SyntaxLink: "#7FD8FF");

    public static readonly ThemeDefinition DarkModern = new(
        Name: "Dark Modern",
        Variant: ThemeVariant.Dark,
        PanelBackground: "#1F1F1F",
        SideBarBackground: "#181818",
        EditorBackground: "#141414",
        EditorSurface: "#141414",
        TitleBarBackground: "#181818",
        MenuBarBackground: "#181818",
        StatusBarBackground: "#181818",
        StatusBarForeground: "#CCCCCC",
        TabBarBackground: "#181818",
        ActiveTabBackground: "#1F1F1F",
        InactiveTabBackground: "#181818",
        TabHoverBackground: "#2B2B2B",
        ToolPanelBackground: "#181818",
        MinimapBackground: "#181818",
        LineNumberGutter: "#1F1F1F",
        CurrentLineHighlight: "#2A2D2E",
        InputBackground: "#313131",
        IconButtonHover: "#2D2D2D",
        SelectionBrush: "#6644AAFF",
        AccentBrush: "#0078D4",
        ButtonAccentBrush: "#0078D4",
        ForegroundPrimary: "#CCCCCC",
        ForegroundSecondary: "#CCCCCC",
        ForegroundMuted: "#7A7A7A",
        ForegroundDimmed: "#6A6A6A",
        ForegroundInactive: "#8B8B8B",
        ForegroundSectionHeader: "#CCCCCC",
        ForegroundOnAccent: "#FFFFFF",
        BorderSubtle: "#2B2B2B",
        BorderMedium: "#3C3C3C",
        BorderAccent: "#2B2B2B",
        ScrollbarTrack: "#141414",
        SyntaxKeyword: "#C586C0",
        SyntaxString: "#CE9178",
        SyntaxComment: "#6A9955",
        SyntaxNumber: "#B5CEA8",
        SyntaxType: "#4EC9B0",
        SyntaxLink: "#8ED4FF");

    public static readonly ThemeDefinition DarkHighContrast = new(
        Name: "Dark High Contrast",
        Variant: ThemeVariant.Dark,
        PanelBackground: "#000000",
        SideBarBackground: "#000000",
        EditorBackground: "#000000",
        EditorSurface: "#000000",
        TitleBarBackground: "#000000",
        MenuBarBackground: "#000000",
        StatusBarBackground: "#000000",
        StatusBarForeground: "#FFFFFF",
        TabBarBackground: "#000000",
        ActiveTabBackground: "#000000",
        InactiveTabBackground: "#000000",
        TabHoverBackground: "#1F1F1F",
        ToolPanelBackground: "#000000",
        MinimapBackground: "#000000",
        LineNumberGutter: "#000000",
        CurrentLineHighlight: "#1A1A1A",
        InputBackground: "#0C0C0C",
        IconButtonHover: "#333333",
        SelectionBrush: "#8800C8FF",
        AccentBrush: "#1AEBFF",
        ButtonAccentBrush: "#1AEBFF",
        ForegroundPrimary: "#FFFFFF",
        ForegroundSecondary: "#FFFFFF",
        ForegroundMuted: "#D4D4D4",
        ForegroundDimmed: "#808080",
        ForegroundInactive: "#CCCCCC",
        ForegroundSectionHeader: "#FFFFFF",
        ForegroundOnAccent: "#000000",
        BorderSubtle: "#6FC3DF",
        BorderMedium: "#6FC3DF",
        BorderAccent: "#6FC3DF",
        ScrollbarTrack: "#000000",
        SyntaxKeyword: "#FF79C6",
        SyntaxString: "#CE9178",
        SyntaxComment: "#6A9955",
        SyntaxNumber: "#B5CEA8",
        SyntaxType: "#4EC9B0",
        SyntaxLink: "#6FF7FF");

    public static readonly ThemeDefinition OneDarkPro = new(
        Name: "One Dark Pro",
        Variant: ThemeVariant.Dark,
        PanelBackground: "#282C34",
        SideBarBackground: "#21252B",
        EditorBackground: "#1D2129",
        EditorSurface: "#1D2129",
        TitleBarBackground: "#21252B",
        MenuBarBackground: "#21252B",
        StatusBarBackground: "#21252B",
        StatusBarForeground: "#FFFFFF",
        TabBarBackground: "#21252B",
        ActiveTabBackground: "#282C34",
        InactiveTabBackground: "#21252B",
        TabHoverBackground: "#2C313A",
        ToolPanelBackground: "#21252B",
        MinimapBackground: "#21252B",
        LineNumberGutter: "#282C34",
        CurrentLineHighlight: "#2C313A",
        InputBackground: "#1B1D23",
        IconButtonHover: "#3B4048",
        SelectionBrush: "#6644AAFF",
        AccentBrush: "#528BFF",
        ButtonAccentBrush: "#4269BF",
        ForegroundPrimary: "#ABB2BF",
        ForegroundSecondary: "#ABB2BF",
        ForegroundMuted: "#636D83",
        ForegroundDimmed: "#4B5263",
        ForegroundInactive: "#636D83",
        ForegroundSectionHeader: "#ABB2BF",
        ForegroundOnAccent: "#FFFFFF",
        BorderSubtle: "#181A1F",
        BorderMedium: "#3B4048",
        BorderAccent: "#3B4048",
        ScrollbarTrack: "#1B1D23",
        SyntaxKeyword: "#C678DD",
        SyntaxString: "#98C379",
        SyntaxComment: "#5C6370",
        SyntaxNumber: "#D19A66",
        SyntaxType: "#E5C07B",
        SyntaxLink: "#8ED4FF");

    public static readonly ThemeDefinition MonokaiPro = new(
        Name: "Monokai Pro",
        Variant: ThemeVariant.Dark,
        PanelBackground: "#2D2A2E",
        SideBarBackground: "#221F22",
        EditorBackground: "#201D21",
        EditorSurface: "#201D21",
        TitleBarBackground: "#221F22",
        MenuBarBackground: "#221F22",
        StatusBarBackground: "#221F22",
        StatusBarForeground: "#FCFCFA",
        TabBarBackground: "#2D2A2E",
        ActiveTabBackground: "#2D2A2E",
        InactiveTabBackground: "#221F22",
        TabHoverBackground: "#3B3739",
        ToolPanelBackground: "#221F22",
        MinimapBackground: "#221F22",
        LineNumberGutter: "#2D2A2E",
        CurrentLineHighlight: "#3B3739",
        InputBackground: "#403E41",
        IconButtonHover: "#403E41",
        SelectionBrush: "#55FFD866",
        AccentBrush: "#FFD866",
        ButtonAccentBrush: "#A9DC76",
        ForegroundPrimary: "#FCFCFA",
        ForegroundSecondary: "#C1C0C0",
        ForegroundMuted: "#727072",
        ForegroundDimmed: "#5B595C",
        ForegroundInactive: "#727072",
        ForegroundSectionHeader: "#C1C0C0",
        ForegroundOnAccent: "#2D2A2E",
        BorderSubtle: "#3B3739",
        BorderMedium: "#5B595C",
        BorderAccent: "#5B595C",
        ScrollbarTrack: "#1D1B1E",
        SyntaxKeyword: "#FF6188",
        SyntaxString: "#FFD866",
        SyntaxComment: "#727072",
        SyntaxNumber: "#AB9DF2",
        SyntaxType: "#A9DC76",
        SyntaxLink: "#9BEAF2");

    public static readonly ThemeDefinition SolarizedDark = new(
        Name: "Solarized Dark",
        Variant: ThemeVariant.Dark,
        PanelBackground: "#002B36",
        SideBarBackground: "#00252E",
        EditorBackground: "#001D27",
        EditorSurface: "#001D27",
        TitleBarBackground: "#00252E",
        MenuBarBackground: "#00252E",
        StatusBarBackground: "#00252E",
        StatusBarForeground: "#93A1A1",
        TabBarBackground: "#002B36",
        ActiveTabBackground: "#002B36",
        InactiveTabBackground: "#00252E",
        TabHoverBackground: "#073642",
        ToolPanelBackground: "#00252E",
        MinimapBackground: "#00252E",
        LineNumberGutter: "#002B36",
        CurrentLineHighlight: "#073642",
        InputBackground: "#073642",
        IconButtonHover: "#073642",
        SelectionBrush: "#55268BD2",
        AccentBrush: "#268BD2",
        ButtonAccentBrush: "#2176AF",
        ForegroundPrimary: "#839496",
        ForegroundSecondary: "#93A1A1",
        ForegroundMuted: "#657B83",
        ForegroundDimmed: "#586E75",
        ForegroundInactive: "#586E75",
        ForegroundSectionHeader: "#93A1A1",
        ForegroundOnAccent: "#FDF6E3",
        BorderSubtle: "#073642",
        BorderMedium: "#586E75",
        BorderAccent: "#073642",
        ScrollbarTrack: "#001E27",
        SyntaxKeyword: "#859900",
        SyntaxString: "#2AA198",
        SyntaxComment: "#586E75",
        SyntaxNumber: "#D33682",
        SyntaxType: "#B58900",
        SyntaxLink: "#8FDFFF");

    public static readonly ThemeDefinition LightPlus = new(
        Name: "Light+",
        Variant: ThemeVariant.Light,
        PanelBackground: "#FFFFFF",
        SideBarBackground: "#F3F3F3",
        EditorBackground: "#F3F3F3",
        EditorSurface: "#F3F3F3",
        TitleBarBackground: "#DDDDDD",
        MenuBarBackground: "#F3F3F3",
        StatusBarBackground: "#007ACC",
        StatusBarForeground: "#FFFFFF",
        TabBarBackground: "#ECECEC",
        ActiveTabBackground: "#FFFFFF",
        InactiveTabBackground: "#ECECEC",
        TabHoverBackground: "#E8E8E8",
        ToolPanelBackground: "#F3F3F3",
        MinimapBackground: "#F3F3F3",
        LineNumberGutter: "#F7F7F7",
        CurrentLineHighlight: "#EDF3FC",
        InputBackground: "#FFFFFF",
        IconButtonHover: "#E0E0E0",
        SelectionBrush: "#773399FF",
        AccentBrush: "#007ACC",
        ButtonAccentBrush: "#005A9E",
        ForegroundPrimary: "#333333",
        ForegroundSecondary: "#444444",
        ForegroundMuted: "#717171",
        ForegroundDimmed: "#A0A0A0",
        ForegroundInactive: "#717171",
        ForegroundSectionHeader: "#444444",
        ForegroundOnAccent: "#FFFFFF",
        BorderSubtle: "#E5E5E5",
        BorderMedium: "#C8C8C8",
        BorderAccent: "#DDDDDD",
        ScrollbarTrack: "#E8E8E8",
        SyntaxKeyword: "#AF00DB",
        SyntaxString: "#A31515",
        SyntaxComment: "#008000",
        SyntaxNumber: "#098658",
        SyntaxType: "#267F99",
        SyntaxLink: "#0066CC");

    public static readonly ThemeDefinition PeachSunsetLight = new(
        Name: "Peach Sunset Light",
        Variant: ThemeVariant.Light,
        PanelBackground: "#FFF8F3",
        SideBarBackground: "#FDF1E8",
        EditorBackground: "#EDE4D9",
        EditorSurface: "#EDE4D9",
        TitleBarBackground: "#F7E8DC",
        MenuBarBackground: "#FBF1E8",
        StatusBarBackground: "#A85034",
        StatusBarForeground: "#FFFFFF",
        TabBarBackground: "#F7EBE1",
        ActiveTabBackground: "#FFFCF8",
        InactiveTabBackground: "#F3E3D7",
        TabHoverBackground: "#FAF0E7",
        ToolPanelBackground: "#FDF3EA",
        MinimapBackground: "#FAEEE4",
        LineNumberGutter: "#FAEFE6",
        CurrentLineHighlight: "#FFF3E9",
        InputBackground: "#FFFEFC",
        IconButtonHover: "#F3E3D8",
        SelectionBrush: "#66E9A777",
        AccentBrush: "#B25A3E",
        ButtonAccentBrush: "#9F4E36",
        ForegroundPrimary: "#3A2C27",
        ForegroundSecondary: "#4C3A33",
        ForegroundMuted: "#66524A",
        ForegroundDimmed: "#8A776F",
        ForegroundInactive: "#766259",
        ForegroundSectionHeader: "#4C3A33",
        ForegroundOnAccent: "#FFFFFF",
        BorderSubtle: "#EFDCCF",
        BorderMedium: "#D8BEAF",
        BorderAccent: "#E4CDBF",
        ScrollbarTrack: "#EBDCCE",
        SyntaxKeyword: "#8F4FBF",
        SyntaxString: "#B24A3D",
        SyntaxComment: "#6E635E",
        SyntaxNumber: "#0F7D85",
        SyntaxType: "#336A94",
        SyntaxLink: "#9A4D2D");

    public static readonly ThemeDefinition PeachSunsetSoft = new(
        Name: "Peach Sunset Soft",
        Variant: ThemeVariant.Light,
        PanelBackground: "#FFFBF7",
        SideBarBackground: "#FDF5EE",
        EditorBackground: "#E9DFCF",
        EditorSurface: "#E9DFCF",
        TitleBarBackground: "#FAEFE6",
        MenuBarBackground: "#FDF6EF",
        StatusBarBackground: "#94503A",
        StatusBarForeground: "#FFFFFF",
        TabBarBackground: "#F9F0E8",
        ActiveTabBackground: "#FFFEFC",
        InactiveTabBackground: "#F6EADF",
        TabHoverBackground: "#FBF4EC",
        ToolPanelBackground: "#FDF6EF",
        MinimapBackground: "#FBF2EA",
        LineNumberGutter: "#FBF2EA",
        CurrentLineHighlight: "#FFF6EE",
        InputBackground: "#FFFFFF",
        IconButtonHover: "#F4E9DF",
        SelectionBrush: "#66DFB99F",
        AccentBrush: "#A86449",
        ButtonAccentBrush: "#995A42",
        ForegroundPrimary: "#3D302A",
        ForegroundSecondary: "#4F3E37",
        ForegroundMuted: "#6E5C54",
        ForegroundDimmed: "#94827A",
        ForegroundInactive: "#7E6A62",
        ForegroundSectionHeader: "#4F3E37",
        ForegroundOnAccent: "#FFFFFF",
        BorderSubtle: "#F0E3D8",
        BorderMedium: "#D9C5B8",
        BorderAccent: "#E7D5C9",
        ScrollbarTrack: "#EDE2D9",
        SyntaxKeyword: "#7D58B0",
        SyntaxString: "#A9584E",
        SyntaxComment: "#746B66",
        SyntaxNumber: "#1A7A84",
        SyntaxType: "#3D6D95",
        SyntaxLink: "#8F5A42");

    public static readonly ThemeDefinition Sand = new(
        Name: "Sand",
        Variant: ThemeVariant.Dark,
        PanelBackground: "#3A3128",
        SideBarBackground: "#332A22",
        EditorBackground: "#2B2017",
        EditorSurface: "#2B2017",
        TitleBarBackground: "#2C241E",
        MenuBarBackground: "#352C24",
        StatusBarBackground: "#6D4F35",
        StatusBarForeground: "#FFFFFF",
        TabBarBackground: "#362D25",
        ActiveTabBackground: "#43372C",
        InactiveTabBackground: "#3A3027",
        TabHoverBackground: "#4A3D31",
        ToolPanelBackground: "#332A22",
        MinimapBackground: "#30271F",
        LineNumberGutter: "#3A3027",
        CurrentLineHighlight: "#4A3D31",
        InputBackground: "#4D4034",
        IconButtonHover: "#5A4B3C",
        SelectionBrush: "#886D4F35",
        AccentBrush: "#C49A6C",
        ButtonAccentBrush: "#A67E54",
        ForegroundPrimary: "#F0E3D2",
        ForegroundSecondary: "#E3D1BC",
        ForegroundMuted: "#C7B29A",
        ForegroundDimmed: "#A89279",
        ForegroundInactive: "#BDA88F",
        ForegroundSectionHeader: "#E6D5C1",
        ForegroundOnAccent: "#FFFFFF",
        BorderSubtle: "#4B3F33",
        BorderMedium: "#655442",
        BorderAccent: "#5A4B3A",
        ScrollbarTrack: "#2A221B",
        SyntaxKeyword: "#D6A6FF",
        SyntaxString: "#F0B487",
        SyntaxComment: "#A89684",
        SyntaxNumber: "#9BD0E5",
        SyntaxType: "#AFC8F5",
        SyntaxLink: "#D8B184");

    public static readonly ThemeDefinition Goth = new(
        Name: "Goth",
        Variant: ThemeVariant.Dark,
        PanelBackground: "#1A181F",
        SideBarBackground: "#17151C",
        EditorBackground: "#11101A",
        EditorSurface: "#11101A",
        TitleBarBackground: "#14121A",
        MenuBarBackground: "#181620",
        StatusBarBackground: "#5A2E8A",
        StatusBarForeground: "#FFFFFF",
        TabBarBackground: "#17151D",
        ActiveTabBackground: "#1D1A24",
        InactiveTabBackground: "#211D29",
        TabHoverBackground: "#2A2435",
        ToolPanelBackground: "#16141B",
        MinimapBackground: "#16141B",
        LineNumberGutter: "#1A1721",
        CurrentLineHighlight: "#2B2537",
        InputBackground: "#2D2737",
        IconButtonHover: "#332C3F",
        SelectionBrush: "#885A2E8A",
        AccentBrush: "#A15CFF",
        ButtonAccentBrush: "#8A48DE",
        ForegroundPrimary: "#E8E0F5",
        ForegroundSecondary: "#D9CCE9",
        ForegroundMuted: "#B4A3CA",
        ForegroundDimmed: "#8F819F",
        ForegroundInactive: "#A99ABD",
        ForegroundSectionHeader: "#E2D7F0",
        ForegroundOnAccent: "#FFFFFF",
        BorderSubtle: "#302A3A",
        BorderMedium: "#433A52",
        BorderAccent: "#3A3248",
        ScrollbarTrack: "#14121A",
        SyntaxKeyword: "#C792EA",
        SyntaxString: "#EAA27B",
        SyntaxComment: "#8B7D9E",
        SyntaxNumber: "#89DDFF",
        SyntaxType: "#82AAFF",
        SyntaxLink: "#CFA6FF");

    public static readonly ThemeDefinition Vampire = new(
        Name: "Vampire",
        Variant: ThemeVariant.Dark,
        PanelBackground: "#1A1416",
        SideBarBackground: "#161012",
        EditorBackground: "#0E0A0C",
        EditorSurface: "#0E0A0C",
        TitleBarBackground: "#120C0E",
        MenuBarBackground: "#181113",
        StatusBarBackground: "#7A1F2B",
        StatusBarForeground: "#FFFFFF",
        TabBarBackground: "#171012",
        ActiveTabBackground: "#1D1518",
        InactiveTabBackground: "#24181C",
        TabHoverBackground: "#312126",
        ToolPanelBackground: "#150F11",
        MinimapBackground: "#150F11",
        LineNumberGutter: "#1A1215",
        CurrentLineHighlight: "#312026",
        InputBackground: "#2B1C21",
        IconButtonHover: "#37242A",
        SelectionBrush: "#887A1F2B",
        AccentBrush: "#B0263C",
        ButtonAccentBrush: "#921F32",
        ForegroundPrimary: "#F0DCE1",
        ForegroundSecondary: "#E2C9CF",
        ForegroundMuted: "#BD9FA7",
        ForegroundDimmed: "#947980",
        ForegroundInactive: "#AE9098",
        ForegroundSectionHeader: "#EAD3D8",
        ForegroundOnAccent: "#FFFFFF",
        BorderSubtle: "#332126",
        BorderMedium: "#4A2F36",
        BorderAccent: "#3D282E",
        ScrollbarTrack: "#120C0E",
        SyntaxKeyword: "#F08CC0",
        SyntaxString: "#E6A97A",
        SyntaxComment: "#927881",
        SyntaxNumber: "#9AD3FF",
        SyntaxType: "#9CB8FF",
        SyntaxLink: "#FF8A9A");
}
