namespace Folium.Storage;

public sealed class LibraryStoreException : Exception
{
    public LibraryStoreException()
        : base("The personal library is unavailable.")
    {
    }

    public LibraryStoreException(string message)
        : base(message)
    {
        RequireMessage(message);
    }

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
