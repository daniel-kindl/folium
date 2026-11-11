using System.Globalization;

namespace Folium.Versioning;

/// <summary>Reads the CHANGELOG section for one released version.</summary>
public static class ChangelogNotes
{
    /// <summary>Returns the body of the CHANGELOG section for the version.</summary>
    /// <param name="changelogText">The full CHANGELOG text.</param>
    /// <param name="version">The version to extract.</param>
    /// <returns>The section body, without the heading.</returns>
    /// <exception cref="ArgumentNullException">The text or the version is null.</exception>
    /// <exception cref="InvalidOperationException">The section is missing or empty.</exception>
    public static string ForVersion(string changelogText, AppVersion version)
    {
        ArgumentNullException.ThrowIfNull(changelogText);
        ArgumentNullException.ThrowIfNull(version);

        string normalized = changelogText.Replace("\r\n", "\n", StringComparison.Ordinal);
        string[] lines = normalized.Split('\n');
        int start = -1;
        for (int index = 0; index < lines.Length; index++)
        {
            if (IsHeadingFor(lines[index], version))
            {
                start = index + 1;
                break;
            }
        }

        if (start < 0)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The CHANGELOG has no section for version {0}.",
                    version.DisplayVersion));
        }

        int end = lines.Length;
        for (int index = start; index < lines.Length; index++)
        {
            if (lines[index].StartsWith("## ", StringComparison.Ordinal))
            {
                end = index;
                break;
            }
        }

        string body = string.Join('\n', lines[start..end]).Trim();
        return body.Length > 0
            ? body
            : throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The CHANGELOG section for version {0} is empty.",
                    version.DisplayVersion));
    }

    private static bool IsHeadingFor(string line, AppVersion version)
    {
        string expected = "## " + version.DisplayVersion;
        return line.Equals(expected, StringComparison.Ordinal)
            || line.StartsWith(expected + " ", StringComparison.Ordinal);
    }
}
