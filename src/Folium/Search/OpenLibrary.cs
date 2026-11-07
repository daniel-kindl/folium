using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Folium.Search;

/// <summary>A paced Open Library client with a bounded session cache. The caller owns the HTTP client.</summary>
/// <param name="http">The HTTP client used for provider requests.</param>
public sealed class OpenLibrary(HttpClient http) : IBookSearch, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, byte[]> _cache = new(StringComparer.Ordinal);
    private DateTimeOffset _nextRequest;

    /// <summary>Releases the request gate. Call only after pending requests finish.</summary>
    public void Dispose()
    {
        _gate.Dispose();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchWork>> SearchAsync(string title, int page, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        string url = "https://openlibrary.org/search.json?title=" + Uri.EscapeDataString(title.Trim())
            + "&fields=key,title,author_name,first_publish_year,cover_i&limit=20&page=" + page.ToString(CultureInfo.InvariantCulture);
        using JsonDocument document = await ReadJsonAsync(url, cancellationToken).ConfigureAwait(false);
        return OpenLibraryJson.Works(document.RootElement);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchEdition>> EditionsAsync(string workKey, int page, CancellationToken cancellationToken)
    {
        RequireKey(workKey, "works", 'W');
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        string url = "https://openlibrary.org" + workKey + "/editions.json?limit=20&offset=" + ((page - 1) * 20).ToString(CultureInfo.InvariantCulture);
        using JsonDocument document = await ReadJsonAsync(url, cancellationToken).ConfigureAwait(false);
        try
        {
            OpenLibraryJson.RequireArray(document.RootElement, "entries");
            return [.. OpenLibraryJson.Array(document.RootElement, "entries")
                .Where(item => OpenLibraryJson.ValidKey(OpenLibraryJson.Text(item, "key"), "books", 'M'))
                .Select(OpenLibraryJson.Edition)];
        }
        catch (JsonException exception)
        {
            throw InvalidResponse(exception);
        }
    }

    /// <inheritdoc />
    public async Task<SearchEdition> PreviewAsync(string editionKey, CancellationToken cancellationToken)
    {
        RequireKey(editionKey, "books", 'M');
        using JsonDocument document = await ReadJsonAsync("https://openlibrary.org" + editionKey + ".json", cancellationToken).ConfigureAwait(false);
        SearchEdition edition = OpenLibraryJson.Edition(document.RootElement);
        List<string> authors = [];
        foreach (JsonElement author in OpenLibraryJson.Array(document.RootElement, "authors"))
        {
            string key = OpenLibraryJson.Text(author, "key");
            if (!OpenLibraryJson.ValidKey(key, "authors", 'A'))
            {
                continue;
            }

            using JsonDocument person = await ReadJsonAsync("https://openlibrary.org" + key + ".json", cancellationToken).ConfigureAwait(false);
            authors.Add(OpenLibraryJson.Text(person.RootElement, "name"));
        }

        return edition with { Authors = string.Join(", ", authors) };
    }

    /// <inheritdoc />
    public Task<byte[]?> CoverAsync(int coverId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(coverId);
        return ReadAsync("https://covers.openlibrary.org/b/id/" + coverId.ToString(CultureInfo.InvariantCulture) + "-M.jpg?default=false", cancellationToken);
    }

    private async Task<JsonDocument> ReadJsonAsync(string url, CancellationToken cancellationToken)
    {
        byte[]? bytes = await ReadAsync(url, cancellationToken).ConfigureAwait(false);
        try
        {
            return JsonDocument.Parse(bytes ?? throw new JsonException("The provider record does not exist."));
        }
        catch (JsonException exception)
        {
            throw InvalidResponse(exception);
        }
    }

    private async Task<byte[]?> ReadAsync(string url, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(url, out byte[]? cached))
            {
                return cached;
            }

            await WaitForProviderAsync(cancellationToken).ConfigureAwait(false);
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("Folium/1.0 (+https://github.com/daniel-kindl/folium)");
            _nextRequest = DateTimeOffset.UtcNow.AddSeconds(1);
            using HttpResponseMessage response = await http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            ApplyStatus(response);
            if (response.StatusCode == HttpStatusCode.NotFound && url.StartsWith("https://covers.", StringComparison.Ordinal))
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            byte[] bytes = await response.Content.ReadAsByteArrayAsync(timeout.Token).ConfigureAwait(false);
            CacheResponse(url, bytes);
            return bytes;
        }
        catch (HttpRequestException exception)
        {
            throw new SearchException("Could not reach Open Library. Check your connection and try again.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SearchException("Open Library took too long to respond. Try again.", exception);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private void CacheResponse(string url, byte[] bytes)
    {
        if (url.StartsWith("https://openlibrary.org/", StringComparison.Ordinal))
        {
            ValidateMetadata(url, bytes);
        }

        if (_cache.Count >= 64)
        {
            _cache.Clear();
        }

        _cache[url] = bytes;
    }

    private static void ValidateMetadata(string url, byte[] bytes)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
            JsonElement root = document.RootElement;
            if (url.Contains("/search.json?", StringComparison.Ordinal))
            {
                _ = OpenLibraryJson.Works(root);
            }
            else if (url.Contains("/editions.json?", StringComparison.Ordinal))
            {
                OpenLibraryJson.RequireArray(root, "entries");
                foreach (JsonElement entry in OpenLibraryJson.Array(root, "entries"))
                {
                    _ = OpenLibraryJson.Edition(entry);
                }
            }
            else if (url.Contains("/books/", StringComparison.Ordinal))
            {
                _ = OpenLibraryJson.Edition(root);
                foreach (JsonElement author in OpenLibraryJson.Array(root, "authors"))
                {
                    _ = OpenLibraryJson.Text(author, "key");
                }
            }
            else
            {
                _ = OpenLibraryJson.Text(root, "name");
            }
        }
        catch (JsonException exception)
        {
            throw InvalidResponse(exception);
        }
    }

    private async Task WaitForProviderAsync(CancellationToken cancellationToken)
    {
        TimeSpan delay = _nextRequest - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private void ApplyStatus(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter is { } retry)
        {
            DateTimeOffset until = retry.Date ?? DateTimeOffset.UtcNow.Add(retry.Delta ?? TimeSpan.FromSeconds(1));
            if (until > _nextRequest)
            {
                _nextRequest = until;
            }
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new SearchException("Open Library is receiving too many requests. Wait before trying again.");
        }

        if ((int)response.StatusCode >= 500)
        {
            throw new SearchException("Open Library is unavailable. Try again later.");
        }
    }

    private static void RequireKey(string key, string kind, char suffix)
    {
        if (!OpenLibraryJson.ValidKey(key, kind, suffix))
        {
            throw new ArgumentException("The provider identifier is invalid.", nameof(key));
        }
    }

    private static SearchException InvalidResponse(JsonException exception)
    {
        return new SearchException("Open Library sent an unreadable response. Try again.", exception);
    }
}
