using Folium.Books;
using Folium.Storage;
using Folium.ViewModels;

namespace Folium.Tests;

public sealed class LibraryRestoreTests : IDisposable
{
    private readonly List<string> _directories = [];

    public void Dispose()
    {
        foreach (string directory in _directories)
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ConfirmRestoreReplacesTheLibraryAndKeepsDistinctBooks()
    {
        (LibraryViewModel viewModel, string path) = OpenAtPath();
        Add(viewModel, "Dune", "Frank Herbert");
        string backup = viewModel.CreateBackup()!;
        Add(viewModel, "Neuromancer", "William Gibson");
        Guid neuromancerId = viewModel.Books[1].Id;

        viewModel.PrepareRestore(backup);

        Assert.True(viewModel.ShowRestore);
        Assert.False(viewModel.ShowLibrary);
        Assert.Equal("Replace your library?", viewModel.Heading);
        Assert.Equal(1, viewModel.RestoreCount);
        Assert.Equal("Dune", Assert.Single(viewModel.RestoreTitles));
        Assert.Equal("The backup has 1 book. Replacing the library removes the books on this device.", viewModel.RestoreMessage);
        Assert.Equal(2, LibraryViewModel.Open(new FileBookStore(path)).Books.Count);

        viewModel.ConfirmRestoreCommand.Execute(parameter: null);

        Assert.True(viewModel.ShowLibrary);
        Assert.Equal("Dune", Assert.Single(viewModel.Books).Title);
        Book restored = Assert.Single(LibraryViewModel.Open(new FileBookStore(path)).Books);
        Assert.Equal("Dune", restored.Title);
        Assert.NotEqual(neuromancerId, restored.Id);
    }

    [Fact]
    public void CancelRestoreKeepsTheCurrentBooks()
    {
        (LibraryViewModel viewModel, string path) = OpenAtPath();
        Add(viewModel, "Dune", "Frank Herbert");
        string backup = LibraryBackup.Export([]);

        viewModel.PrepareRestore(backup);
        Assert.Equal("The backup has 0 books. Replacing the library removes the books on this device.", viewModel.RestoreMessage);
        Assert.True(viewModel.GoBack());

        Assert.True(viewModel.ShowLibrary);
        Assert.Equal("Dune", Assert.Single(viewModel.Books).Title);
        Assert.Equal("Dune", Assert.Single(LibraryViewModel.Open(new FileBookStore(path)).Books).Title);
    }

    [Fact]
    public void KeepLibraryCommandKeepsTheCurrentBooks()
    {
        (LibraryViewModel viewModel, string path) = OpenAtPath();
        Add(viewModel, "Dune", "Frank Herbert");

        viewModel.PrepareRestore(LibraryBackup.Export([]));
        viewModel.KeepLibraryCommand.Execute(parameter: null);

        Assert.True(viewModel.ShowLibrary);
        Assert.Equal("Dune", Assert.Single(LibraryViewModel.Open(new FileBookStore(path)).Books).Title);
    }

    [Fact]
    public void InvalidBackupDoesNotChangeTheLibrary()
    {
        (LibraryViewModel viewModel, string path) = OpenAtPath();
        Add(viewModel, "Dune", "Frank Herbert");

        viewModel.PrepareRestore("{");

        Assert.True(viewModel.ShowLibrary);
        Assert.False(viewModel.ShowRestore);
        Assert.Equal("This backup is incomplete.", viewModel.SaveError);
        Assert.Equal("Dune", Assert.Single(LibraryViewModel.Open(new FileBookStore(path)).Books).Title);
    }

    [Fact]
    public void FailedReplaceKeepsTheCurrentBooks()
    {
        string path = NewPath();
        FileBookStore store = new(path);
        _ = new PersonalLibrary(store).Add("Dune", "Frank Herbert");
        LibraryViewModel viewModel = LibraryViewModel.Open(new FailReplaceStore(store));
        string backup = LibraryBackup.Export([new Book(Guid.NewGuid(), "Neuromancer", "William Gibson")]);

        viewModel.PrepareRestore(backup);
        viewModel.ConfirmRestoreCommand.Execute(parameter: null);

        Assert.True(viewModel.ShowRestore);
        Assert.Equal("Folium could not replace your library. The current books are unchanged.", viewModel.SaveError);
        Assert.Equal("Dune", Assert.Single(LibraryViewModel.Open(new FileBookStore(path)).Books).Title);
    }

    [Fact]
    public void ClosedLibraryCannotExportAndRestoreNeedsConfirmation()
    {
        string path = NewPath();
        File.WriteAllText(path, "null");
        LibraryViewModel viewModel = LibraryViewModel.Open(new FileBookStore(path));

        Assert.Null(viewModel.CreateBackup());
        viewModel.PrepareRestore(LibraryBackup.Export([]));

        Assert.True(viewModel.ShowRestore);
        Assert.Equal("null", File.ReadAllText(path));
        viewModel.KeepLibraryCommand.Execute(parameter: null);
        Assert.True(viewModel.ShowLoadError);
        Assert.False(viewModel.ShowRestore);
        Assert.Equal("null", File.ReadAllText(path));
    }

    private (LibraryViewModel ViewModel, string Path) OpenAtPath()
    {
        string path = NewPath();
        return (LibraryViewModel.Open(new FileBookStore(path)), path);
    }

    private string NewPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "folium-restore-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(directory);
        _directories.Add(directory);
        return Path.Combine(directory, "library.json");
    }

    private static void Add(LibraryViewModel viewModel, string title, string author)
    {
        viewModel.ShowAddCommand.Execute(parameter: null);
        viewModel.TitleInput = title;
        viewModel.AuthorInput = author;
        viewModel.SaveCommand.Execute(parameter: null);
    }

    private sealed class FailReplaceStore(IBookStore store) : IBookStore
    {
        public IReadOnlyList<Book> Read()
        {
            return store.Read();
        }

        public void Replace(IReadOnlyList<Book> books)
        {
            throw new LibraryStoreException("Folium could not write the library file.");
        }
    }
}
