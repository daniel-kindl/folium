namespace Folium.Books;

/// <summary>The saved books for one person. A failed change leaves the stored file unchanged.</summary>
public sealed class PersonalLibrary
{
    private readonly IBookStore _store;
    private readonly List<Book> _books;

    /// <summary>Loads the books from the store. The store must not be null.</summary>
    /// <param name="store">The store that holds the saved books. Must not be null.</param>
    public PersonalLibrary(IBookStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _books = [.. store.Read()];
    }

    /// <summary>A copy of the saved books.</summary>
    public IReadOnlyList<Book> Books => [.. _books];

    /// <summary>Saves a new book when the title and the author contain text. Duplicate text is allowed.</summary>
    /// <param name="title">The title text. Blank text is rejected.</param>
    /// <param name="author">The author text. Blank text is rejected.</param>
    public Book Add(string? title, string? author)
    {
        (string validTitle, string validAuthor) = BookText.Require(title ?? string.Empty, author ?? string.Empty);
        Book book = new(Guid.NewGuid(), validTitle, validAuthor);
        _store.Replace([.. _books, book]);
        _books.Add(book);
        return book;
    }

    /// <summary>Changes the title and the author and keeps the identifier. Fails when the library does not contain the book.</summary>
    /// <param name="id">The identifier of the saved book.</param>
    /// <param name="title">The title text. Blank text is rejected.</param>
    /// <param name="author">The author text. Blank text is rejected.</param>
    public Book Edit(Guid id, string? title, string? author)
    {
        int index = IndexOf(id);
        (string validTitle, string validAuthor) = BookText.Require(title ?? string.Empty, author ?? string.Empty);
        Book updated = _books[index] with { Title = validTitle, Author = validAuthor };
        Book[] next = [.. _books];
        next[index] = updated;
        _store.Replace(next);
        _books[index] = updated;
        return updated;
    }

    /// <summary>Deletes the book. A second removal of the same identifier fails.</summary>
    /// <param name="id">The identifier of the saved book.</param>
    public void Remove(Guid id)
    {
        int index = IndexOf(id);
        List<Book> next = [.. _books];
        next.RemoveAt(index);
        _store.Replace(next);
        _books.RemoveAt(index);
    }

    private int IndexOf(Guid id)
    {
        int index = _books.FindIndex(book => book.Id == id);
        return index < 0 ? throw new MissingBookException(id) : index;
    }
}
