using Folium.Books;
using Folium.Storage;

namespace Folium.Tests;

public class PersonalLibraryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "folium-" + Guid.NewGuid().ToString("N"));

    public PersonalLibraryTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string LibraryPath => Path.Combine(_directory, "library.json");

    private PersonalLibrary Open() => new(new FileBookStore(LibraryPath));

    [Fact]
    public void AddStoresTrimmedTitleAndAuthorAcrossRestart()
    {
        var book = Open().Add("  Dune  ", "  Frank Herbert  ");

        var stored = Assert.Single(Open().Books);

        Assert.Equal(book.Id, stored.Id);
        Assert.Equal("Dune", stored.Title);
        Assert.Equal("Frank Herbert", stored.Author);
    }

    [Fact]
    public void AddRejectsBlankTitleAndDoesNotCreateFile()
    {
        var error = Assert.Throws<BookInputException>(() => Open().Add("   ", "Frank Herbert"));

        Assert.Equal([BookInputProblem.MissingTitle], error.Problems);
        Assert.False(File.Exists(LibraryPath));
    }

    [Fact]
    public void AddRejectsBlankAuthorAndDoesNotCreateFile()
    {
        var error = Assert.Throws<BookInputException>(() => Open().Add("Dune", " "));

        Assert.Equal([BookInputProblem.MissingAuthor], error.Problems);
        Assert.False(File.Exists(LibraryPath));
    }

    [Fact]
    public void AddRejectsMissingTitleAndAuthor()
    {
        var error = Assert.Throws<BookInputException>(() => Open().Add(null, null));

        Assert.Equal([BookInputProblem.MissingTitle, BookInputProblem.MissingAuthor], error.Problems);
        Assert.False(File.Exists(LibraryPath));
    }

    [Fact]
    public void AddAllowsTwoBooksWithTheSameTitleAndAuthor()
    {
        var library = Open();

        var first = library.Add("Dune", "Frank Herbert");
        var second = library.Add("Dune", "Frank Herbert");

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal([first, second], Open().Books);
    }

    [Fact]
    public void MissingFileIsAnEmptyLibraryAndDoesNotCreateFile()
    {
        var books = Open().Books;

        Assert.Empty(books);
        Assert.False(File.Exists(LibraryPath));
    }

    [Fact]
    public void UnreadableFileStaysUnchanged()
    {
        File.WriteAllText(LibraryPath, "{");
        var bytes = File.ReadAllBytes(LibraryPath);

        Assert.Throws<LibraryStoreException>(() => Open());

        Assert.Equal(bytes, File.ReadAllBytes(LibraryPath));
    }

    [Fact]
    public void UnsupportedVersionStaysUnchanged()
    {
        File.WriteAllText(LibraryPath, """{"version":2,"books":[]}""");
        var bytes = File.ReadAllBytes(LibraryPath);

        Assert.Throws<LibraryStoreException>(() => Open());

        Assert.Equal(bytes, File.ReadAllBytes(LibraryPath));
    }

    [Fact]
    public void StoredBookWithoutTitleStaysUnchanged()
    {
        var id = Guid.NewGuid();
        File.WriteAllText(LibraryPath, $$"""{"version":1,"books":[{"id":"{{id}}","title":" ","author":"Frank Herbert"}]}""");
        var bytes = File.ReadAllBytes(LibraryPath);

        Assert.Throws<LibraryStoreException>(() => Open());

        Assert.Equal(bytes, File.ReadAllBytes(LibraryPath));
    }

    [Fact]
    public void ReplaceRejectsDuplicateIdsAndDoesNotCreateFile()
    {
        var id = Guid.NewGuid();
        var store = new FileBookStore(LibraryPath);
        var book = new Book(id, "Dune", "Frank Herbert");

        Assert.Throws<LibraryStoreException>(() => store.Replace([book, book]));

        Assert.False(File.Exists(LibraryPath));
    }

    [Fact]
    public void DuplicateStoredIdsStayUnchanged()
    {
        var id = Guid.NewGuid();
        File.WriteAllText(
            LibraryPath,
            $$"""{"version":1,"books":[{"id":"{{id}}","title":"Dune","author":"Frank Herbert"},{"id":"{{id}}","title":"Dune","author":"Frank Herbert"}]}""");
        var bytes = File.ReadAllBytes(LibraryPath);

        Assert.Throws<LibraryStoreException>(() => Open());

        Assert.Equal(bytes, File.ReadAllBytes(LibraryPath));
    }

    [Fact]
    public void EditChangesTitleAndAuthorAndKeepsId()
    {
        var library = Open();
        var book = library.Add("Dune", "Frank Herbert");
        var other = library.Add("Neuromancer", "William Gibson");

        var edited = library.Edit(book.Id, "  Dune Messiah  ", " Frank Herbert ");

        Assert.Equal(book.Id, edited.Id);
        Assert.Equal("Dune Messiah", edited.Title);
        Assert.Equal("Frank Herbert", edited.Author);
        Assert.Equal([edited, other], Open().Books);
    }

    [Fact]
    public void EditRejectsBlankTextAndKeepsStoredBook()
    {
        var library = Open();
        var book = library.Add("Dune", "Frank Herbert");

        var error = Assert.Throws<BookInputException>(() => library.Edit(book.Id, " ", "Frank Herbert"));

        Assert.Equal([BookInputProblem.MissingTitle], error.Problems);
        Assert.Equal([book], Open().Books);
    }

    [Fact]
    public void EditMissingBookDoesNotCreateIt()
    {
        var library = Open();
        library.Add("Dune", "Frank Herbert");
        var missing = Guid.NewGuid();

        var error = Assert.Throws<MissingBookException>(() => library.Edit(missing, "Neuromancer", "William Gibson"));

        Assert.Equal(missing, error.BookId);
        Assert.DoesNotContain(Open().Books, book => book.Id == missing);
    }

    [Fact]
    public void RemoveDeletesOnlyTheSelectedBook()
    {
        var library = Open();
        var keep = library.Add("Dune", "Frank Herbert");
        var remove = library.Add("Dune", "Frank Herbert");

        library.Remove(remove.Id);

        Assert.Equal([keep], Open().Books);
    }

    [Fact]
    public void RemoveMissingBookFailsAndKeepsLibrary()
    {
        var library = Open();
        var book = library.Add("Dune", "Frank Herbert");
        var missing = Guid.NewGuid();

        var error = Assert.Throws<MissingBookException>(() => library.Remove(missing));

        Assert.Equal(missing, error.BookId);
        Assert.Equal([book], Open().Books);
    }

    [Fact]
    public void SecondRemoveOfTheSameBookFails()
    {
        var library = Open();
        var book = library.Add("Dune", "Frank Herbert");
        library.Remove(book.Id);

        Assert.Throws<MissingBookException>(() => library.Remove(book.Id));

        Assert.Empty(Open().Books);
    }

    [Fact]
    public void FailedWriteKeepsPreviousBooks()
    {
        var store = new FailOnSecondWriteStore(new FileBookStore(LibraryPath));
        var library = new PersonalLibrary(store);
        var book = library.Add("Dune", "Frank Herbert");

        Assert.Throws<LibraryStoreException>(() => library.Add("Neuromancer", "William Gibson"));

        Assert.Equal([book], library.Books);
        Assert.Equal([book], new PersonalLibrary(new FileBookStore(LibraryPath)).Books);
    }

    private sealed class FailOnSecondWriteStore : IBookStore
    {
        private readonly IBookStore _store;
        private int _writes;

        public FailOnSecondWriteStore(IBookStore store) => _store = store;

        public IReadOnlyList<Book> Read() => _store.Read();

        public void Replace(IReadOnlyList<Book> books)
        {
            _writes++;
            if (_writes > 1)
            {
                throw new LibraryStoreException("Folium could not write the library file.");
            }

            _store.Replace(books);
        }
    }
}
