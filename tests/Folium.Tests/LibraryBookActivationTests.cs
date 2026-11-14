using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Folium.Books;
using Folium.ViewModels;
using Folium.Views;

namespace Folium.Tests;

[Collection("Avalonia UI")]
public sealed class LibraryBookActivationTests
{
    [Fact]
    public async Task ActivatingAFilteredBookRowOpensItsDetails()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            Book book = new(Guid.NewGuid(), "Dune", "Frank Herbert");
            LibraryViewModel library = LibraryViewModel.Open(new MutableStore([book]));
            library.SearchQuery = "dune";
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 360, Height = 640 };
            window.Show();
            window.UpdateLayout();

            ItemsControl list = view.FindControl<ItemsControl>("LibraryBooksList")!;
            Button row = list.GetVisualDescendants().OfType<Button>().Single();
            Assert.True(row.IsEffectivelyVisible);
            Assert.Equal(book.Id, row.CommandParameter);
            row.Command!.Execute(row.CommandParameter);

            Assert.True(library.ShowDetails);
            Assert.Equal("Dune", library.SelectedTitle);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    private sealed class MutableStore(IReadOnlyList<Book> initialBooks) : IBookStore
    {
        private IReadOnlyList<Book> _books = initialBooks;

        public LibrarySnapshot Read()
        {
            return new(_books, []);
        }

        public void Replace(LibrarySnapshot snapshot)
        {
            _books = [.. snapshot.Books];
        }
    }
}
