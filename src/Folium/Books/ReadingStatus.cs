namespace Folium.Books;

/// <summary>The place of a saved book in the person's reading.</summary>
public enum ReadingStatus
{
    /// <summary>The person plans to read the book.</summary>
    ToRead = 0,

    /// <summary>The person is reading the book.</summary>
    Reading = 1,

    /// <summary>The person has finished the book.</summary>
    Finished = 2,
}
