using System.Net;
using Folium.Search;

namespace Folium.Tests;

public sealed class OpenLibraryTests
{
    [Fact]
    public async Task SearchMapsWorksWithoutInventingEditionMetadata()
    {
        using FixtureHandler handler = new("search.json");
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        IReadOnlyList<SearchWork> works = (await provider.SearchAsync("Dune & sand", 1, TestContext.Current.CancellationToken)).Items;
        Assert.Equal(2, works.Count);
        Assert.Equal("Dune", works[0].Title);
        Assert.Equal("Frank Herbert", works[0].Authors);
        Assert.Equal("1965", works[0].FirstPublished);
        Assert.True(works[0].HasCover);
        Assert.False(works[1].HasCover);
        Assert.Equal("", works[1].Authors);
        Assert.Contains("title=Dune%20%26%20sand", handler.Request!.Query, StringComparison.Ordinal);
        Assert.Contains("Folium", handler.UserAgent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditionsKeepPublicationAndIsbnTogether()
    {
        using FixtureHandler handler = new("editions.json");
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        IReadOnlyList<SearchEdition> editions = (await provider.EditionsAsync("/works/OL45804W", 1, TestContext.Current.CancellationToken)).Items;
        Assert.Equal("Ace", editions[0].Publisher);
        Assert.Equal("1990", editions[0].PublicationDate);
        Assert.Equal("9780441172719", editions[0].Isbn);
        Assert.Equal("Paperback", editions[0].Edition);
        Assert.Equal("eng", editions[0].Language);
        Assert.Equal("", editions[1].Isbn);
    }

    [Fact]
    public async Task PreviewReadsDescriptionAndActualEditionAuthors()
    {
        using FixtureHandler handler = new("edition.json");
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        SearchEdition edition = await provider.PreviewAsync("/books/OL1M", TestContext.Current.CancellationToken);
        Assert.Equal("A desert planet.", edition.Description);
        Assert.Equal("Frank Herbert", edition.Authors);
        Assert.Equal(123, edition.CoverId);
        Assert.Contains("0441172717", edition.Isbn, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(/*lang=json,strict*/ "{\"docs\":[]}", 0)]
    [InlineData(/*lang=json,strict*/ "{\"docs\":[{\"title\":\"No identifier\"}]}", 0)]
    public async Task EmptyOrUnusableResultsAreEmpty(string json, int expected)
    {
        using FixtureHandler handler = new(json, literal: true);
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        Assert.Equal(expected, (await provider.SearchAsync("Title", 1, TestContext.Current.CancellationToken)).Items.Count);
    }

    [Fact]
    public async Task InvalidResponseIsNotAnEmptySuccess()
    {
        using FixtureHandler handler = new("{", literal: true);
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        _ = await Assert.ThrowsAsync<SearchException>(() => provider.SearchAsync("Title", 1, TestContext.Current.CancellationToken));
    }

    private sealed class FixtureHandler(string fixture, bool literal = false) : HttpMessageHandler
    {
        public Uri? Request { get; private set; }
        public string UserAgent { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request.RequestUri;
            UserAgent = request.Headers.UserAgent.ToString();
            string content = literal ? fixture : await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture), cancellationToken).ConfigureAwait(false);
            string json = string.Equals(request.RequestUri!.AbsolutePath, "/authors/OL1A.json", StringComparison.Ordinal)
                ? /*lang=json,strict*/ "{\"name\":\"Frank Herbert\"}" : content;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        }
    }
}
