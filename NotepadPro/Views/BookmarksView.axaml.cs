using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NotepadPro.Models;
using NotepadPro.ViewModels;

namespace NotepadPro.Views;

public partial class BookmarksView : UserControl
{
    public BookmarksView()
    {
        InitializeComponent();
        var scopedList = this.FindControl<ListBox>("ScopedBookmarksList");
        if (scopedList != null)
        {
            scopedList.DoubleTapped += OnBookmarkDoubleTapped;
        }

        var globalList = this.FindControl<ListBox>("GlobalBookmarksList");
        if (globalList != null)
        {
            globalList.DoubleTapped += OnBookmarkDoubleTapped;
        }
    }

    private BookmarksViewModel? ViewModel => DataContext as BookmarksViewModel;

    private async void OnOpenBookmarkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: BookmarkItem bookmark } || ViewModel == null)
        {
            return;
        }

        await ViewModel.OpenBookmarkAsync(bookmark);
    }

    private void OnRemoveBookmarkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: BookmarkItem bookmark } || ViewModel == null)
        {
            return;
        }

        ViewModel.RemoveBookmark(bookmark);
    }

    private async void OnBookmarkDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not ListBox listBox || ViewModel == null)
        {
            return;
        }

        if (listBox.SelectedItem is BookmarkItem bookmark)
        {
            await ViewModel.OpenBookmarkAsync(bookmark);
        }
    }
}