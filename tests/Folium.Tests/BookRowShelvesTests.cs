using Folium.Books;
using Folium.Storage;
using Folium.ViewModels;

namespace Folium.Tests;

public sealed class BookRowShelvesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "folium-rowshelves-" + Guid.NewGuid().ToString("N"));

    public BookRowShelvesTests()
    {
        _ = Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void RowsNameTheShelvesOfEachBookInShelfOrder()
    {
        string path = Path.Combine(_directory, "library.json");
        PersonalLibrary library = new(new FileBookStore(path));
        Shelf science = library.CreateShelf("Science fiction");
        Shelf favourites = library.CreateShelf("Favourites");
        _ = library.Add("Dune", "Frank Herbert", shelfIds: [science.Id, favourites.Id]);
        _ = library.Add("Piranesi", "Susanna Clarke");

        LibraryViewModel viewModel = LibraryViewModel.Open(new FileBookStore(path));

        BookRow dune = viewModel.ShownBooks.Single(row => string.Equals(row.Title, "Dune", StringComparison.Ordinal));
        BookRow piranesi = viewModel.ShownBooks.Single(row => string.Equals(row.Title, "Piranesi", StringComparison.Ordinal));
        Assert.Equal("Favourites, Science fiction", dune.Shelves);
        Assert.True(dune.HasShelves);
        Assert.Equal(string.Empty, piranesi.Shelves);
        Assert.False(piranesi.HasShelves);
    }

    [Fact]
    public void RenamingAShelfUpdatesTheRows()
    {
        string path = Path.Combine(_directory, "library.json");
        PersonalLibrary library = new(new FileBookStore(path));
        Shelf shelf = library.CreateShelf("Favourites");
        _ = library.Add("Dune", "Frank Herbert", shelfIds: [shelf.Id]);
        LibraryViewModel viewModel = LibraryViewModel.Open(new FileBookStore(path));

        viewModel.ShelfState!.SelectManagedShelfCommand.Execute(shelf.Id);
        viewModel.ShelfState.ShelfNameInput = "Best";
        viewModel.ShelfState.RenameShelfCommand.Execute(parameter: null);

        Assert.Equal("Best", viewModel.ShownBooks.Single().Shelves);
    }
}
