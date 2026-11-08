using Folium.Books;

namespace Folium.Tests;

public sealed class MissingBookExceptionTests
{
    [Fact]
    public void StandardConstructorsDoNotRecordABookId()
    {
        InvalidOperationException inner = new("missing");
        MissingBookException blank = new();
        MissingBookException withMessage = new("That book is gone.");
        MissingBookException withInner = new("That book is gone.", inner);

        Assert.Equal(Guid.Empty, blank.BookId);
        Assert.Equal("The personal library does not contain the book.", blank.Message);
        Assert.Equal(Guid.Empty, withMessage.BookId);
        Assert.Equal(Guid.Empty, withInner.BookId);
        Assert.Same(inner, withInner.InnerException);
    }

    [Fact]
    public void BookIdConstructorRecordsTheMissingId()
    {
        Guid id = Guid.NewGuid();

        MissingBookException exception = new(id);

        Assert.Equal(id, exception.BookId);
        Assert.Contains(id.ToString(), exception.Message, StringComparison.Ordinal);
    }
}
