using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Folium.Books;
using Folium.Storage;
using Folium.ViewModels;
using Folium.Views;

namespace Folium.Tests;

[Collection("Avalonia UI")]
public sealed class ShelfSheetTouchTargetTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "folium-sheet-" + Guid.NewGuid().ToString("N"));

    public ShelfSheetTouchTargetTests()
    {
        _ = Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task EveryVisibleControlInTheShelfSheetIsAtLeast48Tall()
    {
        string path = Path.Combine(_directory, "library.json");
        PersonalLibrary shelves = new(new FileBookStore(path));
        _ = shelves.CreateShelf("Favourites");
        _ = shelves.Add("Dune", "Frank Herbert");

        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            LibraryViewModel library = LibraryViewModel.Open(new FileBookStore(path));
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 412, Height = 915 };
            window.Show();
            library.ToggleShelfPickerCommand.Execute(parameter: null);
            window.UpdateLayout();

            Button[] chips = [.. view.GetVisualDescendants().OfType<Button>()
                .Where(button => button.Classes.Contains("folium-chip") && button.IsEffectivelyVisible)];

            Assert.NotEmpty(chips);
            Assert.All(chips, chip => Assert.True(chip.Bounds.Height >= 48, "A shelf chip must be 48 units tall."));
            window.Close();
        }, TestContext.Current.CancellationToken);
    }
}
