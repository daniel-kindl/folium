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
}
