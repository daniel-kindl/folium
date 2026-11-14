using Folium.Books;

namespace Folium.Tests;

public sealed class LibrarySnapshotTests
{
    [Fact]
    public void NullBookListIsRejected()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new LibrarySnapshot(null!, []));

        Assert.Equal("books", exception.ParamName);
    }

    [Fact]
    public void NullShelfListIsRejected()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new LibrarySnapshot([], null!));

        Assert.Equal("shelves", exception.ParamName);
    }
}
