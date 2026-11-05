namespace Folium.Books;

internal static class BookText
{
    public static IReadOnlyList<BookInputProblem> Problems(string? title, string? author)
    {
        var problems = new List<BookInputProblem>(2);
        if (string.IsNullOrWhiteSpace(title))
        {
            problems.Add(BookInputProblem.MissingTitle);
        }

        if (string.IsNullOrWhiteSpace(author))
        {
            problems.Add(BookInputProblem.MissingAuthor);
        }

        return problems;
    }

    public static (string Title, string Author) Require(string? title, string? author)
    {
        var problems = Problems(title, author);
        if (problems.Count > 0)
        {
            throw new BookInputException(problems);
        }

        return (title!.Trim(), author!.Trim());
    }
}
