namespace Folium.Search;

/// <summary>One page of usable provider records.</summary>
/// <typeparam name="T">The record type in the page.</typeparam>
/// <param name="Items">Records with a valid provider key.</param>
/// <param name="HasAnotherPage">True when the raw page was full. Invalid records do not remove the next page.</param>
public sealed record SearchPage<T>(IReadOnlyList<T> Items, bool HasAnotherPage);
