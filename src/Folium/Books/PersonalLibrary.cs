using System.Collections.Frozen;

namespace Folium.Books;

/// <summary>The saved books and shelves for one person.</summary>
public sealed class PersonalLibrary
{
    private readonly IBookStore _store;
    private readonly List<Book> _books;
    private readonly List<Shelf> _shelves;

    /// <summary>Loads books and shelves from the store. The store must not be null.</summary>
    /// <param name="store">The store that holds the personal library.</param>
    public PersonalLibrary(IBookStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        LibrarySnapshot snapshot = store.Read();
        _books = [.. snapshot.Books];
        _shelves = SortShelves(snapshot.Shelves);
    }

    private PersonalLibrary(IBookStore store, LibrarySnapshot snapshot)
    {
        _store = store;
        _books = [.. snapshot.Books];
        _shelves = SortShelves(snapshot.Shelves);
    }

    internal static PersonalLibrary Restore(IBookStore store, LibrarySnapshot snapshot)
    {
        PersonalLibrary restored = new(store, snapshot);
        store.Replace(restored.Snapshot());
        return restored;
    }

    /// <summary>A copy of the saved books.</summary>
    public IReadOnlyList<Book> Books => [.. _books];

    /// <summary>A copy of the shelves, ordered by folded name.</summary>
    public IReadOnlyList<Shelf> Shelves => [.. _shelves];

    /// <summary>Saves a new book when the title and the author contain text. Duplicate text is allowed.</summary>
    /// <param name="title">The title text. Blank text is rejected.</param>
    /// <param name="author">The author text. Blank text is rejected.</param>
    /// <param name="metadata">Optional edition metadata and cover.</param>
    /// <param name="shelfIds">The shelves to assign. Null assigns no shelves.</param>
    public Book Add(string? title, string? author, BookMetadata? metadata = null, IEnumerable<Guid>? shelfIds = null)
    {
        (string validTitle, string validAuthor) = BookText.Require(title ?? string.Empty, author ?? string.Empty);
        Book book = new(Guid.NewGuid(), validTitle, validAuthor, metadata: metadata, shelfIds: RequireShelfIds(shelfIds ?? []));
        _store.Replace(new([.. _books, book], _shelves));
        _books.Add(book);
        return book;
    }

    /// <summary>Changes the title and author and keeps the identifier and shelves.</summary>
    /// <param name="id">The book identifier.</param>
    /// <param name="title">The new title.</param>
    /// <param name="author">The new author.</param>
    /// <param name="metadata">Replacement metadata. Null keeps the saved metadata.</param>
    /// <param name="shelfIds">Replacement shelves. Null keeps the saved shelves.</param>
    /// <exception cref="MissingBookException">The library does not contain the book.</exception>
    /// <exception cref="BookInputException">The title or author is blank.</exception>
    /// <exception cref="Storage.LibraryStoreException">The store cannot write the change.</exception>
    public Book Edit(Guid id, string? title, string? author, BookMetadata? metadata = null, IEnumerable<Guid>? shelfIds = null)
    {
        int index = IndexOf(id);
        (string validTitle, string validAuthor) = BookText.Require(title ?? string.Empty, author ?? string.Empty);
        Book updated = _books[index] with
        {
            Title = validTitle,
            Author = validAuthor,
            Metadata = metadata ?? _books[index].Metadata,
            ShelfIds = shelfIds is null ? _books[index].ShelfIds : RequireShelfIds(shelfIds),
        };
        Book[] next = [.. _books];
        next[index] = updated;
        _store.Replace(new(next, _shelves));
        _books[index] = updated;
        return updated;
    }

