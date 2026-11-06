namespace Folium.Books;

internal static class BookText
{
    public static IReadOnlyList<BookInputProblem> Problems(string? title, string? author)
    {
        List<BookInputProblem> problems = new(2);
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
        IReadOnlyList<BookInputProblem> problems = Problems(title, author);
        return problems.Count > 0 ? throw new BookInputException(problems) : ((string Title, string Author))(title!.Trim(), author!.Trim());
    }
}
