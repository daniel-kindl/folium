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
public sealed class OpenLeafReviewTests
{
    private static readonly Guid BookId = new(0x11111111, 0x2222, 0x3333, 0x44, 0x44, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55);

    [Fact]
    public async Task LargeTextStatusButtonNamesTheFilterAndChangesIt()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            App app = Assert.IsType<App>(Application.Current);
            app.ApplySystemFontScale(2);
            try
            {
                LibraryViewModel library = LibraryViewModel.Open(new MemoryStore([new Book(BookId, "Middlemarch", "George Eliot")]));
                MainView view = new() { DataContext = library };
                Window window = new() { Content = view, Width = 412, Height = 915 };
                window.Show();
                window.UpdateLayout();

                Button status = view.FindControl<Button>("StatusFilter")!;
                Assert.True(status.IsEffectivelyVisible);
                Assert.Equal("Status: All", AutomationProperties.GetName(status));
                Assert.Contains(status.GetVisualDescendants().OfType<TextBlock>(), text => Equals(text.Text, "Status: All"));
                MenuFlyout menu = Assert.IsType<MenuFlyout>(status.Flyout);
                menu.ShowAt(status);
                window.UpdateLayout();
                MenuItem[] items = [.. menu.Items.OfType<MenuItem>()];
                Assert.Equal(["All", "To read", "Reading", "Finished"], items.Select(item => (string)item.Header!), StringComparer.Ordinal);

                MenuItem reading = items[2];
                reading.Command!.Execute(reading.CommandParameter);
                window.UpdateLayout();
                Assert.True(library.IsReadingSelected);
                Assert.Equal("Status: Reading", library.StatusButtonLabel);

                Button add = view.FindControl<Button>("AddBookButton")!;
                Assert.Contains("folium-compact", add.Classes, StringComparer.Ordinal);
                Assert.Equal("Add book", AutomationProperties.GetName(add));
                Assert.DoesNotContain(add.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible);
                Assert.True(add.Bounds.Height >= 56);
                window.Close();
            }
            finally
            {
                app.ApplySystemFontScale(1);
            }
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SelectedFilterShowsACheckInTheOnPrimaryColor()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            LibraryViewModel library = LibraryViewModel.Open(new MemoryStore([new Book(BookId, "Middlemarch", "George Eliot")]));
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 412, Height = 915, RequestedThemeVariant = ThemeVariant.Light };
            window.Show();
            window.UpdateLayout();

            Avalonia.Controls.Shapes.Path allCheck = Check(view, "AllFilter");
            Assert.True(allCheck.IsEffectivelyVisible);
            AssertColor("#FFFFFF", allCheck.Stroke);
            Assert.False(Check(view, "ToReadFilter").IsEffectivelyVisible);

            window.RequestedThemeVariant = ThemeVariant.Dark;
            window.UpdateLayout();
            AssertColor("#173823", allCheck.Stroke);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ListRowStatusShowsAnIconAndTheStatusColor()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            LibraryViewModel library = LibraryViewModel.Open(new MemoryStore([new Book(BookId, "Middlemarch", "George Eliot", ReadingStatus.Reading)]));
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 412, Height = 915, RequestedThemeVariant = ThemeVariant.Light };
            window.Show();
            window.UpdateLayout();

            ItemsControl list = view.FindControl<ItemsControl>("LibraryBooksList")!;
            TextBlock status = list.GetVisualDescendants().OfType<TextBlock>().Single(text => Equals(text.Text, "Reading"));
            AssertColor("#285E86", status.Foreground);
            Avalonia.Controls.Shapes.Path icon = status.GetVisualParent()!.GetVisualDescendants()
                .OfType<Avalonia.Controls.Shapes.Path>().Single(path => path.IsEffectivelyVisible);
            AssertColor("#285E86", icon.Stroke);

            window.RequestedThemeVariant = ThemeVariant.Dark;
            window.UpdateLayout();
            AssertColor("#A9CEE9", status.Foreground);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PlaceholderToneFollowsALiveThemeChange()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            LibraryViewModel library = LibraryViewModel.Open(new MemoryStore([new Book(BookId, "Middlemarch", "George Eliot")]));
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 412, Height = 915, RequestedThemeVariant = ThemeVariant.Light };
            window.Show();
            window.UpdateLayout();

            CoverTone cover = view.FindControl<ItemsControl>("LibraryBooksList")!.GetVisualDescendants().OfType<CoverTone>().Single();
            string tone = CoverPlaceholder.Tone(BookId);
            AssertColor(Resource(CoverTone.BrushKey(tone, ink: false), ThemeVariant.Light), cover.Background);
            AssertColor(Resource(CoverTone.BrushKey(tone, ink: true), ThemeVariant.Light), cover.Ink);

            window.RequestedThemeVariant = ThemeVariant.Dark;
            window.UpdateLayout();
            AssertColor(Resource(CoverTone.BrushKey(tone, ink: false), ThemeVariant.Dark), cover.Background);
            AssertColor(Resource(CoverTone.BrushKey(tone, ink: true), ThemeVariant.Dark), cover.Ink);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    private static Avalonia.Controls.Shapes.Path Check(MainView view, string filter)
    {
        return view.FindControl<Button>(filter)!.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>()
            .Single(path => path.Classes.Contains("folium-check"));
    }

    private static string Resource(string key, ThemeVariant variant)
    {
        Assert.True(Application.Current!.TryGetResource(key, variant, out object? value), key);
        return Assert.IsType<SolidColorBrush>(value).Color.ToString();
    }

    private static void AssertColor(string expected, IBrush? actual)
    {
        SolidColorBrush brush = Assert.IsType<SolidColorBrush>(actual);
        Assert.Equal(Color.Parse(expected), brush.Color);
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
