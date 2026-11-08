using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Folium.Books;
using Folium.ViewModels;
using Folium.Views;

namespace Folium.Tests;

public sealed class LibraryRowTests
{
    [Fact]
    public async Task TappingASavedBookOpensItsDetails()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            Book book = new(Guid.NewGuid(), "Device test", "Fixture author");
            LibraryViewModel library = LibraryViewModel.Open(new SavedStore(book));
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 400, Height = 850 };
            window.Show();
            window.UpdateLayout();
            Button row = view.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.CommandParameter, book.Id));
            Assert.True(row.IsEffectivelyEnabled);
            Assert.True(row.Focus());
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Assert.True(library.ShowDetails);
            Assert.Equal(book.Title, library.SelectedTitle);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    private sealed class SavedStore(Book book) : IBookStore
    {
        public IReadOnlyList<Book> Read()
        {
            return [book];
        }

        public void Replace(IReadOnlyList<Book> books)
        {
            throw new NotSupportedException();
        }
    }
}
