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
        (string validTitle, string validAuthor) = BookText.Require(title, author);
        Book book = new Book(Guid.NewGuid(), validTitle, validAuthor);
        _store.Replace([.. _books, book]);
        _books.Add(book);
        return book;
    }

    public Book Edit(Guid id, string? title, string? author)
    {
        int index = IndexOf(id);
        (string validTitle, string validAuthor) = BookText.Require(title, author);
        Book updated = _books[index] with { Title = validTitle, Author = validAuthor };
        Book[] next = _books.ToArray();
        next[index] = updated;
        _store.Replace(next);
        _books[index] = updated;
        return updated;
    }

    public void Remove(Guid id)
    {
        int index = IndexOf(id);
        List<Book> next = _books.ToList();
        next.RemoveAt(index);
        _store.Replace(next);
        _books.RemoveAt(index);
    }

    private int IndexOf(Guid id)
    {
        int index = _books.FindIndex(book => book.Id == id);
        if (index < 0)
        {
            throw new MissingBookException(id);
        }

        return index;
    }
}