    /// <summary>Changes the reading status and keeps the title, author, and shelves.</summary>
    /// <param name="id">The book identifier.</param>
    /// <param name="status">The new reading status.</param>
    /// <exception cref="ArgumentOutOfRangeException">The status is not defined.</exception>
    /// <exception cref="MissingBookException">The library does not contain the book.</exception>
    /// <exception cref="Storage.LibraryStoreException">The store cannot write the change.</exception>
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
        _store.Replace(new(next, _shelves));
        _books[index] = updated;
        return updated;
    }

    /// <summary>Replaces every saved book and removes shelves and their memberships.</summary>
    /// <param name="books">The books that replace the saved list.</param>
    /// <exception cref="ArgumentNullException">The book list is null.</exception>
    /// <exception cref="ArgumentException">A book in the list is null.</exception>
    /// <exception cref="Storage.LibraryStoreException">The store cannot write the new snapshot.</exception>
    public void ReplaceAll(IReadOnlyList<Book> books)
    {
        ArgumentNullException.ThrowIfNull(books);
        FrozenSet<Guid> noShelves = Array.Empty<Guid>().ToFrozenSet();
        Book[] replacements = [.. books.Select(book => book is null
            ? throw new ArgumentException("The library cannot replace an empty book.", nameof(books))
            : book with { ShelfIds = noShelves })];
        LibrarySnapshot next = new(replacements, []);
        _store.Replace(next);
        _books.Clear();
        _books.AddRange(next.Books);
        _shelves.Clear();
    }

    /// <summary>Replaces all saved books and shelves in one write.</summary>
    /// <param name="snapshot">The complete personal library.</param>
    /// <exception cref="ArgumentNullException">The snapshot is null.</exception>
    /// <exception cref="Storage.LibraryStoreException">The store cannot write the new snapshot.</exception>
    public void ReplaceSnapshot(LibrarySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        LibrarySnapshot next = new(snapshot.Books, snapshot.Shelves);
        _store.Replace(next);
        _books.Clear();
        _books.AddRange(next.Books);
        _shelves.Clear();
        _shelves.AddRange(SortShelves(next.Shelves));
    }

    /// <summary>Creates an empty shelf.</summary>
    /// <param name="name">The shelf name.</param>
    /// <exception cref="ShelfNameException">The name is invalid or already in use.</exception>
    /// <exception cref="Storage.LibraryStoreException">The store cannot write the new shelf.</exception>
    public Shelf CreateShelf(string? name)
    {
        ShelfName validName = RequireUniqueName(name, except: null);
        Shelf shelf = new(Guid.NewGuid(), validName.Value);
        List<Shelf> next = SortShelves([.. _shelves, shelf]);
        _store.Replace(new(_books, next));
        _shelves.Clear();
        _shelves.AddRange(next);
        return shelf;
    }

    /// <summary>Changes a shelf name.</summary>
    /// <param name="id">The shelf identifier.</param>
    /// <param name="name">The new shelf name.</param>
    /// <exception cref="MissingShelfException">The library does not contain the shelf.</exception>
    /// <exception cref="ShelfNameException">The name is invalid or already in use.</exception>
    /// <exception cref="Storage.LibraryStoreException">The store cannot write the change.</exception>
    public Shelf RenameShelf(Guid id, string? name)
    {
        int index = ShelfIndexOf(id);
        ShelfName validName = RequireUniqueName(name, except: id);
        Shelf renamed = _shelves[index] with { Name = validName.Value };
        Shelf[] next = [.. _shelves];
        next[index] = renamed;
        List<Shelf> ordered = SortShelves(next);
        _store.Replace(new(_books, ordered));
        _shelves.Clear();
        _shelves.AddRange(ordered);
        return renamed;
    }

    /// <summary>Deletes a shelf and removes its identifier from every book.</summary>
    /// <param name="id">The shelf identifier.</param>
    /// <exception cref="MissingShelfException">The library does not contain the shelf.</exception>
    /// <exception cref="Storage.LibraryStoreException">The store cannot write the change.</exception>
    public void DeleteShelf(Guid id)
    {
        int index = ShelfIndexOf(id);
        List<Shelf> nextShelves = [.. _shelves];
        nextShelves.RemoveAt(index);
        Book[] nextBooks = [.. _books.Select(book => book.ShelfIds.Contains(id)
            ? book with { ShelfIds = book.ShelfIds.Where(shelfId => shelfId != id).ToFrozenSet() }
            : book)];
        _store.Replace(new(nextBooks, nextShelves));
        _shelves.RemoveAt(index);
        _books.Clear();
        _books.AddRange(nextBooks);
    }

    /// <summary>Replaces the shelves assigned to one book.</summary>
    /// <param name="bookId">The book identifier.</param>
    /// <param name="shelfIds">The shelf identifiers to assign.</param>
    /// <exception cref="ArgumentNullException">The shelf identifier list is null.</exception>
    /// <exception cref="ArgumentException">The list contains an unknown shelf.</exception>
    /// <exception cref="MissingBookException">The library does not contain the book.</exception>
    /// <exception cref="Storage.LibraryStoreException">The store cannot write the change.</exception>
    public Book SetShelves(Guid bookId, IEnumerable<Guid> shelfIds)
    {
        ArgumentNullException.ThrowIfNull(shelfIds);
        int bookIndex = IndexOf(bookId);
        HashSet<Guid> validIds = [.. _shelves.Select(shelf => shelf.Id)];
        FrozenSet<Guid> assigned = shelfIds.ToFrozenSet();
        if (assigned.Any(id => !validIds.Contains(id)))
        {
            throw new ArgumentException("The shelf list contains an unknown shelf.", nameof(shelfIds));
        }

        Book updated = _books[bookIndex] with { ShelfIds = assigned };
        Book[] next = [.. _books];
        next[bookIndex] = updated;
        _store.Replace(new(next, _shelves));
        _books[bookIndex] = updated;
        return updated;
    }

    /// <summary>Deletes the book. A second removal of the same identifier fails.</summary>
    /// <param name="id">The book identifier.</param>
    /// <exception cref="MissingBookException">The library does not contain the book.</exception>
    /// <exception cref="Storage.LibraryStoreException">The store cannot write the change.</exception>
    public void Remove(Guid id)
    {
        int index = IndexOf(id);
        List<Book> next = [.. _books];
        next.RemoveAt(index);
        _store.Replace(new(next, _shelves));
        _books.RemoveAt(index);
    }

    private int IndexOf(Guid id)
    {
        int index = _books.FindIndex(book => book.Id == id);
        return index < 0 ? throw new MissingBookException(id) : index;
    }

    private int ShelfIndexOf(Guid id)
    {
        int index = _shelves.FindIndex(shelf => shelf.Id == id);
        return index < 0 ? throw new MissingShelfException(id) : index;
    }

    private ShelfName RequireUniqueName(string? name, Guid? except)
    {
        ShelfName valid = ShelfName.Parse(name);
        return _shelves.Exists(shelf => shelf.Id != except
            && string.Equals(ShelfName.Parse(shelf.Name).FoldedValue, valid.FoldedValue, StringComparison.OrdinalIgnoreCase))
            ? throw new ShelfNameException("A shelf with this name already exists.")
            : valid;
    }

    private FrozenSet<Guid> RequireShelfIds(IEnumerable<Guid> shelfIds)
    {
        FrozenSet<Guid> assigned = shelfIds.ToFrozenSet();
        HashSet<Guid> validIds = [.. _shelves.Select(shelf => shelf.Id)];
        return assigned.Any(id => !validIds.Contains(id))
            ? throw new ArgumentException("The shelf list contains an unknown shelf.", nameof(shelfIds))
            : assigned;
    }

    private static List<Shelf> SortShelves(IEnumerable<Shelf> shelves)
    {
        return [.. shelves.OrderBy(shelf => BookText.FoldForSearch(shelf.Name), StringComparer.OrdinalIgnoreCase)
            .ThenBy(shelf => shelf.Name, StringComparer.Ordinal)
            .ThenBy(shelf => shelf.Id)];
    }

    private LibrarySnapshot Snapshot()
    {
        return new(_books, _shelves);
    }
}
