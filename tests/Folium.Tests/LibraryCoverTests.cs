using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Folium.Books;
using Folium.ViewModels;
using Folium.Views;

namespace Folium.Tests;

[Collection("Avalonia UI")]
public sealed class LibraryCoverTests
{
    private static readonly Guid BookId = new(0xaaaaaaaa, 0xbbbb, 0xcccc, 0xdd, 0xdd, 0xee, 0xee, 0xee, 0xee, 0xee, 0xee);

    [Fact]
    public async Task PlaceholdersUseTheBookToneAndTheGridShowsTheTitle()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            Book book = new(BookId, "The Middlemarch", "George Eliot");
            LibraryViewModel library = LibraryViewModel.Open(new MutableStore([book]));
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 400, Height = 850 };
            window.Show();
            window.UpdateLayout();

            string tone = CoverPlaceholder.Tone(book.Id);
            ItemsControl list = view.FindControl<ItemsControl>("LibraryBooksList")!;
            Border rowCover = list.GetVisualDescendants().OfType<Border>().Single(border => border.Width == 48);
            Assert.Equal(48, rowCover.Bounds.Width, tolerance: 0.001);
            Assert.Equal(72, rowCover.Bounds.Height, tolerance: 0.001);
            AssertBrush(tone, ink: false, rowCover.Background);
            Assert.Equal("M", rowCover.GetVisualDescendants().OfType<TextBlock>().Single().Text);
            Assert.False(view.FindControl<Avalonia.Controls.Shapes.Path>("ViewListIcon")!.IsEffectivelyVisible);

            library.ToggleGridCommand.Execute(parameter: null);
            window.UpdateLayout();

            Assert.False(list.IsEffectivelyVisible);
            ItemsControl grid = view.FindControl<ItemsControl>("LibraryBooksGrid")!;
            Assert.True(grid.IsEffectivelyVisible);
            Border gridCover = grid.GetVisualDescendants().OfType<Border>().Single(border => border.Width == 117);
            Assert.Equal(117, gridCover.Bounds.Width, tolerance: 0.001);
            Assert.Equal(176, gridCover.Bounds.Height, tolerance: 0.001);
            AssertBrush(tone, ink: false, gridCover.Background);
            Assert.Contains(gridCover.GetVisualDescendants().OfType<TextBlock>(), text => Equals(text.Text, "The Middlemarch"));
            Assert.Contains(gridCover.GetVisualDescendants().OfType<TextBlock>(), text => Equals(text.Text, "George Eliot"));
            Assert.True(view.FindControl<Avalonia.Controls.Shapes.Path>("ViewListIcon")!.IsEffectivelyVisible);
            Button row = grid.GetVisualDescendants().OfType<Button>().Single();
            Assert.Equal("The Middlemarch, George Eliot, To read", AutomationProperties.GetName(row));
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    private static void AssertBrush(string tone, bool ink, IBrush? actual)
    {
        string key = "FoliumCover" + char.ToUpperInvariant(tone[0]) + tone[1..] + (ink ? "Ink" : "") + "Brush";
        Assert.True(Application.Current!.TryGetResource(key, ThemeVariant.Light, out object? resource), key);
        SolidColorBrush expected = Assert.IsType<SolidColorBrush>(resource);
        SolidColorBrush brush = Assert.IsType<SolidColorBrush>(actual);
        Assert.Equal(expected.Color, brush.Color);
    }

    private sealed class MutableStore(IReadOnlyList<Book> initialBooks) : IBookStore
    {
        private IReadOnlyList<Book> _books = initialBooks;

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
