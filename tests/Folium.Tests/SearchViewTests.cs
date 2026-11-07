using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Folium.Books;
using Folium.Search;
using Folium.ViewModels;
using Folium.Views;

namespace Folium.Tests;

public sealed class SearchViewTests
{
    [Fact]
    public async Task UserCanSearchChooseAnEditionPreviewCoverAndReturnWithoutSaving()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            FakeProvider provider = new();
            LibraryViewModel library = LibraryViewModel.Open(new EmptyStore(), provider);
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 400, Height = 850 };
            window.Show();
            Click(view, "Find a book online");
            Assert.True(library.ShowSearch);
            SearchView search = view.GetVisualDescendants().OfType<SearchView>().Single();
            TextBox title = search.GetVisualDescendants().OfType<TextBox>().Single();
            Assert.True(title.Focus());
            window.KeyTextInput("Dune");
            Click(search, "Search Open Library");
            window.UpdateLayout();
            _ = Assert.Single(library.Search.Works);
            Button work = search.GetVisualDescendants().OfType<Button>().First(button => button.Tag is SearchWork);
            Button resultCover = work.GetVisualDescendants().OfType<Button>().First(button => Equals(button.Content, "Preview cover"));
            Activate(resultCover);
            Assert.True(library.Search.ShowWorks);
            Assert.Equal(1, provider.CoverCalls);
            Assert.NotNull(work.GetVisualDescendants().OfType<Image>().Single().Source);
            Activate(work);
            window.UpdateLayout();
            Assert.True(library.Search.ShowEditions);
            Button edition = search.GetVisualDescendants().OfType<Button>().First(button => button.Tag is SearchEdition);
            Activate(edition);
            window.UpdateLayout();
            Assert.True(library.Search.ShowPreview);
            Assert.Contains(search.GetVisualDescendants().OfType<TextBlock>(), text => string.Equals(text.Text, "Publisher: Ace", StringComparison.Ordinal));
            Assert.NotNull(search.FindControl<Image>("CoverImage")!.Source);
            ReturnToLibrary(search, library);
            window.Content = null;
            window.Content = view;
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    private static void ReturnToLibrary(SearchView search, LibraryViewModel library)
    {
        Click(search, "Try again");
        Click(search, "Back");
        Assert.True(library.Search.ShowEditions);
        Click(search, "Back");
        Assert.True(library.Search.ShowWorks);
        Click(search, "Back");
        Assert.True(library.ShowLibrary);
        Assert.Empty(library.Books);
    }

    private static void Click(Control view, string content)
    {
        Button button = view.GetVisualDescendants().OfType<Button>().First(button => Equals(button.Content, content)
            && button.IsEffectivelyVisible);
        Activate(button);
    }

    private static void Activate(Button button)
    {
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

    private sealed class FakeProvider : IBookSearch
    {
        public int CoverCalls { get; private set; }

        public Task<IReadOnlyList<SearchWork>> SearchAsync(string title, int page, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<SearchWork>>([new("/works/OL1W", "Dune", "Frank Herbert", "1965", 12)]);
        }

        public Task<IReadOnlyList<SearchEdition>> EditionsAsync(string workKey, int page, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<SearchEdition>>([Edition()]);
        }

        public Task<SearchEdition> PreviewAsync(string editionKey, CancellationToken cancellationToken)
        {
            return Task.FromResult(Edition());
        }

        public Task<byte[]?> CoverAsync(int coverId, CancellationToken cancellationToken)
        {
            CoverCalls++;
            return Task.FromResult<byte[]?>(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j1WQAAAAASUVORK5CYII="));
        }

        private static SearchEdition Edition()
        {
            return new("/books/OL1M", "Dune", "Frank Herbert", "Ace", "1990", "123", "Paperback", "eng", "Desert planet.", 12);
        }
    }
}
