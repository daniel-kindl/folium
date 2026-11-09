using System.Globalization;
using Avalonia;
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
public sealed class LibraryViewTests
{
    private static readonly (int Width, int Height)[] LayoutSizes = [(400, 850), (850, 400)];
    private static readonly (int Width, int Height)[] PortraitLayoutSize = [(400, 850)];
    private static readonly double[] FontScales = [1, 1.5, 2];

    [Fact]
    public async Task PrimaryActionIconUsesOnPrimaryInBothThemes()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            MainView view = new() { DataContext = LibraryViewModel.Open(new EmptyStore()) };
            Window window = new()
            {
                Content = view,
                RequestedThemeVariant = ThemeVariant.Light,
                Width = 400,
                Height = 850,
            };
            window.Show();
            window.UpdateLayout();

            Avalonia.Controls.Shapes.Path[] icons = [.. view.GetVisualDescendants()
                .OfType<Avalonia.Controls.Shapes.Path>()
                .Where(path => path.Classes.Contains("folium-icon-primary"))];
            Assert.Equal(2, icons.Length);
            Assert.All(icons, icon => AssertBrushColor("#FFFFFF", icon.Stroke));

            window.RequestedThemeVariant = ThemeVariant.Dark;
            window.UpdateLayout();
            Assert.All(icons, icon => AssertBrushColor("#173823", icon.Stroke));
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ActiveFilterUsesThePrimaryState()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            LibraryViewModel library = LibraryViewModel.Open(new EmptyStore());
            library.FilterCommand.Execute("reading");
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 400, Height = 850 };
            window.Show();
            window.UpdateLayout();

            TextBlock activeFilter = view.GetVisualDescendants().OfType<TextBlock>()
                .Single(text => Equals(text.Text, "Reading") && text.Classes.Contains("folium-active-filter"));
            Assert.True(activeFilter.IsEffectivelyVisible);
            AssertBrushColor("#315C43", activeFilter.Foreground);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SavedBookSearchAndSortControlsWorkAtPhoneWidth()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            LibraryViewModel library = LibraryViewModel.Open(new EmptyStore());
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 360, Height = 640 };
            window.Show();
            window.UpdateLayout();

            TextBox search = view.FindControl<TextBox>("LibrarySearchInput")!;
            Button clear = view.GetVisualDescendants().OfType<Button>()
                .Single(button => string.Equals(Avalonia.Automation.AutomationProperties.GetName(button), "Clear saved book search", StringComparison.Ordinal));
            Grid sortGrid = view.FindControl<Grid>("SortGrid")!;
            ScrollViewer scroll = view.FindControl<ScrollViewer>("LibraryContentScroll")!;
            Assert.True(search.IsEffectivelyVisible);
            Assert.True(search.Bounds.Width >= 200);
            Assert.True(scroll.IsEffectivelyVisible);

            library.SearchQuery = "Dune";
            window.UpdateLayout();
            Assert.True(clear.IsEffectivelyVisible);
            Assert.Equal(4, sortGrid.Children.OfType<Button>().Count());

            library.SortCommand.Execute("author-desc");
            Assert.Equal("Author Z–A", library.SortLabel);
            library.ClearSearchCommand.Execute(parameter: null);
            Assert.Equal("", search.Text);
            Assert.False(clear.IsEffectivelyVisible);

            window.Height = 420;
            window.UpdateLayout();
            Assert.True(search.IsEffectivelyVisible);
            Assert.True(search.Bounds.Height >= 48);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task LibraryActionsKeepTheirTouchBoundsInPortraitAndLandscape()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            App app = Assert.IsType<App>(Application.Current);
            foreach (double scale in FontScales)
            {
                app.ApplySystemFontScale(scale);
                (int Width, int Height)[] sizes = scale == 1 ? LayoutSizes : PortraitLayoutSize;
                foreach ((int width, int height) in sizes)
                {
                    AssertLibraryLayout(width, height, scale);
                }
            }

            app.ApplySystemFontScale(1);
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SearchContentAlignsWithTheScreenTitle()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            LibraryViewModel library = LibraryViewModel.Open(new EmptyStore());
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 400, Height = 850 };
            window.Show();
            library.ShowSearchCommand.Execute(parameter: null);
            window.UpdateLayout();

            TextBlock title = view.GetVisualDescendants().OfType<TextBlock>().Single(text => Equals(text.Text, "Search"));
            TextBox query = view.GetVisualDescendants().OfType<TextBox>().Single(box => box.IsEffectivelyVisible);
            Button back = view.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Back"));
            Assert.Equal(16, BoundsIn(view, title).Left, tolerance: 0.001);
            Assert.Equal(16, BoundsIn(view, query).Left, tolerance: 0.001);
            Assert.Equal(16, BoundsIn(view, back).Left, tolerance: 0.001);
            Assert.Equal(view.Bounds.Width - 16, BoundsIn(view, query).Right, tolerance: 0.001);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    private static void AssertLibraryLayout(int width, int height, double scale)
    {
        LibraryViewModel library = LibraryViewModel.Open(new EmptyStore());
        MainView view = new() { DataContext = library };
        Window window = new() { Content = view, Width = width, Height = height };
        window.Show();
        window.UpdateLayout();

        Button search = view.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Find a book online"));
        Button add = view.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Add a book"));
        TextBlock emptyMessage = view.GetVisualDescendants().OfType<TextBlock>().Single(text => Equals(text.Text, "No books yet."));
        Viewbox emptyIcon = view.FindControl<Viewbox>("EmptyIcon")!;
        Assert.True(search.IsEffectivelyVisible);
        Assert.True(add.IsEffectivelyVisible);
        Assert.True(emptyMessage.IsEffectivelyVisible);
        Assert.Equal(width < height, emptyIcon.IsVisible);
        AssertEmptyIconSize(view, emptyIcon);
        Assert.True(search.Bounds.Height >= 48);
        Assert.True(add.Bounds.Height >= 48);
        Rect emptyBounds = BoundsIn(view, emptyMessage);
        Rect addBounds = BoundsIn(view, add);
        ScrollViewer contentScroll = view.FindControl<ScrollViewer>("LibraryContentScroll")!;
        Rect contentBounds = BoundsIn(view, contentScroll);
        if (emptyBounds.Top >= contentBounds.Bottom)
        {
            Assert.True(contentScroll.Extent.Height > contentScroll.Viewport.Height);
        }
        else
        {
            Assert.True(emptyBounds.Bottom <= addBounds.Top || addBounds.Bottom <= emptyBounds.Top,
                string.Create(CultureInfo.InvariantCulture, $"Empty bounds {emptyBounds}; add bounds {addBounds}; size {width}x{height}; scale {scale}."));
        }

        AssertFilterLayout(view, search, scale);

        Assert.All(view.GetVisualDescendants().OfType<Button>()
                .Where(button => button is not RepeatButton && button.IsEffectivelyVisible),
            button => Assert.True(button.Bounds.Height >= 48));
        window.Width = width + 1;
        window.UpdateLayout();
        window.Width = width;
        window.UpdateLayout();
        window.Close();
    }

    private static void AssertEmptyIconSize(MainView view, Viewbox emptyIcon)
    {
        if (!emptyIcon.IsVisible)
        {
            return;
        }

        Avalonia.Controls.Shapes.Path emptyPath = emptyIcon.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single();
        Rect drawn = new Rect(emptyPath.Bounds.Size).TransformToAABB(emptyPath.TransformToVisual(view)!.Value);
        Assert.Equal(48, drawn.Width, tolerance: 0.001);
        Assert.Equal(48, drawn.Height, tolerance: 0.001);
    }

    private static void AssertFilterLayout(MainView view, Button search, double scale)
    {
        Button[] filters = [.. view.FindControl<Grid>("FilterGrid")!.Children.OfType<Button>()];
        Assert.Equal(4, filters.Length);
        Grid filterGrid = view.FindControl<Grid>("FilterGrid")!;
        Assert.Equal(search.Bounds.Width, filterGrid.Bounds.Width, tolerance: 0.001);
        Assert.Equal(BoundsIn(view, search).Left, filters.Min(filter => BoundsIn(view, filter).Left), tolerance: 0.001);
        Assert.Equal(BoundsIn(view, search).Right, filters.Max(filter => BoundsIn(view, filter).Right), tolerance: 0.001);
        foreach (Button filter in filters)
        {
            string filterLabel = Convert.ToString(filter.Content, CultureInfo.InvariantCulture) ?? string.Empty;
            TextBlock label = new()
            {
                Text = filterLabel,
                FontFamily = filter.FontFamily,
                FontSize = filter.FontSize,
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.True(label.DesiredSize.Width <= filter.Bounds.Width - filter.Padding.Left - filter.Padding.Right,
                $"The {filterLabel} filter label must fit at {scale.ToString(CultureInfo.InvariantCulture)}x.");
            TextBlock shown = filter.GetVisualDescendants().OfType<TextBlock>().Single();
            Rect filterBounds = BoundsIn(view, filter);
            Rect shownBounds = BoundsIn(view, shown);
            Assert.Equal(filterBounds.Center.Y, shownBounds.Center.Y, tolerance: 1);
            Assert.Equal(filterBounds.Center.X, shownBounds.Center.X, tolerance: 1);
        }
    }

    private static void AssertBrushColor(string expected, IBrush? actual)
    {
        SolidColorBrush brush = Assert.IsType<SolidColorBrush>(actual);
        Assert.Equal(Color.Parse(expected), brush.Color);
    }

    private static Rect BoundsIn(MainView view, Control control)
    {
        Point origin = control.TranslatePoint(default, view)!.Value;
        return new Rect(origin, control.Bounds.Size);
    }

    private sealed class EmptyStore : IBookStore
    {
        public IReadOnlyList<Book> Read()
        {
            return [];
        }

        public void Replace(IReadOnlyList<Book> books)
        {
            throw new InvalidOperationException("The library view test must not save books.");
        }
    }

}
