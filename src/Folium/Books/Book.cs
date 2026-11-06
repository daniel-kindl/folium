namespace Folium.Books;

/// <summary>One saved book. The identifier does not change when the title or the author changes.</summary>
/// <param name="Id">Does not change when the title or the author changes.</param>
/// <param name="Title">Text that names the book.</param>
/// <param name="Author">Text that names the writer.</param>
public sealed record Book(Guid Id, string Title, string Author);
