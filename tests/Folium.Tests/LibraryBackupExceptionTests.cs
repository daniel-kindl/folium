using Folium.Storage;

namespace Folium.Tests;

public sealed class LibraryBackupExceptionTests
{
    [Fact]
    public void DefaultMessageExplainsThatTheBackupIsUnavailable()
    {
        LibraryBackupException exception = new();

        Assert.Equal("The personal library backup is unavailable.", exception.Message);
    }

    [Fact]
    public void BlankMessageIsRejected()
    {
        ArgumentException blank = Assert.Throws<ArgumentException>(() => new LibraryBackupException(" "));
        ArgumentException empty = Assert.Throws<ArgumentException>(
            () => new LibraryBackupException(string.Empty, new InvalidOperationException("write failed")));

        Assert.Equal("message", blank.ParamName);
        Assert.Equal("message", empty.ParamName);
    }
}
