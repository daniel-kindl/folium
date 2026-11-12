namespace Folium.Updates;

/// <summary>Release metadata needed to offer and install an update.</summary>
/// <param name="Version">The release version.</param>
/// <param name="ApkUrl">The APK download URL.</param>
/// <param name="SizeBytes">The APK size in bytes.</param>
/// <param name="Notes">The plain-text release notes.</param>
/// <param name="PageUrl">The GitHub release page URL.</param>
public sealed record ReleaseInfo(SemVersion Version, Uri ApkUrl, long SizeBytes, string Notes, Uri PageUrl);
