namespace Folium.Updates;

/// <summary>Applies update timing and offer rules.</summary>
public static class UpdatePolicy
{
    /// <summary>Returns whether an automatic or forced release check must run.</summary>
    /// <param name="settings">The update preferences.</param>
    /// <param name="now">The current time.</param>
    /// <param name="force">Whether a user started the check manually.</param>
    public static bool IsCheckDue(UpdateSettings settings, DateTimeOffset now, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return force || (settings.ChecksEnabled
            && (settings.LastCheck is not DateTimeOffset lastCheck || now - lastCheck >= TimeSpan.FromHours(24)));
    }

    /// <summary>Returns whether the release is newer than the installed and skipped versions.</summary>
    /// <param name="release">The latest release, if available.</param>
    /// <param name="currentVersion">The installed version.</param>
    /// <param name="skippedVersion">The version the user skipped.</param>
    public static bool ShouldOffer(ReleaseInfo? release, string currentVersion, string? skippedVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentVersion);
        return release?.Version.CompareTo(SemVersion.Parse(currentVersion)) > 0
            && !string.Equals(release?.Version.ToString(), skippedVersion, StringComparison.Ordinal);
    }
}
