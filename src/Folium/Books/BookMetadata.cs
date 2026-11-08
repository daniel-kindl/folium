namespace Folium.Books;

/// <summary>Editable edition metadata and its original Open Library identifiers. An empty cover means no image was saved.</summary>
/// <param name="WorkKey">The original work path.</param>
/// <param name="EditionKey">The original edition path.</param>
/// <param name="Publisher">The publisher text.</param>
/// <param name="PublicationDate">The publication date text.</param>
/// <param name="Isbn">The ISBN text.</param>
/// <param name="Edition">The edition statement.</param>
/// <param name="Language">The language text.</param>
/// <param name="Description">The description text.</param>
/// <param name="CoverId">The original cover identifier.</param>
/// <param name="Cover">An optional private copy saved with the user's permission confirmation.</param>
public sealed record BookMetadata(string WorkKey, string EditionKey, string Publisher,
    string PublicationDate, string Isbn, string Edition, string Language, string Description,
    int? CoverId, ReadOnlyMemory<byte> Cover);
