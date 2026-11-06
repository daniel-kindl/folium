namespace Folium.Books;

public sealed class MissingBookException : Exception
{
    public MissingBookException()
        : base("The personal library does not contain the book.")
    {
    }

    public MissingBookException(string message)
        : base(message)
    {
    }

    public MissingBookException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public MissingBookException(Guid bookId)
        : base($"The personal library does not contain book {bookId}.")
    {
        BookId = bookId;
    }

    public Guid BookId { get; }
}
