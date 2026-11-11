using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Folium.Books;
using Folium.Storage;
using Folium.ViewModels;
using Folium.Views;

namespace Folium.Tests;

[Collection("Avalonia UI")]
public sealed class OpenLeafDialogTests
{
    private static readonly Guid BookId = new(0x11111111, 0x2222, 0x3333, 0x44, 0x44, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55);

    [Fact]
    public async Task DeleteAsksInADialogOverTheBookWithKeepBookFirst()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            LibraryViewModel library = LibraryViewModel.Open(new MemoryStore([new Book(BookId, "Middlemarch", "George Eliot")]));
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 412, Height = 915 };
            window.Show();
            library.OpenCommand.Execute(BookId);
            library.AskRemoveCommand.Execute(parameter: null);
            window.UpdateLayout();

            Assert.True(library.ShowDetailsLayer);
            Assert.Equal("Book", library.AppBarHeading);
            DeleteBookDialog dialog = view.GetVisualDescendants().OfType<DeleteBookDialog>().Single();
            Assert.True(dialog.IsEffectivelyVisible);
            Assert.Contains(dialog.GetVisualDescendants().OfType<TextBlock>(), text => Equals(text.Text, "Delete this book?"));
            Button[] actions = [.. dialog.GetVisualDescendants().OfType<WrapPanel>().Single().Children.OfType<Button>()];
            Assert.Equal(["Keep book", "Delete book"], actions.Select(action => (string)action.Content!), StringComparer.Ordinal);

            actions[0].Command!.Execute(parameter: null);
            Assert.True(library.ShowDetails);
            _ = Assert.Single(library.Books);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RestoreConfirmationShowsTheFileNameAndBookCount()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(() =>
        {
            LibraryViewModel library = LibraryViewModel.Open(new MemoryStore([new Book(BookId, "Middlemarch", "George Eliot")]));
            MainView view = new() { DataContext = library };
            Window window = new() { Content = view, Width = 412, Height = 915 };
            window.Show();
            string json = LibraryBackup.Export([new Book(Guid.NewGuid(), "Dune", "Frank Herbert"), new Book(Guid.NewGuid(), "Emma", "Jane Austen")]);
            library.PrepareRestore(json);
            library.SetRestoreFileName("Folium backup.json");
            window.UpdateLayout();

            Assert.True(library.ShowRestore);
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text =>
                Equals(text.Text, "Folium backup.json · 2 books") && text.IsEffectivelyVisible);
            window.Close();
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void SaveSnackbarClosesWhenThePersonLeavesTheLibrary()
    {
        LibraryViewModel library = LibraryViewModel.Open(new MemoryStore([]));
        library.AddByHandCommand.Execute(parameter: null);
        library.TitleInput = "Middlemarch";
        library.AuthorInput = "George Eliot";
        library.SaveCommand.Execute(parameter: null);
        Assert.True(library.ShowSnackbar);

        library.OpenCommand.Execute(library.Books[0].Id);
        library.BackCommand.Execute(parameter: null);

        Assert.True(library.ShowLibrary);
        Assert.False(library.ShowSnackbar);
        Assert.Equal("", library.SnackbarMessage);
    }

    [Fact]
    public void GridChoiceThatCannotBeWrittenKeepsTheLibraryOpen()
    {
        string directory = Path.Combine(Path.GetTempPath(), "folium-tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "library.json");
        _ = Directory.CreateDirectory(path + ".view");
        LibraryViewModel library = LibraryViewModel.Open(new FileBookStore(path));
        library.AddByHandCommand.Execute(parameter: null);
        library.TitleInput = "Middlemarch";
        library.AuthorInput = "George Eliot";
        library.SaveCommand.Execute(parameter: null);

        library.ToggleGridCommand.Execute(parameter: null);

        Assert.True(library.ShowGrid);
        Assert.True(library.ShowLibrary);
        Directory.Delete(directory, recursive: true);
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
