using Folium.Search;
using Folium.ViewModels;

namespace Folium.Tests;

public sealed class SearchPagerVisibilityTests
{
    [Fact]
    public async Task PagerStaysHiddenWhenResultsFitOnOnePage()
    {
        SearchViewModel model = new(new StubSearch(hasAnotherPage: false)) { Query = "Dune" };

        Assert.False(model.ShowPager);

        await model.SearchAsync();

        Assert.False(model.ShowPager);
        await model.OpenWorkAsync(model.Works[0]);
        Assert.False(model.ShowPager);
        await model.OpenEditionAsync(model.Editions[0]);
        Assert.False(model.ShowPager);
    }

    [Fact]
    public async Task PagerShowsWhenAnotherPageExistsAndStaysVisibleWhileLoading()
    {
        StubSearch provider = new(hasAnotherPage: true);
        SearchViewModel model = new(provider) { Query = "Dune" };

        await model.SearchAsync();

        Assert.True(model.ShowPager);
        Assert.True(model.HasNext);

        provider.Gate = new TaskCompletionSource();
        Task next = model.ChangePageAsync(1);

        Assert.True(model.IsBusy);
        Assert.False(model.HasNext);
        Assert.True(model.ShowPager);

        provider.Gate.SetResult();
        await next.ConfigureAwait(true);

        Assert.True(model.ShowPager);
        Assert.True(model.HasPrevious);
    }

    private sealed class StubSearch(bool hasAnotherPage) : IBookSearch
    {
        public TaskCompletionSource? Gate { get; set; }

        public async Task<SearchPage<SearchWork>> SearchAsync(string title, int page, CancellationToken cancellationToken)
        {
            if (Gate is not null)
            {
                await Gate.Task.ConfigureAwait(false);
            }

            return new SearchPage<SearchWork>([new SearchWork("/works/OL1W", "Dune", "Frank Herbert", "1965")], hasAnotherPage && page == 1);
        }

        public Task<SearchPage<SearchEdition>> EditionsAsync(string workKey, int page, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SearchPage<SearchEdition>([new SearchEdition("/books/OL1M", "Dune", "Frank Herbert", "Ace", "1990", "9780441172719", "Reissue", "eng", "", CoverId: null)], HasAnotherPage: false));
        }

        public Task<SearchEdition> PreviewAsync(string editionKey, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SearchEdition(editionKey, "Dune", "Frank Herbert", "Ace", "1990", "9780441172719", "Reissue", "eng", "", CoverId: null));
        }

        public Task<byte[]?> CoverAsync(int coverId, CancellationToken cancellationToken)
        {
            return Task.FromResult<byte[]?>(null);
        }
    }
}
