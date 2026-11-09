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
        Avalonia.Controls.Shapes.Path emptyIcon = view.GetVisualDescendants()
            .OfType<Avalonia.Controls.Shapes.Path>()
            .Single(path => path.Width == 48 && path.Height == 48);
        Assert.True(search.IsEffectivelyVisible);
        Assert.True(add.IsEffectivelyVisible);
        Assert.True(emptyMessage.IsEffectivelyVisible);
        Assert.Equal(width < height, emptyIcon.IsVisible);
        Assert.True(search.Bounds.Height >= 48);
        Assert.True(add.Bounds.Height >= 48);
        Rect emptyBounds = BoundsIn(view, emptyMessage);
        Rect addBounds = BoundsIn(view, add);
        Assert.True(emptyBounds.Bottom <= addBounds.Top || addBounds.Bottom <= emptyBounds.Top,
            string.Create(CultureInfo.InvariantCulture, $"Empty bounds {emptyBounds}; add bounds {addBounds}; size {width}x{height}; scale {scale}."));

        Button[] filters = [.. view.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("folium-filter"))];
        Assert.Equal(4, filters.Length);
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
        }

        Assert.All(view.GetVisualDescendants().OfType<Button>()
                .Where(button => button is not RepeatButton && button.IsEffectivelyVisible),
            button => Assert.True(button.Bounds.Height >= 48));
        window.Width = width + 1;
        window.UpdateLayout();
        window.Width = width;
        window.UpdateLayout();
        window.Close();
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
