namespace Folium.Storage;

/// <summary>The personal library file cannot be read or written.</summary>
public sealed class LibraryStoreException : Exception
{
    /// <summary>Reports that the personal library is unavailable.</summary>
    public LibraryStoreException()
        : base("The personal library is unavailable.")
    {
    }

    /// <summary>Reports a library file failure. The message must contain text.</summary>
    /// <param name="message">Text that explains the failure. The text must not be blank.</param>
    public LibraryStoreException(string message)
        : base(message)
    {
        RequireMessage(message);
    }

    /// <summary>Reports a library file failure and its cause. The message must contain text.</summary>
    /// <param name="message">Text that explains the failure. The text must not be blank.</param>
    /// <param name="innerException">The cause of the failure.</param>
    public LibraryStoreException(string message, Exception? innerException)
        : base(message, innerException)
    {
        RequireMessage(message);
    }

    private static void RequireMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A library store failure needs a message.", nameof(message));
        }
    }
}
