namespace Folium.Books;

public sealed class BookInputException(IReadOnlyList<BookInputProblem> problems) : Exception(Describe(problems))
{
    public IReadOnlyList<BookInputProblem> Problems { get; } = problems;

    private static string Describe(IReadOnlyList<BookInputProblem> problems)
    {
        if (problems is null || problems.Count == 0)
        {
            throw new ArgumentException("A book input failure needs at least one problem.", nameof(problems));
        }

        bool missingTitle = problems.Contains(BookInputProblem.MissingTitle);
        bool missingAuthor = problems.Contains(BookInputProblem.MissingAuthor);
        if (missingTitle && missingAuthor)
        {
            return "The book needs a title and an author.";
        }

        return missingTitle
            ? "The book needs a title."
            : missingAuthor
            ? "The book needs an author."
            : throw new ArgumentException("A book input failure needs a known problem.", nameof(problems));
    }

    public BookInputException()
    {
    }
}
