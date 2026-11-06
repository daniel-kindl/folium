using Folium.Books;
using Folium.Storage;

namespace Folium.Tests;

public class PersonalLibraryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "folium-" + Guid.NewGuid().ToString("N"));

    public PersonalLibraryTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private string LibraryPath => Path.Combine(_directory, "library.json");

    private PersonalLibrary Open()
    {
        return new(new FileBookStore(LibraryPath));
    }

    [Fact]
    public void AddStoresTrimmedTitleAndAuthorAcrossRestart()
    {
        Book book = Open().Add("  Dune  ", "  Frank Herbert  ");

        Book stored = Assert.Single(Open().Books);

        Assert.Equal(book.Id, stored.Id);
        Assert.Equal("Dune", stored.Title);
        Assert.Equal("Frank Herbert", stored.Author);
    }

    [Fact]
    public void AddRejectsBlankTitleAndDoesNotCreateFile()
    {
        BookInputException error = Assert.Throws<BookInputException>(() => Open().Add("   ", "Frank Herbert"));

        Assert.Equal([BookInputProblem.MissingTitle], error.Problems);
        Assert.False(File.Exists(LibraryPath));
    }

    [Fact]
    public void AddRejectsBlankAuthorAndDoesNotCreateFile()
    {
        BookInputException error = Assert.Throws<BookInputException>(() => Open().Add("Dune", " "));

        Assert.Equal([BookInputProblem.MissingAuthor], error.Problems);
        Assert.False(File.Exists(LibraryPath));
    }

    [Fact]
    public void AddRejectsMissingTitleAndAuthor()
    {
        BookInputException error = Assert.Throws<BookInputException>(() => Open().Add(title: null, author: null));

        Assert.Equal([BookInputProblem.MissingTitle, BookInputProblem.MissingAuthor], error.Problems);
        Assert.False(File.Exists(LibraryPath));
    }

    [Fact]
    public void AddAllowsTwoBooksWithTheSameTitleAndAuthor()
    {
        PersonalLibrary library = Open();

        Book first = library.Add("Dune", "Frank Herbert");
        Book second = library.Add("Dune", "Frank Herbert");

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal([first, second], Open().Books);
    }

    [Fact]
    public void MissingFileIsAnEmptyLibraryAndDoesNotCreateFile()
    {
        IReadOnlyList<Book> books = Open().Books;

        Assert.Empty(books);
        Assert.False(File.Exists(LibraryPath));
    }

    [Fact]
    public void UnreadableFileStaysUnchanged()
    {
        File.WriteAllText(LibraryPath, "{");
        byte[] bytes = File.ReadAllBytes(LibraryPath);

        _ = Assert.Throws<LibraryStoreException>(Open);

        Assert.Equal(bytes, File.ReadAllBytes(LibraryPath));
    }

    [Fact]
    public void UnsupportedVersionStaysUnchanged()
    {
        File.WriteAllText(LibraryPath, /*lang=json,strict*/ """{"version":2,"books":[]}""");
        byte[] bytes = File.ReadAllBytes(LibraryPath);

        _ = Assert.Throws<LibraryStoreException>(Open);

        Assert.Equal(bytes, File.ReadAllBytes(LibraryPath));
    }

    [Fact]
    public void StoredBookWithoutTitleStaysUnchanged()
    {
        Guid id = Guid.NewGuid();
        File.WriteAllText(LibraryPath, $$"""{"version":1,"books":[{"id":"{{id}}","title":" ","author":"Frank Herbert"}]}""");
        byte[] bytes = File.ReadAllBytes(LibraryPath);

        _ = Assert.Throws<LibraryStoreException>(Open);

        Assert.Equal(bytes, File.ReadAllBytes(LibraryPath));
    }

    [Fact]
    public void ReplaceRejectsDuplicateIdsAndDoesNotCreateFile()
    {
        Guid id = Guid.NewGuid();
        FileBookStore store = new(LibraryPath);
        Book book = new(id, "Dune", "Frank Herbert");

        _ = Assert.Throws<LibraryStoreException>(() => store.Replace([book, book]));

        Assert.False(File.Exists(LibraryPath));
    }

    [Fact]
    public void DuplicateStoredIdsStayUnchanged()
    {
        Guid id = Guid.NewGuid();
        File.WriteAllText(
            LibraryPath,
            $$"""{"version":1,"books":[{"id":"{{id}}","title":"Dune","author":"Frank Herbert"},{"id":"{{id}}","title":"Dune","author":"Frank Herbert"}]}""");
        byte[] bytes = File.ReadAllBytes(LibraryPath);

        _ = Assert.Throws<LibraryStoreException>(Open);

        Assert.Equal(bytes, File.ReadAllBytes(LibraryPath));
    }

    [Fact]
    public void EditChangesTitleAndAuthorAndKeepsId()
    {
        PersonalLibrary library = Open();
        Book book = library.Add("Dune", "Frank Herbert");
        Book other = library.Add("Neuromancer", "William Gibson");

        Book edited = library.Edit(book.Id, "  Dune Messiah  ", " Frank Herbert ");

        Assert.Equal(book.Id, edited.Id);
        Assert.Equal("Dune Messiah", edited.Title);
        Assert.Equal("Frank Herbert", edited.Author);
        Assert.Equal([edited, other], Open().Books);
    }

    [Fact]
    public void EditRejectsBlankTextAndKeepsStoredBook()
    {
        PersonalLibrary library = Open();
        Book book = library.Add("Dune", "Frank Herbert");

        BookInputException error = Assert.Throws<BookInputException>(() => library.Edit(book.Id, " ", "Frank Herbert"));

        Assert.Equal([BookInputProblem.MissingTitle], error.Problems);
        Assert.Equal([book], Open().Books);
    }

    [Fact]
    public void EditMissingBookDoesNotCreateIt()
    {
        PersonalLibrary library = Open();
        _ = library.Add("Dune", "Frank Herbert");
        Guid missing = Guid.NewGuid();

        MissingBookException error = Assert.Throws<MissingBookException>(() => library.Edit(missing, "Neuromancer", "William Gibson"));

        Assert.Equal(missing, error.BookId);
        Assert.DoesNotContain(Open().Books, book => book.Id == missing);
    }

    [Fact]
    public void RemoveDeletesOnlyTheSelectedBook()
    {
        PersonalLibrary library = Open();
        Book keep = library.Add("Dune", "Frank Herbert");
        Book remove = library.Add("Dune", "Frank Herbert");

        library.Remove(remove.Id);

        Assert.Equal([keep], Open().Books);
    }

    [Fact]
    public void RemoveMissingBookFailsAndKeepsLibrary()
    {
        PersonalLibrary library = Open();
        Book book = library.Add("Dune", "Frank Herbert");
        Guid missing = Guid.NewGuid();

        MissingBookException error = Assert.Throws<MissingBookException>(() => library.Remove(missing));

        Assert.Equal(missing, error.BookId);
        Assert.Equal([book], Open().Books);
    }

    [Fact]
    public void SecondRemoveOfTheSameBookFails()
    {
        PersonalLibrary library = Open();
        Book book = library.Add("Dune", "Frank Herbert");
        library.Remove(book.Id);

        _ = Assert.Throws<MissingBookException>(() => library.Remove(book.Id));

        Assert.Empty(Open().Books);
    }

    [Fact]
    public void FailedWriteKeepsPreviousBooks()
    {
        FailOnSecondWriteStore store = new(new FileBookStore(LibraryPath));
        PersonalLibrary library = new(store);
        Book book = library.Add("Dune", "Frank Herbert");

        _ = Assert.Throws<LibraryStoreException>(() => library.Add("Neuromancer", "William Gibson"));

        Assert.Equal([book], library.Books);
        Assert.Equal([book], new PersonalLibrary(new FileBookStore(LibraryPath)).Books);
    }

    private sealed class FailOnSecondWriteStore(IBookStore store) : IBookStore
    {
        private readonly IBookStore _store = store;
        private int _writes;

        public IReadOnlyList<Book> Read()
        {
            return _store.Read();
        }

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
