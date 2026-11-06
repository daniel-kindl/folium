namespace Folium.Books;

public sealed class BookInputException : Exception
{
    public BookInputException()
        : base("Invalid book input.")
    {
        Problems = [];
    }

    public BookInputException(string message)
        : base(message)
    {
        Problems = [];
    }

    public BookInputException(string message, Exception innerException)
        : base(message, innerException)
    {
        Problems = [];
    }

    public BookInputException(IReadOnlyList<BookInputProblem> problems)
        : base(Describe(problems))
    {
        Problems = problems;
    }

    public IReadOnlyList<BookInputProblem> Problems { get; }

    private static string Describe(IReadOnlyList<BookInputProblem> problems)
    {
        if (problems is null || problems.Count == 0)
        {
            throw new ArgumentException("A book input failure needs at least one problem.", nameof(problems));
        }

        bool missingTitle = problems.Contains(BookInputProblem.MissingTitle);
        bool missingAuthor = problems.Contains(BookInputProblem.MissingAuthor);
        return (missingTitle, missingAuthor) switch
        {
            (true, true) => "The book needs a title and an author.",
            (true, false) => "The book needs a title.",
            (false, true) => "The book needs an author.",
            _ => throw new ArgumentException("A book input failure needs a known problem.", nameof(problems)),
        };
    }
}
