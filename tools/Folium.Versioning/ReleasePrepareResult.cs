namespace Folium.Versioning;

/// <summary>The version and notes produced by a release preparation.</summary>
/// <param name="Version">The next release version.</param>
/// <param name="PreviousTag">The latest vX.Y.Z tag that the work started from, including the v prefix.</param>
/// <param name="Notes">The CHANGELOG body for the new version.</param>
public sealed record ReleasePrepareResult(AppVersion Version, string PreviousTag, string Notes);
