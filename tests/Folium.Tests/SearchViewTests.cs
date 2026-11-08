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
    public async Task UnavailableDecodedCoverShowsFallbackAndKeepsBookDetails()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            ImportStore store = new();
            BookMetadata metadata = new("/works/OL1W", "/books/OL2M", "", "", "", "", "", "", 42, new byte[] { 1, 2, 3 });
            store.Replace([new Book(Guid.NewGuid(), "Dune", "Frank Herbert", Metadata: metadata)]);
            LibraryViewModel library = LibraryViewModel.Open(store);
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 400, Height = 850 };
            window.Show();
            library.OpenCommand.Execute(Assert.Single(library.Books).Id);
            window.UpdateLayout();
            // The headless decoder accepts all bytes. Reproduce the failed decoder result.
            view.FindControl<LocalCover>("SavedCover")!.Source = null;
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => string.Equals(text.Text, "No saved cover", StringComparison.Ordinal) && text.IsEffectivelyVisible);
            Assert.True(library.ShowDetails);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UserCanReviewImportEditMetadataAndSeeSavedCover()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            LibraryViewModel library = LibraryViewModel.Open(new ImportStore(), new FakeProvider());
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 400, Height = 850 };
            window.Show();
            Click(view, "Find a book online");
            SearchView search = view.GetVisualDescendants().OfType<SearchView>().Single();
            library.Search.Query = "Dune";
            Click(search, "Search Open Library");
            window.UpdateLayout();
            Activate(search.GetVisualDescendants().OfType<Button>().First(button => button.Tag is SearchWork));
            window.UpdateLayout();
            Activate(search.GetVisualDescendants().OfType<Button>().First(button => button.Tag is SearchEdition));
            window.UpdateLayout();
            Click(search, "Review and save");
            window.UpdateLayout();
            Assert.True(library.ShowEditor);
            Assert.Empty(library.Books);
            TextBox publisher = view.FindControl<TextBox>("PublisherInput")!;
            publisher.Text = "My publisher";
            view.FindControl<CheckBox>("KeepCoverInput")!.IsChecked = true;
            Click(view, "Save");
            window.UpdateLayout();
            Book saved = Assert.Single(library.Books);
            Assert.Equal("My publisher", saved.Metadata!.Publisher);
            Assert.False(saved.Metadata.Cover.IsEmpty);
            library.OpenCommand.Execute(saved.Id);
            window.UpdateLayout();
            Assert.NotNull(view.FindControl<LocalCover>("SavedCover")!.Source);
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => string.Equals(text.Text, "My publisher", StringComparison.Ordinal));
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

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

    [Fact]
    public async Task RepeatedCoverIdShowsOnTheNewlySelectedResult()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            LibraryViewModel library = LibraryViewModel.Open(new EmptyStore(), new SameCoverProvider());
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 400, Height = 850 };
            window.Show();
            Click(view, "Find a book online");
            SearchView search = view.GetVisualDescendants().OfType<SearchView>().Single();
            TextBox title = search.GetVisualDescendants().OfType<TextBox>().Single();
            Assert.True(title.Focus());
            window.KeyTextInput("Dune");
            Click(search, "Search Open Library");
            window.UpdateLayout();
            List<Button> works = [.. search.GetVisualDescendants().OfType<Button>().Where(button => button.Tag is SearchWork)];
            Assert.Equal(2, works.Count);
            Image first = CoverImage(works[0]);
            Image second = CoverImage(works[1]);
            Assert.NotSame(first, second);
            Activate(CoverButton(works[0]));
            Assert.NotNull(first.Source);
            Assert.Null(second.Source);
            Activate(CoverButton(works[1]));
            Assert.NotNull(second.Source);
            Assert.Null(first.Source);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    private static Button CoverButton(Button work)
    {
        return work.GetVisualDescendants().OfType<Button>().First(button => Equals(button.Content, "Preview cover"));
    }

    private static Image CoverImage(Button work)
    {
        return work.GetVisualDescendants().OfType<Image>().Single();
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

    private sealed class ImportStore : IBookStore
    {
        private IReadOnlyList<Book> _books = [];

        public IReadOnlyList<Book> Read()
        {
            return _books;
        }

        public void Replace(IReadOnlyList<Book> books)
        {
            _books = books;
        }
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

        public Task<SearchPage<SearchWork>> SearchAsync(string title, int page, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SearchPage<SearchWork>([new("/works/OL1W", "Dune", "Frank Herbert", "1965", 12)], HasAnotherPage: false));
        }

        public Task<SearchPage<SearchEdition>> EditionsAsync(string workKey, int page, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SearchPage<SearchEdition>([Edition()], HasAnotherPage: false));
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

    private sealed class SameCoverProvider : IBookSearch
    {
        private readonly byte[] _cover = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j1WQAAAAASUVORK5CYII=");

        public Task<SearchPage<SearchWork>> SearchAsync(string title, int page, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SearchPage<SearchWork>([
                new("/works/OL1W", "Dune", "Frank Herbert", "1965", 12),
                new("/works/OL2W", "Dune Messiah", "Frank Herbert", "1969", 12),
            ], HasAnotherPage: false));
        }

        public Task<SearchPage<SearchEdition>> EditionsAsync(string workKey, int page, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SearchPage<SearchEdition>([], HasAnotherPage: false));
        }

        public Task<SearchEdition> PreviewAsync(string editionKey, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<byte[]?> CoverAsync(int coverId, CancellationToken cancellationToken)
        {
            return Task.FromResult<byte[]?>(_cover);
        }
    }
}
