namespace Folium.Books;

/// <summary>A trimmed, valid shelf name.</summary>
public sealed record ShelfName
{
    private ShelfName(string value)
    {
        Value = value;
    }

    /// <summary>The trimmed shelf name.</summary>
    public string Value { get; }

    internal string FoldedValue => BookText.FoldForSearch(Value);

    /// <summary>Validates and trims a shelf name.</summary>
    /// <param name="value">The name to validate.</param>
    /// <exception cref="ShelfNameException">The name is empty or longer than 40 characters.</exception>
    public static ShelfName Parse(string? value)
    {
        string name = value?.Trim() ?? string.Empty;
        int length = name.EnumerateRunes().Count();
        return length switch
        {
            0 => throw new ShelfNameException("Enter a shelf name."),
            > 40 => throw new ShelfNameException("A shelf name can contain at most 40 characters."),
            _ => new(name),
        };
    }

    /// <summary>Returns the shelf name as text.</summary>
    public override string ToString()
    {
        return Value;
    }
}
