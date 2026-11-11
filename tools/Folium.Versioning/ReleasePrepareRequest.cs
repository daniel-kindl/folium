namespace Folium.Versioning;

/// <summary>The inputs for a one-click release preparation.</summary>
/// <param name="PropsPath">The path of the Directory.Build.props file.</param>
/// <param name="ChangelogPath">The path of the CHANGELOG file.</param>
/// <param name="Tags">Existing git tag names that are ancestors of the current commit.</param>
/// <param name="Commits">Commits on the current branch since the latest release tag, oldest first.</param>
/// <param name="Bump">The requested bump. Use <see cref="VersionBump.Auto"/> to compute it from the commits.</param>
/// <param name="RepositoryUrl">The GitHub repository URL used to link pull requests.</param>
public sealed record ReleasePrepareRequest(
    string PropsPath,
    string ChangelogPath,
    IReadOnlyList<string> Tags,
    IReadOnlyList<ReleaseCommit> Commits,
    VersionBump Bump,
    Uri RepositoryUrl);
