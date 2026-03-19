using System.Reactive;
using Avalonia.Controls;
using Avalonia.Input;
using NotepadPro.ViewModels;

namespace NotepadPro.Views;

public partial class SearchView : UserControl
{
    public SearchView()
    {
        InitializeComponent();
        var list = this.FindControl<ListBox>("ResultsList");
        if (list != null)
        {
            list.DoubleTapped += OnResultDoubleTapped;
        }
    }

    private SearchViewModel? ViewModel => DataContext as SearchViewModel;

    private void OnResultDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        if (sender is not ListBox listBox)
        {
            return;
        }

        if (listBox.SelectedItem is SearchResult result)
        {
            ViewModel.GoToResultCommand.Execute(result)
                .Subscribe(Observer.Create<Unit>(_ => { }));
        }
    }
}
