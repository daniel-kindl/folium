namespace Folium.Storage;

public sealed class LibraryStoreException : Exception
{
    public LibraryStoreException(string message, Exception? inner = null)
        : base(message, inner)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A library store failure needs a message.", nameof(message));
        }
    }
}
