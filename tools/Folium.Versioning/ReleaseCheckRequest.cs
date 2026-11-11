namespace Folium.Versioning;

/// <summary>The inputs for a release version check.</summary>
/// <param name="RequestedVersion">The version from the workflow input or the release tag.</param>
/// <param name="PropsPath">The path of the Directory.Build.props file.</param>
/// <param name="ChangelogPath">The path of the CHANGELOG file.</param>
/// <param name="Tags">Existing git tag names. The tag for the current version may be included.</param>
public sealed record ReleaseCheckRequest(
    string RequestedVersion,
    string PropsPath,
    string ChangelogPath,
    IReadOnlyList<string> Tags);
