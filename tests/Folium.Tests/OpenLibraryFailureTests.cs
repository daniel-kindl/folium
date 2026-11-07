using System.Net;
using Folium.Search;

namespace Folium.Tests;

public sealed class OpenLibraryFailureTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData(/*lang=json,strict*/ "{\"docs\":[null]}")]
    [InlineData(/*lang=json,strict*/ "{\"docs\":false}")]
    public async Task WrongSearchShapeIsARecoverableFailure(string json)
    {
        using ResponseHandler handler = new(() => new(HttpStatusCode.OK) { Content = new StringContent(json) });
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        _ = await Assert.ThrowsAsync<SearchException>(() => provider.SearchAsync("Dune", 1, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "too many requests")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "unavailable")]
    [InlineData(HttpStatusCode.Forbidden, "Could not reach")]
    public async Task HttpFailuresRemainFailures(HttpStatusCode status, string message)
    {
        using ResponseHandler handler = new(() => new(status));
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        SearchException exception = await Assert.ThrowsAsync<SearchException>(() => provider.SearchAsync("Dune", 1, TestContext.Current.CancellationToken));
        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationIsNotANetworkFailure()
    {
        using ResponseHandler handler = new(() => throw new OperationCanceledException());
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        using CancellationTokenSource cancel = new();
        await cancel.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.SearchAsync("Dune", 1, cancel.Token));
    }

    [Fact]
    public async Task MissingCoverReturnsNoImage()
    {
        using ResponseHandler handler = new(() => new(HttpStatusCode.NotFound));
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        Assert.Null(await provider.CoverAsync(12, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SuccessfulResponsesAreCached()
    {
        using ResponseHandler handler = new(() => new(HttpStatusCode.OK) { Content = new StringContent(/*lang=json,strict*/ "{\"docs\":[]}") });
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        _ = await provider.SearchAsync("Dune", 1, TestContext.Current.CancellationToken);
        _ = await provider.SearchAsync("Dune", 1, TestContext.Current.CancellationToken);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("/works/../../evil")]
    [InlineData("https://example.org")]
    public async Task InvalidIdentifiersCannotChangeRequestHost(string key)
    {
        using ResponseHandler handler = new(() => new(HttpStatusCode.OK));
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        _ = await Assert.ThrowsAsync<ArgumentException>(() => provider.EditionsAsync(key, 1, TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task MalformedResponseCanBeRetried()
    {
        int calls = 0;
        using ResponseHandler handler = new(() => new(HttpStatusCode.OK)
        {
            Content = new StringContent(++calls == 1 ? "{" : /*lang=json,strict*/ "{\"docs\":[]}"),
        });
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        _ = await Assert.ThrowsAsync<SearchException>(() => provider.SearchAsync("Dune", 1, TestContext.Current.CancellationToken));
        Assert.Empty(await provider.SearchAsync("Dune", 1, TestContext.Current.CancellationToken));
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    public async Task InvalidEditionListsFailClearly(string json)
    {
        using ResponseHandler handler = new(() => new(HttpStatusCode.OK) { Content = new StringContent(json) });
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        _ = await Assert.ThrowsAsync<SearchException>(() => provider.EditionsAsync("/works/OL1W", 1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MinimalEditionCanBePreviewedWithoutAuthorsOrCover()
    {
        using ResponseHandler handler = new(() => new(HttpStatusCode.OK)
        {
            Content = new StringContent(/*lang=json,strict*/ "{\"key\":\"/books/OL1M\",\"title\":\"Dune\",\"description\":\"Text\",\"authors\":[{\"key\":\"/bad\"}],\"covers\":[-1,\"bad\"]}"),
        });
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        SearchEdition edition = await provider.PreviewAsync("/books/OL1M", TestContext.Current.CancellationToken);
        Assert.Equal("Text", edition.Description);
        Assert.Empty(edition.Authors);
        Assert.False(edition.HasCover);
    }

    [Fact]
    public async Task SuccessfulCoverBytesAreReturned()
    {
        using ResponseHandler handler = new(() => new(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        Assert.Equal([1, 2, 3], await provider.CoverAsync(12, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TimeoutAndOfflineErrorsAreRecoverable()
    {
        using ResponseHandler offline = new(() => throw new HttpRequestException("Offline"));
        using HttpClient offlineHttp = new(offline);
        using OpenLibrary offlineProvider = new(offlineHttp);
        SearchException failure = await Assert.ThrowsAsync<SearchException>(() => offlineProvider.SearchAsync("Dune", 1, TestContext.Current.CancellationToken));
        Assert.Contains("Check your connection", failure.Message, StringComparison.Ordinal);
        using ResponseHandler timeout = new(() => throw new TaskCanceledException());
        using HttpClient timeoutHttp = new(timeout);
        using OpenLibrary timeoutProvider = new(timeoutHttp);
        SearchException slow = await Assert.ThrowsAsync<SearchException>(() => timeoutProvider.SearchAsync("Dune", 1, TestContext.Current.CancellationToken));
        Assert.Contains("too long", slow.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailureDoesNotBypassRequestPacing()
    {
        List<long> starts = [];
        using ResponseHandler handler = new(() =>
        {
            starts.Add(System.Diagnostics.Stopwatch.GetTimestamp());
            throw new HttpRequestException("Offline");
        });
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        _ = await Assert.ThrowsAsync<SearchException>(() => provider.SearchAsync("Dune", 1, TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<SearchException>(() => provider.SearchAsync("Dune", 1, TestContext.Current.CancellationToken));
        Assert.True(System.Diagnostics.Stopwatch.GetElapsedTime(starts[0], starts[1]) >= TimeSpan.FromMilliseconds(950));
    }

    [Fact]
    public async Task RetryAfterCanBeCanceledBeforeAnotherRequest()
    {
        using ResponseHandler handler = new(() =>
        {
            HttpResponseMessage response = new(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(1));
            return response;
        });
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        _ = await Assert.ThrowsAsync<SearchException>(() => provider.SearchAsync("Dune", 1, TestContext.Current.CancellationToken));
        using CancellationTokenSource cancel = new();
        Task<IReadOnlyList<SearchWork>> retry = provider.SearchAsync("Dune", 1, cancel.Token);
        await cancel.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => retry);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task InvalidAuthorReferencesDoNotPoisonThePreviewCache()
    {
        int calls = 0;
        using ResponseHandler handler = new(() => new(HttpStatusCode.OK)
        {
            Content = new StringContent(++calls == 1
                ? /*lang=json,strict*/ "{\"key\":\"/books/OL1M\",\"authors\":[null]}"
                : /*lang=json,strict*/ "{\"key\":\"/books/OL1M\",\"title\":\"Dune\"}"),
        });
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        _ = await Assert.ThrowsAsync<SearchException>(() => provider.PreviewAsync("/books/OL1M", TestContext.Current.CancellationToken));
        SearchEdition preview = await provider.PreviewAsync("/books/OL1M", TestContext.Current.CancellationToken);
        Assert.Equal("Dune", preview.Title);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task InvalidSearchInputsNeverSendRequests()
    {
        using ResponseHandler handler = new(() => new(HttpStatusCode.OK));
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        _ = await Assert.ThrowsAsync<ArgumentException>(() => provider.SearchAsync(" ", 1, TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => provider.SearchAsync("Dune", 0, TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => provider.EditionsAsync("/works/OL1W", 0, TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => provider.PreviewAsync("/books/../", TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.Calls);
    }

    private sealed class ResponseHandler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(response());
        }
    }
}
