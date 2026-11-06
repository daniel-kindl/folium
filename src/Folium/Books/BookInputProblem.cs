namespace Folium.Books;

/// <summary>A reason that book text is not valid.</summary>
public enum BookInputProblem
{
    /// <summary>The title has no text.</summary>
    MissingTitle = 0,

    /// <summary>The author has no text.</summary>
    MissingAuthor = 1,
}
