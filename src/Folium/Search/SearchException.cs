namespace Folium.Search;

/// <summary>A recoverable provider failure with a message suitable for the search screen.</summary>
public sealed class SearchException : Exception
{
    /// <summary>Creates a provider failure without details.</summary>
    public SearchException()
    {
    }

    /// <summary>Creates a provider failure.</summary>
    /// <param name="message">The explanation for the user.</param>
    public SearchException(string message) : base(message)
    {
    }

    /// <summary>Creates a provider failure with the original error.</summary>
    /// <param name="message">The explanation for the user.</param>
    /// <param name="innerException">The original error.</param>
    public SearchException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
