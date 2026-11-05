namespace Folium.Books;

public sealed class MissingBookException : Exception
{
    public MissingBookException(Guid bookId)
        : base($"The personal library does not contain book {bookId}.")
    {
        BookId = bookId;
    }

    public Guid BookId { get; }
}
