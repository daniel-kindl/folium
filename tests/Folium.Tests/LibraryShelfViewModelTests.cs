using Folium.Books;
using Folium.Storage;
using Folium.ViewModels;

namespace Folium.Tests;

public sealed class LibraryShelfViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "folium-shelves-" + Guid.NewGuid().ToString("N"));

    public LibraryShelfViewModelTests()
    {
        _ = Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ShelfStatusAndSearchFiltersCombineAndStayInSession()
    {
        string path = LibraryPath;
        PersonalLibrary library = new(new FileBookStore(path));
        Shelf shelf = library.CreateShelf("Favourites");
        Book match = library.Add("Café Society", "René Writer", shelfIds: [shelf.Id]);
        _ = library.SetStatus(match.Id, ReadingStatus.Reading);
        _ = library.Add("Café Society", "René Writer");
        LibraryViewModel viewModel = LibraryViewModel.Open(new FileBookStore(path));

        viewModel.ShelfState!.SelectFilterCommand.Execute(shelf.Id);
        viewModel.FilterCommand.Execute("reading");
        viewModel.SearchQuery = "CAFE";

        Assert.Equal(["Café Society"], Titles(viewModel));
        Assert.Equal("Reading, Favourites", viewModel.FilterLabel);
        Assert.Equal("1 book matches your search, status, and shelf", viewModel.CountLabel);
        viewModel.SearchQuery = "missing";
        Assert.Equal("No books match your search, status, and shelf.", viewModel.NoMatchMessage);
        Assert.Equal("0 books match your search, status, and shelf", viewModel.CountLabel);

        viewModel.SearchQuery = "CAFE";
        viewModel.OpenCommand.Execute(match.Id);
        viewModel.EditCommand.Execute(parameter: null);
        viewModel.CancelCommand.Execute(parameter: null);
        viewModel.BackCommand.Execute(parameter: null);

        Assert.True(viewModel.ShowLibrary);
        Assert.Equal("CAFE", viewModel.SearchQuery);
        Assert.Equal(shelf.Id, viewModel.ShelfState.FilterId);
        _ = Assert.Single(viewModel.ShownBooks);

        LibraryViewModel reopened = LibraryViewModel.Open(new FileBookStore(path));

        Assert.Null(reopened.ShelfState!.FilterId);
        Assert.Equal(string.Empty, reopened.SearchQuery);
        Assert.True(reopened.IsAllSelected);
        Assert.Equal(2, reopened.ShownBooks.Count);
    }

    [Fact]
    public void AddAndEditSaveShelfMembershipWithTheBook()
    {
        string path = LibraryPath;
        PersonalLibrary library = new(new FileBookStore(path));
        Shelf first = library.CreateShelf("Favourites");
        Shelf second = library.CreateShelf("Read again");
        LibraryViewModel viewModel = LibraryViewModel.Open(new FileBookStore(path));
        ShelvesViewModel shelves = viewModel.ShelfState!;

        viewModel.ShowAddCommand.Execute(parameter: null);
        viewModel.TitleInput = "Dune";
        viewModel.AuthorInput = "Frank Herbert";
        shelves.ToggleMembershipCommand.Execute(first.Id);
        viewModel.SaveCommand.Execute(parameter: null);

        Book saved = Assert.Single(viewModel.Books);
        Assert.Contains(first.Id, (IReadOnlySet<Guid>)saved.ShelfIds);
        viewModel.OpenCommand.Execute(saved.Id);
        viewModel.EditCommand.Execute(parameter: null);
        shelves.ToggleMembershipCommand.Execute(first.Id);
        shelves.ToggleMembershipCommand.Execute(second.Id);
        viewModel.SaveCommand.Execute(parameter: null);

        Book edited = Assert.Single(LibraryViewModel.Open(new FileBookStore(path)).Books);
        Assert.DoesNotContain(first.Id, (IReadOnlySet<Guid>)edited.ShelfIds);
        Assert.Contains(second.Id, (IReadOnlySet<Guid>)edited.ShelfIds);
    }

    [Fact]
    public void EmptyShelfCanBeFilteredAndInvalidSelectionKeepsTheCurrentFilter()
    {
        PersonalLibrary library = new(new FileBookStore(LibraryPath));
        _ = library.Add("Dune", "Frank Herbert");
        Shelf emptyShelf = library.CreateShelf("Waiting");
        LibraryViewModel viewModel = LibraryViewModel.Open(new FileBookStore(LibraryPath));

        viewModel.ShelfState!.SelectFilterCommand.Execute(emptyShelf.Id);
        Assert.Equal("No books on this shelf.", viewModel.NoMatchMessage);
        viewModel.ShelfState.SelectFilterCommand.Execute(Guid.NewGuid());
        Assert.Equal(emptyShelf.Id, viewModel.ShelfState.FilterId);
        viewModel.ShelfState.SelectFilterCommand.Execute(emptyShelf.Id.ToString());
        Assert.Equal(emptyShelf.Id, viewModel.ShelfState.FilterId);
        viewModel.ShelfState.SelectFilterCommand.Execute(new object());
        Assert.Equal(emptyShelf.Id, viewModel.ShelfState.FilterId);
        viewModel.ShelfState.SelectFilterCommand.Execute(parameter: null);
        Assert.Null(viewModel.ShelfState.FilterId);
        viewModel.ShelfState.SelectFilterCommand.Execute(emptyShelf.Id);
        viewModel.ShelfState.SelectFilterCommand.Execute("all");

        Assert.Null(viewModel.ShelfState.FilterId);
        _ = Assert.Single(viewModel.ShownBooks);
    }

    [Fact]
    public void ShelfChecklistSavesMembershipAndCancelRestoresTheSavedChoice()
    {
        PersonalLibrary library = new(new FileBookStore(LibraryPath));
        Shelf shelf = library.CreateShelf("Favourites");
        Book book = library.Add("Dune", "Frank Herbert");
        LibraryViewModel viewModel = LibraryViewModel.Open(new FileBookStore(LibraryPath));
        ShelvesViewModel shelves = viewModel.ShelfState!;
        viewModel.OpenCommand.Execute(book.Id);

        viewModel.OpenShelfChecklistCommand.Execute(parameter: null);
        shelves.ToggleMembershipCommand.Execute(shelf.Id);
        shelves.CloseChecklistCommand.Execute(parameter: null);
        Assert.Empty(shelves.SelectedShelfIds);

        viewModel.OpenShelfChecklistCommand.Execute(parameter: null);
        shelves.ToggleMembershipCommand.Execute(shelf.Id);
        shelves.SaveMembershipCommand.Execute(parameter: null);

        Assert.False(shelves.ShowChecklist);
        Assert.Contains(shelf.Id, (IReadOnlySet<Guid>)Assert.Single(viewModel.Books).ShelfIds);
        Assert.Contains(shelf.Id, (IReadOnlySet<Guid>)Assert.Single(new FileBookStore(LibraryPath).Read().Books).ShelfIds);
    }

    [Fact]
    public void FailedShelfChecklistSaveShowsAnErrorAndKeepsTheDialogOpen()
    {
        FileBookStore files = new(LibraryPath);
        PersonalLibrary setup = new(files);
        Shelf shelf = setup.CreateShelf("Favourites");
        Book book = setup.Add("Dune", "Frank Herbert");
        FailingStore store = new(files);
        LibraryViewModel viewModel = LibraryViewModel.Open(store);
        viewModel.OpenCommand.Execute(book.Id);
        viewModel.OpenShelfChecklistCommand.Execute(parameter: null);
        viewModel.ShelfState!.ToggleMembershipCommand.Execute(shelf.Id);
        store.FailWrites = true;

        viewModel.ShelfState.SaveMembershipCommand.Execute(parameter: null);

        Assert.True(viewModel.ShelfState.ShowChecklist);
        Assert.Equal("Folium could not save these shelves.", viewModel.ShelfState.ShelfNameError);
        Assert.Empty(Assert.Single(files.Read().Books).ShelfIds);
    }

    [Fact]
    public void ShelfControlsCreateRenameAndConfirmDeletion()
    {
        PersonalLibrary library = new(new FileBookStore(LibraryPath));
        Book book = library.Add("Dune", "Frank Herbert");
        ShelvesViewModel viewModel = new(library) { ShelfNameInput = "  Favourites  " };
        int renameCommandNotifications = 0;
        viewModel.RenameShelfCommand.CanExecuteChanged += (_, _) => renameCommandNotifications++;

        viewModel.CreateShelfCommand.Execute(parameter: null);

        Shelf shelf = Assert.Single(viewModel.Shelves);
        Assert.Equal("Favourites", shelf.Name);
        Assert.True(renameCommandNotifications > 0);
        Assert.Equal(shelf.Id, viewModel.ManagedShelfId);
        viewModel.SelectManagedShelfCommand.Execute(shelf.Id);
        viewModel.ShelfNameInput = "Read again";
        viewModel.RenameShelfCommand.Execute(parameter: null);
        Assert.Equal("Read again", Assert.Single(viewModel.Shelves).Name);
        _ = library.SetShelves(book.Id, [shelf.Id]);
        viewModel.AskDeleteShelfCommand.Execute(parameter: null);

        Assert.True(viewModel.ShowDeleteConfirmation);
        Assert.Equal("Delete the shelf Read again? Books on this shelf will stay in your library.", viewModel.DeleteMessage);
        viewModel.ConfirmDeleteShelfCommand.Execute(parameter: null);

        Assert.False(viewModel.ShowDeleteConfirmation);
        Assert.Empty(viewModel.Shelves);
        Book remaining = Assert.Single(library.Books);
        Assert.Equal(book.Id, remaining.Id);
        Assert.Empty(remaining.ShelfIds);
    }

    [Fact]
    public void RenameShowsValidationAndStorageErrors()
    {
        FileBookStore files = new(LibraryPath);
        FailingStore store = new(files);
        PersonalLibrary library = new(store);
        Shelf first = library.CreateShelf("Café");
        _ = library.CreateShelf("Read again");
        ShelvesViewModel viewModel = new(library);
        viewModel.SelectManagedShelfCommand.Execute(first.Id);
        viewModel.ShelfNameInput = "READ AGAIN";

        viewModel.RenameShelfCommand.Execute(parameter: null);

        Assert.Equal("A shelf with this name already exists.", viewModel.ShelfNameError);
        Assert.Equal("Café", library.Shelves.Single(shelf => shelf.Id == first.Id).Name);
        viewModel.ShelfNameInput = "New name";
        store.FailWrites = true;

        viewModel.RenameShelfCommand.Execute(parameter: null);

        Assert.Equal("Folium could not save this shelf.", viewModel.ShelfNameError);
        Assert.Equal("Café", library.Shelves.Single(shelf => shelf.Id == first.Id).Name);
    }

    private string LibraryPath => Path.Combine(_directory, "library.json");

    private static string[] Titles(LibraryViewModel viewModel)
    {
        return [.. viewModel.ShownBooks.Select(row => row.Title)];
    }

    private sealed class FailingStore(IBookStore inner) : IBookStore
    {
        public bool FailWrites { get; set; }

        public LibrarySnapshot Read()
        {
            return inner.Read();
        }

        public void Replace(LibrarySnapshot snapshot)
        {
            if (FailWrites)
            {
                throw new LibraryStoreException("Folium could not write the library file.");
            }

            inner.Replace(snapshot);
        }
    }
}
