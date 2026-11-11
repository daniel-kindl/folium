using Folium.Books;

namespace Folium.ViewModels;

/// <summary>One book on the library list.</summary>
/// <param name="Id">The identifier of the saved book.</param>
/// <param name="Title">The title of the saved book.</param>
/// <param name="Author">The author of the saved book.</param>
/// <param name="Status">The reading status in the words shown on the list.</param>
public sealed record BookRow(Guid Id, string Title, string Author, string Status)
{
    /// <summary>The reading status value. The view uses it to choose the status icon and color.</summary>
    public ReadingStatus ReadingStatus { get; init; } = ReadingStatus.ToRead;

    /// <summary>True when the book is marked to read.</summary>
    public bool IsToRead => ReadingStatus == ReadingStatus.ToRead;

    /// <summary>True when the book is being read.</summary>
    public bool IsReading => ReadingStatus == ReadingStatus.Reading;

    /// <summary>True when the book is finished.</summary>
    public bool IsFinished => ReadingStatus == ReadingStatus.Finished;

    /// <summary>The placeholder tone name.</summary>
    public string Tone { get; init; } = "stone";

    /// <summary>The letter shown on a narrow placeholder.</summary>
    public string Initial { get; init; } = "";

    /// <summary>True when the row has a saved cover.</summary>
    public bool HasCover { get; init; }

    /// <summary>The saved cover bytes. Empty when the book has no saved cover.</summary>
    public ReadOnlyMemory<byte> Cover { get; init; }

    /// <summary>The spoken name of the row: title, author, and status.</summary>
    public string AccessibleName => $"{Title}, {Author}, {Status}";
}
