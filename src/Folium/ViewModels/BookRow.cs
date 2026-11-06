namespace Folium.ViewModels;

/// <summary>One book on the library list.</summary>
/// <param name="Id">The identifier of the saved book.</param>
/// <param name="Title">The title of the saved book.</param>
/// <param name="Author">The author of the saved book.</param>
/// <param name="Status">The reading status in the words shown on the list.</param>
public sealed record BookRow(Guid Id, string Title, string Author, string Status);
