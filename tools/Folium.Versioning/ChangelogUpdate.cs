using System.Globalization;
using System.Text;

namespace Folium.Versioning;

/// <summary>Builds and inserts a CHANGELOG section for a release.</summary>
public static class ChangelogUpdate
{
    private const string UNRELEASED_HEADING = "## Unreleased";

    /// <summary>Returns the notes under the Unreleased heading.</summary>
    /// <param name="changelogText">The full CHANGELOG text.</param>
    /// <returns>The Unreleased body, or an empty string when that section has no notes.</returns>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    /// <exception cref="InvalidOperationException">The Unreleased heading is missing.</exception>
    public static string ReadUnreleased(string changelogText)
    {
        ArgumentNullException.ThrowIfNull(changelogText);
        FindUnreleased(Normalize(changelogText), out string body, out _, out _);
        return body;
    }

    /// <summary>Builds the body of a new CHANGELOG section from Unreleased notes and commit titles.</summary>
    /// <param name="unreleasedNotes">Notes already recorded under Unreleased.</param>
    /// <param name="commits">Commits since the latest release tag, oldest first.</param>
    /// <param name="repositoryUrl">The GitHub repository URL used to link pull requests.</param>
    /// <returns>The section body, without the version heading.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">The repository URL is not an absolute HTTP URL.</exception>
    public static string BuildNotes(
        string unreleasedNotes,
        IReadOnlyList<ReleaseCommit> commits,
        Uri repositoryUrl)
    {
        ArgumentNullException.ThrowIfNull(unreleasedNotes);
        ArgumentNullException.ThrowIfNull(commits);
        string repository = RepositoryBase(repositoryUrl);
        StringBuilder builder = new();
        string trimmedUnreleased = unreleasedNotes.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (trimmedUnreleased.Length > 0)
        {
            _ = builder.Append(trimmedUnreleased);
        }

        AppendGroup(builder, "Features", commits, repository);
        AppendGroup(builder, "Fixes", commits, repository);
        AppendGroup(builder, "Other changes", commits, repository);
        return builder.ToString();
    }

    /// <summary>Clears the Unreleased section and inserts the new version section after it.</summary>
    /// <param name="changelogText">The full CHANGELOG text.</param>
    /// <param name="version">The version of the new section.</param>
    /// <param name="notes">The body of the new section.</param>
    /// <returns>The updated CHANGELOG text.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">The notes are blank.</exception>
    /// <exception cref="InvalidOperationException">The Unreleased heading is missing.</exception>
    public static string Apply(string changelogText, AppVersion version, string notes)
    {
        ArgumentNullException.ThrowIfNull(changelogText);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(notes);
        string trimmedNotes = notes.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (trimmedNotes.Length == 0)
        {
            throw new ArgumentException("The CHANGELOG notes are required.", nameof(notes));
        }

        string normalized = Normalize(changelogText);
        FindUnreleased(normalized, out _, out int headingIndex, out int nextHeadingIndex);
        string[] lines = normalized.Split('\n');
        string prefix = string.Join('\n', lines[..(headingIndex + 1)]);
        string suffix = nextHeadingIndex < lines.Length
            ? string.Join('\n', lines[nextHeadingIndex..])
            : string.Empty;
        return prefix
            + "\n\n## "
            + version.DisplayVersion
            + "\n\n"
            + trimmedNotes
            + "\n"
            + (suffix.Length == 0 ? string.Empty : "\n" + suffix);
    }

    private static void AppendGroup(
        StringBuilder builder,
        string group,
        IReadOnlyList<ReleaseCommit> commits,
        string repository)
    {
        bool headingWritten = false;
        foreach (ReleaseCommit commit in commits)
        {
            if (!string.Equals(ConventionalCommit.Group(commit), group, StringComparison.Ordinal))
            {
                continue;
            }

            if (!headingWritten)
            {
                if (builder.Length > 0)
                {
                    _ = builder.Append("\n\n");
                }

                _ = builder.Append("### ").Append(group).Append("\n\n");
                headingWritten = true;
            }
            else
            {
                _ = builder.Append('\n');
            }

            _ = builder.Append("- ").Append(FormatBullet(commit, repository));
        }
    }

    private static string FormatBullet(ReleaseCommit commit, string repository)
    {
        string subject = ConventionalCommit.Subject(commit.Title);
        int? number = ConventionalCommit.PullRequestNumber(commit.Title);
        return FormatSubject(subject, number, repository);
    }

    private static string FormatSubject(string subject, int? number, string repository)
    {
        return number is null
            ? subject
            : string.Format(
                CultureInfo.InvariantCulture,
                "{0} ([#{1}]({2}/pull/{1}))",
                subject,
                number.Value,
                repository);
    }

    private static string RepositoryBase(Uri repositoryUrl)
    {
        ArgumentNullException.ThrowIfNull(repositoryUrl);
        EnsureHttpUrl(repositoryUrl);
        return repositoryUrl.ToString().TrimEnd('/');
    }

    private static void EnsureHttpUrl(Uri repositoryUrl)
    {
        if (repositoryUrl.IsAbsoluteUri && IsHttp(repositoryUrl))
        {
            return;
        }

        throw new ArgumentException("The repository URL must be an absolute HTTP URL.", nameof(repositoryUrl));
    }

    private static bool IsHttp(Uri repositoryUrl)
    {
        return string.Equals(repositoryUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.Equals(repositoryUrl.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
    }

    private static void FindUnreleased(string changelogText, out string body, out int headingIndex, out int nextHeadingIndex)
    {
        string[] lines = changelogText.Split('\n');
        headingIndex = -1;
        for (int index = 0; index < lines.Length; index++)
        {
            if (lines[index].Equals(UNRELEASED_HEADING, StringComparison.Ordinal))
            {
                headingIndex = index;
                break;
            }
        }

        if (headingIndex < 0)
        {
            throw new InvalidOperationException("The CHANGELOG has no Unreleased section.");
        }

        nextHeadingIndex = lines.Length;
        for (int index = headingIndex + 1; index < lines.Length; index++)
        {
            if (lines[index].StartsWith("## ", StringComparison.Ordinal))
            {
                nextHeadingIndex = index;
                break;
            }
        }

        body = string.Join('\n', lines[(headingIndex + 1)..nextHeadingIndex]).Trim();
    }

    private static string Normalize(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
