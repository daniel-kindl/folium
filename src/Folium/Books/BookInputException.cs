namespace Folium.Books;

/// <summary>The title or the author is not valid. The saved library does not change.</summary>
public sealed class BookInputException : Exception
{
    /// <summary>Reports invalid book text and records no specific problem.</summary>
    public BookInputException()
        : base("Invalid book input.")
    {
        Problems = [];
    }

    /// <summary>Reports invalid book text with the given message and records no specific problem.</summary>
    /// <param name="message">Text that explains the failure.</param>
    public BookInputException(string message)
        : base(message)
    {
        Problems = [];
    }

    /// <summary>Reports invalid book text, its cause, and no specific problem.</summary>
    /// <param name="message">Text that explains the failure.</param>
    /// <param name="innerException">The cause of the failure.</param>
    public BookInputException(string message, Exception innerException)
        : base(message, innerException)
    {
        Problems = [];
    }

    /// <summary>Reports the given problems. The list must contain at least one known problem.</summary>
    /// <param name="problems">The known problems. The list must contain at least one.</param>
    public BookInputException(IReadOnlyList<BookInputProblem> problems)
        : base(Describe(problems))
    {
        Problems = problems;
    }

    /// <summary>The problems that make the book text invalid. Empty when the caller did not supply a list.</summary>
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
