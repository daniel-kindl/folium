namespace Folium.Books;

/// <summary>One saved book. The identifier does not change when the title, the author, or the reading status changes.</summary>
/// <param name="Id">Does not change when the title, the author, or the reading status changes.</param>
/// <param name="Title">Text that names the book.</param>
/// <param name="Author">Text that names the writer.</param>
/// <param name="Status">The reading status. A new book uses <see cref="ReadingStatus.ToRead"/>.</param>
public sealed record Book(Guid Id, string Title, string Author, ReadingStatus Status = ReadingStatus.ToRead);
