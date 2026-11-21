using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Folium.Books;
using Folium.Storage;
using Folium.ViewModels;
using Folium.Views;

namespace Folium.Tests;

[Collection("Avalonia UI")]
public sealed class RowStatusChangeTests
{
    [Fact]
    public void ChangingAStatusFromTheListUpdatesTheRowAndKeepsTheFilter()
    {
        Book first = new(Guid.NewGuid(), "Dune", "Frank Herbert");
        Book second = new(Guid.NewGuid(), "Piranesi", "Susanna Clarke");
        LibraryViewModel library = LibraryViewModel.Open(new MemoryStore([first, second]));

        library.ChangeStatus(first.Id, ReadingStatus.Reading);

        Assert.Equal("Reading", library.ShownBooks.Single(row => row.Id == first.Id).Status);
        Assert.Equal("To read", library.ShownBooks.Single(row => row.Id == second.Id).Status);

        library.FilterCommand.Execute("reading");
        library.ChangeStatus(first.Id, ReadingStatus.Finished);

        Assert.Empty(library.ShownBooks);
        Assert.True(library.IsReadingSelected);
    }

    [Fact]
    public void AFailedSaveReportsTheErrorAndLeavesTheRowUnchanged()
    {
        Book book = new(Guid.NewGuid(), "Dune", "Frank Herbert");
        MemoryStore store = new([book]);
        LibraryViewModel library = LibraryViewModel.Open(store);
        store.FailWrites = true;

        library.ChangeStatus(book.Id, ReadingStatus.Finished);

        Assert.Equal("Folium could not save this status.", library.SaveError);
        Assert.Equal("To read", library.ShownBooks.Single().Status);
    }

    [Fact]
    public void AMissingBookIsIgnoredWithoutAnError()
    {
        LibraryViewModel library = LibraryViewModel.Open(new MemoryStore([new Book(Guid.NewGuid(), "Dune", "Frank Herbert")]));

        library.ChangeStatus(Guid.NewGuid(), ReadingStatus.Finished);

        Assert.Equal("To read", library.ShownBooks.Single().Status);
    }

    [Fact]
    public async Task EachRowOffersAStatusButtonThatNamesTheCurrentStatus()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            Book book = new(Guid.NewGuid(), "Dune", "Frank Herbert", ReadingStatus.Reading);
            LibraryViewModel library = LibraryViewModel.Open(new MemoryStore([book]));
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 400, Height = 850 };
            window.Show();
            window.UpdateLayout();

            Button status = view.FindControl<ItemsControl>("LibraryBooksList")!.GetVisualDescendants().OfType<Button>()
                .Single(button => string.Equals(AutomationProperties.GetHelpText(button), "Change reading status", StringComparison.Ordinal));

            Assert.Equal("Reading", AutomationProperties.GetName(status));
            Assert.True(status.Bounds.Height >= 48);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ThePickedMenuItemChangesTheStatusAndTheMenuMarksTheCurrentOne()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            Book book = new(Guid.NewGuid(), "Dune", "Frank Herbert", ReadingStatus.Reading);
            LibraryViewModel library = LibraryViewModel.Open(new MemoryStore([book]));
            BookRow row = library.ShownBooks.Single();

            MenuFlyout menu = StatusMenu.Build(row, library);
            MenuItem[] items = [.. menu.Items.OfType<MenuItem>()];

            Assert.Equal(["To read", "Reading", "Finished"], items.Select(item => (string)item.Header!), StringComparer.Ordinal);
            Assert.Equal([false, true, false], items.Select(item => item.IsChecked));

            items[2].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Equal("Finished", library.ShownBooks.Single().Status);
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ClickingTheStatusButtonDoesNotOpenTheBook()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            Book book = new(Guid.NewGuid(), "Dune", "Frank Herbert");
            LibraryViewModel library = LibraryViewModel.Open(new MemoryStore([book]));
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 400, Height = 850 };
            window.Show();
            window.UpdateLayout();
            Button status = view.FindControl<ItemsControl>("LibraryBooksList")!.GetVisualDescendants().OfType<Button>()
                .Single(button => string.Equals(AutomationProperties.GetHelpText(button), "Change reading status", StringComparison.Ordinal));

            status.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.True(library.ShowLibrary);
            Assert.False(library.ShowDetails);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TheStatusButtonIsNotInsideTheRowButton()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            LibraryViewModel library = LibraryViewModel.Open(new MemoryStore([new Book(Guid.NewGuid(), "Dune", "Frank Herbert")]));
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 400, Height = 850 };
            window.Show();
            window.UpdateLayout();
            ItemsControl list = view.FindControl<ItemsControl>("LibraryBooksList")!;
            Button row = list.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("folium-row"));

            Assert.DoesNotContain(row.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("folium-status-button"));
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    private sealed class MemoryStore(IReadOnlyList<Book> books) : IBookStore
    {
        private IReadOnlyList<Book> _books = books;

        public bool FailWrites { get; set; }

        public LibrarySnapshot Read()
        {
            return new(_books, []);
        }

        public void Replace(LibrarySnapshot snapshot)
        {
            if (FailWrites)
            {
                throw new LibraryStoreException("The test store refuses writes.");
            }

            _books = snapshot.Books;
        }
    }
}
