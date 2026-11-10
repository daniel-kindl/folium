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

    [Fact]
    public async Task SavedBookSearchAndSortControlsWorkAtPhoneWidth()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            AssertEmptyLibraryHidesSearch();
            AssertSavedBookSearchAtPhoneWidth();
        }, TestContext.Current.CancellationToken);
    }

    private static void AssertEmptyLibraryHidesSearch()
    {
        LibraryViewModel emptyLibrary = LibraryViewModel.Open(new SnapshotStore([]));
        MainView emptyView = new() { DataContext = emptyLibrary };
        Window emptyWindow = new() { Content = emptyView, Width = 360, Height = 640 };
        emptyWindow.Show();
        emptyWindow.UpdateLayout();

        Assert.False(emptyView.FindControl<TextBox>("LibrarySearchInput")!.IsEffectivelyVisible);
        Assert.False(emptyView.FindControl<Grid>("SortGrid")!.IsEffectivelyVisible);
        emptyWindow.Close();
    }

    private static void AssertSavedBookSearchAtPhoneWidth()
    {
        Book book = new(Guid.NewGuid(), "Dune", "Frank Herbert");
        LibraryViewModel library = LibraryViewModel.Open(new SnapshotStore([book]));
        MainView view = new() { DataContext = library };
        Window window = new() { Content = view, Width = 360, Height = 640 };
        window.Show();
        window.UpdateLayout();

        TextBox search = view.FindControl<TextBox>("LibrarySearchInput")!;
        Button clear = ClearButton(view);
        Grid sortGrid = view.FindControl<Grid>("SortGrid")!;
        Button titleAscending = view.FindControl<Button>("TitleAscendingSort")!;
        Button authorDescending = view.FindControl<Button>("AuthorDescendingSort")!;
        Assert.True(search.IsEffectivelyVisible);
        Assert.True(search.Bounds.Width >= 200);
        Assert.True(view.FindControl<ScrollViewer>("LibraryContentScroll")!.IsEffectivelyVisible);
        Assert.Contains("folium-primary", titleAscending.Classes, StringComparer.Ordinal);

        search.Text = "missing";
        window.UpdateLayout();
        TextBlock noMatch = view.GetVisualDescendants().OfType<TextBlock>()
            .Single(text => string.Equals(text.Text, "No books match your search.", StringComparison.Ordinal));
        Assert.True(noMatch.IsVisible);
        Assert.True(clear.IsEffectivelyVisible);
        Assert.Equal(4, sortGrid.Children.OfType<Button>().Count());

        authorDescending.Command!.Execute(authorDescending.CommandParameter);
        window.UpdateLayout();
        Assert.Equal("Author Z–A", library.SortLabel);
        Assert.Contains("folium-primary", authorDescending.Classes, StringComparer.Ordinal);
        Assert.DoesNotContain("folium-primary", titleAscending.Classes, StringComparer.Ordinal);
        library.ClearSearchCommand.Execute(parameter: null);
        window.UpdateLayout();
        Assert.Equal("", search.Text);
        Assert.False(clear.IsEffectivelyVisible);

        window.Height = 420;
        window.UpdateLayout();
        Assert.True(search.IsEffectivelyVisible);
        Assert.True(search.Bounds.Height >= 48);
        window.Close();
    }

    private static Button ClearButton(MainView view)
    {
        return view.GetVisualDescendants().OfType<Button>()
            .Single(button => string.Equals(
                Avalonia.Automation.AutomationProperties.GetName(button),
                "Clear saved book search",
                StringComparison.Ordinal));
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
