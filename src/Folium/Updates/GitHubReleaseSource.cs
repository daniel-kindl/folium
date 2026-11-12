using System.Net.Http.Headers;
using System.Text.Json;

namespace Folium.Updates;

/// <summary>Reads the latest stable Folium release from GitHub.</summary>
/// <param name="client">The HTTP client used for requests.</param>
public sealed class GitHubReleaseSource(HttpClient client) : IReleaseSource
{
    private static readonly Uri Endpoint = new("https://api.github.com/repos/daniel-kindl/folium/releases/latest");
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private readonly HttpClient _client = client ?? throw new ArgumentNullException(nameof(client));

    /// <inheritdoc />
    public async Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, Endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("Folium-Android");
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            using HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (!IsUsableResponse(response))
            {
                return null;
            }

            Stream stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);
                return Parse(document.RootElement);
            }
        }
        catch (Exception exception) when (IsQuietFailure(exception))
        {
            return null;
        }
    }

    private static bool IsUsableResponse(HttpResponseMessage response)
    {
        bool secure = string.Equals(response.RequestMessage?.RequestUri?.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);
        return secure && response.IsSuccessStatusCode;
    }

    private static bool IsQuietFailure(Exception exception)
    {
        return exception is HttpRequestException or OperationCanceledException or JsonException;
    }

    internal static ReleaseInfo? Parse(JsonElement root)
    {
        if (!IsStableRelease(root) || !TryVersion(root, out SemVersion version)
            || !TryHttps(root, "html_url", out Uri? pageUrl)
            || !TryApk(root, version, out Uri? apkUrl, out long size))
        {
            return null;
        }

        string notes = TryString(root, "body", out string body) ? body : string.Empty;
        return new ReleaseInfo(version, apkUrl!, size, notes, pageUrl!);
    }

    private static bool IsStableRelease(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object
            && IsFalseOrMissing(root, "draft")
            && IsFalseOrMissing(root, "prerelease");
    }

    private static bool IsFalseOrMissing(JsonElement root, string name)
    {
        return !root.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.False;
    }

    private static bool TryVersion(JsonElement root, out SemVersion version)
    {
        version = default;
        if (!TryString(root, "tag_name", out string tag) || tag.Length < 2 || tag[0] != 'v')
        {
            return false;
        }
        try
        {
            version = SemVersion.Parse(tag[1..]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryHttps(JsonElement root, string name, out Uri? uri)
    {
        uri = null;
        return TryString(root, name, out string value)
            && Uri.TryCreate(value, UriKind.Absolute, out uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);
    }

    private static bool TryApk(JsonElement root, SemVersion version, out Uri? url, out long size)
    {
        url = null;
        size = 0;
        if (!root.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        string name = $"folium-{version}.apk";
        JsonElement[] matches = [.. assets.EnumerateArray().Where(asset => TryString(asset, "name", out string assetName)
            && string.Equals(assetName, name, StringComparison.Ordinal))];
        if (matches.Length != 1 || !TryHttps(matches[0], "browser_download_url", out url))
        {
            return false;
        }

        if (matches[0].TryGetProperty("size", out JsonElement sizeJson))
        {
            _ = sizeJson.TryGetInt64(out size);
        }
        return true;
    }

    private static bool TryString(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement property)
            && property.ValueKind == JsonValueKind.String && (value = property.GetString() ?? string.Empty).Length > 0;
    }
}
