using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Folium.Books;
using Folium.Search;
using Folium.ViewModels;
using Folium.Views;

namespace Folium.Tests;

[Collection("Avalonia UI")]
public sealed class SearchRetryViewTests
{
    [Fact]
    public async Task TryAgainRunsTheSearchAgain()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            FlakyProvider provider = new();
            LibraryViewModel library = LibraryViewModel.Open(new EmptyStore(), provider);
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 400, Height = 850 };
            window.Show();
            window.UpdateLayout();
            library.ShowSearchCommand.Execute(parameter: null);
            window.UpdateLayout();
            SearchView search = view.GetVisualDescendants().OfType<SearchView>().Single();
            library.Search.Query = "Dune";
            Click(search, "Search Open Library");
            window.UpdateLayout();
            Assert.True(library.Search.ShowRetry);

            Click(search, "Try again");
            window.UpdateLayout();

            Assert.False(library.Search.ShowRetry);
            _ = Assert.Single(library.Search.Works);
            Assert.Equal(2, provider.SearchCalls);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    private static void Click(Control view, string content)
    {
        Button button = view.GetVisualDescendants().OfType<Button>().First(button => Equals(button.Content, content)
            && button.IsEffectivelyVisible);
        Assert.True(button.Focus());
        TopLevel top = TopLevel.GetTopLevel(button)!;
        top.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        top.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
    }

    private sealed class EmptyStore : IBookStore
    {
        public IReadOnlyList<Book> Read()
        {
            return [];
        }

        public void Replace(IReadOnlyList<Book> books)
        {
            throw new InvalidOperationException("Search must not write the library.");
        }
    }

    private sealed class FlakyProvider : IBookSearch
    {
        public int SearchCalls { get; private set; }

        public Task<SearchPage<SearchWork>> SearchAsync(string title, int page, CancellationToken cancellationToken)
        {
            SearchCalls++;
            return SearchCalls == 1
                ? throw new SearchException("Open Library did not answer.")
                : Task.FromResult(new SearchPage<SearchWork>([new("/works/OL1W", "Dune", "Frank Herbert", "1965", 12)], HasAnotherPage: false));
        }

        public Task<SearchPage<SearchEdition>> EditionsAsync(string workKey, int page, CancellationToken cancellationToken)
        {
            throw new SearchException("Not used.");
        }

        public Task<SearchEdition> PreviewAsync(string editionKey, CancellationToken cancellationToken)
        {
            throw new SearchException("Not used.");
        }

        public Task<byte[]?> CoverAsync(int coverId, CancellationToken cancellationToken)
        {
            throw new SearchException("Not used.");
        }
    }
}
