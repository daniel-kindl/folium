using System.Globalization;
using System.Text.Json;

namespace Folium.Search;

internal static class OpenLibraryJson
{
    internal static string Text(JsonElement value, string name)
    {
        RequireObject(value);
        return value.TryGetProperty(name, out JsonElement field)
            && field.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? field.ToString() : "";
    }

    internal static IEnumerable<JsonElement> Array(JsonElement value, string name)
    {
        RequireObject(value);
        return value.TryGetProperty(name, out JsonElement field) && field.ValueKind == JsonValueKind.Array
            ? field.EnumerateArray() : [];
    }

    internal static string Strings(JsonElement value, string name)
    {
        return string.Join(", ", Array(value, name).Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()).Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    internal static IReadOnlyList<SearchWork> Works(JsonElement root)
    {
        RequireArray(root, "docs");
        return [.. Array(root, "docs").Where(item => ValidKey(Text(item, "key"), "works", 'W'))
            .Select(item => new SearchWork(Text(item, "key"), Text(item, "title"),
                Strings(item, "author_name"), Text(item, "first_publish_year"), CoverId(item)))];
    }

    private static int? CoverId(JsonElement item)
    {
        return item.TryGetProperty("cover_i", out JsonElement cover) && cover.ValueKind == JsonValueKind.Number && cover.TryGetInt32(out int id) && id > 0 ? id : null;
    }

    internal static SearchEdition Edition(JsonElement item)
    {
        string description = Text(item, "description");
        if (item.TryGetProperty("description", out JsonElement field) && field.ValueKind == JsonValueKind.Object)
        {
            description = Text(field, "value");
        }

        int? cover = Array(item, "covers").Where(value => value.ValueKind == JsonValueKind.Number)
            .Select(value => value.TryGetInt32(out int id) && id > 0 ? (int?)id : null).FirstOrDefault(value => value is not null);
        string isbn = string.Join(", ", new[] { Strings(item, "isbn_13"), Strings(item, "isbn_10") }.Where(value => value.Length > 0));
        string languages = string.Join(", ", Array(item, "languages").Select(value => Text(value, "key").Replace("/languages/", "", StringComparison.Ordinal)));
        return new SearchEdition(Text(item, "key"), Text(item, "title"), "", Strings(item, "publishers"),
            Text(item, "publish_date"), isbn, Text(item, "edition_name"), languages, description, cover);
    }

    internal static bool ValidKey(string key, string kind, char suffix)
    {
        string prefix = "/" + kind + "/OL";
        return key.StartsWith(prefix, StringComparison.Ordinal) && key.EndsWith(suffix)
            && long.TryParse(key.AsSpan(prefix.Length, key.Length - prefix.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }

    private static void RequireObject(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new SearchException("Open Library sent an unreadable response. Try again.");
        }
    }

    internal static void RequireArray(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out JsonElement field) || field.ValueKind != JsonValueKind.Array)
        {
            throw new SearchException("Open Library sent an unreadable response. Try again.");
        }
    }
}
