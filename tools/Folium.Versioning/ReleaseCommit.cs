namespace Folium.Versioning;

/// <summary>A git commit that may affect the next release version and CHANGELOG.</summary>
/// <param name="Title">The first line of the commit message.</param>
/// <param name="Body">The rest of the commit message after the title.</param>
public sealed record ReleaseCommit(string Title, string Body);
