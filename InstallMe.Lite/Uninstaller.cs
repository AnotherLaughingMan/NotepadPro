using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace InstallMe.Lite
{
    [SupportedOSPlatform("windows")]
    public static class Uninstaller
    {
        private static string UninstallRegKey => InstallerProfile.UninstallRegistryPath;
        private const string StartupRegKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        private static readonly string[] AppNameKeywords = InstallerProfile.RegistryKeywords;
        public static Action<string>? LogSink;

        private static void Log(string message)
        {
            try { LogSink?.Invoke(message); } catch { }
        }

        public static int RunFromArgs()
        {
            try
            {
                var installPath = ReadInstallPathFromRegistry();
                if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
                {
                    return 1;
                }

                // Attempt to stop running processes that might lock files
                TryKillProcessesUsingPath(installPath);

                // Attempt to delete directory with retries
                if (!TryDeleteDirectoryWithRetries(installPath, 5, TimeSpan.FromSeconds(1)))
                {
                    try { MoveToTempAndScheduleDelete(installPath); } catch { }
                }

                // Remove taskbar pins and target-app registry entries
                try { RemoveTaskbarPins(); } catch { }
                try { CleanupTrayIconSettings(); } catch { }
                try { RemoveAllRegistryEntries(); } catch { }

                // Remove registry key
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall", true);
                    if (key != null) key.DeleteSubKeyTree(InstallerProfile.UninstallRegistryKeyName, false);
                }
                catch { }

                return 0;
            }
            catch
            {
                return 2;
            }
        }

        public static void WriteInstallPathToRegistry(string path)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(UninstallRegKey);
                if (key != null)
                {
                    key.SetValue("DisplayName", InstallerProfile.InstallerDisplayName);
                    key.SetValue("DisplayVersion", InstallerProfile.InstallerVersion);
                    key.SetValue("Publisher", InstallerProfile.InstallerPublisher);
                    key.SetValue("InstallLocation", path);
                    var exe = Path.Combine(path, InstallerProfile.InstallerExeName);
                    key.SetValue("UninstallString", exe + " /uninstall");
                    Log("Registry: Set uninstall info (HKCU) at " + UninstallRegKey);
                }
            }
            catch { }
        }

        private static string? ReadInstallPathFromRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(UninstallRegKey);
                if (key == null) return null;
                var val = key.GetValue("InstallLocation") as string;
                return val;
            }
            catch { return null; }
        }

        public static string? GetInstallPath()
        {
            return ReadInstallPathFromRegistry();
        }

        private static void MoveToTempAndScheduleDelete(string path)
        {
            var tmp = Path.Combine(Path.GetTempPath(), InstallerProfile.TempUninstallPrefix + Guid.NewGuid().ToString("N"));
            Directory.Move(path, tmp);
            // Schedule deletion via a cmd line on next reboot
            var cmd = $"/C ping 127.0.0.1 -n 2 >nul & rmdir /s /q \"{tmp}\"";
            var psi = new ProcessStartInfo("cmd.exe", cmd) { CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            Process.Start(psi);
        }

        private static void TryKillProcessesUsingPath(string path)
        {
            try
            {
                var procs = Process.GetProcesses();
                foreach (var p in procs)
                {
                    try
                    {
                        var mod = p.MainModule?.FileName;
                        if (string.IsNullOrEmpty(mod)) continue;
                        if (mod.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                        {
                            try { p.Kill(); p.WaitForExit(2000); } catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static bool TryDeleteDirectoryWithRetries(string path, int attempts, TimeSpan delay)
        {
            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    if (Directory.Exists(path)) Directory.Delete(path, true);
                    return true;
                }
                catch
                {
                    System.Threading.Thread.Sleep(delay);
                }
            }
            return !Directory.Exists(path);
        }

        // Public wrappers so UI code can use the safer uninstall helpers
        public static void StopProcesses(string path) => TryKillProcessesUsingPath(path);

        public static bool DeleteDirectoryWithRetries(string path, int attempts, TimeSpan delay) => TryDeleteDirectoryWithRetries(path, attempts, delay);

        public static void ScheduleDelete(string path) => MoveToTempAndScheduleDelete(path);

        public static void RemoveRegistryKey()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall", true);
                if (key != null) key.DeleteSubKeyTree(InstallerProfile.UninstallRegistryKeyName, false);
            }
            catch { }
        }

        public static void RemoveShortcutsNow()
        {
            try { RemoveShortcuts(); } catch { }
        }

        public static void CleanupRegistryHiveEntries()
        {
            try { RemoveAllRegistryEntries(); } catch { }
        }

        /// <summary>
        /// Removes all target-app entries from Windows startup
        /// </summary>
        public static void RemoveFromStartup()
        {
            // Remove from HKCU Run key
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupRegKey, true);
                if (key != null)
                {
                    var valueNames = key.GetValueNames();
                    foreach (var valueName in valueNames)
                    {
                        if (ContainsAppKeyword(valueName))
                        {
                            try
                            {
                                key.DeleteValue(valueName, false);
                                Log("Registry: Removed HKCU Run value " + valueName);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            // Remove from HKLM Run key (if exists)
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(StartupRegKey, true);
                if (key != null)
                {
                    var valueNames = key.GetValueNames();
                    foreach (var valueName in valueNames)
                    {
                        if (ContainsAppKeyword(valueName))
                        {
                            try
                            {
                                key.DeleteValue(valueName, false);
                                Log("Registry: Removed HKLM Run value " + valueName);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            // Remove from Startup folder
            try
            {
                var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                var startupFiles = Directory.GetFiles(startupFolder, "*.lnk");
                foreach (var file in startupFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (ContainsAppKeyword(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                            Log("File: Removed startup shortcut " + file);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Complete uninstall including Windows 11 Apps and Startup removal
        /// </summary>
        public static void CompleteUninstall(string installPath)
        {
            // Stop processes
            TryKillProcessesUsingPath(installPath);

            // Remove from startup
            RemoveFromStartup();

            // Remove taskbar pins
            RemoveTaskbarPins();

            // Remove tray icon entries (Windows 11 Taskbar settings list)
            CleanupTrayIconSettings();

            // Remove shortcuts
            RemoveShortcuts();

            // Remove files
            if (!TryDeleteDirectoryWithRetries(installPath, 6, TimeSpan.FromSeconds(1)))
            {
                try { MoveToTempAndScheduleDelete(installPath); } catch { }
            }

            // Aggressive registry cleanup for Windows 11 Apps
            RemoveAllRegistryEntries();
        }

        /// <summary>
        /// Aggressively removes all target-app registry entries from all known locations
        /// </summary>
        private static void RemoveAllRegistryEntries()
        {
            var registryPaths = new[]
            {
                // Standard uninstall locations
                ("HKCU", "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall"),
                ("HKLM", "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall"),
                ("HKLM", "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall"),
                
                // Windows Installer paths
                ("HKCU", "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Installer\\UserData"),
                ("HKLM", "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Installer\\UserData"),
                
                // App Paths
                ("HKCU", "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths"),
                ("HKLM", "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths"),
                
                // Applications
                ("HKCU", "SOFTWARE\\Classes\\Applications"),
                ("HKLM", "SOFTWARE\\Classes\\Applications"),
                
                // RegisteredApplications
                ("HKCU", "SOFTWARE\\RegisteredApplications"),
                ("HKLM", "SOFTWARE\\RegisteredApplications"),
                
                // Capabilities
                ("HKCU", "SOFTWARE\\Clients"),
                ("HKLM", "SOFTWARE\\Clients")
            };

            foreach (var (hive, path) in registryPaths)
            {
                try
                {
                    var baseKey = hive == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
                    using var key = baseKey.OpenSubKey(path, true);
                    if (key == null) continue;

                    var subkeys = key.GetSubKeyNames().ToList();
                    foreach (var subkey in subkeys)
                    {
                        using var child = key.OpenSubKey(subkey);
                        if (IsUninstallSubkeyMatch(subkey, child) || ContainsAppKeyword(subkey))
                        {
                            try
                            {
                                key.DeleteSubKeyTree(subkey, false);
                                Log("Registry: Removed " + hive + "\\" + path + "\\" + subkey);
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }

            // Clean up any values (not just subkeys) that reference the target app
            CleanupRegistryValues();
        }

        /// <summary>
        /// Removes registry values that reference the target app
        /// </summary>
        private static void CleanupRegistryValues()
        {
            var valuePaths = new[]
            {
                ("HKCU", "SOFTWARE\\RegisteredApplications"),
                ("HKLM", "SOFTWARE\\RegisteredApplications")
            };

            foreach (var (hive, path) in valuePaths)
            {
                try
                {
                    var baseKey = hive == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
                    using var key = baseKey.OpenSubKey(path, true);
                    if (key == null) continue;

                    var values = key.GetValueNames().ToList();
                    foreach (var value in values)
                    {
                        var valueData = key.GetValue(value)?.ToString() ?? string.Empty;
                        if (ContainsAppKeyword(value) || ContainsAppKeyword(valueData))
                        {
                            try
                            {
                                key.DeleteValue(value, false);
                                Log("Registry: Removed value " + hive + "\\" + path + " : " + value);
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Remove all target-app shortcuts
        /// </summary>
        private static void RemoveShortcuts()
        {
            foreach (var shortcutName in InstallerProfile.ShortcutNames)
            {
                try { File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), shortcutName)); } catch { }
                try { File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), shortcutName)); } catch { }
                try { File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", shortcutName)); } catch { }
                try { File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", shortcutName)); } catch { }
            }
        }

        /// <summary>
        /// Removes pinned taskbar/start menu shortcuts for the target app
        /// </summary>
        public static void RemoveTaskbarPins()
        {
            var locations = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch\User Pinned\StartMenu"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs")
            };

            foreach (var location in locations)
            {
                try
                {
                    if (!Directory.Exists(location)) continue;
                    foreach (var file in Directory.GetFiles(location, "*.lnk", SearchOption.TopDirectoryOnly))
                    {
                        var fileName = Path.GetFileNameWithoutExtension(file) ?? string.Empty;
                        if (ContainsAppKeyword(fileName))
                        {
                            try { File.Delete(file); } catch { }
                            Log("File: Removed pin/shortcut " + file);
                        }
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Removes target-app entries from Windows tray icon settings list (Windows 10/11)
        /// </summary>
        public static void CleanupTrayIconSettings()
        {
            try { RemoveNotifyIconSettings(@"Control Panel\NotifyIconSettings"); } catch { }
            try { RemoveNotifyIconSettings(@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\TrayNotify\NotifyIconSettings"); } catch { }
        }

        private static void RemoveNotifyIconSettings(string subKeyPath)
        {
            using var key = Registry.CurrentUser.OpenSubKey(subKeyPath, true);
            if (key == null) return;

            foreach (var sub in key.GetSubKeyNames())
            {
                try
                {
                    using var child = key.OpenSubKey(sub);
                    if (child == null) continue;

                    var exePath = child.GetValue("ExecutablePath")?.ToString() ?? string.Empty;
                    var tooltip = child.GetValue("InitialTooltip")?.ToString() ?? string.Empty;
                    var appName = child.GetValue("ApplicationName")?.ToString() ?? string.Empty;
                    var appId = child.GetValue("AppUserModelID")?.ToString() ?? string.Empty;

                    if (ContainsAppKeyword(sub) || ContainsAppKeyword(exePath) || ContainsAppKeyword(tooltip) ||
                        ContainsAppKeyword(appName) || ContainsAppKeyword(appId))
                    {
                        try
                        {
                            key.DeleteSubKeyTree(sub, false);
                            Log("Registry: Removed tray icon entry " + subKeyPath + "\\" + sub);
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        private static bool IsUninstallSubkeyMatch(string subkeyName, RegistryKey? subkey)
        {
            if (ContainsAppKeyword(subkeyName)) return true;
            if (subkey == null) return false;

            var displayName = subkey.GetValue("DisplayName")?.ToString() ?? string.Empty;
            var uninstallString = subkey.GetValue("UninstallString")?.ToString() ?? string.Empty;
            var installLocation = subkey.GetValue("InstallLocation")?.ToString() ?? string.Empty;
            var displayIcon = subkey.GetValue("DisplayIcon")?.ToString() ?? string.Empty;

            return ContainsAppKeyword(displayName) || ContainsAppKeyword(uninstallString) ||
                   ContainsAppKeyword(installLocation) || ContainsAppKeyword(displayIcon);
        }

        private static bool ContainsAppKeyword(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            foreach (var keyword in AppNameKeywords)
            {
                if (value.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// Forces Windows to refresh the installed apps list (Windows 11)
        /// </summary>
        public static void ForceRefreshInstalledApps()
        {
            try
            {
                // Notify Windows that installed programs have changed
                // This triggers Windows to refresh various caches
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c rundll32.exe shell32.dll,Control_RunDLL appwiz.cpl",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                };

                using var proc = Process.Start(psi);
                proc?.WaitForExit(1000);
            }
            catch { }

            // Also try to clear Windows Explorer cache
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c ie4uinit.exe -ClearIconCache",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                };

                using var proc = Process.Start(psi);
                proc?.WaitForExit(2000);
            }
            catch { }
        }
    }
}