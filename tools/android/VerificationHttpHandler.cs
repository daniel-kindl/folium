using System.Net;

namespace Folium.Android;

/// <summary>Supplies deterministic HTTP fixtures only in verification builds.</summary>
internal sealed class VerificationHttpHandler() : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string modePath = Path.Combine(global::Android.App.Application.Context.FilesDir!.AbsolutePath, "verification-mode");
        string mode = File.Exists(modePath) ? await File.ReadAllTextAsync(modePath, cancellationToken).ConfigureAwait(false) : "";
        if (string.Equals(mode.Trim(), "offline", StringComparison.Ordinal))
        {
            throw new HttpRequestException("The verification provider is offline.");
        }

        if (string.Equals(mode.Trim(), "slow", StringComparison.Ordinal))
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
        }

        string path = request.RequestUri!.AbsolutePath;
        string asset = path switch
        {
            "/search.json" => request.RequestUri.Query.Contains("page=2", StringComparison.Ordinal) ? "search-page2.json" : "search.json",
            "/works/OL45804W/editions.json" or "/works/OL1W/editions.json" => request.RequestUri.Query.Contains("offset=20", StringComparison.Ordinal) ? "editions-page2.json" : "editions.json",
            "/books/OL1M.json" => "edition.json",
            "/books/OL2M.json" => "empty-edition.json",
            "/books/OL3M.json" => "invalid-edition.json",
            "/authors/OL1A.json" => "author.json",
            "/b/id/123-M.jpg" => "cover.png",
            "/b/id/124-M.jpg" => "folium-verification.txt",
            _ => throw new HttpRequestException("The verification fixture does not exist: " + path),
        };
        Stream stream = global::Android.App.Application.Context.Assets!.Open(asset);
        await using (stream.ConfigureAwait(false))
        {
            MemoryStream bytes = new();
            await using (bytes.ConfigureAwait(false))
            {
                await stream.CopyToAsync(bytes, cancellationToken).ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes.ToArray()) };
            }
        }
    }
}
