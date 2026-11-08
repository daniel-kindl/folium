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
    /// <param name="metadata">Optional edition metadata and cover.</param>
    public Book Add(string? title, string? author, BookMetadata? metadata = null)
    {
        (string validTitle, string validAuthor) = BookText.Require(title ?? string.Empty, author ?? string.Empty);
        Book book = new(Guid.NewGuid(), validTitle, validAuthor, Metadata: metadata);
        _store.Replace([.. _books, book]);
        _books.Add(book);
        return book;
    }

    /// <summary>Changes the title and the author and keeps the identifier. Fails when the library does not contain the book.</summary>
    /// <param name="id">The identifier of the saved book.</param>
    /// <param name="title">The title text. Blank text is rejected.</param>
    /// <param name="author">The author text. Blank text is rejected.</param>
    /// <param name="metadata">Replacement metadata. Null keeps the saved metadata.</param>
    public Book Edit(Guid id, string? title, string? author, BookMetadata? metadata = null)
    {
        int index = IndexOf(id);
        (string validTitle, string validAuthor) = BookText.Require(title ?? string.Empty, author ?? string.Empty);
        Book updated = _books[index] with { Title = validTitle, Author = validAuthor, Metadata = metadata ?? _books[index].Metadata };
        Book[] next = [.. _books];
        next[index] = updated;
        _store.Replace(next);
        _books[index] = updated;
        return updated;
    }

    /// <summary>Changes the reading status and keeps the title and the author. Fails when the library does not contain the book.</summary>
    /// <param name="id">The identifier of the saved book.</param>
    /// <param name="status">The reading status. The value must be a defined status.</param>
    /// <exception cref="ArgumentOutOfRangeException">The status is not a defined value. The saved book stays unchanged.</exception>
    /// <exception cref="MissingBookException">The library does not contain the book.</exception>
    public Book SetStatus(Guid id, ReadingStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "The reading status is not a defined value.");
        }

        int index = IndexOf(id);
        Book updated = _books[index] with { Status = status };
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
