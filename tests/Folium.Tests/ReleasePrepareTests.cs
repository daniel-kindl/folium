using Folium.Versioning;

namespace Folium.Tests;

public sealed class ReleasePrepareTests
{
    private static readonly Uri RepositoryUrl = new("https://github.com/daniel-kindl/folium");

    private const string BASE_CHANGELOG = "# Changelog\n\nNotable changes are recorded here.\n\n## Unreleased\n\n## 0.1.0\n\n- First preview.\n";

    [Fact]
    public void AutoBumpRaisesPatchForFixesDocsAndOtherTitles()
    {
        AppVersion next = ReleasePrepare.NextVersion(
            AppVersion.Parse("0.1.0"),
            [
                new ReleaseCommit("fix: correct the sort order (#25)", string.Empty),
                new ReleaseCommit("docs: rewrite README in ocho style (#43)", string.Empty),
                new ReleaseCommit("Bump Avalonia from 12.1.5 to 12.1.6 (#32)", string.Empty),
            ],
            VersionBump.Auto);

        Assert.Equal("0.1.1", next.DisplayVersion);
    }

    [Fact]
    public void AutoBumpRaisesMinorForAFeature()
    {
        AppVersion next = ReleasePrepare.NextVersion(
            AppVersion.Parse("0.1.0"),
            [new ReleaseCommit("feat(library): export and restore backups (#26)", string.Empty)],
            VersionBump.Auto);

        Assert.Equal("0.2.0", next.DisplayVersion);
    }

    [Fact]
    public void AutoBumpRaisesMinorForABreakingChangeWhileMajorIsZero()
    {
        AppVersion next = ReleasePrepare.NextVersion(
            AppVersion.Parse("0.1.0"),
            [new ReleaseCommit("feat(library)!: rename the backup format", "BREAKING CHANGE: The backup kind changed.\n")],
            VersionBump.Auto);

        Assert.Equal("0.2.0", next.DisplayVersion);
    }

    [Fact]
    public void AutoBumpRaisesMajorForABreakingChangeAfterVersionOne()
    {
        AppVersion next = ReleasePrepare.NextVersion(
            AppVersion.Parse("1.2.3"),
            [new ReleaseCommit("fix: drop the old path", "BREAKING CHANGE: The path is gone.\n")],
            VersionBump.Auto);

        Assert.Equal("2.0.0", next.DisplayVersion);
    }

    [Fact]
    public void AutoBumpRaisesMajorForABangInTheTitle()
    {
        AppVersion next = ReleasePrepare.NextVersion(
            AppVersion.Parse("1.0.0"),
            [new ReleaseCommit("chore!: remove the preview flag", string.Empty)],
            VersionBump.Auto);

        Assert.Equal("2.0.0", next.DisplayVersion);
    }

    [Fact]
    public void AFeatureAndAFixTogetherRaiseMinor()
    {
        AppVersion next = ReleasePrepare.NextVersion(
            AppVersion.Parse("1.0.0"),
            [
                new ReleaseCommit("fix: correct the sort order (#25)", string.Empty),
                new ReleaseCommit("feat: search saved books (#25)", string.Empty),
            ],
            VersionBump.Auto);

        Assert.Equal("1.1.0", next.DisplayVersion);
    }

    [Fact]
    public void AnExplicitBumpOverridesTheComputedBump()
    {
        ReleaseCommit feature = new("feat: search saved books (#25)", string.Empty);
        ReleaseCommit docs = new("docs: rewrite README in ocho style (#43)", string.Empty);

        Assert.Equal(
            "0.1.1",
            ReleasePrepare.NextVersion(AppVersion.Parse("0.1.0"), [feature], VersionBump.Patch).DisplayVersion);
        Assert.Equal(
            "0.2.0",
            ReleasePrepare.NextVersion(AppVersion.Parse("0.1.0"), [docs], VersionBump.Minor).DisplayVersion);
        Assert.Equal(
            "1.0.0",
            ReleasePrepare.NextVersion(AppVersion.Parse("0.1.0"), [docs], VersionBump.Major).DisplayVersion);
    }

