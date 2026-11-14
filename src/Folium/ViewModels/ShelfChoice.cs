namespace Folium.ViewModels;

/// <summary>One shelf option in a membership checklist.</summary>
/// <param name="Id">The shelf identifier.</param>
/// <param name="Name">The shelf name.</param>
/// <param name="IsSelected">True when the current book is on the shelf.</param>
public sealed record ShelfChoice(Guid Id, string Name, bool IsSelected);
