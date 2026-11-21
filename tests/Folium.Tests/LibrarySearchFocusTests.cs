using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Folium.Books;
using Folium.ViewModels;
using Folium.Views;

namespace Folium.Tests;

[Collection("Avalonia UI")]
public sealed class LibrarySearchFocusTests
{
    [Fact]
    public async Task OpeningSavedBookSearchFocusesTheField()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            LibraryViewModel library = LibraryViewModel.Open(new OneBookStore());
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 400, Height = 850 };
            window.Show();
            window.UpdateLayout();
            TextBox field = view.FindControl<TextBox>("LibrarySearchInput")!;
            Assert.False(field.IsFocused);

            library.ToggleSavedSearchCommand.Execute(parameter: null);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.True(field.IsFocused);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    private sealed class OneBookStore : IBookStore
    {
        public LibrarySnapshot Read()
        {
            return new([new Book(Guid.NewGuid(), "Dune", "Frank Herbert")], []);
        }

        public void Replace(LibrarySnapshot snapshot)
        {
            throw new InvalidOperationException("The test must not save books.");
        }
    }
}
