using Folium.Updates;

namespace Folium.Android;

internal sealed class VerificationReleaseSource(HttpClient client) : IReleaseSource
{
    private readonly GitHubReleaseSource _source = new(client);

    public async Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken)
    {
        ReleaseInfo? release = await _source.GetLatestAsync(cancellationToken).ConfigureAwait(false);
        _ = global::Android.Util.Log.Info("FoliumUpdateFixture", $"Parsed release: {release?.Version.ToString() ?? "none"}.");
        return release;
    }
}
