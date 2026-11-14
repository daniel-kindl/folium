namespace Folium.Books;

/// <summary>A complete set of books and shelves for one atomic store operation.</summary>
public sealed record LibrarySnapshot
{
    /// <summary>Copies the books and shelves into one snapshot.</summary>
    /// <param name="books">The books in the snapshot.</param>
    /// <param name="shelves">The shelves in the snapshot.</param>
    public LibrarySnapshot(IReadOnlyList<Book> books, IReadOnlyList<Shelf> shelves)
    {
        ArgumentNullException.ThrowIfNull(books);
        ArgumentNullException.ThrowIfNull(shelves);
        Books = Array.AsReadOnly(books.ToArray());
        Shelves = Array.AsReadOnly(shelves.ToArray());
    }

    /// <summary>The books in the snapshot.</summary>
    public IReadOnlyList<Book> Books { get; }

    /// <summary>The shelves in the snapshot.</summary>
    public IReadOnlyList<Shelf> Shelves { get; }
}
