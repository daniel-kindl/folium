using System.Globalization;
using System.Net;
using Folium.Books;
using Folium.Search;
using Folium.ViewModels;

namespace Folium.Tests;

public sealed class SearchViewModelTests
{
    private const int FULL_RAW_PAGE = 20;
    [Fact]
    public async Task SearchThenEditionPreviewKeepsEditionIdentity()
    {
        SearchViewModel model = new(new FakeSearch()) { Query = "Dune" };
        await model.SearchAsync();
        _ = Assert.Single(model.Works);
        Assert.False(model.IsBusy);
        await model.OpenWorkAsync(model.Works[0]);
        Assert.True(model.ShowEditions);
        await model.OpenEditionAsync(model.Editions[0]);
        Assert.True(model.ShowPreview);
        Assert.Equal("/books/OL1M", model.Preview!.Key);
        Assert.Equal("Ace", model.Preview.Publisher);
        Assert.True(model.GoBack());
        Assert.True(model.ShowEditions);
        Assert.True(model.GoBack());
        Assert.True(model.ShowWorks);
        Assert.False(model.GoBack());
    }

    [Fact]
    public async Task BlankTitleDoesNotCallProvider()
    {
        SearchViewModel model = new(new FakeSearch()) { Query = "  " };
        await model.SearchAsync();
        Assert.Equal("Enter a book title.", model.Message);
        Assert.Empty(model.Works);
    }

    [Fact]
    public async Task FailureCanBeRetried()
    {
        FakeSearch provider = new() { Fail = true };
        SearchViewModel model = new(provider) { Query = "Dune" };
        await model.SearchAsync();
        Assert.Equal("Connection failed.", model.Message);
        Assert.True(model.IsOffline);
        Assert.False(model.ShowRetry);
        provider.Fail = false;
        await model.SearchAsync();
        _ = Assert.Single(model.Works);
        Assert.False(model.IsOffline);
        Assert.False(model.ShowRetry);
    }

    [Fact]
    public async Task AProviderFailureShowsRetryUntilSearchWorks()
    {
        FakeSearch provider = new() { Fail = true, FailMessage = "Open Library did not answer." };
        SearchViewModel model = new(provider) { Query = "Dune" };
        await model.SearchAsync();
        Assert.False(model.IsOffline);
        Assert.Equal("Open Library did not answer.", model.Message);
        Assert.True(model.ShowRetry);
        provider.Fail = false;
        await model.SearchAsync();
        Assert.False(model.ShowRetry);
    }

