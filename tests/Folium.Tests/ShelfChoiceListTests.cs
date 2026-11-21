using Folium.Books;
using Folium.Storage;
using Folium.ViewModels;

namespace Folium.Tests;

public sealed class ShelfChoiceListTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "folium-choices-" + Guid.NewGuid().ToString("N"));

    public ShelfChoiceListTests()
    {
        _ = Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void FilterChoicesMarkTheSelectedShelfAndAllShelvesWhenNoneIsSelected()
    {
        (ShelvesViewModel state, Shelf first, Shelf second) = OpenWithTwoShelves();

        Assert.True(state.IsAllShelvesSelected);
        Assert.All(state.FilterChoices, choice => Assert.False(choice.IsSelected));

        state.SelectFilterCommand.Execute(second.Id);

        Assert.False(state.IsAllShelvesSelected);
        Assert.Equal([first.Id, second.Id], state.FilterChoices.Select(choice => choice.Id));
        Assert.Equal([false, true], state.FilterChoices.Select(choice => choice.IsSelected));

        state.SelectFilterCommand.Execute("all");

        Assert.True(state.IsAllShelvesSelected);
    }

    [Fact]
    public void ManagedChoicesMarkTheShelfSelectedForRenameOrDelete()
    {
        (ShelvesViewModel state, Shelf first, Shelf second) = OpenWithTwoShelves();

        Assert.All(state.ManagedChoices, choice => Assert.False(choice.IsSelected));

        state.SelectManagedShelfCommand.Execute(first.Id);

        Assert.Equal([true, false], state.ManagedChoices.Select(choice => choice.IsSelected));
        Assert.Equal([first.Name, second.Name], state.ManagedChoices.Select(choice => choice.Name), StringComparer.Ordinal);
    }

    [Fact]
    public void ChoiceListsRaiseChangeNotificationsWhenTheSelectionMoves()
    {
        (ShelvesViewModel state, Shelf first, _) = OpenWithTwoShelves();
        List<string?> raised = [];
        state.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        state.SelectFilterCommand.Execute(first.Id);
        state.SelectManagedShelfCommand.Execute(first.Id);

        Assert.Contains(raised, name => string.Equals(name, nameof(ShelvesViewModel.FilterChoices), StringComparison.Ordinal));
        Assert.Contains(raised, name => string.Equals(name, nameof(ShelvesViewModel.IsAllShelvesSelected), StringComparison.Ordinal));
        Assert.Contains(raised, name => string.Equals(name, nameof(ShelvesViewModel.ManagedChoices), StringComparison.Ordinal));
    }

    private (ShelvesViewModel State, Shelf First, Shelf Second) OpenWithTwoShelves()
    {
        string path = Path.Combine(_directory, "library.json");
        PersonalLibrary library = new(new FileBookStore(path));
        Shelf first = library.CreateShelf("Favourites");
        Shelf second = library.CreateShelf("Science fiction");
        LibraryViewModel viewModel = LibraryViewModel.Open(new FileBookStore(path));
        return (viewModel.ShelfState!, first, second);
    }
}
