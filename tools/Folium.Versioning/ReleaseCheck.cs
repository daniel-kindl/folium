namespace Folium.Versioning;

/// <summary>Validates a release version against the version source, earlier tags, and the CHANGELOG.</summary>
public static class ReleaseCheck
{
    /// <summary>Validates the release and returns the CHANGELOG notes for that version.</summary>
    /// <param name="request">The check inputs.</param>
    /// <returns>The CHANGELOG notes for the version.</returns>
    /// <exception cref="ArgumentNullException">The request is null.</exception>
    /// <exception cref="ArgumentException">A required path or version is blank.</exception>
    /// <exception cref="InvalidOperationException">The version, the tags, or the CHANGELOG is not valid for a release.</exception>
    public static string Run(ReleaseCheckRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.RequestedVersion))
        {
            throw new ArgumentException("The requested version is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.PropsPath))
        {
            throw new ArgumentException("The version source path is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ChangelogPath))
        {
            throw new ArgumentException("The CHANGELOG path is required.", nameof(request));
        }

        if (request.Tags is null)
        {
            throw new ArgumentException("The tag list is required.", nameof(request));
        }

        AppVersion requested = AppVersion.Parse(request.RequestedVersion);
        AppVersion source = VersionSource.Read(request.PropsPath);
        VersionSource.EnsureMatches(requested, source);
        requested.EnsureOutranks(AppVersion.PreviousReleases(request.Tags, requested));
        string changelog = File.ReadAllText(request.ChangelogPath);
        return ChangelogNotes.ForVersion(changelog, requested);
    }
}