    [Fact]
    public async Task CancellationDiscardsLateResults()
    {
        FakeSearch provider = new() { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        SearchViewModel model = new(provider) { Query = "Dune" };
        Task searching = model.SearchAsync();
        Assert.True(model.IsBusy);
        model.Cancel();
        provider.Pending.SetResult(new SearchPage<SearchWork>([new SearchWork("/works/OL1W", "Late", "", "")], HasAnotherPage: false));
        await searching.ConfigureAwait(true);
        Assert.Empty(model.Works);
        Assert.False(model.IsBusy);
        Assert.Equal("Search canceled.", model.Message);
    }

    [Fact]
    public async Task EmptyResultsHaveRecoveryMessage()
    {
        SearchViewModel model = new(new FakeSearch { Empty = true }) { Query = "missing" };
        await model.SearchAsync();
        Assert.Equal("No books found. Try another title.", model.Message);
    }

    [Fact]
    public async Task FailedNextPageKeepsTheDisplayedPage()
    {
        FakeSearch provider = new() { FullPage = true };
        SearchViewModel model = new(provider) { Query = "Dune" };
        await model.SearchAsync();
        provider.Fail = true;
        await model.ChangePageAsync(1);
        Assert.False(model.HasPrevious);
        provider.Fail = false;
        await model.ChangePageAsync(1);
        Assert.Equal(2, provider.LastPage);
        await model.OpenWorkAsync(model.Works[0]);
        Assert.True(model.GoBack());
        Assert.True(model.HasPrevious);
    }

    [Fact]
    public async Task RetryBeforeSearchDoesNothing()
    {
        FakeSearch provider = new();
        SearchViewModel model = new(provider);
        await model.RetryAsync().ConfigureAwait(true);
        Assert.Equal(0, provider.LastPage);
    }

    [Fact]
    public async Task SearchPreviewAndBackNeverWriteTheLibrary()
    {
        CountingStore store = new();
        LibraryViewModel library = LibraryViewModel.Open(store, new FakeSearch());
        library.ShowSearchCommand.Execute(parameter: null);
        library.Search.Query = "Dune";
        await library.Search.SearchAsync();
        await library.Search.OpenWorkAsync(library.Search.Works[0]);
        await library.Search.OpenEditionAsync(library.Search.Editions[0]);
        library.SaveCommand.Execute(parameter: null);
        Assert.True(library.GoBack());
        Assert.True(library.GoBack());
        Assert.True(library.GoBack());
        Assert.True(library.ShowLibrary);
        Assert.Equal(0, store.Writes);
        Assert.Empty(library.Books);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CoverPreviewKeepsMetadataOnCoverFailure(bool failure)
    {
        FakeSearch provider = new() { CoverFails = failure, WithCover = true };
        SearchViewModel model = new(provider) { Query = "Dune" };
        await model.SearchAsync();
        await model.OpenWorkAsync(model.Works[0]);
        await model.OpenEditionAsync(model.Editions[0]);
        Assert.NotNull(model.Preview);
        Assert.Equal("https://openlibrary.org/books/OL1M", model.Source!.AbsoluteUri);
        Assert.Equal(failure, model.Cover.IsEmpty);
        Assert.Contains(failure ? "cover could not load" : "Nothing has been saved", model.Message, StringComparison.Ordinal);
        await model.RetryAsync().ConfigureAwait(true);
        Assert.NotNull(model.Preview);
    }

    [Fact]
    public async Task ResultCoverLoadsOnlyOnRequest()
    {
        FakeSearch provider = new() { WithCover = true };
        SearchViewModel model = new(provider) { Query = "Dune" };
        await model.SearchAsync();
        Assert.Equal(0, provider.CoverCalls);
        await model.PreviewCoverAsync(12);
        Assert.Equal(1, provider.CoverCalls);
        Assert.False(model.Cover.IsEmpty);
        Assert.Equal("Cover from Open Library.", model.Message);
        Assert.True(model.ShowWorks);
    }

    [Fact]
    public async Task EmptyEditionPageCanReturnToWorkResults()
    {
        SearchViewModel model = new(new FakeSearch { EmptyEditions = true }) { Query = "Dune" };
        await model.SearchAsync();
        await model.OpenWorkAsync(model.Works[0]);
        Assert.Empty(model.Editions);
        Assert.Contains("No editions", model.Message, StringComparison.Ordinal);
        Assert.True(model.GoBack());
        Assert.True(model.ShowWorks);
    }

    [Fact]
    public async Task PreviousAndNextNavigateExactlyOnePage()
    {
        FakeSearch provider = new() { FullPage = true };
        SearchViewModel model = new(provider) { Query = "Dune" };
        Assert.False(model.HasNext);
        Assert.False(model.HasPrevious);
        await model.ChangePageAsync(-1);
        Assert.Equal(0, provider.LastPage);
        await model.SearchAsync();
        await model.ChangePageAsync(1);
        Assert.True(model.HasPrevious);
        await model.ChangePageAsync(-1);
        Assert.Equal(1, provider.LastPage);
        Assert.False(model.HasPrevious);
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => model.ChangePageAsync(0));
    }

    [Fact]
    public async Task CancelingATokenAwareProviderLeavesARecoverableScreen()
    {
        FakeSearch provider = new() { WaitForCancel = true };
        SearchViewModel model = new(provider) { Query = "Dune" };
        Task searching = model.SearchAsync();
        model.Cancel();
        await searching.ConfigureAwait(true);
        Assert.False(model.IsBusy);
        Assert.Equal("Search canceled.", model.Message);
        provider.WaitForCancel = false;
        await model.RetryAsync().ConfigureAwait(true);
        _ = Assert.Single(model.Works);
    }

    [Fact]
    public async Task MissingCoverLeavesAnExplanationAndMetadata()
    {
        SearchViewModel model = new(new FakeSearch { WithCover = true, CoverMissing = true }) { Query = "Dune" };
        await model.SearchAsync();
        await model.OpenWorkAsync(model.Works[0]);
        await model.OpenEditionAsync(model.Editions[0]);
        Assert.NotNull(model.Preview);
        Assert.True(model.Cover.IsEmpty);
        Assert.Contains("No cover is available", model.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FullRawPageWithOneInvalidRecordStillHasAnotherPage()
    {
        string works = string.Join(',', Enumerable.Range(1, FULL_RAW_PAGE - 1).Select(index =>
            string.Create(CultureInfo.InvariantCulture, $"{{\"key\":\"/works/OL{index}W\",\"title\":\"Dune\"}}")))
            + ",{\"title\":\"No identifier\"}";
        string editions = string.Join(',', Enumerable.Range(1, FULL_RAW_PAGE - 1).Select(index =>
            string.Create(CultureInfo.InvariantCulture, $"{{\"key\":\"/books/OL{index}M\",\"title\":\"Dune\"}}")))
            + ",{\"key\":\"/books/not-a-key\",\"title\":\"Bad\"}";
        using RoutingHandler handler = new(works, editions);
        using HttpClient http = new(handler);
        using OpenLibrary provider = new(http);
        SearchViewModel model = new(provider) { Query = "Dune" };
        await model.SearchAsync();
        Assert.Equal(19, model.Works.Count);
        Assert.True(model.HasNext);
        await model.OpenWorkAsync(model.Works[0]);
        Assert.Equal(19, model.Editions.Count);
        Assert.True(model.HasNext);
    }

    [Fact]
    public async Task NavigationNotifiesTheBoundHeading()
    {
        SearchViewModel model = new(new FakeSearch()) { Query = "Dune" };
        List<string> headings = [];
        model.PropertyChanged += (_, _) => headings.Add(model.Heading);
        Assert.Null(model.Source);
        await model.SearchAsync();
        await model.OpenWorkAsync(model.Works[0]);
        await model.OpenEditionAsync(model.Editions[0]);
        Assert.Contains("Find online", headings, StringComparer.Ordinal);
        Assert.Contains("Dune", headings, StringComparer.Ordinal);
        Assert.Contains("Edition preview", headings, StringComparer.Ordinal);
        Assert.False(model.HasNext);
        Assert.False(model.HasPrevious);
    }

    private sealed class RoutingHandler(string works, string editions) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            string json = path.Contains("/editions.json", StringComparison.Ordinal)
                ? "{\"entries\":[" + editions + "]}"
                : "{\"docs\":[" + works + "]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        }
    }

    private sealed class CountingStore : IBookStore
    {
        public int Writes { get; private set; }

        public LibrarySnapshot Read()
        {
            return new([], []);
        }

        public void Replace(LibrarySnapshot snapshot)
        {
            Writes++;
        }
    }

    private sealed class FakeSearch : IBookSearch
    {
        public bool CoverMissing { get; set; }
        public bool WithCover { get; set; }
        public bool CoverFails { get; set; }
        public bool EmptyEditions { get; set; }
        public bool WaitForCancel { get; set; }
        public int CoverCalls { get; private set; }
        public bool FullPage { get; set; }
        public int LastPage { get; private set; }
        public bool Fail { get; set; }

        public string? FailMessage { get; set; }
        public bool Empty { get; set; }
        public TaskCompletionSource<SearchPage<SearchWork>>? Pending { get; set; }

        public Task<SearchPage<SearchWork>> SearchAsync(string title, int page, CancellationToken cancellationToken)
        {
            LastPage = page;
            return WaitForCancel ? WaitAsync(cancellationToken) : ReadSearchPage();
        }

        private Task<SearchPage<SearchWork>> ReadSearchPage()
        {
            return FullPage && !Fail ? FullResults() : SearchResults();
        }

        private static async Task<SearchPage<SearchWork>> WaitAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return new SearchPage<SearchWork>([], HasAnotherPage: false);
        }

        private static Task<SearchPage<SearchWork>> FullResults()
        {
            SearchWork[] works = [.. Enumerable.Range(0, FULL_RAW_PAGE)
                .Select(_ => new SearchWork("/works/OL1W", "Dune", "Frank Herbert", "1965"))];
            return Task.FromResult(new SearchPage<SearchWork>(works, HasAnotherPage: true));
        }

        private Task<SearchPage<SearchWork>> SearchResults()
        {
            SearchWork[] works = Empty ? [] : [new("/works/OL1W", "Dune", "Frank Herbert", "1965")];
            return Fail
                ? throw new SearchException(FailMessage ?? "Connection failed.")
                : Pending?.Task ?? Task.FromResult(new SearchPage<SearchWork>(works, HasAnotherPage: false));
        }

        public Task<SearchPage<SearchEdition>> EditionsAsync(string workKey, int page, CancellationToken cancellationToken)
        {
            SearchEdition[] editions = EmptyEditions ? [] : [Edition()];
            return Task.FromResult(new SearchPage<SearchEdition>(editions, HasAnotherPage: false));
        }

        public Task<SearchEdition> PreviewAsync(string editionKey, CancellationToken cancellationToken)
        {
            return Task.FromResult(Edition());
        }

        public Task<byte[]?> CoverAsync(int coverId, CancellationToken cancellationToken)
        {
            CoverCalls++;
            return CoverMissing ? Task.FromResult<byte[]?>(null) : ReadCover();
        }

        private Task<byte[]?> ReadCover()
        {
            return CoverFails ? throw new SearchException("Cover unavailable.") : Task.FromResult<byte[]?>([1, 2, 3]);
        }

        private SearchEdition Edition()
        {
            return new("/books/OL1M", "Dune", "Frank Herbert", "Ace", "1990", "9780441172719", "Paperback", "eng", "A desert planet.", CoverId: WithCover ? 12 : null);
        }
    }
}
