using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Folium.Books;
using Folium.ViewModels;
using Folium.Views;

namespace Folium.Tests;

[Collection("Avalonia UI")]
public sealed class LibraryEmptyStateTests
{
    [Fact]
    public async Task EmptyLibraryAndSearchWithNoResultsShowDifferentMessages()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            LibraryViewModel emptyLibrary = LibraryViewModel.Open(new SnapshotStore([]));
            MainView emptyView = new() { DataContext = emptyLibrary };
            Window emptyWindow = new() { Content = emptyView, Width = 400, Height = 850 };
            emptyWindow.Show();
            emptyWindow.UpdateLayout();

            TextBlock emptyMessage = emptyView.GetVisualDescendants().OfType<TextBlock>()
                .Single(text => string.Equals(text.Text, "No books yet.", StringComparison.Ordinal));
            Assert.True(emptyMessage.IsVisible);

            Book book = new(Guid.NewGuid(), "Dune", "Frank Herbert");
            LibraryViewModel noMatchesLibrary = LibraryViewModel.Open(new SnapshotStore([book]));
            noMatchesLibrary.SearchQuery = "missing";
            MainView noMatchesView = new() { DataContext = noMatchesLibrary };
            Window noMatchesWindow = new() { Content = noMatchesView, Width = 400, Height = 850 };
            noMatchesWindow.Show();
            noMatchesWindow.UpdateLayout();

            TextBlock noMatchesMessage = noMatchesView.GetVisualDescendants().OfType<TextBlock>()
                .Single(text => string.Equals(text.Text, "No books match your search.", StringComparison.Ordinal));
            Assert.True(noMatchesMessage.IsVisible);
            emptyWindow.Close();
            noMatchesWindow.Close();
        }, TestContext.Current.CancellationToken);
    }

    private sealed class SnapshotStore(IReadOnlyList<Book> initialBooks) : IBookStore
    {
        public IReadOnlyList<Book> Read()
        {
            return initialBooks;
        }

        public void Replace(IReadOnlyList<Book> books)
        {
            throw new InvalidOperationException("The library empty state test must not save books.");
        }
    }
}
