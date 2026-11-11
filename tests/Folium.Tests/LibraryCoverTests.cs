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

    [Fact]
    public async Task DetailsWithoutACoverShowsTheToneFrameAndHidesTheCoverBox()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            BookMetadata metadata = new(
                "/works/OL1W",
                "/books/OL2M",
                "Simon & Schuster",
                "2025",
                "9781668032145",
                "",
                "eng",
                "",
                CoverId: null,
                Cover: ReadOnlyMemory<byte>.Empty);
            Book book = new(BookId, "Protocols", "Andrew Huberman", ReadingStatus.Reading, metadata);
            LibraryViewModel library = LibraryViewModel.Open(new MutableStore([book]));
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 400, Height = 850, RequestedThemeVariant = ThemeVariant.Dark };
            window.Show();
            library.OpenCommand.Execute(book.Id);
            window.UpdateLayout();

            Border cover = view.GetVisualDescendants().OfType<Border>().Single(border => border.Width == 120);
            Assert.True(cover.IsEffectivelyVisible);
            Assert.Equal(180, cover.Bounds.Height, tolerance: 0.001);
            AssertBrush(CoverPlaceholder.Tone(book.Id), ink: false, cover.Background);
            Assert.Contains(cover.GetVisualDescendants().OfType<TextBlock>(), text => string.Equals(text.Text, "Protocols", StringComparison.Ordinal));
            Assert.Contains(cover.GetVisualDescendants().OfType<TextBlock>(), text => string.Equals(text.Text, "Andrew Huberman", StringComparison.Ordinal));
            LocalCover savedCover = view.FindControl<LocalCover>("SavedCover")!;
            Assert.False(savedCover.IsEffectivelyVisible);
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text =>
                string.Equals(text.Text, "No saved cover", StringComparison.Ordinal) && text.IsEffectivelyVisible);
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBlock>(), text =>
                string.Equals(text.Text, "Edition", StringComparison.Ordinal) && text.IsEffectivelyVisible);
            Button reading = view.GetVisualDescendants().OfType<Button>().Single(button =>
                string.Equals(AutomationProperties.GetName(button), "Reading", StringComparison.Ordinal));
            Button toRead = view.GetVisualDescendants().OfType<Button>().Single(button =>
                string.Equals(AutomationProperties.GetName(button), "To read", StringComparison.Ordinal));
            Assert.Contains("folium-primary", reading.Classes, StringComparer.Ordinal);
            Assert.DoesNotContain("folium-primary", toRead.Classes, StringComparer.Ordinal);

            library.EditCommand.Execute(parameter: null);
            window.UpdateLayout();
            CheckBox keepCover = view.FindControl<CheckBox>("KeepCoverInput")!;
            Assert.False(keepCover.IsEffectivelyVisible);
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBlock>(), text =>
                string.Equals(text.Text, "Cover images can have separate copyright restrictions. Leave the box unchecked to save without a cover.", StringComparison.Ordinal)
                && text.IsEffectivelyVisible);
            TextBox description = view.FindControl<TextBox>("DescriptionInput")!;
            Assert.Equal(TextWrapping.Wrap, description.TextWrapping);
            Assert.True(description.AcceptsReturn);
            Assert.True(description.MaxHeight <= 160);
            TextBox edition = view.FindControl<TextBox>("EditionInput")!;
            Assert.True(edition.Focus());
            window.UpdateLayout();
            Border field = edition.GetVisualDescendants().OfType<Border>().Single(border => string.Equals(border.Name, "PART_BorderElement", StringComparison.Ordinal));
            AssertBrushColor("#202922", field.Background);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    private static void AssertBrushColor(string expected, IBrush? actual)
    {
        SolidColorBrush brush = Assert.IsType<SolidColorBrush>(actual);
        Assert.Equal(Color.Parse(expected), brush.Color);
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
