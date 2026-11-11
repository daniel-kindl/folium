using System.Globalization;

namespace Folium.Versioning;

/// <summary>Works out the next version, writes FoliumVersion, and updates the CHANGELOG.</summary>
public static class ReleasePrepare
{
    /// <summary>Returns the highest strict vX.Y.Z tag.</summary>
    /// <param name="tags">Tag names from the repository.</param>
    /// <returns>The highest parsed release version.</returns>
    /// <exception cref="ArgumentNullException">The list is null.</exception>
    /// <exception cref="InvalidOperationException">No strict vX.Y.Z tag is present.</exception>
    public static AppVersion LatestRelease(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        AppVersion? latest = null;
        foreach (AppVersion version in AppVersion.ParseReleaseTags(tags))
        {
            if (latest is null || version.VersionCode > latest.VersionCode)
            {
                latest = version;
            }
        }

        return latest
            ?? throw new InvalidOperationException("No vX.Y.Z release tag was found.");
    }

    /// <summary>Works out the next version from the current version, the commits, and the requested bump.</summary>
    /// <param name="current">The version of the latest release tag.</param>
    /// <param name="commits">Commits since that tag, oldest first.</param>
    /// <param name="bump">The requested bump, or <see cref="VersionBump.Auto"/> to compute it.</param>
    /// <returns>The next version.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="InvalidOperationException">There are no commits since the tag.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The next version would exceed the version code limits.</exception>
    public static AppVersion NextVersion(AppVersion current, IReadOnlyList<ReleaseCommit> commits, VersionBump bump)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(commits);
        EnsureHasCommits(commits, current);
        VersionBump resolved = bump == VersionBump.Auto ? Resolve(current, commits) : bump;
        return current.Bump(resolved);
    }

    /// <summary>Writes the next FoliumVersion and the new CHANGELOG section.</summary>
    /// <param name="request">The preparation inputs.</param>
    /// <returns>The next version, the previous tag, and the new CHANGELOG notes.</returns>
    /// <exception cref="ArgumentNullException">The request is null.</exception>
    /// <exception cref="ArgumentException">A required path or URL is blank.</exception>
    /// <exception cref="InvalidOperationException">The tags, the commits, or the CHANGELOG cannot produce a release.</exception>
    public static ReleasePrepareResult Run(ReleasePrepareRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
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

        if (request.Commits is null)
        {
            throw new ArgumentException("The commit list is required.", nameof(request));
        }

        if (request.RepositoryUrl is null)
        {
            throw new ArgumentException("The repository URL is required.", nameof(request));
        }

        AppVersion previous = LatestRelease(request.Tags);
        string previousTag = "v" + previous.DisplayVersion;
        EnsureHasCommits(request.Commits, previous, previousTag);
        AppVersion version = NextVersion(previous, request.Commits, request.Bump);
        string changelog = File.ReadAllText(request.ChangelogPath);
        string notes = ChangelogUpdate.BuildNotes(
            ChangelogUpdate.ReadUnreleased(changelog),
            request.Commits,
            request.RepositoryUrl);
        string updatedChangelog = ChangelogUpdate.Apply(changelog, version, notes);
        File.WriteAllText(request.ChangelogPath, updatedChangelog);
        VersionSource.Write(request.PropsPath, version);
        return new ReleasePrepareResult(version, previousTag, notes);
    }

    private static VersionBump Resolve(AppVersion current, IReadOnlyList<ReleaseCommit> commits)
    {
        VersionBump highest = VersionBump.Patch;
        foreach (ReleaseCommit commit in commits)
        {
            VersionBump next = Classify(current, commit);
            if (next > highest)
            {
                highest = next;
            }
        }

        return highest;
    }

    private static VersionBump Classify(AppVersion current, ReleaseCommit commit)
    {
        return ConventionalCommit.IsBreaking(commit)
            ? BreakingBump(current)
            : FeatureOrPatch(commit);
    }

    private static VersionBump BreakingBump(AppVersion current)
    {
        return current.Major == 0 ? VersionBump.Minor : VersionBump.Major;
    }

    private static VersionBump FeatureOrPatch(ReleaseCommit commit)
    {
        return string.Equals(ConventionalCommit.Type(commit.Title), "feat", StringComparison.Ordinal)
            ? VersionBump.Minor
            : VersionBump.Patch;
    }

    private static void EnsureHasCommits(IReadOnlyList<ReleaseCommit> commits, AppVersion previous, string? previousTag = null)
    {
        if (commits.Count > 0)
        {
            return;
        }

        string tag = previousTag ?? ("v" + previous.DisplayVersion);
        throw new InvalidOperationException(
            string.Format(
                CultureInfo.InvariantCulture,
                "There are no commits since tag {0}.",
                tag));
    }
}
