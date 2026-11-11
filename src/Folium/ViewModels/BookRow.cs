namespace Folium.ViewModels;

/// <summary>One book on the library list.</summary>
/// <param name="Id">The identifier of the saved book.</param>
/// <param name="Title">The title of the saved book.</param>
/// <param name="Author">The author of the saved book.</param>
/// <param name="Status">The reading status in the words shown on the list.</param>
public sealed record BookRow(Guid Id, string Title, string Author, string Status)
{
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
