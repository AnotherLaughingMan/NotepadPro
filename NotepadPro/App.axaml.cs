using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using NotepadPro.Models;
using NotepadPro.Services;
using NotepadPro.ViewModels;
using NotepadPro.Views;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;

namespace NotepadPro;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            var settingsService = new AppSettingsService();
            var appSettings = settingsService.Load();
            LogPersistenceSnapshot("load", appSettings);
            var settingsViewModel = new SettingsViewModel(appSettings.Settings);

            // Apply the saved theme immediately before the window is shown
            ThemeService.ApplyTheme(settingsViewModel.Theme);

            var viewModel = new MainWindowViewModel(settingsViewModel);
            viewModel.Explorer.SetExpandedFolderPaths(appSettings.ExpandedExplorerPaths);
            viewModel.SetRecentEditors(appSettings.RecentEditors);
            viewModel.SetRecentFiles(appSettings.RecentFiles);
            viewModel.SetRecentProjects(appSettings.RecentProjects);
            viewModel.SetBookmarkScopes(appSettings.BookmarkScopes);
            var mainWindow = new MainWindow
            {
                DataContext = viewModel,
            };

            ApplyWindowSettings(mainWindow, appSettings.Window);
            desktop.MainWindow = mainWindow;

            // React to theme changes at runtime
            settingsViewModel.WhenAnyValue(x => x.Theme)
                .Skip(1) // skip initial value (already applied above)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(theme => ThemeService.ApplyTheme(theme));

            // Apply saved scrollbar opacity and react to changes
            UpdateScrollbarOpacity(settingsViewModel.ScrollbarOpacity);
            settingsViewModel.WhenAnyValue(x => x.ScrollbarOpacity)
                .Skip(1)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(UpdateScrollbarOpacity);

            settingsViewModel.Changed
                .Throttle(TimeSpan.FromMilliseconds(250))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => SaveSettings(settingsService, appSettings, settingsViewModel, viewModel));

            viewModel.Explorer.ExpandedFolderPathsChanged += (_, _) =>
            {
                SaveSettings(settingsService, appSettings, settingsViewModel, viewModel);
            };

            viewModel.Explorer.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(ExplorerViewModel.IsOpenEditorsExpanded)
                    or nameof(ExplorerViewModel.IsRecentEditorsExpanded)
                    or nameof(ExplorerViewModel.IsFilesExpanded)
                    or nameof(ExplorerViewModel.IsOpenEditorsVisible)
                    or nameof(ExplorerViewModel.IsRecentEditorsVisible)
                    or nameof(ExplorerViewModel.IsFilesVisible))
                {
                    SaveSettings(settingsService, appSettings, settingsViewModel, viewModel);
                }
            };

            mainWindow.Closing += (_, _) =>
            {
                CaptureWindowSettings(mainWindow, appSettings.Window);
                viewModel.AddOpenTabsToRecentEditors();
                SaveSettings(settingsService, appSettings, settingsViewModel, viewModel, logSnapshot: true, snapshotLabel: "closing-save");
            };

            // Open files passed as command-line arguments (e.g. via "Open With")
            if (desktop.Args is { Length: > 0 })
            {
                mainWindow.Opened += async (_, _) =>
                {
                    foreach (var arg in desktop.Args)
                    {
                        if (string.IsNullOrWhiteSpace(arg))
                        {
                            continue;
                        }

                        if (Directory.Exists(arg))
                        {
                            var detectedWorkspace = settingsViewModel.AutoOpenDetectedWorkspaces
                                ? viewModel.Explorer.DetectWorkspaceFileInFolder(arg)
                                : null;

                            if (!string.IsNullOrWhiteSpace(detectedWorkspace))
                            {
                                viewModel.Explorer.LoadWorkspace(detectedWorkspace);
                                viewModel.AddRecentProject(detectedWorkspace);
                                continue;
                            }

                            viewModel.Explorer.LoadFolder(arg);
                            viewModel.AddRecentProject(arg);
                            continue;
                        }

                        if (!File.Exists(arg))
                        {
                            continue;
                        }

                        if (arg.EndsWith(".code-workspace", StringComparison.OrdinalIgnoreCase))
                        {
                            viewModel.Explorer.LoadWorkspace(arg);
                            viewModel.AddRecentProject(arg);
                            continue;
                        }

                        if (File.Exists(arg))
                        {
                            await viewModel.OpenFileFromPathAsync(arg);
                        }
                    }
                };
            }
            else
            {
                mainWindow.Opened += async (_, _) =>
                {
                    RestorePersistedProjectLocation(viewModel, appSettings);

                    if (!settingsViewModel.RestoreOpenDocumentsOnStartup)
                    {
                        return;
                    }

                    if (appSettings.OpenDocumentStates.Count > 0)
                    {
                        await viewModel.RestoreOpenDocumentsSessionAsync(appSettings.OpenDocumentStates, appSettings.ActiveOpenDocumentIndex);
                    }
                    else
                    {
                        await viewModel.RestoreOpenDocumentsSessionAsync(appSettings.OpenDocuments, appSettings.ActiveOpenDocumentIndex);
                    }
                };
            }

            FileAssociationService.RegisterDefaults();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private static void SaveSettings(
        AppSettingsService service,
        AppSettings settings,
        SettingsViewModel viewModel,
        MainWindowViewModel windowViewModel,
        bool logSnapshot = false,
        string snapshotLabel = "save")
    {
        settings.Settings = viewModel.ToData();
        settings.Settings.ExplorerOpenEditorsExpanded = windowViewModel.Explorer.IsOpenEditorsExpanded;
        settings.Settings.ExplorerRecentEditorsExpanded = windowViewModel.Explorer.IsRecentEditorsExpanded;
        settings.Settings.ExplorerFilesExpanded = windowViewModel.Explorer.IsFilesExpanded;
        settings.Settings.ExplorerOpenEditorsVisible = windowViewModel.Explorer.IsOpenEditorsVisible;
        settings.Settings.ExplorerRecentEditorsVisible = windowViewModel.Explorer.IsRecentEditorsVisible;
        settings.Settings.ExplorerFilesVisible = windowViewModel.Explorer.IsFilesVisible;
        settings.RecentEditors = windowViewModel.GetRecentEditorsData();
        settings.RecentFiles = windowViewModel.GetRecentFilesData();
        settings.RecentProjects = windowViewModel.GetRecentProjectsData();
        settings.LastWorkspacePath = windowViewModel.GetCurrentWorkspacePathData();
        settings.LastFolderPath = windowViewModel.GetCurrentFolderPathData();
        settings.ExpandedExplorerPaths = windowViewModel.Explorer.GetExpandedFolderPathsData();
        settings.OpenDocumentStates = windowViewModel.GetOpenDocumentSessionData();
        settings.OpenDocuments = windowViewModel.GetOpenDocumentPathsData();
        settings.ActiveOpenDocumentIndex = windowViewModel.GetActiveOpenDocumentIndexData();
        settings.BookmarkScopes = windowViewModel.GetBookmarkScopesData();
        service.Save(settings);

        if (logSnapshot)
        {
            LogPersistenceSnapshot(snapshotLabel, settings);
        }
    }

    private static void LogPersistenceSnapshot(string label, AppSettings settings)
    {
#if DEBUG
        try
        {
            var expandedPaths = settings.ExpandedExplorerPaths ?? new System.Collections.Generic.List<string>();
            var expandedCount = expandedPaths.Count;
            var expandedPreview = expandedCount > 0
                ? string.Join(" | ", expandedPaths.Take(3))
                : "(none)";

            var window = settings.Window ?? new WindowStateData();
            Console.WriteLine(
                $"[Persistence:{label}] " +
                $"WindowState={window.WindowState}; Size={window.Width}x{window.Height}; Pos=({window.X?.ToString() ?? "null"},{window.Y?.ToString() ?? "null"}); " +
                $"LastFolder='{settings.LastFolderPath}'; LastWorkspace='{settings.LastWorkspacePath}'; " +
                $"ExpandedPaths={expandedCount}; Preview={expandedPreview}");
        }
        catch
        {
        }
#endif
    }

    private static void RestorePersistedProjectLocation(MainWindowViewModel viewModel, AppSettings appSettings)
    {
        if (!string.IsNullOrWhiteSpace(appSettings.LastWorkspacePath) && File.Exists(appSettings.LastWorkspacePath))
        {
            viewModel.Explorer.LoadWorkspace(appSettings.LastWorkspacePath);
            viewModel.AddRecentProject(appSettings.LastWorkspacePath);
            return;
        }

        if (!string.IsNullOrWhiteSpace(appSettings.LastFolderPath) && Directory.Exists(appSettings.LastFolderPath))
        {
            viewModel.Explorer.LoadFolder(appSettings.LastFolderPath);
            viewModel.AddRecentProject(appSettings.LastFolderPath);
        }
    }

    private void UpdateScrollbarOpacity(double opacity)
    {
        Resources["ScrollbarOpacityValue"] = opacity;
    }

    private static void ApplyWindowSettings(Window window, WindowStateData data)
    {
        if (data.Width > 0)
        {
            window.Width = data.Width;
        }

        if (data.Height > 0)
        {
            window.Height = data.Height;
        }

        if (data.X.HasValue && data.Y.HasValue)
        {
            window.Position = new PixelPoint(data.X.Value, data.Y.Value);
        }

        if (data.WindowState != WindowState.Normal)
        {
            window.WindowState = data.WindowState;
        }
    }

    private static void CaptureWindowSettings(Window window, WindowStateData data)
    {
        var width = window.Width;
        var height = window.Height;

        if (width > 0 && height > 0)
        {
            data.Width = (int)width;
            data.Height = (int)height;
        }

        if (window.WindowState == WindowState.Normal)
        {
            if (width > 0 && height > 0)
            {
                data.X = window.Position.X;
                data.Y = window.Position.Y;
            }
        }

        data.WindowState = window.WindowState;
    }
}