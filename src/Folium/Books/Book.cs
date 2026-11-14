using System.Collections.Frozen;

namespace Folium.Books;

/// <summary>One saved book. The identifier does not change when the title, the author, or the reading status changes.</summary>
public sealed record Book
{
    /// <summary>Creates one saved book.</summary>
    /// <param name="id">The local identifier.</param>
    /// <param name="title">The title of the book.</param>
    /// <param name="author">The author of the book.</param>
    /// <param name="status">The reading status.</param>
    /// <param name="metadata">Optional imported metadata and cover.</param>
    /// <param name="shelfIds">The shelves that contain the book.</param>
    public Book(
        Guid id,
        string title,
        string author,
        ReadingStatus status = ReadingStatus.ToRead,
        BookMetadata? metadata = null,
        IEnumerable<Guid>? shelfIds = null)
    {
        Id = id;
        Title = title;
        Author = author;
        Status = status;
        Metadata = metadata;
        ShelfIds = (shelfIds ?? []).ToFrozenSet();
    }

    /// <summary>The local identifier. It does not change when the book changes.</summary>
    public Guid Id { get; init; }

    /// <summary>The title of the book.</summary>
    public string Title { get; init; }

    /// <summary>The author of the book.</summary>
    public string Author { get; init; }

    /// <summary>The reading status.</summary>
    public ReadingStatus Status { get; init; }

    /// <summary>Optional imported metadata and cover.</summary>
    public BookMetadata? Metadata { get; init; }

    /// <summary>The shelves that contain this book.</summary>
    public FrozenSet<Guid> ShelfIds { get; init; }
}
