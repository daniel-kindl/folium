namespace Folium.Books;

public sealed class PersonalLibrary
{
    private readonly IBookStore _store;
    private readonly List<Book> _books;

    public PersonalLibrary(IBookStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _books = [.. store.Read()];
    }

    public IReadOnlyList<Book> Books => _books.ToArray();

    public Book Add(string? title, string? author)
    {
        var (validTitle, validAuthor) = BookText.Require(title, author);
        var book = new Book(Guid.NewGuid(), validTitle, validAuthor);
        _store.Replace([.. _books, book]);
        _books.Add(book);
        return book;
    }
}
