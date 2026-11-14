namespace Folium.Books;

/// <summary>The shelf name is empty, too long, or already in use.</summary>
public sealed class ShelfNameException : Exception
{
    /// <summary>Creates an empty shelf name exception.</summary>
    public ShelfNameException() { }

    /// <summary>Creates a shelf name exception.</summary>
    /// <param name="message">The reason the shelf name is invalid.</param>
    public ShelfNameException(string message) : base(message) { }

    /// <summary>Creates a shelf name exception with its cause.</summary>
    /// <param name="message">The reason the shelf name is invalid.</param>
    /// <param name="innerException">The cause of the exception.</param>
    public ShelfNameException(string message, Exception innerException) : base(message, innerException) { }
}
