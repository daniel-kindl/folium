using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Folium.Books;
using Folium.Search;
using Folium.ViewModels;
using Folium.Views;

namespace Folium.Tests;

[Collection("Avalonia UI")]
public sealed class SearchLayoutTests
{
    [Fact]
    public async Task ResultListEndsAboveThePageButtons()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            LibraryViewModel library = LibraryViewModel.Open(new EmptyStore(), new OneWork());
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 400, Height = 850 };
            window.Show();
            library.ShowSearchCommand.Execute(parameter: null);
            window.UpdateLayout();
            library.Search.Query = "Dune";
            library.Search.SearchAsync().GetAwaiter().GetResult();
            window.UpdateLayout();
            SearchView search = view.GetVisualDescendants().OfType<SearchView>().Single();

            ScrollViewer results = search.FindControl<ScrollViewer>("SearchResults")!;
            Button previous = search.GetVisualDescendants().OfType<Button>().Single(button =>
                string.Equals(button.Content as string, "Previous page", StringComparison.Ordinal));
            double resultsBottom = Bottom(search, results);
            double previousTop = Top(search, previous);
            string detail = string.Create(
                CultureInfo.InvariantCulture,
                $"Results {Top(search, results)}-{resultsBottom} h {results.Bounds.Height}; page {previousTop}-{Bottom(search, previous)}; search h {search.Bounds.Height}; parent {results.Parent?.GetType().Name}.");
            Assert.True(resultsBottom <= previousTop + 1, detail);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    private static double Top(Control host, Control control)
    {
        return control.Bounds.TransformToAABB(control.TransformToVisual(host)!.Value).Top;
    }

    private static double Bottom(Control host, Control control)
    {
        return control.Bounds.TransformToAABB(control.TransformToVisual(host)!.Value).Bottom;
    }

    private sealed class EmptyStore : IBookStore
    {
        public IReadOnlyList<Book> Read()
        {
            return [];
        }

        public void Replace(IReadOnlyList<Book> books)
        {
        }
    }

    private sealed class OneWork : IBookSearch
    {
        public Task<SearchPage<SearchWork>> SearchAsync(string title, int page, CancellationToken cancellationToken)
        {
            SearchWork[] works =
            [
                new("/works/OL1W", "Dune", "Frank Herbert", "1965", 12),
                new("/works/OL2W", "Dune Messiah", "Frank Herbert", "1969", 12),
                new("/works/OL3W", "Children of Dune", "Frank Herbert", "1976", 12),
            ];
            return Task.FromResult(new SearchPage<SearchWork>(works, HasAnotherPage: false));
        }

        public Task<SearchPage<SearchEdition>> EditionsAsync(string workKey, int page, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SearchPage<SearchEdition>([], HasAnotherPage: false));
        }

        public Task<SearchEdition> PreviewAsync(string editionKey, CancellationToken cancellationToken)
        {
            throw new SearchException("The layout test does not open an edition.");
        }

        public Task<byte[]?> CoverAsync(int coverId, CancellationToken cancellationToken)
        {
            return Task.FromResult<byte[]?>(null);
        }
    }
}
