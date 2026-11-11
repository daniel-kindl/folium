using Folium.Versioning;

namespace Folium.Tests;

public sealed class ReleaseVersionCheckTests
{
    [Fact]
    public void OutranksPreviousReleasesWhenTheCodeIsStrictlyGreater()
    {
        AppVersion[] previous =
        [
            AppVersion.Parse("0.1.0"),
            AppVersion.Parse("0.99.99"),
            AppVersion.Parse("1.0.0"),
        ];

        Assert.True(AppVersion.Parse("1.0.1").Outranks(previous));
        Assert.False(AppVersion.Parse("1.0.0").Outranks(previous));
        Assert.False(AppVersion.Parse("0.99.99").Outranks(previous));
        Assert.False(AppVersion.Parse("0.2.0").Outranks(previous));
        Assert.True(AppVersion.Parse("0.1.0").Outranks([]));
    }

    [Fact]
    public void EnsureOutranksFailsWhenTheCodeDoesNotIncrease()
    {
        AppVersion[] previous =
        [
            AppVersion.Parse("0.99.99"),
            AppVersion.Parse("1.0.0"),
        ];

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => AppVersion.Parse("1.0.0").EnsureOutranks(previous));

        Assert.Contains("1.0.0", exception.Message, StringComparison.Ordinal);
        Assert.Contains("10000", exception.Message, StringComparison.Ordinal);
        AppVersion.Parse("1.0.1").EnsureOutranks(previous);
        AppVersion.Parse("0.1.0").EnsureOutranks([]);
    }

    [Fact]
    public void ParseReleaseTagsReadsStrictVPrefixesAndIgnoresOtherTags()
    {
        IReadOnlyList<AppVersion> versions = AppVersion.ParseReleaseTags(
        [
            "v0.1.0",
            "v1.2.3",
            "v1.2.3-beta",
            "1.2.3",
            "v01.2.3",
            "release-1.0.0",
            "v2.0.0",
            "v2.0",
        ]);

        Assert.Equal(
            [AppVersion.Parse("0.1.0"), AppVersion.Parse("1.2.3"), AppVersion.Parse("2.0.0")],
            versions);
    }

    [Fact]
    public void PreviousTagsExcludeTheVersionBeingReleased()
    {
        AppVersion current = AppVersion.Parse("1.0.1");
        IReadOnlyList<AppVersion> previous = AppVersion.PreviousReleases(
            ["v0.1.0", "v1.0.0", "v1.0.1"],
            current);

        Assert.Equal([AppVersion.Parse("0.1.0"), AppVersion.Parse("1.0.0")], previous);
        current.EnsureOutranks(previous);
    }

    [Fact]
    public void ReadVersionSourceReturnsTheFoliumVersionProperty()
    {
        string path = Path.Combine(Path.GetTempPath(), "folium-props-" + Guid.NewGuid().ToString("N") + ".props");
        File.WriteAllText(
            path,
            "<Project><PropertyGroup><FoliumVersion>1.2.3</FoliumVersion></PropertyGroup></Project>\n");

        try
        {
            AppVersion version = VersionSource.Read(path);
            Assert.Equal("1.2.3", version.DisplayVersion);
            Assert.Equal(10203, version.VersionCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadVersionSourceFailsWhenThePropertyIsMissing()
    {
        string path = Path.Combine(Path.GetTempPath(), "folium-props-" + Guid.NewGuid().ToString("N") + ".props");
        File.WriteAllText(path, "<Project><PropertyGroup></PropertyGroup></Project>");

        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => VersionSource.Read(path));
            Assert.Contains("FoliumVersion", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void EnsureMatchesFailsWhenTheRequestedVersionDiffers()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => VersionSource.EnsureMatches(AppVersion.Parse("1.2.3"), AppVersion.Parse("1.2.4")));

        Assert.Contains("1.2.3", exception.Message, StringComparison.Ordinal);
        Assert.Contains("1.2.4", exception.Message, StringComparison.Ordinal);
        VersionSource.EnsureMatches(AppVersion.Parse("1.2.3"), AppVersion.Parse("1.2.3"));
    }

    [Fact]
    public void ChangelogNotesReturnTheSectionForTheVersion()
    {
        const string CHANGELOG = "# Changelog\n\n## Unreleased\n\n- Not in a release.\n\n## 1.2.3 - 2026-11-11\n\n- Add the signed release workflow.\n- Document the version formula.\n\n## 0.1.0\n\n- First preview.\n";

        string notes = ChangelogNotes.ForVersion(CHANGELOG, AppVersion.Parse("1.2.3"));

        Assert.Equal(
            "- Add the signed release workflow.\n- Document the version formula.",
            notes);
        Assert.DoesNotContain("Unreleased", notes, StringComparison.Ordinal);
        Assert.DoesNotContain("0.1.0", notes, StringComparison.Ordinal);
        Assert.Equal("- First preview.", ChangelogNotes.ForVersion(CHANGELOG, AppVersion.Parse("0.1.0")));
    }

    [Fact]
    public void ChangelogNotesFailWhenTheSectionIsMissingOrEmpty()
    {
        const string CHANGELOG = "# Changelog\n\n## Unreleased\n\n- Work in progress.\n\n## 0.1.0\n\n";

        InvalidOperationException missing = Assert.Throws<InvalidOperationException>(
            () => ChangelogNotes.ForVersion(CHANGELOG, AppVersion.Parse("1.0.0")));
        InvalidOperationException empty = Assert.Throws<InvalidOperationException>(
            () => ChangelogNotes.ForVersion(CHANGELOG, AppVersion.Parse("0.1.0")));

        Assert.Contains("1.0.0", missing.Message, StringComparison.Ordinal);
        Assert.Contains("0.1.0", empty.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RunFailsWhenTheRequestedVersionDoesNotMatchTheSource()
    {
        using ReleaseCheckFiles files = ReleaseCheckFiles.Create("1.2.3", "## 1.2.4\n\n- Notes.\n");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ReleaseCheck.Run(
                new ReleaseCheckRequest(
                    "1.2.4",
                    files.PropsPath,
                    files.ChangelogPath,
                    ["v0.1.0"])));

        Assert.Contains("1.2.3", exception.Message, StringComparison.Ordinal);
        Assert.Contains("1.2.4", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RunFailsWhenTheVersionCodeDoesNotIncrease()
    {
        using ReleaseCheckFiles files = ReleaseCheckFiles.Create("1.0.0", "## 1.0.0\n\n- Same version.\n");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ReleaseCheck.Run(
                new ReleaseCheckRequest(
                    "1.0.0",
                    files.PropsPath,
                    files.ChangelogPath,
                    ["v0.99.99", "v1.0.0", "v1.0.1"])));

        Assert.Contains("10000", exception.Message, StringComparison.Ordinal);
        Assert.Contains("1.0.1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RunReturnsChangelogNotesWhenTheReleaseIsValid()
    {
        using ReleaseCheckFiles files = ReleaseCheckFiles.Create(
            "1.0.1",
            "## 1.0.1\n\n- Increase the version code.\n");

        string notes = ReleaseCheck.Run(
            new ReleaseCheckRequest(
                "1.0.1",
                files.PropsPath,
                files.ChangelogPath,
                ["v0.99.99", "v1.0.0", "v1.0.1"]));

        Assert.Equal("- Increase the version code.", notes);
    }

    [Fact]
    public void RepositoryVersionSourceIsStrictAndMatchesTheFormula()
    {
        string root = RepositoryRoot();
        AppVersion version = VersionSource.Read(Path.Combine(root, "Directory.Build.props"));

        Assert.Equal(version.DisplayVersion, version.ToString());
        Assert.Equal(
            (version.Major * 10000) + (version.Minor * 100) + version.Patch,
            version.VersionCode);
        Assert.Equal("0.1.0", version.DisplayVersion);
        Assert.Equal(100, version.VersionCode);
    }

    [Fact]
    public void RunAcceptsTheFirstReleaseWhenNoEarlierTagsExist()
    {
        using ReleaseCheckFiles files = ReleaseCheckFiles.Create("0.1.0", "## 0.1.0\n\n- First release.\n");

        string notes = ReleaseCheck.Run(
            new ReleaseCheckRequest("0.1.0", files.PropsPath, files.ChangelogPath, []));

        Assert.Equal("- First release.", notes);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Folium.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The repository root was not found from the test output directory.");
    }
}
