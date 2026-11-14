using Folium.Books;

namespace Folium.Tests;

public sealed class LibraryQueryTests
{
    [Fact]
    public void SearchUsesTitleAndAuthorAndFoldsCaseAndAccents()
    {
        Book title = new(Guid.NewGuid(), "Café Society", "Writer");
        Book author = new(Guid.NewGuid(), "Other", "René Example");
        Book missing = new(Guid.NewGuid(), "Unmatched", "Unknown");

        IReadOnlyList<Book> result = LibraryQuery.Apply([title, author, missing], status: null, shelfId: null, query: "RENE", sort: "title-asc");

        Assert.Equal([author], result);
    }

    [Fact]
    public void SearchKeepsSupplementaryUnicodeCharacters()
    {
        Book book = new(Guid.NewGuid(), "Dune 😀", "Frank Herbert");
        Book other = new(Guid.NewGuid(), "Dune", "Frank Herbert");

        IReadOnlyList<Book> result = LibraryQuery.Apply([book, other], status: null, shelfId: null, query: "😀", sort: "title-asc");

        Assert.Equal([book], result);
    }

    [Fact]
    public void SearchCombinesStatusAndShelfByAnd()
    {
        Guid shelf = Guid.NewGuid();
        Book match = new(Guid.NewGuid(), "Dune", "Frank Herbert", ReadingStatus.Reading, shelfIds: [shelf]);
        Book wrongStatus = new(Guid.NewGuid(), "Dune Messiah", "Frank Herbert", ReadingStatus.Finished, shelfIds: [shelf]);
        Book wrongShelf = new(Guid.NewGuid(), "Children of Dune", "Frank Herbert", ReadingStatus.Reading);

        IReadOnlyList<Book> result = LibraryQuery.Apply([match, wrongStatus, wrongShelf], status: ReadingStatus.Reading, shelfId: shelf, query: "dune", sort: "title-asc");

        Assert.Equal([match], result);
    }

    [Fact]
    public void SortUsesFoldedTitleThenOriginalTextThenId()
    {
        Book upper = new(new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2), "eclair", "Writer");
        Book accent = new(new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1), "Éclair", "Writer");
        Book other = new(Guid.NewGuid(), "Dune", "Writer");

        IReadOnlyList<Book> result = LibraryQuery.Apply([upper, accent, other], status: null, shelfId: null, query: "", sort: "title-asc");

        Assert.Equal([other, upper, accent], result);
    }

    [Fact]
    public void UnknownSortIsRejected()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            LibraryQuery.Apply([], status: null, shelfId: null, query: string.Empty, sort: "unknown"));

        Assert.Equal("sort", exception.ParamName);
        Assert.Contains("The library sort value is not valid.", exception.Message, StringComparison.Ordinal);
    }
}
