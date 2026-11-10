using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Folium.Books;
using Folium.Storage;
using Folium.ViewModels;
using Folium.Views;

namespace Folium.Tests;

[Collection("Avalonia UI")]
public sealed class RestoreViewTests
{
    private static readonly (int Width, int Height)[] LayoutSizes = [(400, 850), (850, 400)];
    private static readonly string[] RestoreLabels = ["Replace library", "Keep current library"];

    [Fact]
    public async Task LargeRestoreKeepsConfirmationActionsVisible()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            foreach ((int width, int height) in LayoutSizes)
            {
                LibraryViewModel library = LibraryViewModel.Open(new EmptyStore());
                Book[] books = [.. Enumerable.Range(0, 100).Select(index => new Book(Guid.NewGuid(), "Book " + index.ToString(CultureInfo.InvariantCulture), "Author"))];
                library.PrepareRestore(LibraryBackup.Export(books));
                MainView view = new() { DataContext = library };
                Window window = new() { Content = view, Width = width, Height = height };
                window.Show();
                window.UpdateLayout();
                foreach (string label in RestoreLabels)
                {
                    Button button = view.GetVisualDescendants().OfType<Button>().Single(item => Equals(item.Content, label));
                    Rect bounds = BoundsIn(view, button);
                    Assert.True(bounds.Top >= 0 && bounds.Bottom <= view.Bounds.Height, bounds.ToString());
                    Assert.True(button.Bounds.Height >= 48);
                }

                window.Close();
            }
        }, TestContext.Current.CancellationToken);
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
