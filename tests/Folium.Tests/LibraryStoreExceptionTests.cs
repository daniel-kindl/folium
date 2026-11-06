using Folium.Storage;

namespace Folium.Tests;

public sealed class LibraryStoreExceptionTests
{
    [Fact]
    public void DefaultMessageExplainsThatTheLibraryIsUnavailable()
    {
        LibraryStoreException exception = new();

        Assert.Equal("The personal library is unavailable.", exception.Message);
    }

    [Fact]
    public void BlankMessageIsRejected()
    {
        ArgumentException blank = Assert.Throws<ArgumentException>(() => new LibraryStoreException(" "));
        ArgumentException empty = Assert.Throws<ArgumentException>(
            () => new LibraryStoreException(string.Empty, new InvalidOperationException("write failed")));

        Assert.Equal("message", blank.ParamName);
        Assert.Equal("message", empty.ParamName);
    }
}
