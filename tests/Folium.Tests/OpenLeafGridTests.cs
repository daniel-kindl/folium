using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Folium.Books;
using Folium.ViewModels;
using Folium.Views;

namespace Folium.Tests;

[Collection("Avalonia UI")]
public sealed class OpenLeafGridTests
{
    [Fact]
    public async Task GridUsesThreeColumnsAndTwoAtLargerText()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            App app = Assert.IsType<App>(Application.Current);
            try
            {
                Assert.Equal(3, GridColumnsAt(1));
                Assert.Equal(2, GridColumnsAt(1.5));
            }
            finally
            {
                app.ApplySystemFontScale(1);
            }

            int GridColumnsAt(double scale)
            {
                app.ApplySystemFontScale(scale);
                Book[] books = [.. Enumerable.Range(1, 4).Select(index => new Book(Guid.NewGuid(), "Book " + index.ToString(System.Globalization.CultureInfo.InvariantCulture), "Author"))];
                LibraryViewModel library = LibraryViewModel.Open(new MemoryStore(books));
                library.ToggleGridCommand.Execute(parameter: null);
                MainView view = new() { DataContext = library };
                Window window = new() { Content = view, Width = 412, Height = 915 };
                window.Show();
                window.UpdateLayout();
                UniformGrid panel = view.FindControl<ItemsControl>("LibraryBooksGrid")!.GetVisualDescendants().OfType<UniformGrid>().Single();
                int columns = panel.Columns;
                Button[] tiles = [.. view.FindControl<ItemsControl>("LibraryBooksGrid")!.GetVisualDescendants().OfType<Button>()];
                double firstRow = tiles[0].TranslatePoint(default, panel)!.Value.Y;
                Assert.Equal(columns, tiles.Count(tile => Math.Abs(tile.TranslatePoint(default, panel)!.Value.Y - firstRow) < 0.5));
                Assert.All(tiles, tile => Assert.Contains(tile.GetVisualDescendants().OfType<TextBlock>(), text =>
                    text.IsEffectivelyVisible && text.FindAncestorOfType<CoverTone>() is null && text.Text!.StartsWith("Book ", StringComparison.Ordinal)));
                window.Close();
                return columns;
            }
        }, TestContext.Current.CancellationToken);
    }

    private sealed class MemoryStore(IReadOnlyList<Book> books) : IBookStore
    {
        private IReadOnlyList<Book> _books = books;

        public IReadOnlyList<Book> Read()
        {
            return _books;
        }

        public void Replace(IReadOnlyList<Book> books)
        {
            _books = [.. books];
        }
    }
}
