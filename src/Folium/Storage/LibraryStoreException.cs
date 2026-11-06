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
    }

    public LibraryStoreException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
