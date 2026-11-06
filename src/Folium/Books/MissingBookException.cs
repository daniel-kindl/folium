namespace Folium.Books;

public sealed class MissingBookException(Guid bookId) : Exception($"The personal library does not contain book {bookId}.")
{
    public Guid BookId { get; } = bookId;

    public MissingBookException()
    {
    }
}
