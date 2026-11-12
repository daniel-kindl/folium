namespace Folium.Updates;

/// <summary>Update-check preferences and remembered state.</summary>
/// <param name="ChecksEnabled">Whether automatic checks are enabled.</param>
/// <param name="LastCheck">The time of the last check.</param>
/// <param name="SkippedVersion">The version the user chose to skip.</param>
public sealed record UpdateSettings(bool ChecksEnabled = true, DateTimeOffset? LastCheck = null, string? SkippedVersion = null);
