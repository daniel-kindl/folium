namespace Folium.Books;

/// <summary>Reads and replaces one complete personal library.</summary>
public interface IBookStore
{
    /// <summary>Returns all saved books and shelves. Returns an empty snapshot when the file is absent.</summary>
    LibrarySnapshot Read();

    /// <summary>Replaces all saved books and shelves in one write.</summary>
    /// <param name="snapshot">The complete personal library.</param>
    void Replace(LibrarySnapshot snapshot);
}
