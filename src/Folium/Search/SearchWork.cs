namespace Folium.Search;

/// <summary>An Open Library work. Publication information describes the work, not an edition.</summary>
/// <param name="Key">The provider work path.</param>
/// <param name="Title">The supplied title.</param>
/// <param name="Authors">The supplied author names, or empty text.</param>
/// <param name="FirstPublished">The first publication year, or empty text.</param>
/// <param name="CoverId">The representative cover identifier, or null.</param>
public sealed record SearchWork(string Key, string Title, string Authors, string FirstPublished, int? CoverId = null)
{
    /// <summary>True when the provider supplied a usable cover identifier.</summary>
    public bool HasCover => CoverId is > 0;
}
