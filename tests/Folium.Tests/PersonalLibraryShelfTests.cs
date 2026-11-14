using System.Text;
using Folium.Books;
using Folium.Storage;

namespace Folium.Tests;

public sealed class PersonalLibraryShelfTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "folium-" + Guid.NewGuid().ToString("N"));

    public PersonalLibraryShelfTests()
    {
        _ = Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ShelfCanExistWithoutBooksAndCanBeRenamed()
    {
        PersonalLibrary library = Open();
        Shelf shelf = library.CreateShelf("  Favourites  ");

        Shelf renamed = library.RenameShelf(shelf.Id, " Read again ");

        Assert.Equal("Read again", renamed.Name);
        Assert.Equal([renamed], Open().Shelves);
        Assert.Empty(Open().Books);
    }

    [Fact]
    public void ShelfNamesMustBeTrimmedNonEmptyAtMostFortyCharactersAndUniqueWhenFolded()
    {
        PersonalLibrary library = Open();
        _ = library.CreateShelf("Café");

        ShelfNameException empty = Assert.Throws<ShelfNameException>(() => library.CreateShelf(" "));
        ShelfNameException tooLong = Assert.Throws<ShelfNameException>(() => library.CreateShelf(new string('x', 41)));
        ShelfNameException duplicate = Assert.Throws<ShelfNameException>(() => library.CreateShelf("CAFE"));

        Assert.Equal("Enter a shelf name.", empty.Message);
        Assert.Equal("A shelf name can contain at most 40 characters.", tooLong.Message);
        Assert.Equal("A shelf with this name already exists.", duplicate.Message);
        _ = Assert.Single(library.Shelves);
    }

    [Fact]
    public void ShelfNameLimitCountsUnicodeCodePoints()
    {
        PersonalLibrary library = Open();
        StringBuilder characters = new();
        for (int index = 0; index < 40; index++)
        {
            _ = characters.Append("📚");
        }

        string fortyCharacters = characters.ToString();

        Shelf shelf = library.CreateShelf(fortyCharacters);

        Assert.Equal(fortyCharacters, shelf.Name);
        _ = Assert.Throws<ShelfNameException>(() => library.CreateShelf(characters.Append("📚").ToString()));
    }

    [Fact]
    public void BookCanBelongToManyShelvesAndDeletingOneKeepsBookAndStatus()
    {
        PersonalLibrary library = Open();
        Book book = library.Add("Dune", "Frank Herbert");
        book = library.SetStatus(book.Id, ReadingStatus.Finished);
        Shelf favourites = library.CreateShelf("Favourites");
        Shelf reread = library.CreateShelf("Reread");

        book = library.SetShelves(book.Id, [favourites.Id, reread.Id]);
        library.DeleteShelf(favourites.Id);

        Book saved = Assert.Single(Open().Books);
        Assert.Equal(ReadingStatus.Finished, saved.Status);
        Assert.Equal([reread.Id], saved.ShelfIds);
        Assert.Equal("Dune", saved.Title);
        Assert.DoesNotContain(library.Shelves, shelf => shelf.Id == favourites.Id);
        Assert.Contains(library.Shelves, shelf => shelf.Id == reread.Id);
        Assert.DoesNotContain(Open().Shelves, shelf => shelf.Id == favourites.Id);
        Assert.Contains(book.ShelfIds, id => id == favourites.Id);
    }

    [Fact]
    public void FailedShelfChangesKeepTheSavedSnapshotInMemoryAndOnDisk()
    {
        FileBookStore files = new(LibraryPath);
        FailingStore store = new(files);
        PersonalLibrary library = new(store);
        Shelf shelf = library.CreateShelf("Favourites");
        Book book = library.Add("Dune", "Frank Herbert");
        _ = library.SetShelves(book.Id, [shelf.Id]);
        store.FailWrites = true;

        _ = Assert.Throws<LibraryStoreException>(() => library.RenameShelf(shelf.Id, "Read again"));
        _ = Assert.Throws<LibraryStoreException>(() => library.DeleteShelf(shelf.Id));

        Assert.Equal("Favourites", Assert.Single(library.Shelves).Name);
        Assert.Contains(shelf.Id, (IReadOnlySet<Guid>)Assert.Single(library.Books).ShelfIds);
        LibrarySnapshot saved = files.Read();
        Assert.Equal("Favourites", Assert.Single(saved.Shelves).Name);
        Assert.Contains(shelf.Id, (IReadOnlySet<Guid>)Assert.Single(saved.Books).ShelfIds);
    }

    [Fact]
    public void ShelfOperationsRejectAnUnknownShelfId()
    {
        PersonalLibrary library = Open();
        Shelf shelf = library.CreateShelf("Favourites");
        Guid missingId = Guid.NewGuid();

        _ = Assert.Throws<MissingShelfException>(() => library.RenameShelf(missingId, "Read again"));
        _ = Assert.Throws<MissingShelfException>(() => library.DeleteShelf(missingId));

        Assert.Equal([shelf], library.Shelves);
    }

    [Fact]
    public void SettingShelvesRejectsOneUnknownIdEvenWhenAnotherIdIsValid()
    {
        PersonalLibrary library = Open();
        Shelf shelf = library.CreateShelf("Favourites");
        Book book = library.Add("Dune", "Frank Herbert");

        ArgumentException exception = Assert.Throws<ArgumentException>(() => library.SetShelves(book.Id, [shelf.Id, Guid.NewGuid()]));

        Assert.Equal("shelfIds", exception.ParamName);
        Assert.Equal("The shelf list contains an unknown shelf. (Parameter 'shelfIds')", exception.Message);
        Assert.Empty(Assert.Single(library.Books).ShelfIds);
        Assert.Empty(Assert.Single(Open().Books).ShelfIds);
    }

    [Fact]
    public void AddingABookRejectsShelfIdsOutsideTheLibrary()
    {
        PersonalLibrary library = Open();
        Shelf shelf = library.CreateShelf("Favourites");

        ArgumentException exception = Assert.Throws<ArgumentException>(() => library.Add(
            "Dune",
            "Frank Herbert",
            shelfIds: [shelf.Id, Guid.NewGuid()]));

        Assert.Equal("shelfIds", exception.ParamName);
        Assert.Equal("The shelf list contains an unknown shelf. (Parameter 'shelfIds')", exception.Message);
        Assert.Empty(library.Books);
        Assert.Empty(Open().Books);
    }

    [Fact]
    public void ReplaceAllRejectsAnEmptyBookEntryAndKeepsTheCurrentSnapshot()
    {
        PersonalLibrary library = Open();
        Book book = library.Add("Dune", "Frank Herbert");
        Shelf shelf = library.CreateShelf("Favourites");
        _ = library.SetShelves(book.Id, [shelf.Id]);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => library.ReplaceAll([null!]));

        Assert.Equal("books", exception.ParamName);
        Assert.Equal("The library cannot replace an empty book. (Parameter 'books')", exception.Message);
        Assert.Equal([shelf], library.Shelves);
        Assert.Contains(shelf.Id, (IReadOnlySet<Guid>)Assert.Single(library.Books).ShelfIds);
        Assert.Contains(shelf.Id, (IReadOnlySet<Guid>)Assert.Single(Open().Books).ShelfIds);
    }

    [Fact]
    public void ReplaceSnapshotRejectsNullAndKeepsTheCurrentSnapshot()
    {
        PersonalLibrary library = Open();
        Book book = library.Add("Dune", "Frank Herbert");

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => library.ReplaceSnapshot(null!));

        Assert.Equal("snapshot", exception.ParamName);
        Assert.Equal([book], library.Books);
        Assert.Equal([book], Open().Books);
    }

    [Fact]
    public void ReplaceSnapshotUpdatesBooksAndShelvesInMemoryAndOnDisk()
    {
        PersonalLibrary library = Open();
        _ = library.Add("Dune", "Frank Herbert");
        _ = library.CreateShelf("Old shelf");
        Guid newShelfId = Guid.NewGuid();
        Shelf newShelf = new(newShelfId, "Reading list");
        Book newBook = new(Guid.NewGuid(), "Neuromancer", "William Gibson", shelfIds: [newShelfId]);
        LibrarySnapshot replacement = new([newBook], [newShelf]);

        library.ReplaceSnapshot(replacement);

        Assert.Equal([newBook], library.Books);
        Assert.Equal([newShelf], library.Shelves);
        LibrarySnapshot reopened = new FileBookStore(LibraryPath).Read();
        Assert.Equal(newBook.Id, Assert.Single(reopened.Books).Id);
        Assert.Equal(newShelf, Assert.Single(reopened.Shelves));
        Assert.Contains(newShelfId, (IReadOnlySet<Guid>)Assert.Single(reopened.Books).ShelfIds);
    }

    [Fact]
    public void ReplaceAllRemovesShelvesAndTheirBookMemberships()
    {
        PersonalLibrary library = Open();
        Book book = library.Add("Dune", "Frank Herbert");
        Shelf shelf = library.CreateShelf("Favourites");
        book = library.SetShelves(book.Id, [shelf.Id]);

        library.ReplaceAll([book]);

        Book replaced = Assert.Single(library.Books);
        Assert.Empty(library.Shelves);
        Assert.Empty(replaced.ShelfIds);
        LibrarySnapshot saved = new FileBookStore(LibraryPath).Read();
        Assert.Empty(saved.Shelves);
        Assert.Empty(Assert.Single(saved.Books).ShelfIds);
    }

    private string LibraryPath => Path.Combine(_directory, "library.json");

    private PersonalLibrary Open()
    {
        return new(new FileBookStore(LibraryPath));
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
