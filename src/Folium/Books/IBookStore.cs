namespace Folium.Books;

/// <summary>Reads and replaces the saved list of books.</summary>
public interface IBookStore
{
    /// <summary>Returns the saved books. Returns an empty list when the library file is absent.</summary>
    IReadOnlyList<Book> Read();

    /// <summary>Replaces the saved books. A failed write leaves the previous file unchanged.</summary>
    /// <param name="books">The books that replace the saved list.</param>
    void Replace(IReadOnlyList<Book> books);
}
