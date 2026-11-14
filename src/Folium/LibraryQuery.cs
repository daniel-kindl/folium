using Folium.Books;

namespace Folium;

/// <summary>Filters, searches, and sorts saved books for the library screen.</summary>
internal static class LibraryQuery
{
    private static readonly HashSet<string> SortModes = new(StringComparer.Ordinal)
    {
        "title-asc",
        "title-desc",
        "author-asc",
        "author-desc",
    };

    public static IReadOnlyList<Book> Apply(
        IReadOnlyList<Book> books,
        ReadingStatus? status,
        Guid? shelfId,
        string query,
        string sort)
    {
        if (!SortModes.Contains(sort))
        {
            throw new ArgumentOutOfRangeException(nameof(sort), "The library sort value is not valid.");
        }

        string foldedQuery = BookText.FoldForSearch(query.Trim());
        IEnumerable<Book> matching = books.Where(book => Matches(book, status, shelfId, foldedQuery));
        return SortBooks(matching, sort);
    }

    private static IReadOnlyList<Book> SortBooks(IEnumerable<Book> matching, string sort)
    {
        bool byTitle = sort.StartsWith("title", StringComparison.Ordinal);
        bool descending = sort.EndsWith("desc", StringComparison.Ordinal);
        IOrderedEnumerable<Book> sorted = descending
            ? matching.OrderByDescending(book => BookText.FoldForSearch(SortText(book, byTitle)), StringComparer.OrdinalIgnoreCase)
                .ThenBy(book => SortText(book, byTitle), StringComparer.Ordinal)
                .ThenBy(book => book.Id)
            : matching.OrderBy(book => BookText.FoldForSearch(SortText(book, byTitle)), StringComparer.OrdinalIgnoreCase)
                .ThenBy(book => SortText(book, byTitle), StringComparer.Ordinal)
                .ThenBy(book => book.Id);

        return [.. sorted];
    }

    private static bool Matches(Book book, ReadingStatus? status, Guid? shelfId, string query)
    {
        return MatchesStatus(book, status) && MatchesShelf(book, shelfId) && MatchesQuery(book, query);
    }

    private static bool MatchesStatus(Book book, ReadingStatus? status)
    {
        return status is null || book.Status == status;
    }

    private static bool MatchesShelf(Book book, Guid? shelfId)
    {
        return shelfId is null || book.ShelfIds.Contains(shelfId.Value);
    }

    private static bool MatchesQuery(Book book, string query)
    {
        return query.Length == 0 || Contains(book.Title, query) || Contains(book.Author, query);
    }

    private static string SortText(Book book, bool byTitle)
    {
        return byTitle ? book.Title : book.Author;
    }

    private static bool Contains(string value, string query)
    {
        return BookText.FoldForSearch(value).Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
