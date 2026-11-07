namespace Folium.Search;

/// <summary>Reads provider metadata without access to the personal library.</summary>
public interface IBookSearch
{
    /// <summary>Searches work titles. Each page has at most 20 results.</summary>
    /// <param name="title">The title to search.</param>
    /// <param name="page">A page number, starting at one.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<SearchWork>> SearchAsync(string title, int page, CancellationToken cancellationToken);

    /// <summary>Reads one page of up to 20 editions of a work.</summary>
    /// <param name="workKey">The provider work path.</param>
    /// <param name="page">A page number, starting at one.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<SearchEdition>> EditionsAsync(string workKey, int page, CancellationToken cancellationToken);

    /// <summary>Reads one edition and its author names.</summary>
    /// <param name="editionKey">The provider edition path.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<SearchEdition> PreviewAsync(string editionKey, CancellationToken cancellationToken);

    /// <summary>Reads a cover by identifier. Returns null when no image exists.</summary>
    /// <param name="coverId">The provider cover identifier.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<byte[]?> CoverAsync(int coverId, CancellationToken cancellationToken);
}
