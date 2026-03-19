using System;
using Microsoft.Win32;

namespace NotepadPro.Services;

public static class FileAssociationService
{
    private const string ProgId = "NotepadPro.File";

    public static void RegisterDefaults()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return;
        }

        var extensions = new[] { ".txt", ".md", ".json", ".xml", ".cs", ".xaml", ".html", ".htm", ".css" };
        foreach (var ext in extensions)
        {
            TryRegisterExtension(ext, exePath);
        }
    }

    private static void TryRegisterExtension(string extension, string exePath)
    {
        try
        {
            using var extKey = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{extension}");
            extKey?.SetValue(string.Empty, ProgId);

            using var progKey = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{ProgId}");
            progKey?.SetValue(string.Empty, "Notepad Pro");

            using var iconKey = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{ProgId}\\DefaultIcon");
            iconKey?.SetValue(string.Empty, exePath);

            using var commandKey = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{ProgId}\\shell\\open\\command");
            commandKey?.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");
        }
        catch (Exception)
        {
        }
    }
}
