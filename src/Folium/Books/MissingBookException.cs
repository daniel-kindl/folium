namespace Folium.Books;

/// <summary>The personal library does not contain the requested book.</summary>
public sealed class MissingBookException : Exception
{
    /// <summary>Reports a missing book and does not record an identifier.</summary>
    public MissingBookException()
        : base("The personal library does not contain the book.")
    {
    }

    /// <summary>Reports a missing book with the given message and does not record an identifier.</summary>
    /// <param name="message">Text that explains the failure.</param>
    public MissingBookException(string message)
        : base(message)
    {
    }

    /// <summary>Reports a missing book, its cause, and no identifier.</summary>
    /// <param name="message">Text that explains the failure.</param>
    /// <param name="innerException">The cause of the failure.</param>
    public MissingBookException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Reports the book identifier that the library does not contain.</summary>
    /// <param name="bookId">The identifier that the library does not contain.</param>
    public MissingBookException(Guid bookId)
        : base($"The personal library does not contain book {bookId}.")
    {
        BookId = bookId;
    }

    /// <summary>The missing book identifier. Empty when the caller did not supply one.</summary>
    public Guid BookId { get; }
}
