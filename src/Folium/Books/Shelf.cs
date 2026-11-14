namespace Folium.Books;

/// <summary>A named group of books.</summary>
/// <param name="Id">The local identifier of the shelf.</param>
/// <param name="Name">The validated shelf name.</param>
public sealed record Shelf(Guid Id, string Name);
