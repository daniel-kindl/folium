using Folium.Books;

namespace Folium.ViewModels;

/// <summary>An editable copy of metadata. Source identifiers stay unchanged.</summary>
/// <param name="source">The metadata to edit.</param>
/// <param name="keepCover">True when the user already confirmed permission to save this cover.</param>
public sealed class BookMetadataEditor(BookMetadata source, bool keepCover)
{
    private readonly BookMetadata _source = source;

    /// <summary>The editable publisher text.</summary>
    public string Publisher { get; set; } = source.Publisher;

    /// <summary>The editable publication date text.</summary>
    public string PublicationDate { get; set; } = source.PublicationDate;

    /// <summary>The editable ISBN text.</summary>
    public string Isbn { get; set; } = source.Isbn;

    /// <summary>The editable edition statement.</summary>
    public string Edition { get; set; } = source.Edition;

    /// <summary>The editable language text.</summary>
    public string Language { get; set; } = source.Language;

    /// <summary>The editable description text.</summary>
    public string Description { get; set; } = source.Description;

    /// <summary>True when the user confirms permission to save the cover locally.</summary>
    public bool KeepCover { get; set; } = keepCover;

    /// <summary>True when a downloaded or saved cover is available.</summary>
    public bool HasCover => !_source.Cover.IsEmpty;

    /// <summary>The source edition path for attribution.</summary>
    public string EditionKey => _source.EditionKey;

    /// <summary>Creates independent metadata from the current text and cover choice.</summary>
    public BookMetadata ToMetadata()
    {
        return _source with
        {
            Publisher = Publisher,
            PublicationDate = PublicationDate,
            Isbn = Isbn,
            Edition = Edition,
            Language = Language,
            Description = Description,
            Cover = KeepCover ? _source.Cover.ToArray() : ReadOnlyMemory<byte>.Empty,
        };
    }
}
