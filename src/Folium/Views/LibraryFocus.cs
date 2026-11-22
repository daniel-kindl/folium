using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using Folium.ViewModels;

namespace Folium.Views;

/// <summary>Moves focus to the first field of a screen when the screen opens.</summary>
/// <param name="root">The view that holds the fields.</param>
internal sealed class LibraryFocus(Control root)
{
    private LibraryViewModel? _observed;

    /// <summary>Follows a new data context. The previous one is released.</summary>
    /// <param name="context">The new data context. Other types are ignored.</param>
    public void Observe(object? context)
    {
        _observed?.PropertyChanged -= Changed;
        _observed = context as LibraryViewModel;
        _observed?.PropertyChanged += Changed;
    }

    private void Changed(object? sender, PropertyChangedEventArgs args)
    {
        if (_observed is null)
        {
            return;
        }

        if (string.Equals(args.PropertyName, nameof(LibraryViewModel.ShowSavedSearch), StringComparison.Ordinal) && _observed.ShowSavedSearch)
        {
            FocusLater("LibrarySearchInput");
        }
        else if (string.Equals(args.PropertyName, nameof(LibraryViewModel.ShowEditor), StringComparison.Ordinal) && _observed.ShowEditor)
        {
            FocusLater("TitleField");
        }
    }

    private void FocusLater(string name)
    {
        Dispatcher.UIThread.Post(() => root.FindControl<TextBox>(name)?.Focus());
    }
}
