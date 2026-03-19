using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using NotepadPro.ViewModels;

namespace NotepadPro.Views;

public partial class ExplorerView : UserControl
{
    public ExplorerView()
    {
        InitializeComponent();
    }

    private ExplorerViewModel? ViewModel => DataContext as ExplorerViewModel;

    private MainWindowViewModel? MainViewModel
    {
        get
        {
            var window = this.FindAncestorOfType<MainWindow>();
            return window?.ViewModel;
        }
    }

    private async void OnSaveAll(object? sender, RoutedEventArgs e)
    {
        var vm = MainViewModel;
        if (vm != null)
        {
            await vm.SaveAllAsync();
        }
    }

    private void OnCloseAll(object? sender, RoutedEventArgs e)
    {
        MainViewModel?.CloseAllTabs();
    }

    private void OnNewUntitledFile(object? sender, RoutedEventArgs e)
    {
        MainViewModel?.NewDocument();
    }

    private void OnOpenEditorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is EditorTabViewModel tab)
        {
            MainViewModel?.ActivateTab(tab);
        }
    }

    private async void OnCloseEditorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is EditorTabViewModel tab)
        {
            var window = this.FindAncestorOfType<MainWindow>();
            if (window != null)
            {
                await window.CloseTabWithPromptAsync(tab);
                return;
            }

            MainViewModel?.CloseTab(tab);
        }
    }

    private async void OnOpenRecentEditorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is RecentEditorItem item)
        {
            var vm = MainViewModel;
            if (vm != null)
            {
                await vm.OpenRecentEditorAsync(item);
            }
        }
    }

    private void OnClearRecentEditors(object? sender, RoutedEventArgs e)
    {
        MainViewModel?.ClearRecentEditors();
    }

    private async void OnNewFileInRoot(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            await ViewModel.CreateNewFileInRootAsync();
        }
    }

    private void OnNewFolderInRoot(object? sender, RoutedEventArgs e)
    {
        ViewModel?.CreateNewFolderInRoot();
    }

    private void OnRefreshExplorer(object? sender, RoutedEventArgs e)
    {
        ViewModel?.Refresh();
    }

    private void OnCollapseAllExplorerFolders(object? sender, RoutedEventArgs e)
    {
        ViewModel?.CollapseAll();
    }

    private void OnFoldAllFromWorkspaceTitle(object? sender, RoutedEventArgs e)
    {
        ViewModel?.CollapseAll();
    }

    private void OnUnfoldAllFromWorkspaceTitle(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ExpandAll();
    }

    private void OnToggleOpenEditorsSection(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        ViewModel.IsOpenEditorsExpanded = !ViewModel.IsOpenEditorsExpanded;
    }

    private void OnToggleFilesSection(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        ViewModel.IsFilesExpanded = !ViewModel.IsFilesExpanded;
    }

    private void OnToggleRecentEditorsSection(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        ViewModel.IsRecentEditorsExpanded = !ViewModel.IsRecentEditorsExpanded;
    }

    private void OnOpenSectionsMenu(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        if (button.ContextMenu != null)
        {
            button.ContextMenu.Open(button);
        }
    }

    private void OnResetExplorerLayout(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ResetLayout();
    }

    private async void OnOpenFolder(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var provider = topLevel?.StorageProvider;
        if (provider == null)
        {
            return;
        }

        var result = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });

        var folder = result.FirstOrDefault();
        var path = folder?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            var window = this.FindAncestorOfType<MainWindow>();
            if (window != null)
            {
                await window.OpenFolderWithWorkspaceDetectionAsync(path);
            }
            else
            {
                ViewModel?.LoadFolder(path);
            }
        }
    }

    private async void OnFlatItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is ExplorerItem item
            && ViewModel != null && !item.IsPlaceholder)
        {
            if (item.IsFolder)
                await ViewModel.ToggleItemAsync(item);
            else
                await ViewModel.TryOpenFileAsync(item.FullPath);
        }
    }
}
