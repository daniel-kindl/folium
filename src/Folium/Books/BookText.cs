using System.Globalization;
using System.Text;

namespace Folium.Books;

internal static class BookText
{
    public static IReadOnlyList<BookInputProblem> Problems(string? title, string? author)
    {
        List<BookInputProblem> problems = new(2);
        if (string.IsNullOrWhiteSpace(title))
        {
            problems.Add(BookInputProblem.MissingTitle);
        }

        if (string.IsNullOrWhiteSpace(author))
        {
            problems.Add(BookInputProblem.MissingAuthor);
        }

        return problems;
    }

    public static (string Title, string Author) Require(string? title, string? author)
    {
        IReadOnlyList<BookInputProblem> problems = Problems(title, author);
        return problems.Count > 0 ? throw new BookInputException(problems) : ((string Title, string Author))(title!.Trim(), author!.Trim());
    }

    public static string FoldForSearch(string value)
    {
        StringBuilder normalized = new();
        foreach (Rune rune in WithoutUnpairedSurrogates(value).Normalize(NormalizationForm.FormD).EnumerateRunes())
        {
            UnicodeCategory category = Rune.GetUnicodeCategory(rune);
            if (category is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark))
            {
                _ = normalized.Append(rune.ToString());
            }
        }

        return normalized.ToString();
    }

    private static string WithoutUnpairedSurrogates(string value)
    {
        StringBuilder builder = new(value.Length);
        for (int index = 0; index < value.Length; index++)
        {
            char unit = value[index];
            if (char.IsHighSurrogate(unit))
            {
                if (index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                {
                    _ = builder.Append(unit).Append(value[index + 1]);
                    index++;
                }

                continue;
            }

            if (!char.IsLowSurrogate(unit))
            {
                _ = builder.Append(unit);
            }
        }

        return builder.ToString();
    }
}
