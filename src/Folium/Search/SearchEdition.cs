namespace Folium.Search;

/// <summary>Metadata for one edition. Empty text means the provider did not supply a field.</summary>
/// <param name="Key">The provider edition path.</param>
/// <param name="Title">The supplied title.</param>
/// <param name="Authors">The edition's author names.</param>
/// <param name="Publisher">The edition's publishers.</param>
/// <param name="PublicationDate">The publication date as supplied.</param>
/// <param name="Isbn">The edition's ISBNs.</param>
/// <param name="Edition">The edition statement.</param>
/// <param name="Language">The language codes.</param>
/// <param name="Description">The description, or empty text.</param>
/// <param name="CoverId">The cover identifier, or null.</param>
public sealed record SearchEdition(string Key, string Title, string Authors, string Publisher,
    string PublicationDate, string Isbn, string Edition, string Language, string Description, int? CoverId)
{
    /// <summary>True when the provider supplied a usable cover identifier.</summary>
    public bool HasCover => CoverId is > 0;
}
