using Avalonia.Controls;
using Folium.Books;
using Folium.ViewModels;

namespace Folium.Views;

/// <summary>Shows the menu that changes the reading status of one book in the list.</summary>
internal static class StatusMenu
{
    private static readonly ReadingStatus[] Choices = [ReadingStatus.ToRead, ReadingStatus.Reading, ReadingStatus.Finished];

    /// <summary>Opens the status menu under the status button of a row.</summary>
    /// <param name="anchor">The status button that opens the menu.</param>
    /// <param name="row">The row whose book changes.</param>
    /// <param name="library">The library that saves the change.</param>
    public static void Show(Button anchor, BookRow row, LibraryViewModel library)
    {
        Build(row, library).ShowAt(anchor);
    }

    /// <summary>Builds the status menu. The item for the current status is checked.</summary>
    /// <param name="row">The row whose book changes.</param>
    /// <param name="library">The library that saves the change.</param>
    internal static MenuFlyout Build(BookRow row, LibraryViewModel library)
    {
        MenuFlyout menu = new() { Placement = PlacementMode.BottomEdgeAlignedLeft };
        foreach (ReadingStatus status in Choices)
        {
            MenuItem item = new()
            {
                Header = LibraryViewModel.StatusText(status),
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = status == row.ReadingStatus,
            };
            item.Click += (_, _) => library.ChangeStatus(row.Id, status);
            _ = menu.Items.Add(item);
        }

        return menu;
    }
}
