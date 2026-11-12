namespace Folium.Updates;

/// <summary>Provides the newest stable release.</summary>
public interface IReleaseSource
{
    /// <summary>Returns the newest stable release, or null when none is available.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken);
}
