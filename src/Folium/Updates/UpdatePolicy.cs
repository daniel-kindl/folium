namespace Folium.Updates;

/// <summary>The result of applying update-check rules.</summary>
/// <summary>Pure update timing and offer rules.</summary>
public static class UpdatePolicy
{
    /// <summary>Decides whether a check is due and whether its release must be offered.</summary>
    /// <param name="settings">The update preferences.</param>
    /// <param name="currentVersion">The installed version.</param>
    /// <param name="now">The current time.</param>
    /// <param name="lastCheck">The time of the last check.</param>
    /// <param name="skippedVersion">The version the user skipped.</param>
    /// <param name="release">The latest release, if available.</param>
    /// <param name="force">Whether to ignore the disabled and 24-hour rules.</param>
    public static UpdateDecision Decide(UpdateSettings settings, string currentVersion, DateTimeOffset now,
        DateTimeOffset? lastCheck, string? skippedVersion, ReleaseInfo? release, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return !settings.ChecksEnabled && !force ? UpdateDecision.Disabled
            : GetDueDecision(now, lastCheck, force, currentVersion, skippedVersion, release);
    }

    private static bool IsDue(DateTimeOffset now, DateTimeOffset? lastCheck, bool force)
    {
        return force || lastCheck is not DateTimeOffset checkedAt || now - checkedAt >= TimeSpan.FromHours(24);
    }

    private static UpdateDecision GetDueDecision(DateTimeOffset now, DateTimeOffset? lastCheck, bool force,
        string currentVersion, string? skippedVersion, ReleaseInfo? release)
    {
        return IsDue(now, lastCheck, force)
            ? GetOffer(currentVersion, skippedVersion, release)
            : UpdateDecision.NotDue;
    }

    private static UpdateDecision GetOffer(string currentVersion, string? skippedVersion, ReleaseInfo? release)
    {
        return release is null || release.Version.CompareTo(SemVersion.Parse(currentVersion)) <= 0
            || string.Equals(release.Version.ToString(), skippedVersion, StringComparison.Ordinal)
            ? UpdateDecision.None
            : UpdateDecision.Offer;
    }
}
