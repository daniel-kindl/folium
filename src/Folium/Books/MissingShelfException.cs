namespace Folium.Books;

/// <summary>The personal library does not contain the shelf.</summary>
public sealed class MissingShelfException : Exception
{
    /// <summary>Creates an exception for a missing shelf.</summary>
    public MissingShelfException() { }

    /// <summary>Creates an exception for a missing shelf.</summary>
    /// <param name="message">The failure message.</param>
    public MissingShelfException(string message) : base(message) { }

    /// <summary>Creates an exception for a missing shelf.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The cause of the exception.</param>
    public MissingShelfException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>Creates an exception for the shelf identifier.</summary>
    /// <param name="shelfId">The missing shelf identifier.</param>
    public MissingShelfException(Guid shelfId) : base($"The personal library does not contain shelf {shelfId}.")
    {
        ShelfId = shelfId;
    }

    /// <summary>The missing shelf identifier.</summary>
    public Guid ShelfId { get; }
}
