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

    public Book Edit(Guid id, string? title, string? author)
    {
        var index = IndexOf(id);
        var (validTitle, validAuthor) = BookText.Require(title, author);
        var updated = _books[index] with { Title = validTitle, Author = validAuthor };
        var next = _books.ToArray();
        next[index] = updated;
        _store.Replace(next);
        _books[index] = updated;
        return updated;
    }

    public void Remove(Guid id)
    {
        var index = IndexOf(id);
        var next = _books.ToList();
        next.RemoveAt(index);
        _store.Replace(next);
        _books.RemoveAt(index);
    }

    private int IndexOf(Guid id)
    {
        var index = _books.FindIndex(book => book.Id == id);
        if (index < 0)
        {
            throw new MissingBookException(id);
        }

        return index;
    }
}
