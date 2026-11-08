using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.VisualTree;
using Folium.Books;
using Folium.ViewModels;
using Folium.Views;

namespace Folium.Tests;

[Collection("Avalonia UI")]
public sealed class LibraryViewTests
{
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
            foreach ((int width, int height) in new[] { (400, 850), (850, 400) })
            {
                LibraryViewModel library = LibraryViewModel.Open(new EmptyStore());
                MainView view = new() { DataContext = library };
                Window window = new() { Content = view, Width = width, Height = height };
                window.Show();
                window.UpdateLayout();

                Button search = view.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Find a book online"));
                Button add = view.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Add a book"));
                Assert.True(search.IsEffectivelyVisible);
                Assert.True(add.IsEffectivelyVisible);
                Assert.True(search.Bounds.Height >= 48);
                Assert.True(add.Bounds.Height >= 48);
                Assert.All(view.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible),
                    button => Assert.True(button.Bounds.Height >= 48));
                window.Close();
            }
        }, TestContext.Current.CancellationToken);
    }

    private static void AssertBrushColor(string expected, IBrush? actual)
    {
        SolidColorBrush brush = Assert.IsType<SolidColorBrush>(actual);
        Assert.Equal(Color.Parse(expected), brush.Color);
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
