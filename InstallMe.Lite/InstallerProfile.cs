using System;

namespace InstallMe.Lite;

internal static class InstallerProfile
{
    public const string AppDisplayName = "Notepad Pro";
    public const string AppExeName = "NotepadPro.exe";
    public const string AppProcessName = "NotepadPro";
    public const string AppUserModelId = "NotepadPro.App";
    public const string AppDataHintPath = "%LOCALAPPDATA%\\NotepadPro";

    public const string InstallerExeName = "InstallMe.Lite.exe";
    public const string InstallerDisplayName = "Notepad Pro (InstallMe Lite)";
    public const string InstallerPublisher = "AnotherLaughingMan";
    public const string InstallerVersion = "1.1.9574";

    public const string DefaultInstallPath = "C:\\Apps\\NotepadPro";
    public const string UninstallRegistryKeyName = "NotepadPro_InstallMeLite";

    public static readonly string[] PackageResourceNameCandidates =
    {
        "NotepadPro-win-x64-framework-dependent.zip",
        "notepadpro_release.zip"
    };

    public static readonly string[] ShortcutNames =
    {
        "Notepad Pro.lnk",
        "NotepadPro.lnk"
    };

    public static readonly string[] RegistryKeywords =
    {
        "NotepadPro",
        "Notepad Pro",
        "NotepadPro.App"
    };

    public static string UninstallRegistryPath =>
        $"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{UninstallRegistryKeyName}";

    public static string TempPackagePrefix =>
        AppProcessName + "_Package_";

    public static string TempReleaseZipPrefix =>
        AppProcessName.ToLowerInvariant() + "_release_";

    public static string TempUninstallPrefix =>
        AppProcessName + "_Uninstall_";
}
