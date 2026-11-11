using System.Globalization;

namespace Folium.Views;

/// <summary>Chooses the cover placeholder tone and letter for a book.</summary>
public static class CoverPlaceholder
{
    private static readonly string[] Tones = ["sage", "sand", "clay", "mist", "heather", "stone"];

    /// <summary>Returns the tone name for a saved book. The same book keeps the same tone.</summary>
    /// <param name="bookId">The saved book identifier.</param>
    public static string Tone(Guid bookId)
    {
        int hash = 0;
        foreach (byte unit in bookId.ToByteArray())
        {
            hash = unchecked((hash * 31) + unit);
        }

        return Tones[(hash & int.MaxValue) % Tones.Length];
    }

    /// <summary>True when the name is one of the six placeholder tones.</summary>
    /// <param name="tone">The tone name to check.</param>
    public static bool IsTone(string? tone)
    {
        return tone is not null && Array.IndexOf(Tones, tone) >= 0;
    }

    /// <summary>Returns the first letter of the title. A leading "The " is skipped.</summary>
    /// <param name="title">The book title. A null title is treated as empty.</param>
    public static string Initial(string title)
    {
        string text = (title ?? "").Trim();
        if (text.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
        {
            text = text[4..].TrimStart();
        }

        if (text.Length == 0)
        {
            return "";
        }

        StringInfo info = new(text);
        return info.SubstringByTextElements(0, 1);
    }

    /// <summary>True when the cover is too narrow to show the title and the author.</summary>
    /// <param name="width">The cover width in device-independent pixels.</param>
    public static bool ShowsLetterOnly(double width)
    {
        return width < 80;
    }
}