    [Fact]
    public void NextVersionFailsWhenThereAreNoCommits()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ReleasePrepare.NextVersion(AppVersion.Parse("0.1.0"), [], VersionBump.Auto));

        Assert.Contains("no commits", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BumpFailsWhenTheNextCodeWouldExceedTheFormulaLimits()
    {
        ArgumentOutOfRangeException patch = Assert.Throws<ArgumentOutOfRangeException>(
            () => AppVersion.Parse("0.1.99").Bump(VersionBump.Patch));
        ArgumentOutOfRangeException minor = Assert.Throws<ArgumentOutOfRangeException>(
            () => AppVersion.Parse("0.99.1").Bump(VersionBump.Minor));
        ArgumentOutOfRangeException major = Assert.Throws<ArgumentOutOfRangeException>(
            () => AppVersion.Parse("209999.0.0").Bump(VersionBump.Major));

        Assert.Equal("patch", patch.ParamName);
        Assert.Equal("minor", minor.ParamName);
        Assert.Equal("major", major.ParamName);
    }

    [Fact]
    public void LatestReleaseReadsTheHighestStrictTag()
    {
        AppVersion latest = ReleasePrepare.LatestRelease(["v0.1.0", "v0.0.9", "ignore", "v0.2.0"]);

        Assert.Equal("0.2.0", latest.DisplayVersion);
    }

    [Fact]
    public void LatestReleaseFailsWhenNoStrictTagExists()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ReleasePrepare.LatestRelease(["v1.2.3-beta", "release-1.0.0"]));

        Assert.Contains("tag", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChangelogGroupsStrippedTitlesAndLinksPullRequests()
    {
        string notes = ChangelogUpdate.BuildNotes(
            unreleasedNotes: string.Empty,
            [
                new ReleaseCommit("feat(library): export and restore backups (#26)", string.Empty),
                new ReleaseCommit("feat: search saved books (#25)", string.Empty),
                new ReleaseCommit("fix(library): correct the sort order (#36)", string.Empty),
                new ReleaseCommit("docs: rewrite README in ocho style (#43)", string.Empty),
                new ReleaseCommit("Bump Avalonia from 12.1.5 to 12.1.6 (#32)", string.Empty),
                new ReleaseCommit("chore: tidy the workflow", string.Empty),
            ],
            RepositoryUrl);

        Assert.Equal(
            "### Features\n\n- export and restore backups ([#26](https://github.com/daniel-kindl/folium/pull/26))\n- search saved books ([#25](https://github.com/daniel-kindl/folium/pull/25))\n\n### Fixes\n\n- correct the sort order ([#36](https://github.com/daniel-kindl/folium/pull/36))\n\n### Other changes\n\n- rewrite README in ocho style ([#43](https://github.com/daniel-kindl/folium/pull/43))\n- Bump Avalonia from 12.1.5 to 12.1.6 ([#32](https://github.com/daniel-kindl/folium/pull/32))\n- tidy the workflow",
            notes);
    }

    [Fact]
    public void ChangelogKeepsUnreleasedNotesInFrontAndOmitsEmptyGroups()
    {
        string notes = ChangelogUpdate.BuildNotes(
            unreleasedNotes: "- Document the one-click release flow.",
            [new ReleaseCommit("docs: rewrite README in ocho style (#43)", string.Empty)],
            RepositoryUrl);

        Assert.Equal(
            "- Document the one-click release flow.\n\n### Other changes\n\n- rewrite README in ocho style ([#43](https://github.com/daniel-kindl/folium/pull/43))",
            notes);
    }

    [Fact]
    public void ApplyWritesTheNewSectionAndClearsUnreleased()
    {
        const string NOTES = "- Document the one-click release flow.\n\n### Other changes\n\n- rewrite README in ocho style ([#43](https://github.com/daniel-kindl/folium/pull/43))";

        string updated = ChangelogUpdate.Apply(BASE_CHANGELOG, AppVersion.Parse("0.1.1"), NOTES);

        Assert.Equal(
            "# Changelog\n\nNotable changes are recorded here.\n\n## Unreleased\n\n## 0.1.1\n\n- Document the one-click release flow.\n\n### Other changes\n\n- rewrite README in ocho style ([#43](https://github.com/daniel-kindl/folium/pull/43))\n\n## 0.1.0\n\n- First preview.\n",
            updated);
        Assert.Equal(NOTES, ChangelogNotes.ForVersion(updated, AppVersion.Parse("0.1.1")));
        Assert.Equal("- First preview.", ChangelogNotes.ForVersion(updated, AppVersion.Parse("0.1.0")));
        Assert.Equal(string.Empty, ChangelogUpdate.ReadUnreleased(updated));
    }

    [Fact]
    public void ApplyFailsWhenTheUnreleasedHeadingIsMissing()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ChangelogUpdate.Apply("# Changelog\n\n## 0.1.0\n\n- First preview.\n", AppVersion.Parse("0.1.1"), "- Notes."));

        Assert.Contains("Unreleased", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RunWritesTheVersionSourceAndTheChangelogSection()
    {
        using ReleaseCheckFiles files = ReleaseCheckFiles.Create("0.1.0", BASE_CHANGELOG);

        ReleasePrepareResult result = ReleasePrepare.Run(
            new ReleasePrepareRequest(
                files.PropsPath,
                files.ChangelogPath,
                ["v0.1.0"],
                [
                    new ReleaseCommit("docs: rewrite README in ocho style (#43)", string.Empty),
                    new ReleaseCommit("feat: search saved books (#25)", string.Empty),
                ],
                VersionBump.Auto,
                RepositoryUrl));

        Assert.Equal("0.2.0", result.Version.DisplayVersion);
        Assert.Equal("v0.1.0", result.PreviousTag);
        Assert.Equal("0.2.0", VersionSource.Read(files.PropsPath).DisplayVersion);
        string notes = ChangelogNotes.ForVersion(File.ReadAllText(files.ChangelogPath), result.Version);
        Assert.Equal(result.Notes, notes);
        Assert.Contains("### Features", notes, StringComparison.Ordinal);
        Assert.Contains("search saved books", notes, StringComparison.Ordinal);
        Assert.Contains("rewrite README in ocho style", notes, StringComparison.Ordinal);
        Assert.Equal(string.Empty, ChangelogUpdate.ReadUnreleased(File.ReadAllText(files.ChangelogPath)));
        Assert.Equal(
            notes,
            ReleaseCheck.Run(
                new ReleaseCheckRequest("0.2.0", files.PropsPath, files.ChangelogPath, ["v0.1.0"])));
    }

    [Fact]
    public void RunFailsWhenThereAreNoCommitsSinceTheLatestTag()
    {
        using ReleaseCheckFiles files = ReleaseCheckFiles.Create("0.1.0", BASE_CHANGELOG);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ReleasePrepare.Run(
                new ReleasePrepareRequest(
                    files.PropsPath,
                    files.ChangelogPath,
                    ["v0.1.0"],
                    [],
                    VersionBump.Auto,
                    RepositoryUrl)));

        Assert.Contains("no commits", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("v0.1.0", exception.Message, StringComparison.Ordinal);
        Assert.Equal("0.1.0", VersionSource.Read(files.PropsPath).DisplayVersion);
    }

    [Fact]
    public void WriteVersionSourceReplacesOnlyTheFoliumVersionValue()
    {
        string path = Path.Combine(Path.GetTempPath(), "folium-props-" + Guid.NewGuid().ToString("N") + ".props");
        const string ORIGINAL = "<Project>\n  <PropertyGroup>\n    <!-- keep this comment -->\n    <FoliumVersion>0.1.0</FoliumVersion>\n    <Nullable>enable</Nullable>\n  </PropertyGroup>\n</Project>\n";
        File.WriteAllText(path, ORIGINAL);

        try
        {
            VersionSource.Write(path, AppVersion.Parse("0.2.0"));
            string written = File.ReadAllText(path);
            Assert.Equal(
                "<Project>\n  <PropertyGroup>\n    <!-- keep this comment -->\n    <FoliumVersion>0.2.0</FoliumVersion>\n    <Nullable>enable</Nullable>\n  </PropertyGroup>\n</Project>\n",
                written);
            Assert.Equal("0.2.0", VersionSource.Read(path).DisplayVersion);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
