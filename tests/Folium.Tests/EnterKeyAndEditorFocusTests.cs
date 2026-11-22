using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Folium.Books;
using Folium.Search;
using Folium.ViewModels;
using Folium.Views;

namespace Folium.Tests;

[Collection("Avalonia UI")]
public sealed class EnterKeyAndEditorFocusTests
{
    [Fact]
    public async Task EnterInTheTitleFieldRunsTheSearch()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            LibraryViewModel library = LibraryViewModel.Open(new EmptyStore(), new StubSearch());
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 400, Height = 850 };
            window.Show();
            library.ShowSearchCommand.Execute(parameter: null);
            window.UpdateLayout();
            TextBox title = view.GetVisualDescendants().OfType<SearchView>().Single()
                .GetVisualDescendants().OfType<TextBox>().Single();
            Assert.True(title.Focus());
            window.KeyTextInput("Dune");

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            _ = Assert.Single(library.Search.Works);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OpeningTheEditorFocusesTheTitleField()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            LibraryViewModel library = LibraryViewModel.Open(new EmptyStore());
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 400, Height = 850 };
            window.Show();
            window.UpdateLayout();

            library.AddByHandCommand.Execute(parameter: null);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.True(view.FindControl<TextBox>("TitleField")!.IsFocused);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    private sealed class EmptyStore : IBookStore
    {
        public LibrarySnapshot Read()
        {
            return new([], []);
        }

        public void Replace(LibrarySnapshot snapshot)
        {
            throw new InvalidOperationException("The test must not save books.");
        }
    }

    private sealed class StubSearch : IBookSearch
    {
        public Task<SearchPage<SearchWork>> SearchAsync(string title, int page, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SearchPage<SearchWork>([new SearchWork("/works/OL1W", "Dune", "Frank Herbert", "1965")], HasAnotherPage: false));
        }

        public Task<SearchPage<SearchEdition>> EditionsAsync(string workKey, int page, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SearchPage<SearchEdition>([], HasAnotherPage: false));
        }

        public Task<SearchEdition> PreviewAsync(string editionKey, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Not used.");
        }

        public Task<byte[]?> CoverAsync(int coverId, CancellationToken cancellationToken)
        {
            return Task.FromResult<byte[]?>(null);
        }
    }
}
