using Folium.Books;

namespace Folium.Tests;

public sealed class BookInputExceptionTests
{
    [Fact]
    public void StandardConstructorsKeepAnEmptyProblemList()
    {
        InvalidOperationException inner = new("input failed");
        BookInputException blank = new();
        BookInputException withMessage = new("The title is missing.");
        BookInputException withInner = new("The title is missing.", inner);

        Assert.Empty(blank.Problems);
        Assert.Equal("Invalid book input.", blank.Message);
        Assert.Empty(withMessage.Problems);
        Assert.Empty(withInner.Problems);
        Assert.Same(inner, withInner.InnerException);
    }

    [Fact]
    public void EmptyProblemListIsRejected()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new BookInputException([]));

        Assert.Equal("problems", exception.ParamName);
    }

    [Fact]
    public void UnknownProblemIsRejected()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new BookInputException([(BookInputProblem)9]));

        Assert.Equal("problems", exception.ParamName);
    }
}
