using System.Globalization;
using System.Text.RegularExpressions;

namespace Folium.Versioning;

/// <summary>Reads Conventional Commit titles and breaking-change markers.</summary>
internal static partial class ConventionalCommit
{
    [GeneratedRegex(
        @"^(?<type>[a-z]+)(?:\((?<scope>[^)]*)\))?(?<breaking>!)?: (?<subject>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        1000)]
    private static partial Regex HeaderPattern { get; }

    [GeneratedRegex(
        @" \(#(?<number>[0-9]+)\)$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        1000)]
    private static partial Regex TrailingPullRequestPattern { get; }

    /// <summary>Returns true when the commit is a breaking change.</summary>
    /// <param name="commit">The commit to inspect.</param>
    /// <returns>true when the title has a bang or the body has a BREAKING CHANGE footer.</returns>
    public static bool IsBreaking(ReleaseCommit commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        Match header = HeaderPattern.Match(commit.Title ?? string.Empty);
        if (header.Success && header.Groups["breaking"].Success)
        {
            return true;
        }

        foreach (string line in (commit.Body ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("BREAKING CHANGE:", StringComparison.Ordinal)
                || trimmed.StartsWith("BREAKING-CHANGE:", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the Conventional Commit type, or null when the title does not match.</summary>
    /// <param name="title">The first line of the commit message.</param>
    /// <returns>The type, such as feat or fix, or null.</returns>
    public static string? Type(string? title)
    {
        Match header = HeaderPattern.Match(title ?? string.Empty);
        return header.Success ? header.Groups["type"].Value : null;
    }

    /// <summary>Returns the CHANGELOG group for the commit.</summary>
    /// <param name="commit">The commit to classify.</param>
    /// <returns>Features for feat, Fixes for fix, and Other changes otherwise.</returns>
    public static string Group(ReleaseCommit commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        return GroupName(Type(commit.Title));
    }

    /// <summary>Returns the title with the type, the scope, and a trailing pull request reference removed.</summary>
    /// <param name="title">The first line of the commit message.</param>
    /// <returns>The stripped subject text.</returns>
    public static string Subject(string? title)
    {
        string text = title ?? string.Empty;
        Match header = HeaderPattern.Match(text);
        string subject = header.Success ? header.Groups["subject"].Value : text;
        return TrailingPullRequestPattern.Replace(subject, string.Empty);
    }

    /// <summary>Returns the trailing pull request number, when the title ends with (#NN).</summary>
    /// <param name="title">The first line of the commit message.</param>
    /// <returns>The pull request number, or null when the title has no trailing reference.</returns>
    public static int? PullRequestNumber(string? title)
    {
        string text = title ?? string.Empty;
        Match header = HeaderPattern.Match(text);
        string subject = header.Success ? header.Groups["subject"].Value : text;
        Match pullRequest = TrailingPullRequestPattern.Match(subject);
        return pullRequest.Success
            ? int.Parse(pullRequest.Groups["number"].Value, CultureInfo.InvariantCulture)
            : null;
    }

    private static string GroupName(string? type)
    {
        return type switch
        {
            "feat" => "Features",
            "fix" => "Fixes",
            _ => "Other changes",
        };
    }
}
